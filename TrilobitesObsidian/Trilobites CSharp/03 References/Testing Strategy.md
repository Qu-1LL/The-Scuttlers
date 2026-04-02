---
tags:
  - trilobites/csharp
  - trilobites/csharp/tests
  - trilobites/csharp/reference
type: reference
area: verification
aliases:
  - Test Strategy
---
# Testing Strategy

Linked notes: [[Trilobites CSharp Home]] - [[File Inventory - TriloGame.Tests]] - [[Behavior Contract and Existing Docs]]

## Purpose of the test project

`src/TriloGame.Tests/` is the safety net for the C# port. It protects:

- gameplay parity
- regression-prone UI geometry and interaction helpers from [[UI and Input]]
- pathfinding behavior from [[Pathfinding and BFS]]
- mining-post behavior from [[Buildings]]
- crash reporting and profiler output from [[Diagnostics and Crash Reports]] and [[Simulation and Ticks]]

## Testing style

- xUnit test project
- focused subsystem tests rather than one monolithic integration suite
- helper world setup through `TestWorldFactory.cs`, which supports [[World Tiles and Cave]]
- regression tests added for bug fixes and layout fixes

## What is covered well

- cave generation and reachability from [[World Tiles and Cave]]
- BFS field behavior from [[Pathfinding and BFS]]
- mining-post queue and scaffolding flows from [[Buildings]]
- enemy and trilobite behavior slices from [[Entities and Roles]]
- crash reporting from [[Diagnostics and Crash Reports]]
- tick-profiler summaries from [[Simulation and Ticks]]
- UI layout helpers for menu, debug, focus, settings, and radial selection from [[UI and Input]]

## What is still more manual than automated

- full feel of camera pan and zoom from [[Rendering]] and [[UI and Input]]
- exact visual polish of SpriteBatch and Gum composition from [[Rendering]]
- long-session performance under very large colonies
- full packaged-build smoke testing across multiple machines

## How to use the tests when changing code

1. Identify the owning system note in this vault, usually from [[System Map]]
2. Open the matching runtime files
3. Open the matching tests in `src/TriloGame.Tests/`
4. Add a regression test when fixing a bug that was not already covered

## Helpful pairings

- [[Simulation and Ticks]] <-> `Simulation/TickProfilerTests.cs`
- [[Pathfinding and BFS]] <-> `Pathfinding/BfsFieldTests.cs`
- [[Buildings]] <-> `Buildings/*.cs`
- [[UI and Input]] <-> `UI/*.cs`
- [[Diagnostics and Crash Reports]] <-> `Diagnostics/CrashReporterTests.cs`

## Related notes

- [[File Inventory - TriloGame.Tests]]
- [[Behavior Contract and Existing Docs]]
- [[Runtime Flow]]
