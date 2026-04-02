---
tags:
  - trilobites/csharp
  - trilobites/csharp/reference
  - trilobites/csharp/tests
type: reference
area: tests
aliases:
  - Test File Inventory
---
# File Inventory - TriloGame.Tests

Linked notes: [[Trilobites CSharp Home]] - [[Testing Strategy]] - [[System Map]]

Use this inventory alongside [[Testing Strategy]]. The folders mirror the runtime system notes, so this page works best when read beside [[Entities and Roles]], [[Buildings]], [[Pathfinding and BFS]], [[Simulation and Ticks]], [[UI and Input]], [[World Tiles and Cave]], [[Audio]], and [[Diagnostics and Crash Reports]].

## Root files

- `src/TriloGame.Tests/TriloGame.Tests.csproj` - test project manifest
- `src/TriloGame.Tests/TestWorldFactory.cs` - helper for creating repeatable world setups

## AI

This section verifies behavior described in [[Entities and Roles]].

- `AI/EnemyBehaviorTests.cs` - enemy movement and combat behavior checks
- `AI/TrilobiteBehaviorTests.cs` - trilobite role and behavior checks

## Audio

This section verifies behavior described in [[Audio]].

- `Audio/AudioServiceTests.cs` - cue registration, volume, and playback-facing behavior
- `Audio/ClickPitchVariationTests.cs` - randomized pitch selection behavior

## Buildings

This section verifies behavior described in [[Buildings]].

- `Buildings/BuildingRotationTests.cs` - building preview and placement rotation behavior
- `Buildings/MiningPostTests.cs` - mining-post queue and mining workflow regressions
- `Buildings/ScaffoldingTests.cs` - construction completion and scaffolding flow

## Diagnostics

This section verifies behavior described in [[Diagnostics and Crash Reports]].

- `Diagnostics/CrashReporterTests.cs` - crash-report creation behavior

## Pathfinding

This section verifies behavior described in [[Pathfinding and BFS]].

- `Pathfinding/BfsFieldTests.cs` - BFS rebuild, rebalance, and field behavior

## Simulation

This section verifies behavior described in [[Simulation and Ticks]].

- `Simulation/TickProfilerTests.cs` - tick-timing summaries and dominant-work reporting

## UI

This section verifies behavior described in [[UI and Input]].

- `UI/DebugMenuLayoutTests.cs` - debug-menu layout geometry
- `UI/DoubleClickTrackerTests.cs` - double-click timing helper
- `UI/MenuControllerTests.cs` - menu state and layout expectations
- `UI/RoleRadialLayoutTests.cs` - role radial layout and clipping guards
- `UI/RoleSelectionStateTests.cs` - mixed-role selection state handling
- `UI/SelectionFocusLayoutTests.cs` - focus-hint placement rules
- `UI/SettingsMenuLayoutTests.cs` - settings panel layout

## World

This section verifies behavior described in [[World Tiles and Cave]].

- `World/CaveGenerationTests.cs` - cave generation and startup topology checks
- `World/CaveOccupancyTests.cs` - occupancy and removal behavior
- `World/ReachabilityTests.cs` - reachable-tile logic

## How the test tree mirrors the runtime

- `AI/` mirrors `Core/Entities/`
- `Buildings/` mirrors `Core/Buildings/`
- `Pathfinding/` mirrors `Core/Pathfinding/`
- `Simulation/` mirrors `Core/Simulation/`
- `UI/` mirrors `UI/`
- `World/` mirrors `Core/World/`
- `Audio/` mirrors `Audio/`
- `Diagnostics/` mirrors `Shared/Diagnostics/`

## Best companion notes

- [[Testing Strategy]]
- [[File Inventory - TriloGame.Game]]
- [[Behavior Contract and Existing Docs]]
