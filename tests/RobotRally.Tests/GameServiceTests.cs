using Microsoft.Extensions.Time.Testing;
using RobotRally.Game;
using static RobotRally.Game.Block;

namespace RobotRally.Tests;

public class GameServiceTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 4, 18, 0, 0, TimeSpan.Zero));
    private readonly GameService _game;

    public GameServiceTests() => _game = new GameService(_time, new Random(42));

    private Player Join() => _game.Join(CharacterLook.Default, _game.SuggestNickname())!;

    private void StartRace()
    {
        _game.StartRace();
        _time.Advance(GameService.CountdownTime);
        Assert.Equal(GamePhase.Race, _game.Phase);
    }

    private void Solve(Player p)
    {
        var run = _game.Run(p.Token, Levels.Solution(p.LevelIndex + 1))!;
        Assert.Equal(RunOutcome.Success, run.Result.Outcome);
        _time.Advance(Playback.Duration(run.Result));
    }

    [Fact]
    public void Join_AssignsUniqueGeneratedNicknames_AndRejectsTypedNames()
    {
        var a = _game.Join(CharacterLook.Default, "Zippy Octopus")!;
        var b = _game.Join(CharacterLook.Default, "Zippy Octopus")!;
        var c = _game.Join(CharacterLook.Default, "Something Rude")!;
        Assert.Equal("Zippy Octopus", a.Nickname);
        Assert.NotEqual(a.Nickname, b.Nickname);
        Assert.True(Nicknames.IsGenerated(c.Nickname));
    }

    [Fact]
    public void Join_RejectsInvalidLook_AndFullRoom()
    {
        Assert.Null(_game.Join(new CharacterLook(BaseKind.Car, "#000000", []), "Zippy Octopus"));
        Assert.Null(_game.Join(new CharacterLook(BaseKind.Car, Characters.Colors[0], ["propeller"]), "Zippy Octopus"));
        for (var i = 0; i < GameService.MaxPlayers; i++) Assert.NotNull(Join());
        Assert.Null(_game.Join(CharacterLook.Default, "Zippy Octopus"));
    }

    [Fact]
    public void Resume_ReturnsSamePlayerWithProgram()
    {
        var p = Join();
        StartRace();
        _game.SaveProgram(p.Token, [F, F], 0);
        var again = _game.Resume(p.Token);
        Assert.Same(p, again);
        Assert.Equal(2, again!.CloneProgram().Count);
        Assert.Null(_game.Resume("nope"));
    }

    [Fact]
    public void Reset_RejectsOldTokens_AndReturnsToEmptyLobby()
    {
        var p = Join();
        StartRace();
        var session = _game.Session;
        _game.Reset();
        Assert.Null(_game.Resume(p.Token));
        Assert.Empty(_game.Players);
        Assert.Equal(GamePhase.Lobby, _game.Phase);
        Assert.Equal(session + 1, _game.Session);
    }

    [Fact]
    public void StartRace_CountsDownThenRaces()
    {
        Join();
        _game.StartRace();
        Assert.Equal(GamePhase.Countdown, _game.Phase);
        _time.Advance(GameService.CountdownTime - TimeSpan.FromMilliseconds(1));
        Assert.Equal(GamePhase.Countdown, _game.Phase);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(GamePhase.Race, _game.Phase);
    }

    [Fact]
    public void Run_RejectedWhileRunning_AndOutsideRace()
    {
        var p = Join();
        Assert.Null(_game.Run(p.Token, [F]));               // lobby
        _game.StartRace();
        Assert.Null(_game.Run(p.Token, [F]));               // countdown
        _time.Advance(GameService.CountdownTime);
        Assert.NotNull(_game.Run(p.Token, [F]));
        Assert.Null(_game.Run(p.Token, [F]));               // already running
    }

    [Fact]
    public void Run_RejectsOverLimitAndDisallowedBlocks()
    {
        var p = Join();
        StartRace();
        Assert.Null(_game.Run(p.Token, [F, F, F, F, F, F, F, F])); // level 1 limit 7
        Assert.Null(_game.Run(p.Token, [Repeat(2, F)]));        // Repeat not offered on level 1
    }

    [Fact]
    public void SuccessfulRun_AdvancesAfterAnimation_AndClearsProgram()
    {
        var p = Join();
        StartRace();
        var run = _game.Run(p.Token, Levels.Solution(1))!;
        _time.Advance(Playback.Duration(run.Result) - TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, p.LevelIndex);
        Assert.NotNull(p.CurrentRun);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, p.LevelIndex);
        Assert.Null(p.CurrentRun);
        Assert.Empty(p.CloneProgram());
    }

    [Fact]
    public void CrashedRun_StaysOnLevel_AndCountsCrash()
    {
        var p = Join();
        StartRace();
        var run = _game.Run(p.Token, [F, F, F])!;
        Assert.Equal(RunOutcome.Crash, run.Result.Outcome);
        _time.Advance(Playback.Duration(run.Result));
        Assert.Equal(0, p.LevelIndex);
        Assert.Equal(1, p.Crashes);
        Assert.NotNull(_game.Run(p.Token, [F]));
    }

    [Fact]
    public void StopRun_EndsCrashRunEarly_ButNotAWin()
    {
        var p = Join();
        StartRace();
        _game.Run(p.Token, [F, F, F]);
        _game.StopRun(p.Token);
        Assert.Null(p.CurrentRun);

        _game.Run(p.Token, Levels.Solution(1));
        _game.StopRun(p.Token);
        Assert.NotNull(p.CurrentRun);
    }

    [Fact]
    public void FinishingAllLevels_FinishesPlayer_ButOnlyTheAdminEndsTheRace()
    {
        var p = Join();
        StartRace();
        for (var i = 0; i < Levels.All.Count; i++) Solve(p);
        Assert.True(p.Finished);
        Assert.Null(p.PlayingIndex);
        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(GamePhase.Race, _game.Phase);
    }

    [Fact]
    public void Standings_MostPoints_ThenLevel_ThenWhoGotThereFirst()
    {
        var a = Join(); var b = Join(); var c = Join(); var d = Join();
        StartRace();
        Solve(c);               // c reaches level 2 first
        Solve(b);               // b reaches level 2 second
        Solve(b);               // b reaches level 3
        Assert.Equal([b, c, a, d], _game.Standings());
        Assert.Equal(1, _game.PlaceOf(b));
    }

    [Fact]
    public void LateJoiner_DuringRace_StartsOnLevelOne()
    {
        Join();
        StartRace();
        var late = Join();
        Assert.Equal(0, late.LevelIndex);
        Assert.NotNull(_game.Run(late.Token, [F]));
    }

    [Fact]
    public void RaceAgain_KeepsPlayers_WipesProgress_CancelsPendingRuns()
    {
        var p = Join();
        StartRace();
        Solve(p);
        var run = _game.Run(p.Token, Levels.Solution(2))!;
        _game.RaceAgain();
        _time.Advance(Playback.Duration(run.Result));
        Assert.Same(p, _game.Resume(p.Token));
        Assert.Equal(0, p.LevelIndex);
        Assert.Equal(0, p.Score);
        Assert.Empty(p.Best);
        Assert.Null(p.CurrentRun);
        Assert.Equal(GamePhase.Lobby, _game.Phase);
    }

    [Fact]
    public void SolvingALevel_BanksPoints_AndShowsTheAward()
    {
        var p = Join();
        StartRace();
        _time.Advance(TimeSpan.FromSeconds(5));
        Solve(p);
        var score = p.Best[0];
        Assert.Equal(Scoring.CompletePoints + Scoring.MaxSpeedPoints + Scoring.MaxBlockPoints, score.Total);
        Assert.Equal(score.Total, p.Score);
        Assert.Same(score, p.LastAward!.Score);
        Assert.False(p.LastAward.Practice);
    }

    [Fact]
    public void BonusStar_IsWorthPoints_OnlyWhenTheRunWins()
    {
        var p = Join();
        StartRace();
        var crash = _game.Run(p.Token, [R, F, F, F])!; // grabs the star, then hits a hedge
        Assert.Equal(1, crash.Result.Stars);
        Assert.Null(crash.Award);
        _time.Advance(Playback.Duration(crash.Result));
        Assert.Equal(0, p.Score);

        var run = _game.Run(p.Token, Levels.StarSolution(1)!)!;
        _time.Advance(Playback.Duration(run.Result));
        Assert.Equal(1, p.Best[0].StarsTaken);
        Assert.Equal(Scoring.PointsPerStar, p.Best[0].StarPoints);
        Assert.Equal(Scoring.MaxBlockPoints - Scoring.PointsPerExtraBlock, p.Best[0].Blocks); // one block over par
    }

    [Fact]
    public void FastMode_PlaysRunsFaster()
    {
        var p = Join();
        StartRace();
        _game.SetFast(p.Token, true);
        var run = _game.Run(p.Token, Levels.Solution(1))!;
        Assert.Equal(Playback.FastSpeed, run.Speed);
        _time.Advance(Playback.Duration(run.Result, Playback.FastSpeed));
        Assert.Equal(1, p.LevelIndex);
    }

    [Fact]
    public void FastMode_ChangedMidRun_RetimesFromWhereTheRacerIs()
    {
        var p = Join();
        StartRace();
        var run = _game.Run(p.Token, Levels.Solution(1))!;
        _time.Advance(Playback.StepTime * 2);
        _game.SetFast(p.Token, true);
        var fast = p.CurrentRun!;
        Assert.Equal(run.Id, fast.Id);
        Assert.Equal(2, Playback.Frame(fast.Result, _game.Now - fast.StartedAt, fast.Speed).StepIndex);

        // Slowing back down: the earlier (fast) timer must not end the run early.
        _game.SetFast(p.Token, false);
        _time.Advance(Playback.Duration(run.Result, Playback.FastSpeed));
        Assert.Equal(0, p.LevelIndex);
        _time.Advance(Playback.Duration(run.Result));
        Assert.Equal(1, p.LevelIndex);
    }

    [Fact]
    public void Finished_PlayerCanReplayLevels_BestScoreKept()
    {
        var p = Join();
        StartRace();
        for (var i = 0; i < Levels.All.Count; i++) Solve(p);
        var total = p.Score;
        var level1 = p.Best[0].Total;

        Assert.True(_game.StartPractice(p.Token, 0));
        Assert.Equal(0, p.PlayingIndex);
        Assert.Equal(ProgramOps.Count(Levels.Solution(1)), ProgramOps.Count(p.CloneProgram())); // their winning program is loaded

        // Slower and no star: not better, so nothing changes.
        _time.Advance(TimeSpan.FromMinutes(10));
        var slow = _game.Run(p.Token, Levels.Solution(1))!;
        _time.Advance(Playback.Duration(slow.Result));
        Assert.Equal(total, p.Score);
        Assert.False(p.LastAward!.Improved);

        // Grabbing the star beats it.
        _game.StartPractice(p.Token, 0);
        var star = _game.Run(p.Token, Levels.StarSolution(1)!)!;
        _time.Advance(Playback.Duration(star.Result));
        Assert.True(p.LastAward!.Improved);
        Assert.True(p.Best[0].Total > level1);
        Assert.Equal(total - level1 + p.Best[0].Total, p.Score);
        Assert.Equal(Levels.All.Count, p.LevelIndex);
        Assert.Equal(6, ProgramOps.Count(p.CloneSolution(0)!));

        _game.EndPractice(p.Token);
        Assert.Null(p.PlayingIndex);
        Assert.Null(_game.Run(p.Token, [F]));
    }

    [Fact]
    public void StartPractice_OnlyForFinishers()
    {
        var p = Join();
        StartRace();
        Assert.False(_game.StartPractice(p.Token, 0));
    }

    [Fact]
    public void Hints_GoToTheBackOfThePack_NotAtTheStart()
    {
        var players = Enumerable.Range(0, 10).Select(_ => Join()).ToList();
        StartRace();
        Assert.All(players, p => Assert.False(_game.IsAtBackOfPack(p))); // everyone tied

        foreach (var p in players.Take(7)) Solve(p);
        Assert.All(players.Take(7), p => Assert.False(_game.IsAtBackOfPack(p)));
        Assert.All(players.Skip(7), p => Assert.True(_game.IsAtBackOfPack(p)));
    }

    [Fact]
    public void EndRace_CreditsAWinningRunThatIsStillCelebrating()
    {
        var p = Join();
        StartRace();
        _game.Run(p.Token, Levels.Solution(1));
        _game.EndRace();
        Assert.Equal(1, p.LevelIndex);
        Assert.True(p.Score > 0);
    }

    [Fact]
    public void EndRace_GoesToPodium_AndStopsRuns()
    {
        var p = Join();
        StartRace();
        _game.Run(p.Token, [F]);
        _game.EndRace();
        Assert.Equal(GamePhase.Podium, _game.Phase);
        Assert.Null(p.CurrentRun);
        Assert.Null(_game.Run(p.Token, [F]));
    }

    [Fact]
    public void Changed_FiresOnJoin_AndBadSubscriberDoesNotBreakGame()
    {
        var count = 0;
        _game.Changed += () => throw new InvalidOperationException("boom");
        _game.Changed += () => count++;
        Join();
        Assert.Equal(1, count);
    }
}
