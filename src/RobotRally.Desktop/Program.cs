using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Photino.NET;
using Velopack;
using RobotRally.Services;

namespace RobotRally.Desktop;

/// <summary>
/// Starts the Robot Rally server on this computer (reachable from the network) and opens the projector page in a
/// window, already logged in. Closing the window stops the game.
/// </summary>
internal static class Program
{
    /// <summary>Port 5000 if it's free (easy for kids to type), else the next few, else whatever the system offers (0).
    /// Something else often holds 5000, e.g. AirPlay Receiver on a Mac. The join address on screen shows the real port.</summary>
    private static IEnumerable<int> Ports => [.. Enumerable.Range(5000, 10), 0];

    [STAThread] // the Windows web view needs it
    private static void Main(string[] args)
    {
        // Must come first: right after an update installs, this finishes the job (and may exit instead of returning).
        VelopackApp.Build().Run();

        var shell = new DesktopShell();
        WebApplication? app = null;
        try
        {
            foreach (var port in Ports)
            {
                app = RobotRallyServer.Create(args, builder =>
                {
                    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
                    builder.Services.AddSingleton<IDesktopShell>(shell);
                    builder.Services.AddSingleton(shell);
                    builder.Services.AddHostedService<DesktopUpdates>();
                }, contentRoot: ContentRoot());
                try
                {
                    app.Start();
                    break;
                }
                catch (Exception ex) when (port != 0 && IsAddressInUse(ex))
                {
                    app.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    app = null;
                }
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return;
        }
        if (app is null) return; // unreachable: port 0 always binds or throws

        shell.LocalUrl = $"http://localhost:{Port(app)}";
        var key = app.Services.GetRequiredService<AdminAuth>().IssueToken();

        var window = new PhotinoWindow()
            .SetTitle("Robot Rally")
            // A normal-sized window, not maximized, so whatever else opens (a browser, the racer window) isn't hidden
            // behind it. Maximize it, or press F for full screen, when it goes on the projector.
            .SetUseOsDefaultSize(false)
            .SetSize(1200, 750)
            .Center()
            .Load(new Uri($"{shell.LocalUrl}/admin?key={key}"));
        shell.Window = window;
        // If the server is told to stop (logout, shutdown), close the window too instead of leaving a dead page.
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            try { window.Invoke(window.Close); } catch { }
        });
        window.WaitForClose();

        shell.ApplyOnExit();
        app.StopAsync().GetAwaiter().GetResult();
    }

    /// <summary>wwwroot and appsettings.json sit next to the executable, except in a Mac app, where they live in
    /// Contents/Resources (the executable is in Contents/MacOS).</summary>
    private static string ContentRoot()
    {
        var exeDir = AppContext.BaseDirectory;
        var macResources = Path.GetFullPath(Path.Combine(exeDir, "..", "Resources"));
        return Directory.Exists(Path.Combine(macResources, "wwwroot")) ? macResources : exeDir;
    }

    private static int Port(WebApplication app) =>
        app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses
            .Select(a => Uri.TryCreate(a.Replace("0.0.0.0", "localhost").Replace("[::]", "localhost").Replace("+", "localhost").Replace("*", "localhost"), UriKind.Absolute, out var u) ? u.Port : 0)
            .FirstOrDefault(p => p > 0) ?? 5000;

    private static bool IsAddressInUse(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is AddressInUseException or SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse }) return true;
        return false;
    }

    /// <summary>Startup failed for some other reason: say so in a window instead of vanishing.</summary>
    private static void ShowError(Exception ex)
    {
        Console.Error.WriteLine(ex);
        var message = WebUtility.HtmlEncode(ex.Message);
        new PhotinoWindow()
            .SetTitle("Robot Rally")
            .SetUseOsDefaultSize(false)
            .SetSize(640, 320)
            .Center()
            .LoadRawString($$"""
                <!doctype html><meta charset="utf-8">
                <body style="font: 18px system-ui, sans-serif; background: #3dbb5f; color: #fff; display: grid; place-items: center; height: 100vh; margin: 0; text-align: center; padding: 24px; box-sizing: border-box">
                <div><h1 style="margin: 0 0 12px">🏁 Robot Rally couldn't start</h1><p>{{message}}</p></div>
                """)
            .WaitForClose();
    }
}
