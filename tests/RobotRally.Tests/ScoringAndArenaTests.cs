using RobotRally.Game;
using static RobotRally.Game.Block;

namespace RobotRally.Tests;

public class ScoringTests
{
    [Fact]
    public void FastAndAtPar_GetsEveryPoint()
    {
        var s = Scoring.For(1, Levels.Par(1), TimeSpan.FromSeconds(10), 0);
        Assert.Equal(100 + 100 + 50, s.Total);
    }

    [Fact]
    public void SpeedPoints_SlideToZero_AfterPar()
    {
        var par = Scoring.ParTime(3);
        Assert.Equal(100, Scoring.For(3, 10, par, 0).Speed);
        Assert.Equal(50, Scoring.For(3, 10, par + Scoring.SpeedWindow / 2, 0).Speed);
        Assert.Equal(0, Scoring.For(3, 10, par + Scoring.SpeedWindow * 3, 0).Speed);
        Assert.True(Scoring.ParTime(10) > Scoring.ParTime(1));
    }

    [Fact]
    public void BlockPoints_DropPerExtraBlock_NeverBelowZero()
    {
        var par = Levels.Par(2);
        Assert.Equal(50, Scoring.For(2, par, TimeSpan.Zero, 0).Blocks);
        Assert.Equal(30, Scoring.For(2, par + 2, TimeSpan.Zero, 0).Blocks);
        Assert.Equal(0, Scoring.For(2, par + 20, TimeSpan.Zero, 0).Blocks);
    }

    [Fact]
    public void Stars_AreWorthMoreThanTheBlocksTheyCost()
    {
        var plain = Scoring.For(4, Levels.Par(4), TimeSpan.Zero, 0);
        var star = Scoring.For(4, ProgramOps.Count(Levels.StarSolution(4)!), TimeSpan.Zero, 1);
        Assert.True(star.Total > plain.Total);
    }
}

public class PlaybackSpeedTests
{
    private static readonly RunResult Win = Interpreter.Run(Levels.All[0], Levels.Solution(1));

    [Fact]
    public void Fast_ShortensDriving_NotTheCelebration()
    {
        Assert.Equal(Playback.StepTime * Win.Steps.Count / Playback.FastSpeed + Playback.SuccessTime, Playback.Duration(Win, Playback.FastSpeed));
    }

    [Fact]
    public void PositionAndElapsed_AreInverses()
    {
        foreach (var ms in new[] { 0, 300, 900, 2000, 3000 })
        {
            var pos = TimeSpan.FromMilliseconds(ms);
            Assert.Equal(pos, Playback.Position(Win, Playback.ElapsedFor(Win, pos, 2.5), 2.5));
        }
    }

    [Fact]
    public void StarsTaken_TrackPlayback_AndResetAfterAFailedRun()
    {
        var level = Levels.All[0];
        var crash = Interpreter.Run(level, [R, F, F, F]);
        Assert.Empty(Playback.StarsTaken(crash, Playback.Frame(crash, TimeSpan.Zero)));
        Assert.Single(Playback.StarsTaken(crash, Playback.Frame(crash, Playback.StepTime * 1.5)));
        Assert.Empty(Playback.StarsTaken(crash, Playback.Frame(crash, Playback.Duration(crash))));
    }

    [Fact]
    public void Animator_CuesStarPickup()
    {
        var anim = new RunAnimator();
        var run = Interpreter.Run(Levels.All[0], Levels.StarSolution(1)!);
        anim.Begin(run, DateTimeOffset.UnixEpoch);
        var cues = new List<Cue>();
        for (var t = 0; t < 4000; t += 50) anim.Update(DateTimeOffset.UnixEpoch.AddMilliseconds(t), run.Start, cues);
        Assert.Contains(Cue.Star, cues);
    }
}

public class LobbyArenaTests
{
    private static LobbyArena Arena(params string[] ids)
    {
        var a = new LobbyArena(new Random(1));
        a.Sync(ids);
        return a;
    }

