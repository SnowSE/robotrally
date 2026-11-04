namespace RobotRally.Game;

using static Block;

public static class Levels
{
    private static readonly BlockKind[] Motion = [BlockKind.Forward, BlockKind.TurnLeft, BlockKind.TurnRight];
    private static readonly BlockKind[] WithRepeat = [.. Motion, BlockKind.Repeat];
    private static readonly BlockKind[] WithIfWall = [.. WithRepeat, BlockKind.IfWallAhead];
    private static readonly BlockKind[] WithIfPath = [.. WithIfWall, BlockKind.IfPathLeft, BlockKind.IfPathRight];
    private static readonly BlockKind[] Everything = [.. WithIfPath, BlockKind.RepeatUntilGoal];

    public static IReadOnlyList<Level> All { get; } =
    [
        new(1, "First Steps", [
            "#####",
            "#S..#",
            "###G#",
            "#####",
        ], Direction.East, 6, Motion, Motion, "Get to the flag!"),

        new(2, "Twisty Path", [
            "#######",
            "#S.####",
            "##.####",
            "##...##",
            "####.G#",
            "#######",
        ], Direction.East, 12, Motion, null, "Lots of turns!"),

        new(3, "Debug Alley", [
            "########",
            "#S...###",
            "####.###",
            "#G...###",
            "########",
        ], Direction.East, 11, Motion, null, "Which way is right?"),

        new(4, "Long Hallway", [
            "###########",
            "#S........#",
            "#########.#",
            "#########.#",
            "#########.#",
            "#########.#",
            "#########G#",
            "###########",
        ], Direction.East, 5, WithRepeat, [BlockKind.Repeat], "Too many blocks? Repeat!"),

        new(5, "Staircase", [
            "#########",
            "#S.######",
            "##..#####",
            "###..####",
            "####..###",
            "#####..##",
            "######G##",
            "#########",
        ], Direction.East, 5, WithRepeat, null, "Find the pattern."),

        new(6, "Around the Block", [
            "#######",
            "#S....#",
            "#####.#",
            "#G###.#",
            "#.###.#",
            "#.....#",
            "#######",
        ], Direction.East, 5, WithRepeat, null, "A repeat inside a repeat?!"),

        new(7, "Wall Bumper", [
            "#######",
            "#S....#",
            "#####.#",
            "#..G#.#",
            "#.###.#",
            "#.....#",
            "#######",
        ], Direction.East, 5, WithIfWall, [BlockKind.IfWallAhead], "Turn only when you need to."),

        new(8, "Left or Right?", [
            "###########",
            "#######..G#",
            "#######.###",
            "###...#.###",
            "###.#.#.###",
            "###.#.#.###",
            "###.#...###",
            "#S..#######",
            "###########",
        ], Direction.East, 6, WithIfPath, [BlockKind.IfPathLeft, BlockKind.IfPathRight], "Look both ways!"),

        new(9, "The Long Way", [
            "###########",
            "#S....#####",
            "#####.#####",
            "#####.....#",
            "#########.#",
            "###.....#.#",
            "###.###.#.#",
            "#G..###...#",
            "###########",
        ], Direction.East, 6, Everything, [BlockKind.RepeatUntilGoal], "Keep going until the flag!"),

        new(10, "The Maze", [
            "###########",
            "#S....#####",
            "###.#.#####",
            "#.#.#.....#",
            "#.#####.#.#",
            "#.......#.#",
            "###.#######",
            "###......G#",
            "###########",
        ], Direction.East, 8, Everything, null, "Dead ends everywhere. Can you write a maze solver?"),
    ];

    /// <summary>Known-good programs (used by tests and the admin's live demo).</summary>
    public static List<Block> Solution(int levelNumber) => levelNumber switch
    {
        1 => [F, F, R, F],
        2 => [F, R, F, F, L, F, F, R, F, L, F],
        3 => [F, F, F, R, F, F, R, F, F, F],
        4 => [Repeat(8, F), R, Repeat(5, F)],
        5 => [Repeat(5, F, R, F, L)],
        6 => [Repeat(4, Repeat(4, F), R)],
        7 => [Repeat(20, IfWall(R), F)],
        8 => [Repeat(20, IfLeft(L), IfRight(R), F)],
        9 => [Until(IfLeft(L), IfRight(R), F)],
        10 => [Until(IfRight(R), IfWall(L), IfWall(L), F)],
        _ => [],
    };

    // ---- Demo levels (never used in the race, so no spoilers) ----

    public static Level DemoBasics { get; } = new(0, "Demo: Basics", [
        "######",
        "#S..##",
        "###.##",
        "###G##",
        "######",
    ], Direction.East, 0, Motion, Motion);

    public static Level DemoRepeat { get; } = new(0, "Demo: Repeat", [
        "#########",
        "#S.....G#",
        "#########",
    ], Direction.East, 3, WithRepeat, [BlockKind.Repeat]);

    public static Level DemoIf { get; } = new(0, "Demo: If", [
        "#######",
        "#S...##",
        "####.##",
        "#G...##",
        "#######",
    ], Direction.East, 0, Everything, [BlockKind.IfWallAhead, BlockKind.RepeatUntilGoal]);

    public static Level Sandbox { get; } = new(0, "Sandbox", [
        "##########",
        "#S.......#",
        "#..##....#",
        "#...#..#.#",
        "#.#....#.#",
        "#.#..###.#",
        "#......#G#",
        "##########",
    ], Direction.East, 0, Everything);
}
