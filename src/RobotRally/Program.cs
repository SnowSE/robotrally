using RobotRally.Components;
using RobotRally.Game;
using RobotRally.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o => o.DetailedErrors = builder.Environment.IsDevelopment());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new GameService(sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<AdminAuth>();
builder.Services.AddSingleton<JoinInfo>();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<DevBots>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<DevBots>());
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (app.Environment.IsDevelopment())
{
    // Practice racers for rehearsals: http://localhost:5000/dev/bots?n=10
    app.MapGet("/dev/bots", (DevBots bots, int? n) => $"Added {bots.Add(Math.Clamp(n ?? 8, 1, 24))} practice racers.");
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    var join = app.Services.GetRequiredService<JoinInfo>();
    var auth = app.Services.GetRequiredService<AdminAuth>();
    Console.WriteLine();
    Console.WriteLine("  🤖🏁  ROBOT RALLY is running!");
    foreach (var url in join.Urls) Console.WriteLine($"     Kids go to:      {url}");
    Console.WriteLine($"     Projector/admin: {join.Primary}/admin   (password: {(auth.IsDefaultPassword ? "rally — set ROBOTRALLY_ADMIN_PASSWORD to change" : "from config")})");
    Console.WriteLine();
});

app.Run();
