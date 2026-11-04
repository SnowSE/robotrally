using RobotRally.Game;
using static RobotRally.Game.Block;

namespace RobotRally.Tests;

public class LevelTests
{
    public static TheoryData<int> LevelNumbers => new(Levels.All.Select(l => l.Number));

    [Fact]
    public void ThereAreTenLevelsNumberedInOrder()
    {
        Assert.Equal(Enumerable.Range(1, 10), Levels.All.Select(l => l.Number));
    }

    [Theory, MemberData(nameof(LevelNumbers))]
    public void ReferenceSolution_SolvesLevel_WithinLimit_UsingToolboxBlocks(int n)
    {
        var level = Levels.All[n - 1];
        var solution = Levels.Solution(n);
        Assert.Equal(RunOutcome.Success, Interpreter.Run(level, solution).Outcome);
        Assert.True(ProgramOps.Count(solution) <= level.BlockLimit, $"solution uses {ProgramOps.Count(solution)} > {level.BlockLimit}");
        Assert.All(ProgramOps.KindsUsed(solution), k => Assert.Contains(k, level.Toolbox));
        Assert.All(solution.SelectMany(ProgramOpsAllRepeats), c => Assert.InRange(c, Block.MinCount, Block.MaxCount));
    }

    private static IEnumerable<int> ProgramOpsAllRepeats(Block b) =>
        ProgramOps.All([b]).Where(x => x.Kind == BlockKind.Repeat).Select(x => x.Count);

    [Theory, MemberData(nameof(LevelNumbers))]
    public void NewBlocks_AreInToolbox(int n)
    {
        var level = Levels.All[n - 1];
        Assert.All(level.NewBlocks, k => Assert.Contains(k, level.Toolbox));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EarlyLevels_SolvableWithStraightLineCode(int n)
    {
        var level = Levels.All[n - 1];
        Assert.True(ShortestStraightLine(level) <= level.BlockLimit);
    }

    [Theory]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public void LaterLevels_RequireLoops_StraightLineIsOverLimit(int n)
    {
        var level = Levels.All[n - 1];
        Assert.True(ShortestStraightLine(level) > level.BlockLimit);
    }

    [Fact]
    public void Level6_FlatRepeatDoesNotFit_NestedRepeatIsNeeded()
    {
        var flat = new List<Block> { Repeat(4, F, F, F, F, R) };
        Assert.Equal(RunOutcome.Success, Interpreter.Run(Levels.All[5], flat).Outcome);
        Assert.True(ProgramOps.Count(flat) > Levels.All[5].BlockLimit);
    }

    [Fact]
    public void Level7_NestedRepeatAlone_Crashes()
    {
        Assert.NotEqual(RunOutcome.Success, Interpreter.Run(Levels.All[6], [Repeat(4, Repeat(4, F), R)]).Outcome);
    }

    [Fact]
    public void Level8_WallOnlyProgram_Fails()
    {
        Assert.NotEqual(RunOutcome.Success, Interpreter.Run(Levels.All[7], [Repeat(20, IfWall(R), F)]).Outcome);
        Assert.NotEqual(RunOutcome.Success, Interpreter.Run(Levels.All[7], [Repeat(20, IfWall(L), F)]).Outcome);
    }

    [Fact]
    public void Level9_Level8SolutionRunsOutOfRepeats()
    {
        Assert.Equal(RunOutcome.OutOfBlocks, Interpreter.Run(Levels.All[8], Levels.Solution(8)).Outcome);
    }

    [Fact]
    public void Level10_Level9SolutionHitsDeadEnd()
    {
        Assert.NotEqual(RunOutcome.Success, Interpreter.Run(Levels.All[9], Levels.Solution(9)).Outcome);
    }

    [Fact]
    public void Level10_LeftHandRuleAlsoWorks()
    {
        Assert.Equal(RunOutcome.Success, Interpreter.Run(Levels.All[9], [Until(IfLeft(L), IfWall(R), IfWall(R), F)]).Outcome);
    }

    [Fact]
    public void DemoLevels_ScriptedProgramsBehaveAsNarrated()
    {
        Assert.Equal(RunOutcome.Crash, Interpreter.Run(Levels.DemoBasics, [F, F, F]).Outcome);
        Assert.Equal(RunOutcome.Success, Interpreter.Run(Levels.DemoBasics, [F, F, R, F, F]).Outcome);
        Assert.Equal(RunOutcome.Success, Interpreter.Run(Levels.DemoRepeat, [Repeat(6, F)]).Outcome);
        Assert.Equal(RunOutcome.Success, Interpreter.Run(Levels.DemoIf, [Until(IfWall(R), F)]).Outcome);
    }

    /// <summary>Fewest Forward/Turn blocks needed to reach the goal (BFS over position + facing).</summary>
    private static int ShortestStraightLine(Level level)
    {
        var start = level.StartPose;
        var dist = new Dictionary<Pose, int> { [start] = 0 };
        var q = new Queue<Pose>([start]);
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            foreach (var next in new[] { p.Ahead(), p.TurnedLeft(), p.TurnedRight() })
            {
                if (level.IsWall(next.X, next.Y) || dist.ContainsKey(next)) continue;
                dist[next] = dist[p] + 1;
                if (level.IsGoal(next.X, next.Y)) return dist[next];
                q.Enqueue(next);
            }
        }
        throw new InvalidOperationException($"Level {level.Number} is unsolvable");
    }
}
