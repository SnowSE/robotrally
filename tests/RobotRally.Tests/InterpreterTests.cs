using RobotRally.Game;
using static RobotRally.Game.Block;

namespace RobotRally.Tests;

public class InterpreterTests
{
    // S at (1,1) facing East, goal at (4,1); wall row below except (1,2).
    private static readonly Level Hall = new(0, "t", [
        "######",
        "#S..G#",
        "#.####",
        "######",
    ], Direction.East, 0, Levels.Sandbox.Toolbox);

    [Fact]
    public void Forward_MovesOneCellInFacingDirection()
    {
        var r = Interpreter.Run(Hall, [F]);
        Assert.Equal(RunOutcome.OutOfBlocks, r.Outcome);
        Assert.Equal(new Pose(2, 1, Direction.East), r.Steps.Single().Pose);
        Assert.Equal(StepAction.Move, r.Steps.Single().Action);
    }

    [Fact]
    public void Turns_ChangeDirectionWithoutMoving()
    {
        var r = Interpreter.Run(Hall, [L, R, R]);
        Assert.Equal([Direction.North, Direction.East, Direction.South], r.Steps.Select(s => s.Pose.Dir));
        Assert.All(r.Steps, s => Assert.Equal((1, 1), (s.Pose.X, s.Pose.Y)));
    }

    [Fact]
    public void MovingIntoWall_Crashes_AndStops()
    {
        var crashBlock = L;
        var forward = F;
        var r = Interpreter.Run(Hall, [crashBlock, forward, F, F]);
        Assert.Equal(RunOutcome.Crash, r.Outcome);
        Assert.Equal(StepAction.Crash, r.Steps[^1].Action);
        Assert.Equal(forward.Id, r.Steps[^1].BlockId);
        Assert.Equal(new Pose(1, 1, Direction.North), r.Steps[^1].Pose);
        Assert.Equal(2, r.Steps.Count);
    }

    [Fact]
    public void OffGrid_CountsAsWall()
    {
        var open = new Level(0, "o", ["SG"], Direction.West, 0, Levels.Sandbox.Toolbox);
        Assert.Equal(RunOutcome.Crash, Interpreter.Run(open, [F]).Outcome);
    }

    [Fact]
    public void ReachingGoal_SucceedsImmediately_EvenWithBlocksLeft()
    {
        var r = Interpreter.Run(Hall, [F, F, F, F, F, F]);
        Assert.Equal(RunOutcome.Success, r.Outcome);
        Assert.Equal(3, r.Steps.Count);
        Assert.Equal(StepAction.Goal, r.Steps[^1].Action);
    }

    [Fact]
    public void Repeat_RunsBodyCountTimes_AndNests()
    {
        var r = Interpreter.Run(Levels.Sandbox, [Repeat(2, Repeat(3, R))]);
        Assert.Equal(6, r.Steps.Count);
        Assert.Equal(Direction.West, r.Steps[^1].Pose.Dir); // 6 right turns from East
    }

    [Fact]
    public void IfWallAhead_OnlyRunsWhenBlocked()
    {
        // At (1,1) facing East: open ahead. Facing North: wall.
        Assert.Empty(Interpreter.Run(Hall, [IfWall(R)]).Steps);
        Assert.Equal(2, Interpreter.Run(Hall, [L, IfWall(R)]).Steps.Count);
    }

    [Fact]
    public void IfPathLeftRight_CheckSideCells()
    {
        // At (1,1) facing East: right (South) is open at (1,2); left (North) is wall.
        Assert.Single(Interpreter.Run(Hall, [IfRight(R)]).Steps);
        Assert.Empty(Interpreter.Run(Hall, [IfLeft(L)]).Steps);
    }

    [Fact]
    public void RepeatUntilGoal_LoopsUntilFlag()
    {
        var r = Interpreter.Run(Hall, [Until(F)]);
        Assert.Equal(RunOutcome.Success, r.Outcome);
    }

    [Fact]
    public void RepeatUntilGoal_WithNoMoves_HitsEvaluationCap()
    {
        var r = Interpreter.Run(Hall, [Until(IfWall(R))]); // facing open hall: never turns, never moves
        Assert.Equal(RunOutcome.StepLimit, r.Outcome);
        Assert.Empty(r.Steps);
    }

    [Fact]
    public void SpinningForever_HitsActionCap()
    {
        var r = Interpreter.Run(Hall, [Until(R)]);
        Assert.Equal(RunOutcome.StepLimit, r.Outcome);
        Assert.Equal(Interpreter.MaxActions, r.Steps.Count);
    }

    [Fact]
    public void EmptyProgram_IsOutOfBlocks()
    {
        Assert.Equal(RunOutcome.OutOfBlocks, Interpreter.Run(Hall, []).Outcome);
    }
}

