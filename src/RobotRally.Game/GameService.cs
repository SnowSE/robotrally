using System.Collections.Immutable;
using System.Security.Cryptography;

namespace RobotRally.Game;

public enum GamePhase { Lobby, Demo, Countdown, Race, Podium }

public enum DemoMode { Scripted, Live }

/// <summary>
/// All game state for one room, in memory. Thread-safe; raises <see cref="Changed"/> after every change
/// (from arbitrary threads — UI subscribers must marshal with InvokeAsync).
/// </summary>
public sealed class GameService
{
    public const int MaxPlayers = 24;
    public static readonly TimeSpan CountdownTime = TimeSpan.FromSeconds(3.6);
    /// <summary>Kids in this slice at the back of the pack get a 💡 Hint button.</summary>
    public const double HintShare = 0.3;

    private readonly object _gate = new();
    private readonly TimeProvider _time;
    private readonly Random _rng;
    private readonly List<Player> _players = [];
    private readonly List<ITimer> _timers = [];
    private int _generation; // bumps on reset/race-again so stale timers do nothing

    public GameService(TimeProvider time, Random? rng = null)
    {
        _time = time;
        _rng = rng ?? new Random();
    }

    public event Action? Changed;

    public IReadOnlyList<Level> Levels => Game.Levels.All;
    public GamePhase Phase { get; private set; } = GamePhase.Lobby;
    public DemoMode Demo { get; private set; }
    public DateTimeOffset? CountdownEndsAt { get; private set; }
    public DateTimeOffset? RaceStartedAt { get; private set; }
    public DateTimeOffset? RaceEndedAt { get; private set; }
    /// <summary>Bumps on every reset so pages can tell a new group started.</summary>
    public int Session { get; private set; } = 1;
    public DateTimeOffset Now => _time.GetUtcNow();

    public IReadOnlyList<Player> Players
    {
        get { lock (_gate) return _players.ToList(); }
    }

    public string SuggestNickname()
    {
        lock (_gate) return Nicknames.Random(_rng, TakenNames());
    }

    private HashSet<string> TakenNames() => _players.Select(p => p.Nickname).ToHashSet();

