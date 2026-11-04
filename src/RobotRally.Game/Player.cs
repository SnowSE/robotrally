using System.Collections.Immutable;

namespace RobotRally.Game;

/// <summary>A run being played back. <see cref="Award"/> is what a successful run will be worth when it ends.</summary>
public sealed record ActiveRun(Guid Id, int LevelIndex, RunResult Result, DateTimeOffset StartedAt, double Speed = 1,
    LevelScore? Award = null, bool Practice = false)
{
    public DateTimeOffset EndsAt => StartedAt + Playback.Duration(Result, Speed);
}

/// <summary>Points from a solved level, for the kid's "+points" card. <see cref="PreviousBest"/> is set for a replay.</summary>
public sealed record Award(LevelScore Score, bool Practice, int? PreviousBest)
{
    public bool Improved => PreviousBest is not { } before || Score.Total > before;
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

    /// <summary>Best score per level index (immutable snapshots, safe to read from any thread).</summary>
    public IReadOnlyDictionary<int, LevelScore> Best { get; internal set; } = ImmutableDictionary<int, LevelScore>.Empty;
    /// <summary>The program that earned each best score.</summary>
    internal ImmutableDictionary<int, List<Block>> Solutions { get; set; } = ImmutableDictionary<int, List<Block>>.Empty;
    public int Score { get; internal set; }
    public Award? LastAward { get; internal set; }

    /// <summary>The 🐇 button: plays runs <see cref="Playback.FastSpeed"/> times faster.</summary>
    public bool Fast { get; internal set; }

    /// <summary>After finishing: the level being replayed, if any.</summary>
    public int? PracticeIndex { get; internal set; }
    public DateTimeOffset PracticeStartedAt { get; internal set; }

    /// <summary>The level this player's editor is on right now, or null (finished and not replaying).</summary>
    public int? PlayingIndex => Finished ? PracticeIndex : LevelIndex;

    /// <summary>The program in the editor for the current level (a private copy; clone before handing out).</summary>
    internal List<Block> ProgramStore { get; set; } = [];
    public List<Block> CloneProgram() => ProgramOps.CloneAll(ProgramStore);
    public List<Block>? CloneSolution(int levelIndex) =>
        Solutions.TryGetValue(levelIndex, out var s) ? ProgramOps.CloneAll(s) : null;

    public ActiveRun? CurrentRun { get; internal set; }
}
