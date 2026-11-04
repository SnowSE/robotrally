namespace RobotRally.Game;

using static Block;

/// <summary>A nudge for a stuck kid, optionally with a few blocks to show.</summary>
public sealed record Hint(string Text, List<Block>? Blocks = null);

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
            "#*#.#",
            "#..G#",
            "#####",
        ], Direction.East, 7, Motion, Motion, "Get to the flag! Can you grab the ⭐ too?"),

        new(2, "Twisty Path", [
            "#######",
            "#S.####",
            "##.####",
            "##...##",
            "##*#.G#",
            "##...##",
            "#######",
        ], Direction.East, 14, Motion, null, "Lots of turns!"),

        new(3, "Debug Alley", [
            "########",
            "#S....##",
            "####..##",
            "#G...*##",
            "########",
        ], Direction.East, 12, Motion, null, "Which way is right?"),

        new(4, "Long Hallway", [
            "###########",
            "#S........#",
            "#*#######.#",
            "#.#######.#",
            "#.#######.#",
            "#.#######.#",
            "#........G#",
            "###########",
        ], Direction.East, 6, WithRepeat, [BlockKind.Repeat], "Too many blocks? Repeat!"),

        new(5, "Staircase", [
            "#########",
            "#S.######",
            "#...#####",
            "##...####",
            "###*..###",
            "####...##",
            "#####.G##",
            "#########",
        ], Direction.East, 6, WithRepeat, null, "Find the pattern."),

        new(6, "Around the Block", [
            "########",
            "#S....*#",
            "#####..#",
            "#G###..#",
            "#.###..#",
            "#......#",
            "#......#",
            "########",
        ], Direction.East, 5, WithRepeat, null, "A repeat inside a repeat?!"),

        new(7, "Wall Bumper", [
            "#########",
            "#...*...#",
            "#.#####.#",
            "#.#S....#",
            "#.#####.#",
            "#...G...#",
            "#########",
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
            "#.......#*#",
            "###.#######",
            "###......G#",
            "###########",
        ], Direction.East, 8, Everything, null, "Dead ends everywhere. Can you write a maze solver?"),
    ];

    /// <summary>Known-good programs (used by tests and the admin's live demo).</summary>
    public static List<Block> Solution(int levelNumber) => levelNumber switch
    {
        1 => [F, F, R, F, F],
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

    /// <summary>Fewest blocks we know of for a level: using this many (or fewer) earns full block points.</summary>
    public static int Par(int levelNumber) => ProgramOps.Count(Solution(levelNumber));

    /// <summary>A program that grabs every bonus star on the way to the flag (null if the level has none).</summary>
    public static List<Block>? StarSolution(int levelNumber) => levelNumber switch
    {
        1 => [R, F, F, L, F, F],
        2 => [F, R, F, F, F, F, L, F, F, L, F, R, F],
        3 => [F, F, F, F, R, F, F, R, F, F, F, F],
        4 => [R, Repeat(5, F), L, Repeat(8, F)],
        5 => [R, Repeat(5, F, L, F, R)],
        6 => [Repeat(4, Repeat(5, F), R)],
        7 => [Repeat(20, IfWall(L), F)],
        10 => [Until(IfLeft(L), IfWall(R), IfWall(R), F)],
        _ => null,
    };

    /// <summary>Hints for kids at the back of the pack, gentlest first. The last one shows how a program could start.</summary>
    public static IReadOnlyList<Hint> Hints(int levelNumber) => levelNumber switch
    {
        1 => [
            new("Put your finger on your racer. Count the squares to the end of the road."),
            new("Drive to the end of the road, then turn toward the flag."),
            new("Start like this, then drive to the flag:", [F, F, R]),
        ],
        2 => [
            new("Do one bit at a time: drive, press GO, and see where you end up."),
            new("Pretend you're in the car. Is the next turn to YOUR left or YOUR right?"),
            new("Here's the start:", [F, R, F, F, L]),
        ],
        3 => [
            new("Look at the 🐞 block after a crash. That's the one to fix."),
            new("This road goes right, right again, then a long way to the flag."),
            new("Here's the start:", [F, F, F, R, F, F]),
        ],
        4 => [
            new("Lots of Forwards in a row? Put ONE Forward inside a 🔁 Repeat."),
            new("Count the squares in the long road. Use + to make the Repeat that number."),
            new("Here's the start:", [Repeat(8, F), R]),
        ],
        5 => [
            new("Every stair is the same: forward, turn, forward, turn back."),
            new("Put all four of those blocks inside ONE Repeat."),
            new("One stair, repeated:", [Repeat(5, F, R, F, L)]),
        ],
        6 => [
            new("The road is a square. Every side has the same number of squares."),
            new("One side is a Repeat of Forward, then a turn. Repeat that 4 times!"),
            new("Put a Repeat inside a Repeat:", [Repeat(4, Repeat(4, F))]),
        ],
        7 => [
            new("Don't count! Ask a question: is there a wall ahead?"),
            new("Repeat lots of times: if there's a wall ahead, turn. Then go forward."),
            new("Here's the idea. Pick a turn to put inside the If:", [Repeat(20, IfWall(), F)]),
        ],
        8 => [
            new("Some turns are left, some are right. Check both!"),
            new("Inside a Repeat: if there's a path left, turn left. If there's a path right, turn right. Then go forward."),
            new("Here's how it starts:", [Repeat(20, IfLeft(L))]),
        ],
        9 => [
            new("The road is very long. A Repeat with 20 isn't enough!"),
            new("🏁 Repeat until flag keeps going forever until you reach the flag."),
            new("Here's how it starts:", [Until(IfLeft(L))]),
        ],
        10 => [
            new("Real maze trick: keep one hand on the wall and never let go."),
            new("Always turn right if you can. If you're blocked, turn left. Blocked again? Turn left again."),
            new("Here's how it starts:", [Until(IfRight(R), IfWall(L))]),
        ],
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
        "#G*..##",
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
