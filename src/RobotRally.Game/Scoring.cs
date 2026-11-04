namespace RobotRally.Game;

/// <summary>Points for one solved level.</summary>
public sealed record LevelScore(int LevelNumber, int Complete, int Speed, int Blocks, int StarPoints, int StarsTaken, int BlocksUsed, TimeSpan Time)
{
    public int Total => Complete + Speed + Blocks + StarPoints;
}

/// <summary>
/// Solving a level is worth 100. Up to 100 more for speed (full points inside the level's par time, then sliding to 0),
/// up to 50 for using few blocks (full points at par, minus 10 per extra block), and 100 per bonus star.
/// </summary>
public static class Scoring
{
    public const int CompletePoints = 100;
    public const int MaxSpeedPoints = 100;
    public const int MaxBlockPoints = 50;
    public const int PointsPerExtraBlock = 10;
    public const int PointsPerStar = 100;
    public static readonly TimeSpan SpeedWindow = TimeSpan.FromMinutes(4);

    /// <summary>Time to solve a level and still get every speed point: 20 s for level 1, 10 s more per level.</summary>
    public static TimeSpan ParTime(int levelNumber) => TimeSpan.FromSeconds(20 + 10 * (levelNumber - 1));

    public static LevelScore For(int levelNumber, int blocksUsed, TimeSpan time, int stars)
    {
        var over = time - ParTime(levelNumber);
        var speed = over <= TimeSpan.Zero
            ? MaxSpeedPoints
            : (int)Math.Round(MaxSpeedPoints * Math.Max(0, 1 - over / SpeedWindow));
        var extra = Math.Max(0, blocksUsed - Levels.Par(levelNumber));
        var blocks = Math.Max(0, MaxBlockPoints - PointsPerExtraBlock * extra);
        return new LevelScore(levelNumber, CompletePoints, speed, blocks, stars * PointsPerStar, stars, blocksUsed, time);
    }
}
