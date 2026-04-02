---
tags:
  - trilobites/csharp
  - trilobites/csharp/architecture
type: architecture
area: structure
aliases:
  - C# File Structure
---
# File Structure

Linked notes: [[Trilobites CSharp Home]] - [[System Map]] - [[File Inventory - TriloGame.Game]] - [[File Inventory - TriloGame.Tests]]

## Top-level project layout

```text
TriloGame/TriloGame.CSharp/
|- agents.md
|- TriloGame.sln
|- TriloGame.slnx
|- docs/
|- src/
|  |- TriloGame.Game/
|  |- TriloGame.Tests/
|- tools/
```

## What each top-level item is for

- `agents.md`
  - the behavioral contract for the C# port, documented in [[Behavior Contract and Existing Docs]]
- `TriloGame.sln` and `TriloGame.slnx`
  - solution entry points for the game and tests
- `docs/`
  - smaller project notes created during the port
- `src/TriloGame.Game/`
  - the actual MonoGame runtime project, broken down in [[File Inventory - TriloGame.Game]]
- `src/TriloGame.Tests/`
  - xUnit tests for gameplay, UI helpers, diagnostics, and pathfinding, broken down in [[File Inventory - TriloGame.Tests]] and [[Testing Strategy]]
- `tools/`
  - helper tooling area, currently including `tools/content-pipeline/README.md`

## Runtime project layout

### `src/TriloGame.Game/`

- root files
  - `Program.cs`
  - `GameApp.cs`
  - `TriloGame.Game.csproj`
  - app icon and manifest files
- `.config/`
  - local .NET tool manifest used for content tooling
- `.vscode/`
  - editor launch settings
- `Audio/`
  - runtime audio system described in [[Audio]]
- `Content/`
  - MGCB content root for textures, audio, fonts, icons, and UI textures used in [[Build Content and Packaging]]
- `Core/`
  - gameplay systems mapped in [[System Map]]
- `Rendering/`
  - camera, sprite registry, rendering context from [[Rendering]]
- `Shared/`
  - crash reporting, math helpers, collection helpers, and utilities, especially [[Diagnostics and Crash Reports]]
- `UI/`
  - menu, selection, settings, Gum shape helpers, input, and view models described in [[UI and Input]]

## Gameplay core layout

### `Core/Buildings/`

- building classes, scaffolding, recipes, and factory metadata from [[Buildings]]

### `Core/Constants/`

- global numbers such as tick speeds, zoom limits, drag threshold, tile size

### `Core/Economy/`

- inventory, resource reservations, ore definitions, and stats from [[Resources Events and Stats]]

### `Core/Entities/`

- base creature class, trilobites, and enemies from [[Entities and Roles]]

### `Core/Events/`

- event bus and event-name contract from [[Resources Events and Stats]]

### `Core/Pathfinding/`

- BFS field system and path reconstruction from [[Pathfinding and BFS]]

### `Core/Simulation/`

- session state, one-tick executor, and performance profiler from [[Simulation and Ticks]]

### `Core/World/`

- cave graph, generation, reveal, reachability, occupancy, and movement from [[World Tiles and Cave]]

## Test project layout

### `src/TriloGame.Tests/`

- `TestWorldFactory.cs`
  - helper used to stand up reproducible test worlds
- `AI/`, `Audio/`, `Buildings/`, `Diagnostics/`, `Pathfinding/`, `Simulation/`, `UI/`, `World/`
  - subsystem-specific test folders that mirror the runtime architecture described in [[Testing Strategy]]

## Files that are generated rather than authored

- `bin/`
  - build and publish output
- `obj/`
  - intermediate build artifacts
- compiled `.xnb` content
  - produced by MGCB during build

These folders matter for running the game, but they are not the best place to understand how the program works.

## Existing project docs

- `agents.md`
  - authoritative behavior contract used by [[Behavior Contract and Existing Docs]]
- `docs/architecture.md`
  - short rendering notes from the port
- `docs/port-notes.md`
  - smaller porting reminders and UI notes

## Best next notes

- [[Runtime Flow]]
- [[Build Content and Packaging]]
- [[File Inventory - TriloGame.Game]]
- [[File Inventory - TriloGame.Tests]]
