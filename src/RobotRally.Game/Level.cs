namespace RobotRally.Game;

/// <summary>
/// A grid maze. Map chars: '#' wall, '.' floor, 'S' start (floor), 'G' goal. Off-grid counts as wall.
/// </summary>
public sealed class Level
{
    public Level(int number, string name, string[] map, Direction startDir, int blockLimit,
        BlockKind[] toolbox, BlockKind[]? newBlocks = null, string? tagline = null)
    {
        if (map.Length == 0 || map.Any(r => r.Length != map[0].Length))
            throw new ArgumentException($"Level {number} map must be a non-empty rectangle.");
        Number = number;
        Name = name;
        Map = map;
        StartDir = startDir;
        BlockLimit = blockLimit;
        Toolbox = toolbox;
        NewBlocks = newBlocks ?? [];
        Tagline = tagline ?? "";
        Start = Find('S') ?? throw new ArgumentException($"Level {number} has no start.");
        Goal = Find('G') ?? throw new ArgumentException($"Level {number} has no goal.");
    }

    public int Number { get; }
    public string Name { get; }
    public string[] Map { get; }
    public Direction StartDir { get; }
    /// <summary>Max blocks in a program. 0 = unlimited.</summary>
    public int BlockLimit { get; }
    public BlockKind[] Toolbox { get; }
    public BlockKind[] NewBlocks { get; }
    public string Tagline { get; }
    public (int X, int Y) Start { get; }
    public (int X, int Y) Goal { get; }
    public int Width => Map[0].Length;
    public int Height => Map.Length;
    public Pose StartPose => new(Start.X, Start.Y, StartDir);

    public bool IsWall(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height || Map[y][x] == '#';
    public bool IsGoal(int x, int y) => (x, y) == Goal;

    private (int, int)? Find(char c)
    {
        for (var y = 0; y < Map.Length; y++)
        {
            var x = Map[y].IndexOf(c);
            if (x >= 0) return (x, y);
        }
        return null;
    }
}
