# Robot Rally — Design

*2026-11-04 · for a science-night software-engineering presentation*

## Intent

Jonathan is presenting software engineering to kids aged 6–12 in a computer lab. Robot Rally is a live, classroom-wide coding race. Each kid builds a character, then programs it through a series of grid mazes by dragging blocks such as Forward, Turn, Repeat and If. The projector shows one shared race course with every kid's character on whichever level they've reached, so the room can watch the race unfold.

The point is to make sequencing, loops, conditionals and **debugging** feel fun. Crashing is expected and funny, and fixing the program is the game.

## Goals

- Up to **24 concurrent players** (one per lab machine), all on one LAN.
- Fits a **~30-minute slot**: about 5 min sign-in/lobby, 5 min demo, 15–18 min race, and a podium at the end.
- **Reset between groups**, so the same server runs several sessions in one night.
- Delightful: lots of animation and sound, and characters kids are proud of.
- Works for a 6-year-old: early levels need no reading (icons only). Hard enough at the top end that a 12-year-old is challenged and **few or no kids finish** in the time.

## Non-goals

- Accounts, persistence across server restarts, analytics.
- Internet hosting or a cloud deployment. It runs on the presenter's laptop.
- Mobile/touch support (lab machines have a mouse).
- Kids typing free text anywhere.

## Anti-goals (failure even if it "works")

- A kid stuck staring at a broken screen, such as a lost connection or an accidental refresh that wipes them out.
- Anything inappropriate on the projector (hence no typed names).
- Sounds that won't play because the browser blocked audio.
- A level that is impossible or ambiguous, so kids give up.
- The projector showing admin clutter during the race.

## Decided by Jonathan

- **Stack:** C# Blazor.
- **Hosting:** runs on the presenter's machine; lab machines connect by the host's IP address on the LAN.
- **Lobby first:** players join and wait in a fun lobby until the admin clicks Start. Then every browser automatically switches to Level 1.
- **Admin auth:** a simple password.
- **Demo before the race,** shown on the projector, in two modes:
  - a **scripted animation** that Jonathan narrates over. It must show the whole loop: dragging blocks, running, **crashing, resetting and fixing the program**, reaching the goal, and the newer blocks (Repeat, If).
  - a **live mode** where the admin drives the kid editor on the projector to show something custom.
- **Levels:** everyone starts on Level 1. Finishing a level advances you to the next, and you appear on that level on the projector as you arrive. Levels get progressively harder, and **new blocks are introduced on later levels** (start simple → Repeat → If → …). Fixed set of levels with a **finish line**, and it should be **hard**.
- **Failing:** when a program crashes, the robot resets to the start of the current level. The kid edits and reruns.
- **Characters:** the kid picks a **base** (car, motorcycle, turtle, plane, etc.) plus **add-ons specific to that base**, so everyone can find themselves on the projector.
- **Kid screen:** only their own character and level, with the **block toolbox on the left** and blocks dragged out of it into a program.
- **Projector (admin) screen:** only the race course with all players moving on it.
- **Sound:** yes. Lab machines have small speakers.

## Builder's calls (Claude's, open to Jonathan's veto)

### Platform
- **.NET 10, Blazor Web App, Interactive Server render mode.** The SignalR circuit is the real-time channel: a singleton `GameService` holds all state in memory and raises events that components subscribe to. No database.
- Kestrel listens on `http://0.0.0.0:5000`, and the startup banner prints the LAN URL(s) for kids to type. The host's firewall port must be opened.
- Admin password comes from `appsettings.json` / the `ROBOTRALLY_ADMIN_PASSWORD` env var. The admin session is a cookie.

### Identity and reconnects
- On character creation, the browser stores a player token in `localStorage`. Reloading or reconnecting with that token resumes the same player: same character, same level, and the program they were editing.
- **Reset** (admin) clears all players and returns to an empty lobby. Every connected kid browser goes back to the character builder. Stale tokens from the previous group are rejected.
- Late joiners after Start skip the lobby and go straight to Level 1.
- The admin can remove a player, for example a duplicate from a kid who opened two tabs.

### Pages
| Route | Who | What |
|---|---|---|
| `/` | kids | character builder → lobby → game (one page with phases, driven by server state) |
| `/admin` | Jonathan | password → projector view |

