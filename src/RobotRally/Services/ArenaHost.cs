using RobotRally.Game;

namespace RobotRally.Services;

/// <summary>
/// Runs the lobby bumper-car physics at 20 frames a second while the game is in the lobby, and hands each frame to
/// every screen showing the arena. Outside the lobby the cars are parked.
/// </summary>
public sealed class ArenaHost(GameService game, LobbyArena arena, ILogger<ArenaHost> log) : BackgroundService
{
    public static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

    public LobbyArena Arena => arena;

    /// <summary>Raised on a background thread; subscribers must marshal (InvokeAsync).</summary>
    public event Action<ArenaFrame>? Frame;

    public bool Live => game.Phase == GamePhase.Lobby;

    /// <summary>Arrow keys from a kid's browser. Ignored outside the lobby.</summary>
    public void Drive(string playerId, DriveInput input)
    {
        if (Live) arena.SetInput(playerId, input);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Tick);
        var wasLive = false;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var live = Live;
                if (!live)
                {
                    if (wasLive) arena.Halt();
                    wasLive = false;
                    continue;
                }
                wasLive = true;
                arena.Sync(game.Players.Select(p => p.Id));
                var frame = arena.Step(Tick.TotalSeconds);
                var handlers = Frame;
                if (handlers is null) continue;
                foreach (var h in handlers.GetInvocationList().Cast<Action<ArenaFrame>>())
                {
                    try { h(frame); }
                    catch { /* a dead circuit must not stop the others */ }
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Arena tick failed");
            }
        }
    }
}
