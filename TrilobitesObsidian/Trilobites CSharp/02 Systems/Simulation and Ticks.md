---
tags:
  - trilobites/csharp
  - trilobites/csharp/system
type: system
area: simulation
aliases:
  - Tick System
---
# Simulation and Ticks

Linked notes: [[Trilobites CSharp Home]] - [[Boot and Game Root]] - [[World Tiles and Cave]] - [[Resources Events and Stats]] - [[Diagnostics and Crash Reports]]

## Primary files

- `src/TriloGame.Game/Core/Simulation/GameSession.cs`
- `src/TriloGame.Game/Core/Simulation/TickRunner.cs`
- `src/TriloGame.Game/Core/Simulation/TickProfiler.cs`
- `src/TriloGame.Game/Core/Constants/GameConstants.cs`

## `GameSession` owns persistent simulation state

### Major fields

- `EventBus`
- `Stats`
- `Resources`
- `BfsFields`
- `UnlockedBuildings`
- `Cave`
- `Danger`
- `TickCount`
- `DebugEnemyCount`
- `TickProfiler`
- `AudioCueRequested`

### Resource dictionary keys

- `algae`
- `sandstone`
- `malachite`
- `magnetite`
- `perotene`
- `ilmenite`
- `cochinium`

### Important methods

- `Emit(...)`
- `On(...)`
- `RequestAudioCue(...)`
- `MineTile(...)`
- `MineWallTile(...)`
- `EmitMineEvents(...)`
- `FormatInventory()`
- `FormatStatsSnapshot()`

## Tick speed contract

`GameConstants.cs` defines the runtime tick intervals:

- slow: `500 ms`
- normal: `250 ms`
- fast: `100 ms`
- fastest: `50 ms`

The colony does **not** simulate once per rendered frame. `GameApp` advances the accumulator and calls `TickRunner` only when enough time has passed.

That update coordination lives in [[Boot and Game Root]] and appears step-by-step in [[Runtime Flow]].

## One simulation step

`TickRunner.RunTick(GameSession)` performs one colony step.

### Order

1. Increment `TickCount`
2. If danger is active, refresh enemy BFS through [[Pathfinding and BFS]]
3. Copy a stable snapshot of trilobites and move them through [[Entities and Roles]]
4. If danger is active, refresh colony BFS through [[Pathfinding and BFS]]
5. Copy a stable snapshot of enemies and move them through [[Entities and Roles]]
6. Copy a stable snapshot of buildings and tick them through [[Buildings]]
7. Record timing, allocation, GC, and unit-count data into `TickProfiler`, which feeds [[Diagnostics and Crash Reports]]

## Why the snapshot buffers matter

`TickRunner` uses reusable buffers instead of allocating fresh `ToArray()` snapshots every tick. That lowers per-tick garbage creation while still allowing safe iteration if the live cave collections change during movement or building ticks.

Those live collections come from [[World Tiles and Cave]], while the timing impact shows up in [[Diagnostics and Crash Reports]].

## `TickProfiler`

### What it records

- total tick time
- enemy BFS time
- trilobite move time
- colony BFS time
- enemy move time
- building tick time
- allocated bytes
- GC collections for generations 0, 1, and 2
- last-known trilobite, enemy, and building counts

### What consumes it

- debug menu performance card in [[UI and Input]]
- crash diagnostics text in [[Diagnostics and Crash Reports]]
- performance-focused regression tests in [[Testing Strategy]]

### Useful outputs

- `Last`
- `Average`
- `DescribeDominantWork()`
- `DescribeDominantWorkShort()`

## Danger-state semantics

- `GameSession.Danger` changes which shared BFS fields are refreshed during a tick in [[Pathfinding and BFS]]
- danger activates enemy path refresh and colony defense response
- the exact movement cadence still goes through the same tick accumulator

## Related notes

- [[Runtime Flow]]
- [[World Tiles and Cave]]
- [[Pathfinding and BFS]]
- [[Diagnostics and Crash Reports]]
