namespace RobotRally.Game;

public enum BlockKind { Forward, TurnLeft, TurnRight, Repeat, IfWallAhead, IfPathLeft, IfPathRight, RepeatUntilGoal }

public sealed class Block
{
    public const int MinCount = 2;
    public const int MaxCount = 20;
    public const int DefaultCount = 3;

    public Guid Id { get; init; } = Guid.NewGuid();
    public BlockKind Kind { get; init; }
    public int Count { get; set; } = DefaultCount;
    public List<Block> Body { get; init; } = [];

    public bool IsContainer => Kind.IsContainer();

    public Block Clone() => new()
    {
        Id = Id,
        Kind = Kind,
        Count = Count,
        Body = Body.Select(b => b.Clone()).ToList(),
    };

    // Convenience builders (used by levels, tests and the scripted demo).
    public static Block F => new() { Kind = BlockKind.Forward };
    public static Block L => new() { Kind = BlockKind.TurnLeft };
    public static Block R => new() { Kind = BlockKind.TurnRight };
    public static Block Repeat(int n, params Block[] body) => new() { Kind = BlockKind.Repeat, Count = n, Body = [.. body] };
    public static Block IfWall(params Block[] body) => new() { Kind = BlockKind.IfWallAhead, Body = [.. body] };
    public static Block IfLeft(params Block[] body) => new() { Kind = BlockKind.IfPathLeft, Body = [.. body] };
    public static Block IfRight(params Block[] body) => new() { Kind = BlockKind.IfPathRight, Body = [.. body] };
    public static Block Until(params Block[] body) => new() { Kind = BlockKind.RepeatUntilGoal, Body = [.. body] };
}

public static class BlockKindExtensions
{
    public static bool IsContainer(this BlockKind k) =>
        k is BlockKind.Repeat or BlockKind.IfWallAhead or BlockKind.IfPathLeft or BlockKind.IfPathRight or BlockKind.RepeatUntilGoal;

    public static string Icon(this BlockKind k) => k switch
    {
        BlockKind.Forward => "⬆️",
        BlockKind.TurnLeft => "↩️",
        BlockKind.TurnRight => "↪️",
        BlockKind.Repeat => "🔁",
        BlockKind.IfWallAhead => "🧱",
        BlockKind.IfPathLeft => "⬅️",
        BlockKind.IfPathRight => "➡️",
        BlockKind.RepeatUntilGoal => "🏁",
        _ => "?",
    };

    public static string Label(this BlockKind k) => k switch
    {
        BlockKind.Forward => "Forward",
        BlockKind.TurnLeft => "Turn left",
        BlockKind.TurnRight => "Turn right",
        BlockKind.Repeat => "Repeat",
        BlockKind.IfWallAhead => "If wall ahead",
        BlockKind.IfPathLeft => "If path to the left",
        BlockKind.IfPathRight => "If path to the right",
        BlockKind.RepeatUntilGoal => "Repeat until flag",
        _ => "?",
    };

    /// <summary>Kid-friendly explanation, shown when a block is new.</summary>
    public static string Explain(this BlockKind k) => k switch
    {
        BlockKind.Forward => "Moves your racer one square forward.",
        BlockKind.TurnLeft => "Turns your racer to its left. It doesn't move!",
        BlockKind.TurnRight => "Turns your racer to its right. It doesn't move!",
        BlockKind.Repeat => "Does the blocks inside it again and again. Use + and − to pick how many times!",
        BlockKind.IfWallAhead => "Only does the blocks inside it IF there's a wall right in front of your racer.",
        BlockKind.IfPathLeft => "Only does the blocks inside IF there's an open path on your racer's left side.",
        BlockKind.IfPathRight => "Only does the blocks inside IF there's an open path on your racer's right side.",
        BlockKind.RepeatUntilGoal => "Keeps doing the blocks inside, over and over, until your racer reaches the flag!",
        _ => "",
    };

    /// <summary>CSS category for coloring.</summary>
    public static string Category(this BlockKind k) => k switch
    {
        BlockKind.Forward or BlockKind.TurnLeft or BlockKind.TurnRight => "motion",
        BlockKind.Repeat or BlockKind.RepeatUntilGoal => "loop",
        _ => "logic",
    };
}
