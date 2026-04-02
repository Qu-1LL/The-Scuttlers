---
aliases:
  - Trilobites C# Home
  - Trilobites CSharp Home
tags:
  - trilobites/csharp
  - trilobites/csharp/moc
type: moc
area: home
---
# Trilobites CSharp Home

> [!summary]
> This vault documents the C# / MonoGame version of Trilobites in `TriloGame/TriloGame.CSharp`.
> It is organized as a map of content first, then by runtime flow, subsystem, and file inventory.

## What this vault covers

- The runtime entry path described in [[Boot and Game Root]] and [[Runtime Flow]]
- The simulation loop, tick profiler, and game session state in [[Simulation and Ticks]]
- World generation, tiles, reveal, reachability, and BFS pathfinding in [[World Tiles and Cave]] and [[Pathfinding and BFS]]
- Trilobites, enemies, buildings, resources, events, UI, rendering, audio, tests, and diagnostics across [[Entities and Roles]], [[Buildings]], [[Resources Events and Stats]], [[UI and Input]], [[Rendering]], [[Audio]], and [[Diagnostics and Crash Reports]]
- The project file structure, content pipeline, and support files covered in [[File Structure]] and [[Build Content and Packaging]]

## Best place to start

1. [[Using This Vault]]
2. [[Runtime Flow]]
3. [[System Map]]
4. [[File Structure]]

## Architecture hubs

- [[Runtime Flow]]
- [[System Map]]
- [[File Structure]]
- [[Build Content and Packaging]]

## Game wiki notes

- [[Game Wiki Home]]
- [[Getting Started and How to Play]]
- [[Controls and Shortcuts]]
- [[Core Loop and Colony Growth]]
- [[Trilobite Roles]]
- [[Buildings and Placement]]
- [[Resources Mining and Danger]]
- [[Features Overview]]

## System notes

- [[Boot and Game Root]]
- [[Simulation and Ticks]]
- [[World Tiles and Cave]]
- [[Pathfinding and BFS]]
- [[Entities and Roles]]
- [[Buildings]]
- [[Resources Events and Stats]]
- [[UI and Input]]
- [[Rendering]]
- [[Audio]]
- [[Diagnostics and Crash Reports]]

## Reference notes

- [[Behavior Contract and Existing Docs]]
- [[External Docs - MonoGame and Gum]]
- [[File Inventory - TriloGame.Game]]
- [[File Inventory - TriloGame.Tests]]
- [[Testing Strategy]]

## Source of truth inside the repository

- `TriloGame/TriloGame.CSharp/agents.md` is the behavioral contract for the C# port.
- `TriloGame/TriloGame.CSharp/src/TriloGame.Game/` is the runtime source tree.
- `TriloGame/TriloGame.CSharp/src/TriloGame.Tests/` is the regression and parity test tree.
- `TriloGame/TriloGame.CSharp/src/TriloGame.Game/Content/` is the MonoGame content pipeline root.

## Fast mental model

- `Program.cs` installs crash reporting and starts the game, which is documented in [[Boot and Game Root]] and [[Diagnostics and Crash Reports]].
- `GameApp.cs` owns the MonoGame lifecycle, world input, UI orchestration, camera, selection, debug tools, and draw passes, which makes it the center of [[Runtime Flow]], [[UI and Input]], and [[Rendering]].
- `GameSession.cs` owns persistent simulation state such as resources, BFS fields, stats, event bus, and profiler data, as described in [[Simulation and Ticks]] and [[Resources Events and Stats]].
- `Cave.cs` owns the live world graph, creature and building collections, occupancy caches, reveal state, reachability, and BFS management, which ties directly into [[World Tiles and Cave]] and [[Pathfinding and BFS]].
- `TickRunner.cs` performs one simulation step and feeds [[Simulation and Ticks]] plus [[Diagnostics and Crash Reports]].
- `Trilobite.cs`, `Enemy.cs`, and the building classes implement gameplay behavior across [[Entities and Roles]] and [[Buildings]].

## Recommended reading order by topic

### Learn the game itself

- [[Game Wiki Home]]
- [[Getting Started and How to Play]]
- [[Controls and Shortcuts]]
- [[Core Loop and Colony Growth]]

### Understand how the game runs

- [[Boot and Game Root]]
- [[Simulation and Ticks]]
- [[Rendering]]

### Understand colony gameplay

- [[World Tiles and Cave]]
- [[Pathfinding and BFS]]
- [[Entities and Roles]]
- [[Buildings]]
- [[Resources Events and Stats]]

### Understand player interaction

- [[UI and Input]]
- [[Audio]]
- [[Diagnostics and Crash Reports]]

### Understand maintenance and testing

- [[Behavior Contract and Existing Docs]]
- [[External Docs - MonoGame and Gum]]
- [[File Inventory - TriloGame.Game]]
- [[File Inventory - TriloGame.Tests]]
- [[Testing Strategy]]

## Graph view guidance

- This note is the main hub for the C# documentation cluster.
- System notes all link back here and sideways to the systems they depend on, such as [[World Tiles and Cave]] -> [[Pathfinding and BFS]] -> [[Entities and Roles]].
- Reference notes such as [[File Inventory - TriloGame.Game]], [[File Inventory - TriloGame.Tests]], and [[Testing Strategy]] are intentionally separate so the graph shows architecture relationships first and file inventories second.
- Tags are grouped by note role: `moc`, `architecture`, `system`, `reference`, `tests`, and `meta`.

## Related notes

- [[Using This Vault]]
- [[Runtime Flow]]
- [[System Map]]
- [[Behavior Contract and Existing Docs]]
- [[External Docs - MonoGame and Gum]]
- [[Game Wiki Home]]