The projector view (`/admin`) shows the lobby, the demo, the race or the podium, depending on game phase. Admin controls (Start Demo, Live Demo, Start Race, End Race, Reset, Remove Player) live in a **slide-out drawer** opened by a small corner button or the `Esc` key. The drawer is hidden by default so the projector stays clean.

### Characters
- Bases: **car, motorcycle, turtle, plane, rocket** (5).
- Each base has 3–4 add-ons of its own, toggled independently, for example:
  - car: spoiler, flames, racing stripes, roof light
  - motorcycle: sidecar, streamers, helmet, exhaust fire
  - turtle: sunglasses, party hat, shell spikes, bow tie
  - plane: banner, propeller hat, smoke trail, wing stars
  - rocket: fins, flame trail, astronaut window, antenna
- Plus a **color** (8 bright choices) and a **generated nickname** (adjective + animal/thing, e.g. "Zippy Octopus") with a reroll button. Nicknames are unique per session.
- All art is hand-built SVG components, layered as base + add-ons. Nothing comes from external assets.

### Game rules
- Square grid, robot has a position and a facing direction. A cell is floor, wall, or goal.
- A program runs step by step (~350 ms per step, faster on retries is fine). **Crash** = moving into a wall or off the grid. The robot does a crash animation, plays a "bonk" sound, and resets to the level start; the program stays in the editor for fixing.
- **Reaching the goal** → fanfare, confetti, and "Level N complete!" → advance to the next level after a ~2 s celebration.
- Running out of blocks in the program without reaching the goal = a "didn't make it" fail (gentler sound), then reset.
- **Infinite-loop guard:** a step cap of about 200 steps per run, which counts as a fail.
- **Block limit** per level, shown as "Blocks: 5 / 7". This is what forces kids to use Repeat and If rather than long chains, and it's the main difficulty lever.
- Robots on the same level never interact. Each kid has their own copy of the maze.
- **Execution is server-side:** the server interprets the program into a timeline of steps and broadcasts it, and both the kid screen and the projector animate it. The server is authoritative about level completion.

### Blocks and level progression (10 levels)
New blocks appear in the toolbox with a "NEW!" sparkle on the level where they're introduced.

| Lvl | Teaches | New block(s) | Shape of the puzzle |
|---|---|---|---|
| 1 | sequencing | Forward, Turn Left, Turn Right | short straight path, one turn |
| 2 | sequencing | — | a few turns |
| 3 | debugging | — | longer zigzag; tight block limit is still enough without loops |
| 4 | loops | **Repeat N** (N = 2–9 picker) | long corridor, block limit makes loops required |
| 5 | loops | — | staircase: repeat a 2–4 block pattern |
| 6 | loops | — | square/spiral, repeat with a longer body |
| 7 | conditionals | **If wall ahead → [ ]** | path where the same loop needs to turn only at walls |
| 8 | conditionals | **If path left / If path right** | branching corridors |
| 9 | while loops | **Repeat until goal** | winding path solvable with one loop + ifs |
| 10 | algorithms | — | real maze: "follow the wall" solution under a tight block limit |

