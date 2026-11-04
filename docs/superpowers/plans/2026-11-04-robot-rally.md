# Robot Rally Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A LAN Blazor Server game: kids program robots through 10 grid-maze levels in a shared race shown on a projector.

**Architecture:** A `RobotRally.Game` class library holds all rules: levels, interpreter, playback timing, characters, and the in-memory `GameService` singleton, which is driven by `TimeProvider` so it can be tested. `RobotRally` (a Blazor Web App, global Interactive Server, no prerender) renders the kid page `/` and the projector `/admin`. Components subscribe to `GameService.Changed` and animate from server time with a periodic ticker. `wwwroot/js/rally.js` holds Web Audio sounds, confetti, localStorage, the HTML5 drag-and-drop shim, stage scaling and key capture.

**Tech Stack:** .NET 10, Blazor Server, xUnit, Microsoft.Extensions.TimeProvider.Testing.

**Spec:** `docs/superpowers/specs/2026-11-04-robot-rally-design.md`

## Global Constraints
- Up to 24 players. Kestrel listens on `http://0.0.0.0:5000`.
- Admin password: `ROBOTRALLY_ADMIN_PASSWORD` env var, else `Admin:Password` config, else `rally`.
- Step = 350 ms. Fail result shows for 1500 ms, success for 2200 ms. Run caps: 200 actions, 2000 evaluations.
- Repeat count range is 2–20, default 3. Off-grid counts as a wall.
- No typed text for kids. All art is SVG, all sound is Web Audio. No asset files.

## Review Focus
1. A kid refreshes mid-race → resumes the same player, level and program (test: `Resume_ReturnsSamePlayerWithProgram`).
2. Reset while kids are connected → their old tokens are rejected and their browsers return to the builder (test: `Reset_RejectsOldTokens`).
3. A program with no movement inside Repeat-until-goal → ends as StepLimit, with no hang (test: `RepeatUntilGoal_WithNoMoves_HitsEvaluationCap`).
4. Run pressed twice or during the countdown → rejected (test: `Run_RejectedWhileRunning_AndOutsideRace`).
5. A program over the block limit or using a block the level doesn't offer is rejected server-side (test: `Run_RejectsOverLimitAndDisallowedBlocks`).

---

### Task 1: Game core — grid, blocks, interpreter, playback
**Files:** `src/RobotRally.Game/{Direction.cs,Level.cs,Block.cs,ProgramOps.cs,Interpreter.cs,Playback.cs}`, `tests/RobotRally.Tests/InterpreterTests.cs`
**Produces:**
- `enum Direction { North, East, South, West }`
- `record struct Pose(int X, int Y, Direction Dir)`
- `Level(int Number, string Name, string[] Map, Direction StartDir, int BlockLimit, BlockKind[] Toolbox, BlockKind[] NewBlocks)`, with `Start`, `Goal`, `IsWall(x,y)` and `Width`/`Height`. Map characters: `#` wall, `.` floor, `S` start, `G` goal.
- `enum BlockKind { Forward, TurnLeft, TurnRight, Repeat, IfWallAhead, IfPathLeft, IfPathRight, RepeatUntilGoal }`
- `Block { Guid Id; BlockKind Kind; int Count; List<Block> Body; Block Clone() }`
- `ProgramOps.Count / Find / Remove / Contains / CloneAll`
- `Interpreter.Run(Level, IReadOnlyList<Block>) -> RunResult(Steps, Outcome)` with `Step(Pose Pose, StepAction Action, Guid BlockId)`, `enum StepAction { Move, TurnLeft, TurnRight, Crash, Goal }` and `enum RunOutcome { Success, Crash, OutOfBlocks, StepLimit }`
- `Playback.Frame(Level, RunResult, TimeSpan elapsed) -> PlaybackFrame(Pose, int StepIndex, PlayStatus, Guid? BlockId, bool Done)`
- `Playback.Duration(RunResult)`

**Tests:**
- forward and turns
- crash into a wall or off the grid
- goal reached mid-program stops the run
- nested repeat, if blocks, repeat-until
- action cap and evaluation cap
- playback frames at given times
- `ProgramOps` count and remove

### Task 2: Levels
**Files:** `src/RobotRally.Game/Levels.cs` (`Levels.All`, `Levels.Demo*`, `Levels.Sandbox`), `tests/RobotRally.Tests/LevelTests.cs`
**Tests:**
- every level's reference solution succeeds within its limit and uses only toolbox blocks
- for levels 4–10, the BFS shortest straight-line program is longer than the block limit
- L7 cannot be solved by nested repeats alone
- the L9 program fails on L10, and the L8 wall-only program fails

### Task 3: Characters and nicknames
**Files:** `src/RobotRally.Game/Characters.cs` (`BaseKind`, `CharacterLook`, `Characters.Bases`, `Characters.Colors`, `Characters.IsValid`), `Nicknames.cs`. Tests: add-ons are validated per base, and generated nicknames are unique.

### Task 4: GameService
**Files:** `src/RobotRally.Game/GameService.cs`, `Player.cs`, `tests/RobotRally.Tests/GameServiceTests.cs`
**Produces:**
- `Phase` (`Lobby | Demo | Countdown | Race | Podium`) and `DemoMode`
- `Join`, `Resume`, `Remove`, `SaveProgram`, `Run`, `StopRun`
- `StartDemo`, `StartRace`, `EndRace`, `RaceAgain`, `Reset`
- `Standings()`, `event Action Changed`, `CountdownEndsAt`, `Now`

Rules:
- A run ends via a `TimeProvider` timer. A successful run advances the player's level.
- A countdown of 3.6 s leads into `Race`.
- 3 finishers, or all players finished, moves to `Podium` 5 s later.

**Tests:** see Review Focus 1, 2 and 4, plus advancing, finishing, standings order, late join, and `RaceAgain`.

### Task 5: Web shell, JS, styles, Stage, characters SVG
Scaffold the Blazor app with no prerender.
- `rally.js`: storage, sfx, confetti, dnd, stage, keys
- `app.css`
- `CharacterArt.razor`
- `Board.razor`, which tracks rotation and teleports

### Task 6: Kid flow
- `Home.razor`, which switches by phase
- `CharacterBuilder.razor`
- `KidLobby.razor`
- `PlayArea.razor` + `ProgramEditor.razor` + `BlockView.razor`, with drag-and-drop, click-to-append, ✖ delete, repeat −/+ and the limit
- `KidRace.razor`: ticker, sounds, new-block intro, level-complete overlay, finished view
- `KidPodium`

### Task 7: Projector
- `Admin.razor`: login and phase switch
- `Stage`
- `AdminDrawer`
- `ProjectorLobby`
- `Countdown`
- `ProjectorRace`: tiles, overlay fly-over and ribbon
- `ProjectorPodium`
- `LiveDemo` (level picker)
- `ScriptedDemo` with its beats

### Task 8: Verify
Build, run the tests, then run the server and smoke-test with browser tabs (kid + admin) via Playwright.
