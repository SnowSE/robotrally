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
    public static readonly TimeSpan PodiumDelay = TimeSpan.FromSeconds(5);

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
            CheckForPodium();
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
            if (p is null || p.LevelIndex != levelIndex || p.CurrentRun is not null) return;
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
            if (p is null || Phase != GamePhase.Race || p.Finished || p.CurrentRun is not null) return null;
            var level = Levels[p.LevelIndex];
            var copy = ProgramOps.CloneAll(program);
            if (level.BlockLimit > 0 && ProgramOps.Count(copy) > level.BlockLimit) return null;
            if (ProgramOps.KindsUsed(copy).Any(k => !level.Toolbox.Contains(k))) return null;

            p.ProgramStore = copy;
            var result = Interpreter.Run(level, copy);
            run = new ActiveRun(Guid.NewGuid(), p.LevelIndex, result, Now);
            p.CurrentRun = run;
            p.Runs++;
            if (result.Outcome != RunOutcome.Success) p.Crashes++;
            var gen = _generation;
            After(Playback.Duration(result), () => FinishRun(p, run.Id, gen));
        }
        Notify();
        return run;
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

    private void FinishRun(Player p, Guid runId, int gen)
    {
        lock (_gate)
        {
            if (gen != _generation || p.CurrentRun?.Id != runId) return;
            var success = p.CurrentRun.Result.Outcome == RunOutcome.Success;
            p.CurrentRun = null;
            if (success && Phase == GamePhase.Race)
            {
                // Set FinishedAt before LevelIndex so lock-free readers never see LevelIndex == Count while unfinished.
                if (p.LevelIndex + 1 >= Levels.Count) p.FinishedAt = Now;
                p.LevelReachedAt = Now;
                p.ProgramStore = [];
                p.LevelIndex++;
                if (p.Finished) CheckForPodium();
            }
        }
        Notify();
    }

    private void CheckForPodium()
    {
        if (Phase != GamePhase.Race || _players.Count == 0) return;
        var finished = _players.Count(p => p.Finished);
        if (finished >= Math.Min(3, _players.Count))
        {
            var gen = _generation;
            After(PodiumDelay, () =>
            {
                lock (_gate)
                {
                    if (gen != _generation || Phase != GamePhase.Race) return;
                    EnterPodium();
                }
                Notify();
            });
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
        foreach (var p in _players) p.CurrentRun = null;
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

    /// <summary>Finished players by finish time, then by level reached (earliest first).</summary>
    public IReadOnlyList<Player> Standings()
    {
        lock (_gate)
        {
            return _players
                .OrderBy(p => p.FinishedAt ?? DateTimeOffset.MaxValue)
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
