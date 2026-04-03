---
tags:
  - trilobites/csharp
  - trilobites/csharp/system
type: system
area: boot
aliases:
  - Game Root
---
# Boot and Game Root

Linked notes: [[Trilobites CSharp Home]] - [[Runtime Flow]] - [[Simulation and Ticks]] - [[UI and Input]] - [[Rendering]]

If you need the official framework details for `Game`, MonoGame startup, or Gum initialization, use [[External Docs - MonoGame and Gum]].

## Primary files

- `src/TriloGame.Game/Program.cs`
- `src/TriloGame.Game/GameApp.cs`

## Responsibilities

### `Program.cs`

- install process-level managed crash handlers from [[Diagnostics and Crash Reports]]
- construct `GameApp`
- register the crash snapshot callback
- run the game inside a top-level exception boundary

### `GameApp.cs`

- own MonoGame lifecycle methods: `Initialize()`, `LoadContent()`, `Update()`, `Draw()`
- own global runtime services
- coordinate selection, input routing, menus, settings, debug tools, and game-over flow through [[UI and Input]]
- own the simulation accumulator
- own world draw order and UI draw passes through [[Rendering]]
- build crash diagnostics for `CrashReporter` in [[Diagnostics and Crash Reports]]

## Long-lived services owned by `GameApp`

- `AudioService`
- `InputController`
- `DoubleClickTracker`
- `CameraController`
- `MenuController`
- `GameSession`
- `GumShapePool`

## Key methods worth knowing first

### Lifecycle

- `Initialize()`
- `LoadContent()`
- `Update(GameTime)`
- `Draw(GameTime)`

### Session bootstrap

- `EnterMainMenu()`
- `StartGameplaySession()`
- `StartNewGame()`
- `PopulateUnlockedBuildings()`
- `BuildInitialColony(Cave)`

### Interaction

- `HandleKeyboard(GameTime)`
- `HandleWorldClick(Point)`
- `HandleWorldRightClick(Point)`
- `SetSelectedObject(object?)`
- `SetSelectedTrilobites(IEnumerable<Trilobite>, bool)`

### Simulation coordination

- `RunSingleTick()`
- `AdvanceSimulation(GameTime)`
- `TogglePauseState()`

### Diagnostics

- `BuildCrashDiagnostics()`
- `RefreshBfsFieldDebug()`
- `SpawnDebugEnemy()`

## Root flow

- `Initialize()` now enters a top-level main menu state instead of creating a colony immediately
- `EnterMainMenu()` clears the current run, resets transient UI/input state, and leaves the app waiting for `Start Game`
- `StartGameplaySession()` exits the main menu and then calls `StartNewGame()` to build a fresh colony

## Startup state reset in `StartNewGame()`

- creates a fresh `GameSession` from [[Simulation and Ticks]]
- wires audio cue requests
- creates a new `Cave` from [[World Tiles and Cave]]
- builds and reveals the starter colony
- spawns the initial four trilobites from [[Entities and Roles]]
- resets camera scale and origin
- clears selection, building placement, radial menu, panning, and selection box state
- resets debug/settings/game-over state
- resets tick speed and tick accumulator
- resets menu state

## Why `GameApp` matters so much

`GameApp` is not just a shell around MonoGame. It is the integration point between:

- raw input polling
- colony simulation cadence
- camera transforms
- selection and build placement state
- Gum-backed UI chrome
- world rendering
- debug overlays
- crash reporting

If you need to understand "where does this feature actually get coordinated?" the answer is often `GameApp.cs`.

## Most important outgoing links

- Calls [[Simulation and Ticks]] through `AdvanceSimulation()` and `TickRunner.RunTick(...)`
- Reads and writes [[World Tiles and Cave]] through the active `GameSession.Cave`
- Routes selection, menus, and shortcuts into [[UI and Input]]
- Uses [[Rendering]] for camera transforms, sprite lookup, and draw context
- Uses [[Audio]] through `AudioService` and `GameSession.AudioCueRequested`
- Emits diagnostics into [[Diagnostics and Crash Reports]]

## Related notes

- [[Runtime Flow]]
- [[Simulation and Ticks]]
- [[UI and Input]]
- [[Rendering]]
- [[External Docs - MonoGame and Gum]]