    /// <summary>Adds a player. Returns null when the room is full or the look is invalid.
    /// A taken or non-generated nickname is replaced with a fresh one.</summary>
    public Player? Join(CharacterLook look, string nickname)
    {
        Player player;
        lock (_gate)
        {
            if (_players.Count >= MaxPlayers || !Characters.IsValid(look)) return null;
            var taken = TakenNames();
            if (taken.Contains(nickname) || !Nicknames.IsGenerated(nickname))
                nickname = Nicknames.Random(_rng, taken);
            var now = Now;
            player = new Player
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                Nickname = nickname,
                Look = look,
                JoinedAt = now,
                LevelReachedAt = now,
            };
            _players.Add(player);
        }
        Notify();
        return player;
    }

    public Player? Resume(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        lock (_gate) return _players.FirstOrDefault(p => p.Token == token);
    }

    public void Remove(string playerId)
    {
        lock (_gate)
        {
            if (_players.RemoveAll(p => p.Id == playerId) == 0) return;
        }
        Notify();
    }

    public Level? CurrentLevel(Player p) => p.LevelIndex < Levels.Count ? Levels[p.LevelIndex] : null;

    /// <summary>Saves the editor contents. Ignored mid-run or if the player moved on to another level.</summary>
    public void SaveProgram(string token, IReadOnlyList<Block> program, int levelIndex)
    {
        lock (_gate)
        {
            var p = _players.FirstOrDefault(x => x.Token == token);
            if (p is null || p.PlayingIndex != levelIndex || p.CurrentRun is not null) return;
            p.ProgramStore = ProgramOps.CloneAll(program);
        }
    }

    /// <summary>Runs the player's program. Returns null if not allowed right now.</summary>
    public ActiveRun? Run(string token, IReadOnlyList<Block> program)
    {
        ActiveRun run;
        lock (_gate)
        {
            var p = _players.FirstOrDefault(x => x.Token == token);
            if (p is null || Phase != GamePhase.Race || p.CurrentRun is not null || p.PlayingIndex is not { } index) return null;
            var level = Levels[index];
            var copy = ProgramOps.CloneAll(program);
            var blocks = ProgramOps.Count(copy);
            if (level.BlockLimit > 0 && blocks > level.BlockLimit) return null;
            if (ProgramOps.KindsUsed(copy).Any(k => !level.Toolbox.Contains(k))) return null;

            p.ProgramStore = copy;
            var result = Interpreter.Run(level, copy);
            var practice = p.Finished;
            var award = result.Outcome == RunOutcome.Success
                ? Scoring.For(level.Number, blocks, Now - (practice ? p.PracticeStartedAt : p.LevelReachedAt), result.Stars)
                : null;
            run = new ActiveRun(Guid.NewGuid(), index, result, Now, p.Fast ? Playback.FastSpeed : 1, award, practice);
            p.CurrentRun = run;
            p.Runs++;
            if (result.Outcome != RunOutcome.Success) p.Crashes++;
            ScheduleFinish(p, run);
        }
        Notify();
        return run;
    }

    /// <summary>The 🐇 button. Also speeds up (or slows down) a run that's already playing, from where it is now.</summary>
    public void SetFast(string token, bool fast)
    {
        lock (_gate)
        {
            var p = _players.FirstOrDefault(x => x.Token == token);
            if (p is null || p.Fast == fast) return;
            p.Fast = fast;
            if (p.CurrentRun is { } run)
            {
                var speed = fast ? Playback.FastSpeed : 1;
                var position = Playback.Position(run.Result, Now - run.StartedAt, run.Speed);
                var retimed = run with { StartedAt = Now - Playback.ElapsedFor(run.Result, position, speed), Speed = speed };
                p.CurrentRun = retimed;
                ScheduleFinish(p, retimed);
            }
        }
        Notify();
    }

    private void ScheduleFinish(Player p, ActiveRun run)
    {
        var gen = _generation;
        var left = run.EndsAt - Now;
        After(left > TimeSpan.Zero ? left : TimeSpan.Zero, () => FinishRun(p, run, gen));
    }

    /// <summary>Stops a run early (robot goes back to start). A run that already reached the flag can't be stopped.</summary>
    public void StopRun(string token)
    {
        lock (_gate)
        {
            var p = _players.FirstOrDefault(x => x.Token == token);
            if (p?.CurrentRun is not { } run || run.Result.Outcome == RunOutcome.Success) return;
            p.CurrentRun = null;
        }
        Notify();
    }

    private void FinishRun(Player p, ActiveRun run, int gen)
    {
        lock (_gate)
        {
            // Reference check: a speed change replaces the run record, and the old timer must then do nothing.
            if (gen != _generation || !ReferenceEquals(p.CurrentRun, run)) return;
            p.CurrentRun = null;
            if (Phase == GamePhase.Race) Credit(p, run);
        }
        Notify();
    }

    /// <summary>Banks a successful run's points (best per level) and, in the race, moves the player up a level.</summary>
    private void Credit(Player p, ActiveRun run)
    {
        if (run.Award is not { } score) return;
        int? before = p.Best.TryGetValue(run.LevelIndex, out var old) ? old.Total : null;
        if (before is null || score.Total > before)
        {
            p.Best = ((ImmutableDictionary<int, LevelScore>)p.Best).SetItem(run.LevelIndex, score);
            p.Solutions = p.Solutions.SetItem(run.LevelIndex, ProgramOps.CloneAll(p.ProgramStore));
            p.Score = p.Best.Values.Sum(s => s.Total);
        }
        p.LastAward = new Award(score, run.Practice, run.Practice ? before : null);
        if (run.Practice || run.LevelIndex != p.LevelIndex) return;

        // Set FinishedAt before LevelIndex so lock-free readers never see LevelIndex == Count while unfinished.
        if (p.LevelIndex + 1 >= Levels.Count) p.FinishedAt = Now;
        p.LevelReachedAt = Now;
        p.ProgramStore = [];
        p.LevelIndex++;
    }

    /// <summary>A finished player replays a level. Their winning program is loaded so they can try to beat it.</summary>
    public bool StartPractice(string token, int levelIndex)
    {
        lock (_gate)
        {
            var p = _players.FirstOrDefault(x => x.Token == token);
            if (p is null || !p.Finished || Phase != GamePhase.Race || p.CurrentRun is not null
                || levelIndex < 0 || levelIndex >= Levels.Count) return false;
            p.PracticeIndex = levelIndex;
            p.PracticeStartedAt = Now;
            p.ProgramStore = p.CloneSolution(levelIndex) ?? [];
        }
        Notify();
        return true;
    }

    /// <summary>Back to the finished player's level list.</summary>
    public void EndPractice(string token)
    {
        lock (_gate)
        {
            var p = _players.FirstOrDefault(x => x.Token == token);
            if (p?.PracticeIndex is null) return;
            p.PracticeIndex = null;
            if (p.CurrentRun is { Practice: true } run && run.Award is not null) Credit(p, run);
            p.CurrentRun = null;
            p.ProgramStore = [];
        }
        Notify();
    }

    /// <summary>True for a racer in the back 30% of the pack (by level, then points). Ties count as level,
    /// so nobody gets hints at the very start when everyone is still on level 1 with no points.</summary>
    public bool IsAtBackOfPack(Player player)
    {
        lock (_gate)
        {
            if (Phase != GamePhase.Race || player.Finished || _players.Count < 2) return false;
            var back = Math.Max(1, (int)Math.Round(_players.Count * HintShare));
            var ahead = _players.Count(o => o.LevelIndex > player.LevelIndex
                || (o.LevelIndex == player.LevelIndex && o.Score > player.Score));
            return ahead >= _players.Count - back;
        }
    }

    // ---- Admin controls ----

    public void StartDemo(DemoMode mode)
    {
        lock (_gate)
        {
            if (Phase is not (GamePhase.Lobby or GamePhase.Demo)) return;
            Phase = GamePhase.Demo;
            Demo = mode;
        }
        Notify();
    }

    public void BackToLobby()
    {
        lock (_gate)
        {
            if (Phase is not (GamePhase.Demo or GamePhase.Lobby)) return;
            Phase = GamePhase.Lobby;
        }
        Notify();
    }

    public void StartRace()
    {
        lock (_gate)
        {
            if (Phase is not (GamePhase.Lobby or GamePhase.Demo)) return;
            Phase = GamePhase.Countdown;
            CountdownEndsAt = Now + CountdownTime;
            var gen = _generation;
            After(CountdownTime, () =>
            {
                lock (_gate)
                {
                    if (gen != _generation || Phase != GamePhase.Countdown) return;
                    Phase = GamePhase.Race;
                    RaceStartedAt = Now;
                    foreach (var p in _players) p.LevelReachedAt = Now;
                }
                Notify();
            });
        }
        Notify();
    }

    public void EndRace()
    {
        lock (_gate)
        {
            if (Phase != GamePhase.Race) return;
            EnterPodium();
        }
        Notify();
    }

    private void EnterPodium()
    {
        Phase = GamePhase.Podium;
        RaceEndedAt = Now;
        foreach (var p in _players)
        {
            // A racer whose winning run is still celebrating gets the points (and the level).
            if (p.CurrentRun is { } run) Credit(p, run);
            p.CurrentRun = null;
            p.PracticeIndex = null;
        }
    }

    /// <summary>Same group races again: keep players and characters, wipe progress, back to lobby.</summary>
    public void RaceAgain()
    {
        lock (_gate)
        {
            NewGeneration();
            foreach (var p in _players)
            {
                p.LevelIndex = 0;
                p.FinishedAt = null;
                p.CurrentRun = null;
                p.ProgramStore = [];
                p.Runs = p.Crashes = 0;
                p.LevelReachedAt = Now;
                p.Best = ImmutableDictionary<int, LevelScore>.Empty;
                p.Solutions = p.Solutions.Clear();
                p.Score = 0;
                p.LastAward = null;
                p.PracticeIndex = null;
            }
        }
        Notify();
    }

    /// <summary>New group: everyone is removed (their tokens stop working) and we go back to an empty lobby.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            NewGeneration();
            _players.Clear();
            Session++;
        }
        Notify();
    }

    private void NewGeneration()
    {
        _generation++;
        foreach (var t in _timers) t.Dispose();
        _timers.Clear();
        Phase = GamePhase.Lobby;
        CountdownEndsAt = RaceStartedAt = RaceEndedAt = null;
    }

    /// <summary>Most points first; ties go to the higher level, then to whoever got there first.</summary>
    public IReadOnlyList<Player> Standings()
    {
        lock (_gate)
        {
            return _players
                .OrderByDescending(p => p.Score)
                .ThenByDescending(p => p.LevelIndex)
                .ThenBy(p => p.LevelReachedAt)
                .ThenBy(p => p.JoinedAt)
                .ToList();
        }
    }

    public int PlaceOf(Player p)
    {
        var standings = Standings();
        for (var i = 0; i < standings.Count; i++)
            if (standings[i].Id == p.Id) return i + 1;
        return 0;
    }

    private void After(TimeSpan delay, Action action)
    {
        ITimer? timer = null;
        timer = _time.CreateTimer(_ =>
        {
            lock (_gate) if (timer is not null) _timers.Remove(timer);
            action();
        }, null, delay, Timeout.InfiniteTimeSpan);
        _timers.Add(timer);
    }

    private void Notify()
    {
        var handlers = Changed;
        if (handlers is null) return;
        foreach (var h in handlers.GetInvocationList().Cast<Action>())
        {
            try { h(); }
            catch { /* a broken subscriber (e.g. disposed circuit) must not break the game */ }
        }
    }
}
