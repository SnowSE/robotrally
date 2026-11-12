using Velopack;
using Velopack.Sources;

namespace RobotRally.Desktop;

/// <summary>
/// Looks for a newer GitHub release shortly after start-up and then every half hour, and downloads it in the background.
/// The projector then offers a restart (DesktopShell.RestartToUpdate); if nobody restarts, the update installs when
/// the window closes. Quiet when offline, and does nothing in a copy that wasn't installed from a release (e.g. run
/// from a build folder), since there's no package to replace.
/// </summary>
internal sealed class DesktopUpdates(DesktopShell shell, ILogger<DesktopUpdates> log) : BackgroundService
{
    /// <summary>Where releases are published (public, so no token is needed).</summary>
    public const string Repository = "https://github.com/SnowSE/robotrally";

    private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var manager = new UpdateManager(new GithubSource(Repository, accessToken: null, prerelease: false));
        if (!manager.IsInstalled)
        {
            log.LogInformation("Not installed from a release, so no automatic updates");
            return;
        }

        try
        {
            await Task.Delay(FirstCheck, stoppingToken);
            using var timer = new PeriodicTimer(Every);
            do
            {
                if (shell.UpdateReady is null) await CheckAsync(manager, stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { }
    }

    private async Task CheckAsync(UpdateManager manager, CancellationToken stoppingToken)
    {
        try
        {
            var update = await manager.CheckForUpdatesAsync();
            if (update is null) return;
            log.LogInformation("Downloading update {Version}", update.TargetFullRelease.Version);
            await manager.DownloadUpdatesAsync(update, cancelToken: stoppingToken);
            shell.Downloaded(manager, update);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            log.LogInformation("Couldn't check for updates ({Error})", ex.Message); // offline is normal at events
        }
    }
}
