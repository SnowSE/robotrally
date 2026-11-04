namespace RobotRally.Game;

public sealed record ActiveRun(Guid Id, int LevelIndex, RunResult Result, DateTimeOffset StartedAt)
{
    public DateTimeOffset EndsAt => StartedAt + Playback.Duration(Result);
}

public sealed class Player
{
    public required string Id { get; init; }
    public required string Token { get; init; }
    public required string Nickname { get; init; }
    public required CharacterLook Look { get; init; }
    public required DateTimeOffset JoinedAt { get; init; }

    /// <summary>0-based index into <see cref="Levels.All"/>. Equal to the level count once finished.</summary>
    public int LevelIndex { get; internal set; }
    public DateTimeOffset LevelReachedAt { get; internal set; }
    public DateTimeOffset? FinishedAt { get; internal set; }
    public bool Finished => FinishedAt is not null;
    public int Runs { get; internal set; }
    public int Crashes { get; internal set; }

    /// <summary>The program in the editor for the current level (a private copy; clone before handing out).</summary>
    internal List<Block> ProgramStore { get; set; } = [];
    public List<Block> CloneProgram() => ProgramOps.CloneAll(ProgramStore);

    public ActiveRun? CurrentRun { get; internal set; }
}
