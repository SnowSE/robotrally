namespace RobotRally.Services;

/// <summary>
/// Present only when Robot Rally runs as the desktop app (RobotRally.Desktop): lets the projector controls do things
/// on the computer that is running the game.
/// </summary>
public interface IDesktopShell
{
    /// <summary>The running version, e.g. "1.0.7".</summary>
    string Version { get; }

    /// <summary>Opens the racer page in a window on this computer, so someone can play on the same machine.</summary>
    void OpenRacer();

    /// <summary>The version of an update that has been downloaded and is waiting for a restart; null until there is one.</summary>
    string? UpdateReady { get; }

    /// <summary>Raised (on a background thread) when <see cref="UpdateReady"/> changes.</summary>
    event Action? UpdateReadyChanged;

    /// <summary>Installs the waiting update and starts Robot Rally again. The game in progress ends.</summary>
    void RestartToUpdate();
}
