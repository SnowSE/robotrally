using RobotRally.Game;

namespace RobotRally.Components.Shared;

/// <summary>A racer to draw on a <see cref="Board"/>.</summary>
public sealed record BoardRacer(string Key, CharacterLook Look, Pose Pose, PlayStatus Status, bool Fast = false);
