---
tags:
  - trilobites/csharp
  - trilobites/csharp/architecture
type: architecture
area: map
aliases:
  - C# System Map
---
# System Map

Linked notes: [[Trilobites CSharp Home]] - [[Runtime Flow]] - [[File Structure]]

## Core subsystem map

| Subsystem | Primary files | Main responsibility | Closest connected notes |
| --- | --- | --- | --- |
| Boot and root game object | `Program.cs`, `GameApp.cs` | Start the game, own global runtime services, coordinate update and draw | [[Boot and Game Root]], [[Runtime Flow]] |
| Simulation state | `Core/Simulation/GameSession.cs` | Own resources, event bus, BFS registry, stats, danger state, audio requests, profiler | [[Simulation and Ticks]], [[Resources Events and Stats]] |
| Tick execution | `Core/Simulation/TickRunner.cs`, `Core/Simulation/TickProfiler.cs` | Advance one colony tick and measure performance | [[Simulation and Ticks]], [[Diagnostics and Crash Reports]] |
| World model | `Core/World/Cave.cs`, `Graph.cs`, `Tile.cs`, `CaveGenerator.cs`, `RevealSystem.cs`, `ReachabilitySystem.cs` | Represent the cave graph, reveal state, reachability, occupancy, and generation | [[World Tiles and Cave]], [[Pathfinding and BFS]] |
| Pathfinding | `Core/Pathfinding/BfsField.cs`, `PathBuilder.cs` | Maintain BFS fields for colony, enemies, buildings, and point destinations | [[Pathfinding and BFS]], [[Entities and Roles]] |
| Creatures | `Core/Entities/Creature.cs`, `Trilobite.cs`, `Enemy.cs` | Implement unit state, movement, role logic, combat, and manual movement | [[Entities and Roles]], [[World Tiles and Cave]] |
| Buildings | `Core/Buildings/*.cs`, `Factory.cs` | Implement structures, recipes, scaffolding, storage, production, assignment targets, and reveal behavior | [[Buildings]], [[Resources Events and Stats]] |
| Progression | `Core/Progression/*.cs` | Model feature trees and unlockable skill nodes without wiring them into gameplay yet | [[Progression and Feature Trees]], [[Skill Tree and Quest Overview]] |
| Resources and events | `Core/Economy/*.cs`, `Core/Events/*.cs` | Track resources, reservations, mining events, and statistics | [[Resources Events and Stats]], [[Simulation and Ticks]] |
| Input and gameplay UI | `UI/Input/*.cs`, `UI/Menu/*.cs`, `UI/Selection/*.cs`, `UI/Settings/*.cs`, `UI/ViewModels/*.cs` | Poll input, manage menus, selection, settings, and layout helpers | [[UI and Input]], [[Rendering]] |
| Rendering | `Rendering/*.cs`, `GameApp.cs`, `UI/Gum/GumShapePool.cs` | Convert world state into screen-space drawing, camera transforms, sprite registration, Gum-backed rounded UI chrome | [[Rendering]], [[UI and Input]] |
| Audio | `Audio/*.cs` | Load sounds, play cues, manage volume, apply pitch variation | [[Audio]], [[Resources Events and Stats]] |
| Diagnostics | `Shared/Diagnostics/CrashReporter.cs`, `GameApp.BuildCrashDiagnostics()` | Capture crash snapshots and performance data | [[Diagnostics and Crash Reports]], [[Simulation and Ticks]] |
| Tests | `src/TriloGame.Tests/**/*.cs` | Lock down gameplay parity, layout, performance helpers, and regressions | [[Testing Strategy]], [[File Inventory - TriloGame.Tests]] |

## High-value cross-system relationships

### `GameApp` is the runtime hub

- owns `AudioService`, `InputController`, `CameraController`, `MenuController`, `GumShapePool`, `DoubleClickTracker`, and `GameSession`, which makes it the heart of [[Boot and Game Root]]
- calls into world input, tick execution, draw helpers, and crash diagnostics across [[UI and Input]], [[Simulation and Ticks]], [[Rendering]], and [[Diagnostics and Crash Reports]]

### `GameSession` is the state hub

- connects the event bus, stats, resources, unlocked buildings, BFS fields, audio requests, and the active `Cave`, which ties [[Simulation and Ticks]] to [[Resources Events and Stats]], [[Buildings]], [[Pathfinding and BFS]], and [[World Tiles and Cave]]

### `Cave` is the gameplay world hub

- connects tiles, creatures, buildings, reveal state, reachability, BFS fields, occupancy caches, and danger-state transitions, which is why [[World Tiles and Cave]] sits between [[Entities and Roles]], [[Buildings]], and [[Pathfinding and BFS]]

### `Trilobite` is the colony behavior hub

- reads building registries and BFS fields from `Cave` through [[World Tiles and Cave]] and [[Pathfinding and BFS]]
- writes assignments, reservations, movement, and mining/building/farming/combat actions back into the world through [[Buildings]] and [[Resources Events and Stats]]

## Good local-graph starting points

- [[Boot and Game Root]] for lifecycle
- [[World Tiles and Cave]] for core game state
- [[Entities and Roles]] for actual colony behavior
- [[UI and Input]] for player interaction

## Related notes

- [[Runtime Flow]]
- [[File Structure]]
- [[File Inventory - TriloGame.Game]]
