namespace RobotRally.Game;

public enum StepAction { Move, TurnLeft, TurnRight, Crash, Goal }

public enum RunOutcome { Success, Crash, OutOfBlocks, StepLimit }

/// <summary>One animated step. <see cref="Pose"/> is where the robot is after the step
/// (for <see cref="StepAction.Crash"/> it stays put and bumps the wall). <see cref="Star"/> marks picking up a bonus star.</summary>
public readonly record struct Step(Pose Pose, StepAction Action, Guid BlockId, bool Star = false);

public sealed record RunResult(IReadOnlyList<Step> Steps, RunOutcome Outcome, Pose Start)
{
    /// <summary>Bonus stars picked up on the way (they only count if the run reaches the flag).</summary>
    public int Stars => Steps.Count(s => s.Star);
}

public static class Interpreter
{
    public const int MaxActions = 200;
    public const int MaxEvaluations = 2000;

    public static RunResult Run(Level level, IReadOnlyList<Block> program)
    {
        var run = new Runner(level);
        RunOutcome outcome;
        try
        {
            run.Exec(program);
            outcome = RunOutcome.OutOfBlocks;
        }
        catch (Stop stop)
        {
            outcome = stop.Outcome;
        }
        return new RunResult(run.Steps, outcome, level.StartPose);
    }

    private sealed class Stop(RunOutcome outcome) : Exception
    {
        public RunOutcome Outcome { get; } = outcome;
    }

    private sealed class Runner(Level level)
    {
        public List<Step> Steps { get; } = [];
        private Pose _pose = level.StartPose;
        private int _evaluations;
        private readonly HashSet<(int, int)> _stars = [];

        public void Exec(IReadOnlyList<Block> blocks)
        {
            foreach (var b in blocks) Exec(b);
        }

        private void Tick()
        {
            if (++_evaluations > MaxEvaluations) throw new Stop(RunOutcome.StepLimit);
        }

        private void Act(Pose next, StepAction action, Guid id)
        {
            if (Steps.Count >= MaxActions) throw new Stop(RunOutcome.StepLimit);
            _pose = next;
            var star = action == StepAction.Move && level.IsStar(next.X, next.Y) && _stars.Add((next.X, next.Y));
            Steps.Add(new Step(next, action, id, star));
        }

        private bool WallAt(Pose p) => level.IsWall(p.X, p.Y);

        private void Exec(Block b)
        {
            Tick();
            switch (b.Kind)
            {
                case BlockKind.Forward:
                    var ahead = _pose.Ahead();
                    if (WallAt(ahead))
                    {
                        Act(_pose, StepAction.Crash, b.Id);
                        throw new Stop(RunOutcome.Crash);
                    }
                    if (level.IsGoal(ahead.X, ahead.Y))
                    {
                        Act(ahead, StepAction.Goal, b.Id);
                        throw new Stop(RunOutcome.Success);
                    }
                    Act(ahead, StepAction.Move, b.Id);
                    break;
                case BlockKind.TurnLeft:
                    Act(_pose.TurnedLeft(), StepAction.TurnLeft, b.Id);
                    break;
                case BlockKind.TurnRight:
                    Act(_pose.TurnedRight(), StepAction.TurnRight, b.Id);
                    break;
                case BlockKind.Repeat:
                    for (var i = 0; i < Math.Clamp(b.Count, 1, Block.MaxCount); i++)
                    {
                        Tick();
                        Exec(b.Body);
                    }
                    break;
                case BlockKind.RepeatUntilGoal:
                    while (true) // reaching the goal always throws Stop(Success)
                    {
                        Tick();
                        Exec(b.Body);
                    }
                case BlockKind.IfWallAhead:
                    if (WallAt(_pose.Ahead())) Exec(b.Body);
                    break;
                case BlockKind.IfPathLeft:
                    if (!WallAt(_pose.TurnedLeft().Ahead())) Exec(b.Body);
                    break;
                case BlockKind.IfPathRight:
                    if (!WallAt(_pose.TurnedRight().Ahead())) Exec(b.Body);
                    break;
            }
        }
    }
}
