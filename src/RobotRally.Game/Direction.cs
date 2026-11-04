namespace RobotRally.Game;

public enum Direction { North, East, South, West }

public readonly record struct Pose(int X, int Y, Direction Dir)
{
    public Pose Ahead() => Dir switch
    {
        Direction.North => this with { Y = Y - 1 },
        Direction.East => this with { X = X + 1 },
        Direction.South => this with { Y = Y + 1 },
        _ => this with { X = X - 1 },
    };

    public Pose TurnedLeft() => this with { Dir = (Direction)(((int)Dir + 3) % 4) };
    public Pose TurnedRight() => this with { Dir = (Direction)(((int)Dir + 1) % 4) };
}

public static class DirectionExtensions
{
    /// <summary>Degrees clockwise from East (art is drawn facing East).</summary>
    public static int Degrees(this Direction d) => d switch
    {
        Direction.East => 0,
        Direction.South => 90,
        Direction.West => 180,
        _ => 270,
    };
}