    [Fact]
    public void Sync_AddsAndRemovesCars_WithoutOverlap()
    {
        var a = Arena("a", "b", "c");
        Assert.Equal(3, a.Count);
        a.Sync(["a", "c"]);
        var f = a.Snapshot();
        Assert.Equal(["a", "c"], f.Cars.Select(c => c.Id).Order());
    }

    [Fact]
    public void ArrowKeys_Accelerate_AndDragStops()
    {
        var a = Arena("a");
        a.Place("a", 400, 450);
        a.SetInput("a", DriveInput.Right);
        for (var i = 0; i < 10; i++) a.Step(0.05);
        var moving = a.StateOf("a");
        Assert.True(moving.X > 450 && moving.Vx > 300);
        Assert.InRange(moving.Vx, 0, LobbyArena.MaxSpeed + 1);

        a.SetInput("a", DriveInput.None);
        for (var i = 0; i < 60; i++) a.Step(0.05);
        Assert.True(Math.Abs(a.StateOf("a").Vx) < 5);
    }

    [Fact]
    public void Walls_KeepCarsInside()
    {
        var a = Arena("a");
        a.Place("a", 100, 100, -2000, -2000);
        a.Step(0.25);
        var s = a.StateOf("a");
        Assert.InRange(s.X, LobbyArena.MinX, LobbyArena.MaxX);
        Assert.InRange(s.Y, LobbyArena.MinY, LobbyArena.MaxY);
        Assert.True(s.Vx >= 0 && s.Vy >= 0);
    }

    [Fact]
    public void HeadOnBump_BouncesBothCars_AndReportsOneBump()
    {
        var a = Arena("a", "b");
        a.Place("a", 700, 450, 300, 0);
        a.Place("b", 800, 450, -300, 0);
        var bumps = new List<ArenaBump>();
        for (var i = 0; i < 10; i++) bumps.AddRange(a.Step(0.05).Bumps);

        Assert.Single(bumps);
        Assert.True(a.StateOf("a").Vx < 0);
        Assert.True(a.StateOf("b").Vx > 0);
        var gap = a.StateOf("b").X - a.StateOf("a").X;
        Assert.True(gap >= 2 * LobbyArena.Radius - 0.01);
    }

    [Fact]
    public void GentlePush_IsNotABump()
    {
        var a = Arena("a", "b");
        a.Place("a", 700, 450, 40, 0);
        a.Place("b", 793, 450);
        Assert.Empty(a.Step(0.2).Bumps);
    }

    [Fact]
    public void HoldingIntoAWall_CarStaysPut_AndFacesTheWayYouSteer()
    {
        var a = Arena("a");
        a.Place("a", 300, 450);
        a.SetInput("a", DriveInput.Left);
        for (var i = 0; i < 20; i++) a.Step(0.05); // reach the wall (about 0.7 s from x = 300)
        var angles = new List<double>();
        var xs = new List<double>();
        for (var i = 0; i < 30; i++)
        {
            angles.Add(a.Step(0.05).Cars[0].Angle);
            xs.Add(a.StateOf("a").X);
        }
        Assert.All(xs, x => Assert.InRange(x, LobbyArena.MinX, LobbyArena.MinX + 1)); // no rattling off the wall
        Assert.All(angles, ang => Assert.InRange(((ang % 360) + 360) % 360, 175, 185)); // facing left, steady
    }

    [Fact]
    public void Frames_CarTheArenaClock_ForSmoothBlending()
    {
        var a = Arena("a");
        var t0 = a.Snapshot().Time;
        var f1 = a.Step(0.05);
        var f2 = a.Step(0.05);
        Assert.Equal(t0 + 0.05, f1.Time, 6);
        Assert.Equal(t0 + 0.10, f2.Time, 6);
        Assert.True(f2.Seq > f1.Seq);
    }

    [Fact]
    public void Halt_StopsEveryone()
    {
        var a = Arena("a");
        a.SetInput("a", DriveInput.Up | DriveInput.Left);
        a.Step(0.3);
        a.Halt();
        var before = a.StateOf("a");
        a.Step(0.2);
        Assert.Equal(before.X, a.StateOf("a").X, 3);
    }
}