public class PlaybackTests
{
    private static readonly RunResult Crash = Interpreter.Run(Levels.DemoBasics, [F, F, F]);
    private static readonly RunResult Win = Interpreter.Run(Levels.DemoBasics, [F, F, R, F, F]);

    [Fact]
    public void Frame_ShowsStepByTime()
    {
        var f = Playback.Frame(Crash, Playback.StepTime * 1.5);
        Assert.Equal(1, f.StepIndex);
        Assert.Equal(PlayStatus.Moving, f.Status);
        Assert.Equal(new Pose(3, 1, Direction.East), f.Pose);
    }

    [Fact]
    public void Frame_CrashShowsResultThenResetsToStart()
    {
        var during = Playback.Frame(Crash, Playback.StepTime * 3 + TimeSpan.FromMilliseconds(100));
        Assert.Equal(PlayStatus.Crashed, during.Status);
        Assert.False(during.Done);

        var after = Playback.Frame(Crash, Playback.Duration(Crash));
        Assert.True(after.Done);
        Assert.Equal(PlayStatus.Idle, after.Status);
        Assert.Equal(Levels.DemoBasics.StartPose, after.Pose);
    }

    [Fact]
    public void Frame_SuccessStaysOnGoalCelebrating()
    {
        var after = Playback.Frame(Win, Playback.Duration(Win) + TimeSpan.FromSeconds(5));
        Assert.Equal(PlayStatus.Celebrating, after.Status);
        Assert.Equal((3, 3), (after.Pose.X, after.Pose.Y));
        Assert.Equal(Playback.StepTime * 5 + Playback.SuccessTime, Playback.Duration(Win));
    }
}

public class ProgramOpsTests
{
    [Fact]
    public void Count_IncludesNestedBlocks()
    {
        Assert.Equal(4, ProgramOps.Count([Repeat(4, Repeat(4, F), R)]));
    }

    [Fact]
    public void Move_RefusesToDropContainerIntoItself()
    {
        var inner = Repeat(2, F);
        var outer = Repeat(2, inner);
        List<Block> program = [outer];
        Assert.False(ProgramOps.Move(program, outer.Id, inner.Body, 0));
        Assert.False(ProgramOps.Move(program, outer.Id, outer.Body, 0));
        Assert.Single(program);
    }

    [Fact]
    public void Move_ReordersWithinSameList()
    {
        var a = F; var b = L; var c = R;
        List<Block> program = [a, b, c];
        Assert.True(ProgramOps.Move(program, a.Id, program, 3)); // drop at end
        Assert.Equal([b, c, a], program);
    }

    [Fact]
    public void Remove_FindsNestedBlocks()
    {
        var f = F;
        List<Block> program = [Repeat(2, L, f)];
        Assert.Same(f, ProgramOps.Remove(program, f.Id));
        Assert.Equal(2, ProgramOps.Count(program));
    }
}

public class RunAnimatorTests
{
    [Fact]
    public void Update_CuesEachEventOnce_AndOnlyLatestStepWhenLagging()
    {
        var run = Interpreter.Run(Levels.DemoBasics, [F, F, F]); // move, move, crash
        var start = DateTimeOffset.UnixEpoch;
        var anim = new RunAnimator();
        anim.Begin(run, start);
        var cues = new List<Cue>();

        anim.Update(start, Levels.DemoBasics.StartPose, cues);
        Assert.Equal([Cue.Step], cues);
        cues.Clear();
        anim.Update(start + Playback.StepTime * 0.5, Levels.DemoBasics.StartPose, cues);
        Assert.Empty(cues);

        anim.Update(start + Playback.StepTime * 2.1, Levels.DemoBasics.StartPose, cues); // lagged past step 1
        Assert.Equal([Cue.Bonk], cues);
        cues.Clear();

        var f = anim.Update(start + Playback.Duration(run), Levels.DemoBasics.StartPose, cues);
        Assert.Equal([Cue.Done], cues);
        Assert.Equal(PlayStatus.Idle, f.Status);
    }

    [Fact]
    public void Update_CuesOutOfBlocksAndDizzy()
    {
        var anim = new RunAnimator();
        var cues = new List<Cue>();
        var oob = Interpreter.Run(Levels.DemoBasics, [F]);
        anim.Begin(oob, DateTimeOffset.UnixEpoch);
        anim.Update(DateTimeOffset.UnixEpoch + Playback.StepTime * 1.1, default, cues);
        Assert.Contains(Cue.OutOfBlocks, cues);

        cues.Clear();
        var dizzy = Interpreter.Run(Levels.DemoBasics, [Until(IfWall(R))]);
        anim.Begin(dizzy, DateTimeOffset.UnixEpoch);
        anim.Update(DateTimeOffset.UnixEpoch, default, cues);
        Assert.Equal([Cue.Dizzy], cues);
    }
}
