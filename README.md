# Robot Rally

Kids program a racer through 10 grid mazes by dragging blocks (Forward, Turn, Repeat, If, Repeat until flag). Everyone races at the same time, and the projector shows a shared race course. Built for a ~30-minute science-night slot with up to 24 lab machines.

- Kids open `http://<laptop-ip>:5000`
- The projector opens `http://<laptop-ip>:5000/admin` (password below)

## Run it

For the event, run it in Production. This turns off the practice-bot endpoint and the detailed error pages:

```bash
cd src/RobotRally
ASPNETCORE_ENVIRONMENT=Production ROBOTRALLY_ADMIN_PASSWORD=pick-one dotnet run -c Release --no-launch-profile
```

When it starts, it prints the URLs for the kids. The laptop has more than one network address, so check which one the lab machines can reach. If the projector shows the wrong one, pin it:

```bash
JoinUrl=http://192.168.1.50:5000 ASPNETCORE_ENVIRONMENT=Production ROBOTRALLY_ADMIN_PASSWORD=pick-one dotnet run -c Release --no-launch-profile
```

If you don't set a password, it's `rally`.

### Open the firewall for the night (Fedora / firewalld)

If your firewall blocks 5000/tcp (zone `FedoraWorkstation` here; use yours), open it for this boot only. Nothing is saved, so it closes again on reboot or `firewall-cmd --reload`:

```bash
sudo firewall-cmd --zone=FedoraWorkstation --add-port=5000/tcp
# afterwards:
sudo firewall-cmd --zone=FedoraWorkstation --remove-port=5000/tcp
```


## Running the session

On the projector, **Esc** opens the controls. **F** toggles full screen.

1. **Lobby.** The projector shows the join URL. Kids build a racer (ride, add-ons, color, random silly name) and drop into the lobby.
2. **Demo.** Choose **▶ Play demo** for the narrated walkthrough. Press **Space** or **→** for each next step (a yellow "Space ▶" shows when it's ready) and **←** to start over. It covers dragging blocks, GO!, a crash with the 🐞 bug marker, fixing it, the block limit, Repeat, and If. **🎮 Live demo** gives you the real editor on any level (or the sandbox), with a **Show a solution** button.
3. **Start race.** There's a 3-2-1-GO countdown, then every kid's screen switches to Level 1. Kids who join late start at Level 1.
4. **Race.** The projector shows all 10 levels live, plus a race-track ribbon of everyone's progress. A banner announces each time someone moves up a level.
5. **Winners.** The podium appears 5 seconds after the third kid finishes all 10 levels, or whenever you click **End race and show winners**. Ranking: finishers by finish time, then by level reached, then by who got there first.
6. **Next group:** **🔄 New group (reset)** removes everyone, and every kid's screen goes back to the character builder. **🔁 Same kids race again** keeps the racers and wipes their progress.

The player list in the controls panel can remove a stray tab (✖, then confirm).

### Levels

| # | Name | New block | Limit |
|---|---|---|---|
| 1 | First Steps | Forward, Turn left, Turn right | 6 |
| 2 | Twisty Path | — | 12 |
| 3 | Debug Alley | — | 11 |
| 4 | Long Hallway | Repeat | 5 |
| 5 | Staircase | — | 5 |
| 6 | Around the Block | (a repeat inside a repeat) | 5 |
| 7 | Wall Bumper | If wall ahead | 5 |
| 8 | Left or Right? | If path to the left / right | 6 |
| 9 | The Long Way | Repeat until flag | 6 |
| 10 | The Maze | (wall-follower algorithm) | 8 |

Reference solutions are in `Levels.Solution(n)` (src/RobotRally.Game/Levels.cs), and the live demo shows them.

## Rehearse

In Development (plain `dotnet run`, which uses the launch profile), `http://localhost:5000/dev/bots?n=14` adds practice racers. They crash a lot and sometimes solve their level, so you can see a busy projector. They're removed by **New group**.

## Pre-event checklist

- [ ] Open port 5000 (above) and confirm the kids' URL loads on one lab machine.
- [ ] On that lab machine, check that drag-and-drop works, that sound plays, and that a refresh comes back as the same racer.
- [ ] Projector: log in, press F for full screen, and run the scripted demo once.
- [ ] Do a dry run with bots, then **New group**.

## Develop

```bash
dotnet test            # rules, levels (every level is checked solvable and loop-requiring), game service
```

- `src/RobotRally.Game` holds the pure rules: levels, interpreter, playback timing, and the in-memory `GameService`.
- `src/RobotRally` is the Blazor Server UI. All sounds are synthesized in `wwwroot/js/rally.js`, and all art is SVG in `Components/Shared/CharacterArt.razor`.
- Game state lives in memory, so restarting the server ends the session.

Design spec: `docs/superpowers/specs/2026-11-04-robot-rally-design.md`.
