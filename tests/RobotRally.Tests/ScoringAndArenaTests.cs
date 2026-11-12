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

public class KingOfTheHillTests
{
    private const double Cx = LobbyArena.HillX, Cy = LobbyArena.HillY;

    private static LobbyArena Arena(params string[] ids)
    {
        var a = new LobbyArena(new Random(1));
        a.Sync(ids);
        // Park everyone well away from the hill unless a test moves them.
        for (var i = 0; i < ids.Length; i++) a.Place(ids[i], 200 + i * 120, 150);
        return a;
    }

    private static List<HillChange> Run(LobbyArena a, double seconds)
    {
        var changes = new List<HillChange>();
        for (var t = 0.0; t < seconds - 1e-9; t += 0.05) changes.AddRange(a.Step(0.05).HillChanges);
        return changes;
    }

    [Fact]
    public void DrivingIntoTheCircle_ClaimsIt_AndTheTimerCountsUp()
    {
        var a = Arena("a", "b");
        a.Place("a", Cx, Cy);
        var first = a.Step(0.05);
        Assert.Equal(new HillChange("a", null, null, 0), Assert.Single(first.HillChanges));
        Assert.Equal("a", first.Hill!.Value.Holder);

        var later = a.Step(0.05);
        for (var i = 0; i < 39; i++) later = a.Step(0.05);
        Assert.Equal(2.0, later.Time - later.Hill!.Value.Since, 3);
    }

    [Fact]
    public void SomeoneElseDrivingIn_DoesNotTakeIt()
    {
        var a = Arena("a", "b");
        a.Place("a", Cx, Cy);
        a.Step(0.05);
        a.Place("b", Cx + 100, Cy + 10);
        Assert.Empty(Run(a, 1));
        Assert.Equal("a", a.Hill!.Value.Holder);
    }

    [Fact]
    public void BumpingTheHolderOut_HandsTheCircleToTheBumper_AndBanksTheReign()
    {
        var a = Arena("a", "b");
        a.Place("a", Cx, Cy);
        Run(a, 3);
        a.Place("b", Cx - 125, Cy, LobbyArena.MaxSpeed, 0); // already in the circle, ramming the holder at top speed
        a.SetInput("b", DriveInput.Right);
        var changes = Run(a, 0.15); // contact
        a.SetInput("b", DriveInput.None);
        changes.AddRange(Run(a, 1.5));

        var change = Assert.Single(changes);
        Assert.Equal("b", change.Holder);
        Assert.Equal("a", change.Previous);
        Assert.Equal("b", change.KnockedBy);
        Assert.InRange(change.Reign, 3, 4.5);
        Assert.Equal("a", Assert.Single(a.HallOfFame()).Id);
    }

    [Fact]
    public void DrivingOut_EmptiesTheCircle_WithNoKnockout()
    {
        var a = Arena("a");
        a.Place("a", Cx, Cy);
        Run(a, 1.5);
        a.Place("a", 200, 150);
        var change = Assert.Single(Run(a, 0.1));
        Assert.Null(change.Holder);
        Assert.Equal("a", change.Previous);
        Assert.Null(change.KnockedBy);
        Assert.Null(a.Hill!.Value.Holder);
    }

    [Fact]
    public void WobblingOnTheEdge_DoesNotDropTheCrown()
    {
        var a = Arena("a");
        a.Place("a", Cx, Cy);
        a.Step(0.05);
        a.Place("a", Cx + LobbyArena.HillRadius + LobbyArena.HillSlack / 2, Cy);
        Assert.Empty(Run(a, 0.1));
        Assert.Equal("a", a.Hill!.Value.Holder);
    }

    [Fact]
    public void WhenTheHolderLeaves_WhoeverWasInsideLongestTakesOver()
    {
        var a = Arena("a", "b", "c");
        a.Place("a", Cx, Cy);
        a.Step(0.05);
        a.Place("b", Cx + 90, Cy - 50);
        a.Step(0.05);
        a.Place("c", Cx - 90, Cy + 50);
        a.Step(0.05);
        a.Place("a", 200, 150);
        var change = Assert.Single(Run(a, 0.05));
        Assert.Equal("b", change.Holder);
    }

    [Fact]
    public void TurningItOff_ClearsTheHill_AndKeepsTheRecord()
    {
        var a = Arena("a");
        a.Place("a", Cx, Cy);
        Run(a, 2);
        Assert.True(a.SetHillEnabled(false));
        Assert.False(a.SetHillEnabled(false));
        var f = a.Step(0.05);
        Assert.Null(f.Hill);
        Assert.Empty(f.HillChanges);
        Assert.InRange(Assert.Single(a.HallOfFame()).Seconds, 1.9, 2.1);

        a.SetHillEnabled(true);
        Assert.Equal("a", a.Step(0.05).Hill!.Value.Holder); // still sitting there: claims it again
    }

    [Fact]
    public void HallOfFame_KeepsEachRacersBest_Top3_AndForgetsRacersWhoLeave()
    {
        var a = Arena("a", "b", "c", "d");
        void Reign(string id, double secs)
        {
            a.Place(id, Cx, Cy);
            Run(a, secs);
            a.Place(id, 200, 150);
            Run(a, 0.05);
        }
        Reign("a", 2);
        Reign("a", 4);
        Reign("b", 3);
        Reign("c", 5);
        Reign("d", 0.5); // too short to count
        Assert.Equal(["c", "a", "b"], a.HallOfFame().Select(r => r.Id));

        a.Sync(["a", "b", "d"]);
        Assert.Equal(["a", "b"], a.HallOfFame().Select(r => r.Id));
    }

    [Fact]
    public void EndReign_BanksAndEmptiesTheCircle()
    {
        var a = Arena("a");
        a.Place("a", Cx, Cy);
        Run(a, 1.5);
        Assert.True(a.EndReign());
        Assert.False(a.EndReign());
        Assert.Null(a.Hill!.Value.Holder);
        Assert.Single(a.HallOfFame());
    }
}
