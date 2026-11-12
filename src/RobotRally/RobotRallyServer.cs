using RobotRally.Components;
using RobotRally.Game;
using RobotRally.Services;

namespace RobotRally;

/// <summary>
/// Builds the Robot Rally web app. Program.cs runs it as a plain server; RobotRally.Desktop runs the same app
/// in-process behind a desktop window.
/// </summary>
public static class RobotRallyServer
{
    /// <param name="configure">Last-minute changes before the app is built (e.g. the desktop app's services).</param>
    /// <param name="contentRoot">Where appsettings.json and wwwroot live. Defaults to the current directory.</param>
    public static WebApplication Create(string[] args, Action<WebApplicationBuilder>? configure = null, string? contentRoot = null)
    {
        // ApplicationName stays the launching app's name: the static-asset manifest is named after it, and the desktop
        // app's manifest already includes everything from this project.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = contentRoot });

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents(o => o.DetailedErrors = builder.Environment.IsDevelopment());

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(sp => new GameService(sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<LobbyArena>();
        builder.Services.AddSingleton<ArenaHost>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ArenaHost>());
        builder.Services.AddSingleton<AdminAuth>();
        builder.Services.AddSingleton<JoinInfo>();
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddSingleton<DevBots>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<DevBots>());
        }

        configure?.Invoke(builder);

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseAntiforgery();

        app.MapStaticAssets(StaticAssetsManifest(app));
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        if (app.Environment.IsDevelopment())
        {
            // Practice racers for rehearsals: http://localhost:5000/dev/bots?n=10
            app.MapGet("/dev/bots", (DevBots bots, int? n) => $"Added {bots.Add(Math.Clamp(n ?? 8, 1, 24))} practice racers.");
            // Rehearse the finish screen: solves this racer's current level (call it again after each win).
            app.MapGet("/dev/solve", (GameService game, string token) =>
            {
                if (game.Resume(token) is not { } p) return "No such racer.";
                if (p.PlayingIndex is not { } i) return "Finished.";
                game.SetFast(token, true);
                return game.Run(token, Levels.Solution(i + 1)) is null ? "Can't run right now." : $"Solving level {i + 1}.";
            });
        }

        return app;
    }

    /// <summary>The static-asset manifest in the content root if it's there (the desktop app keeps it next to wwwroot,
    /// e.g. in a Mac app's Resources folder); otherwise the default lookup next to the executable.</summary>
    private static string? StaticAssetsManifest(WebApplication app)
    {
        var path = Path.Combine(app.Environment.ContentRootPath, $"{app.Environment.ApplicationName}.staticwebassets.endpoints.json");
        return File.Exists(path) ? path : null;
    }

    /// <summary>The startup banner with the addresses for the kids and the projector.</summary>
    public static void PrintBanner(WebApplication app)
    {
        var join = app.Services.GetRequiredService<JoinInfo>();
        var auth = app.Services.GetRequiredService<AdminAuth>();
        Console.WriteLine();
        Console.WriteLine("  🤖🏁  ROBOT RALLY is running!");
        foreach (var url in join.Urls) Console.WriteLine($"     Kids go to:      {url}");
        Console.WriteLine($"     Projector/admin: {join.Primary}/admin   (password: {(auth.IsDefaultPassword ? "rally — set ROBOTRALLY_ADMIN_PASSWORD to change" : "from config")})");
        Console.WriteLine();
    }
}
