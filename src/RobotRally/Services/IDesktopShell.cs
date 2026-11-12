namespace RobotRally.Services;

/// <summary>
/// Present only when Robot Rally runs as the desktop app (RobotRally.Desktop): lets the projector controls do things
/// on the computer that is running the game.
/// </summary>
public interface IDesktopShell
{
    /// <summary>Opens the racer page in this computer's web browser, so someone can play on the same machine.</summary>
    void OpenRacer();
}
