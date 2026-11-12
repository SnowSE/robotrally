using System.Diagnostics;
using RobotRally.Services;
using Velopack;

namespace RobotRally.Desktop;

/// <summary>What the projector controls can ask of the desktop app: open the racer page here, and install an update.</summary>
internal sealed class DesktopShell : IDesktopShell
{
    private readonly object _gate = new();
    private UpdateManager? _manager;
    private UpdateInfo? _pending;

    public string LocalUrl { get; set; } = "http://localhost:5000";

    public string Version { get; } = CurrentVersion();

    public string? UpdateReady
    {
        get { lock (_gate) return _pending?.TargetFullRelease.Version.ToString(); }
    }

    public event Action? UpdateReadyChanged;

    public void OpenRacer()
    {
        try { Process.Start(new ProcessStartInfo(LocalUrl) { UseShellExecute = true }); }
        catch { /* no default browser: the kids-go-to address is on screen anyway */ }
    }

    /// <summary>Called by <see cref="DesktopUpdates"/> once an update is downloaded.</summary>
    internal void Downloaded(UpdateManager manager, UpdateInfo update)
    {
        lock (_gate) (_manager, _pending) = (manager, update);
        UpdateReadyChanged?.Invoke();
    }

    public void RestartToUpdate()
    {
        UpdateManager? manager;
        UpdateInfo? pending;
        lock (_gate) (manager, pending) = (_manager, _pending);
        if (manager is not null && pending is not null) manager.ApplyUpdatesAndRestart(pending);
    }

    /// <summary>The window closed without a restart: install the waiting update quietly as the app exits, so the next
    /// launch is the new version.</summary>
    public void ApplyOnExit()
    {
        UpdateManager? manager;
        UpdateInfo? pending;
        lock (_gate) (manager, pending) = (_manager, _pending);
        if (manager is null || pending is null) return;
        try { manager.WaitExitThenApplyUpdates(pending, silent: true, restart: false); }
        catch { /* it'll be offered again next time */ }
    }

    /// <summary>The version the releases use (1.0.7), not the four-part assembly form.</summary>
    private static string CurrentVersion()
    {
        var v = typeof(DesktopShell).Assembly.GetName().Version;
        return v is null ? "unknown" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}
