using RobotRally.Game;

namespace RobotRally.Services;

/// <summary>
/// Development-only practice racers, for rehearsing with a busy projector.
/// GET /dev/bots?n=10 adds bots. They crash a lot and occasionally solve their level.
/// </summary>
public sealed class DevBots(GameService game, ArenaHost arena) : BackgroundService
{
    private readonly List<string> _tokens = [];
    private readonly object _gate = new();

    public int Add(int n)
    {
        var added = 0;
        for (var i = 0; i < n; i++)
        {
            var b = Characters.Bases[Random.Shared.Next(Characters.Bases.Count)];
            var addOns = b.AddOns.Where(_ => Random.Shared.Next(2) == 0).Select(a => a.Id).ToList();
            var look = new CharacterLook(b.Kind, Characters.Colors[Random.Shared.Next(Characters.Colors.Count)], addOns);
            if (game.Join(look, game.SuggestNickname()) is not { } p) break;
            lock (_gate) _tokens.Add(p.Token);
            added++;
        }
        return added;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(0.5));
        var tick = 0;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            string[] tokens;
            lock (_gate) tokens = [.. _tokens];
            if (game.Phase == GamePhase.Lobby)
            {
                // Wander around the bumper-car floor.
                foreach (var token in tokens)
                    if (game.Resume(token) is { } bot && Random.Shared.NextDouble() < 0.35)
                        arena.Drive(bot.Id, (DriveInput)Random.Shared.Next(16));
                continue;
            }
            if (game.Phase != GamePhase.Race || ++tick % 3 != 0) continue;
            foreach (var token in tokens)
            {
                var p = game.Resume(token);
                if (p is null) { lock (_gate) _tokens.Remove(token); continue; }
                if (p.Finished || p.CurrentRun is not null || Random.Shared.NextDouble() > 0.3) continue;
                if (Random.Shared.NextDouble() < 0.2) game.SetFast(token, !p.Fast);
                List<Block> program = Random.Shared.NextDouble() < 0.55
                    ? [Block.F, Block.L, Block.F, Block.F]
                    : Random.Shared.NextDouble() < 0.3 && Levels.StarSolution(p.LevelIndex + 1) is { } star ? star : Levels.Solution(p.LevelIndex + 1);
                if (ProgramOps.Count(program) > game.Levels[p.LevelIndex].BlockLimit) program = [Block.L, Block.F];
                game.Run(token, program);
            }
        }
    }
}
