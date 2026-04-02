---
tags:
  - trilobites/csharp
  - trilobites/csharp/architecture
  - trilobites/csharp/flow
type: architecture
area: runtime
aliases:
  - C# Runtime Flow
---
# Runtime Flow

Linked notes: [[Trilobites CSharp Home]] - [[Boot and Game Root]] - [[Simulation and Ticks]] - [[Rendering]] - [[UI and Input]]

For the underlying MonoGame lifecycle and Gum initialization APIs behind this flow, use [[External Docs - MonoGame and Gum]].

## Runtime chain at a glance

1. `Program.cs`
2. `GameApp.Initialize()`
3. `GameApp.LoadContent()`
4. `GameApp.Update(GameTime)`
5. `GameApp.AdvanceSimulation(GameTime)`
6. `TickRunner.RunTick(GameSession)`
7. `GameApp.Draw(GameTime)`

## Boot flow

### `Program.cs`

- installs managed crash handlers through `CrashReporter.InstallProcessHandlers()`, which is part of [[Diagnostics and Crash Reports]]
- creates `GameApp`
- registers `GameApp.BuildCrashDiagnostics` as the snapshot provider for crash reports
- runs the game inside a top-level `try/catch`

### `GameApp.Initialize()`

- sets the backbuffer size
- initializes Gum with `GumUi.Initialize(...)`, which feeds the UI layer described in [[UI and Input]]
- initializes Gum shapes through `ShapeRenderer`, which participates in [[Rendering]]
- creates Gum controls
- sizes the camera viewport through the camera layer from [[Rendering]]
- starts a new game through `StartNewGame()`

### `GameApp.LoadContent()`

- creates `SpriteBatch`
- creates `GumBatch`
- creates the white pixel helper texture
- registers every texture into `SpriteFactory`, which is part of [[Rendering]]
- loads fonts and builds `RenderingContext` for [[Rendering]] and [[UI and Input]]
- loads audio content into `AudioService`, which belongs to [[Audio]]

## New-game flow

`GameApp.StartNewGame()` performs the high-level world bootstrap:

1. Create a fresh `GameSession` from [[Simulation and Ticks]]
2. Subscribe the game to `GameSession.AudioCueRequested`
3. Populate unlocked building factories from [[Buildings]]
4. Create a new `Cave` from [[World Tiles and Cave]]
5. Build the initial colony layout
6. Spawn starter trilobites from [[Entities and Roles]]
7. Reveal the starting cave state through [[World Tiles and Cave]]
8. Reset camera, selection, menus, debug state, and tick accumulator

## Per-frame update flow

`GameApp.Update(GameTime)` is the main coordinator.

### Frame input and cleanup

- `InputController.BeginFrame()`
- advance the UI clock
- expire pending manual move state
- remove invalid selections if objects were deleted

### High-priority modal states

- toggle the debug menu with backtick
- detect queen death and enter game-over state
- if game over: handle only game-over input, sync Gum, update Gum, return
- if debug menu open: handle debug menu input, advance simulation, sync Gum, update Gum, return through the coordination described in [[UI and Input]] and [[Simulation and Ticks]]

### Normal frame behavior

- handle keyboard shortcuts through [[UI and Input]]
- route mouse wheel to menu scroll or zoom through [[UI and Input]] and [[Rendering]]
- route clicks to menu, settings, radial menu, or world interaction
- manage left-drag panning and right-drag selection-box behavior
- advance simulation if not paused through [[Simulation and Ticks]]
- sync Gum controls
- update Gum

## Simulation step flow

`GameApp.AdvanceSimulation(GameTime)` uses an accumulator instead of tying the colony to render FPS.

- if paused, return
- add elapsed time to `_tickAccumulatorMs`
- while accumulator is above the selected tick speed:
  - call `TickRunner.RunTick(_session)` from [[Simulation and Ticks]]
  - subtract one tick interval
  - stop early if the queen dies

## Tick flow

`TickRunner.RunTick(GameSession)` performs one simulation step in this order:

1. Refresh `enemy` BFS when danger is active through [[Pathfinding and BFS]]
2. Move trilobites from [[Entities and Roles]]
3. Refresh `colony` BFS when danger is active through [[Pathfinding and BFS]]
4. Move enemies from [[Entities and Roles]]
5. Tick buildings from [[Buildings]]
6. Record timings, counts, allocation data, and GC data in `TickProfiler`, which feeds [[Diagnostics and Crash Reports]]

## Draw flow

`GameApp.Draw(GameTime)` uses a layered render sequence.

### World pass

- clear the screen
- begin `SpriteBatch` with `SamplerState.PointClamp`
- draw tiles from [[World Tiles and Cave]]
- draw buildings from [[Buildings]]
- draw creatures from [[Entities and Roles]]
- draw role labels
- draw selection markers
- draw the selection box
- draw floating building preview
- draw the lightweight world debug overlay

### Gum-backed background pass

- resize the Gum shape container to the current viewport
- begin a Gum shape frame
- draw rounded Gum-backed backgrounds for menu, settings, radial, focus, and game-over UI from [[UI and Input]] and [[Rendering]]
- end the Gum shape frame

### UI foreground pass

- begin a second `SpriteBatch`
- draw menu foreground content from [[UI and Input]]
- draw debug menu overlay
- draw settings, radial, focus, and game-over foreground text and buttons
- end the batch

## Crash path

- `Program.cs` reports unhandled managed exceptions through [[Boot and Game Root]]
- `CrashReporter` writes a timestamped report into `CrashReports` through [[Diagnostics and Crash Reports]]
- `GameApp.BuildCrashDiagnostics()` contributes camera, input, session, selection, and profiler state

## Best next notes

- [[Boot and Game Root]]
- [[Simulation and Ticks]]
- [[UI and Input]]
- [[Rendering]]
- [[External Docs - MonoGame and Gum]]