- Loop and if blocks are containers. Blocks are dropped inside them, and nesting is allowed.
- Editor interactions: drag from the toolbox into the program, drag to reorder, drag back to the toolbox (or onto a trash can) to delete. **Clicking a toolbox block appends it to the end** as a fallback for small hands. There's also a Clear button.
- Every level ships with a reference solution, and a unit test asserts it solves the level within the block limit. Another test asserts that naive solutions fail where they should (e.g. Level 4 can't be solved without Repeat).

### Projector race view
- All 10 levels are shown at once as mini maze tiles in a grid (5 × 2), each labeled with the level number and any newly introduced block icon. Each player's character sits on their current level's tile and **animates its runs live**: moving, crashing, celebrating. When a player advances, their character flies or drives over to the next tile.
- A **progress ribbon** along the bottom shows every character ordered by progress.
- When a player finishes Level 10 there's a big on-screen celebration. The first three finishers take podium spots.
- **Finish race** (admin only; see Additions) → **podium screen**: top 3 by points, then level reached, then who got there first, plus a parade of everyone else with their points.

### Lobby
- Projector: a playful scene where each character drops or drives in when the kid finishes building it, with a pop sound and their nickname. It shows a big "Join at http://<ip>:5000" banner and a player count.
- Kid screen: their character idling or bouncing, "Waiting for the race to start…", and a little interactive toy (click your character to make it do a trick + sound).
- On Start: a projector countdown "3… 2… 1… GO!", then all kid browsers switch to Level 1 at the same moment.

### Demo
- **Scripted:** a fixed, self-running animation on the projector of a fake player on a demo level. It slows down at key moments and pauses at each "beat" until the admin presses Space/→ (so Jonathan controls the pace while narrating). Beats: what the toolbox is → drag Forward → run → **crash and bonk** → reset to start → fix (add a Turn) → run → goal fanfare → a later level introducing Repeat (shorter program) → If wall ahead.
- **Live:** the projector shows the full kid editor on a sandbox level. All blocks are available and there's no block limit. The admin drives it with the mouse.

### Sound
- Synthesized with the Web Audio API through a small JS interop module: block pick-up and drop, run start, step tick, bonk, fail, fanfare, level-up whoosh, lobby pop, countdown beeps, podium music. No audio files.
- Kid browsers unlock audio on the first click in the character builder (browsers block autoplay otherwise). The projector unlocks it on the admin login click.
- Mute toggle on the kid screen.

### Visual style
- Bright, chunky, rounded and cartoony. Large hit targets and big readable numbers. Colored block types (motion = blue, loops = orange, ifs = purple). Animations with CSS transitions/keyframes plus a small JS confetti helper.

## Left to the builder (implementation)
- Exact maze layouts (subject to the progression table and the solvability tests).
- Component breakdown, CSS approach, drag-and-drop implementation (HTML5 DnD with a JS interop helper is fine).
- The precise demo script and timing.

## Stuck kids
Adults walk the room and help in person. There's no skip-ahead control, and the early levels (1–3) are tuned to be quick wins. Jonathan later asked for a hint button for kids in the bottom 30% of the pack: see Additions.

## Additions (asked for by Jonathan after the first build)
- **Bonus stars** (`*` in a level map) sit on optional longer routes, a "turn in the road" or the long way round. They're on levels 1–7 and 10 (8 and 9 are single winding paths). Each level ships a star route that's tested to collect every star within the block limit, and the reference solution is tested to collect none.
- **Points:** 100 for solving a level, up to 100 for speed (par time 20 s + 10 s per level, sliding to 0 over 4 minutes, timed from reaching the level to pressing GO), up to 50 for blocks (par = reference solution, −10 per extra block), and 100 per star on a winning run. Standings and the podium rank by points.
- **The admin ends the race.** There's no automatic podium. **Finish race & show scores** shows the podium with points, and a winning run still celebrating at that moment is credited.
- **Lobby bumper cars:** the arrow keys drive your racer on a shared floor. Server-side physics (equal-mass discs, springy minimum bounce, glancing hits spin) run at 20 frames a second; the browser smooths the motion with CSS. Bump sound: one short soft "boop", played only on the machines of the two kids involved, never on the projector, because a whole room does this at once.
- **🐢/🐇 Fast button:** runs play 2.5× faster (driving only; crash and celebration timing unchanged). Toggling mid-run re-times the run from where the racer is.
- **💡 Hint button** for kids in the back 30% of the pack (by level, then points; ties don't count as behind, so no hints at the start). Three hints per level, gentlest first; the last shows how a program could start.
- **After finishing:** a big celebration (rays, trophy, fireworks, fanfare), then a "My levels" screen with each winning program and **Play again** on any level. A replay counts only if it beats that level's best score.

## Build order (Claude's call; event date not given)
Core first, polish layered on top:
1. interpreter + levels + tests
2. game service
3. kid editor
4. projector race view
5. lobby + countdown
6. characters
7. sounds
8. scripted demo
9. live demo
10. podium

## Pre-event checklist
- Open TCP 5000 in the laptop firewall.
- From one lab machine, open the URL, then confirm that drag-and-drop works, sound plays, and a refresh resumes the same player.
- Run a full dry run with a few tabs, then Reset.

## Testing
- Unit tests (xUnit) for the interpreter: moves, turns, crash, repeat, nested blocks, if conditions, repeat-until, the step cap.
- Level tests: reference solutions pass within limits, and naive or loop-free solutions fail where intended.
- GameService tests: join, rejoin by token, start, advance, finish, podium ordering, reset rejecting stale tokens.
- A manual multi-browser run (several tabs plus the projector) before the event.
