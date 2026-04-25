# AGENTS.md

## Purpose

This file is the local agent contract for the game assembly under `src/TriloGame.Game`.
Read it after the repository root [AGENTS.md](../../AGENTS.md) when work touches the runtime
game code.

This project contains the current C# / MonoGame version of `Trilobites` (`The-Scuttlers`).

Use this file as:

- the solution map for the game project
- the architecture boundary contract for the game assembly
- the placement guide for where new code should go
- the decomposition guide for keeping `GameApp` thin

Read these after this file when you need deeper detail:

- [README.md](../../README.md)
- [docs/architecture.md](../../docs/architecture.md)
- [docs/runtime-systems.md](../../docs/runtime-systems.md)
- [docs/playtest-api.md](../../docs/playtest-api.md)
- [docs/agents.md](../../docs/agents.md)
- [docs/port-notes.md](../../docs/port-notes.md)
- [Core/AGENTS.md](Core/AGENTS.md)
- [Runtime/AGENTS.md](Runtime/AGENTS.md)
- [UI/AGENTS.md](UI/AGENTS.md)
- [Rendering/AGENTS.md](Rendering/AGENTS.md)
- [Audio/AGENTS.md](Audio/AGENTS.md)
- [Shared/AGENTS.md](Shared/AGENTS.md)

## What The Game Is

`Trilobites` is a 2D colony-sim / roguelike where trilobites:

- mine and haul resources
- farm algae and feed the queen
- build through scaffolding and recipes
- defend against ants and ant holes
- manage other world hazards

The project already contains:

- a deterministic tick-based simulation
- a MonoGame desktop host
- a Gum-backed screen-space UI rendering path for panels, sprites, controls, and text
- Gum text should generally use fixed integer `FontSize` values instead of fractional `FontScale`
- a runtime play/test API
- a growing modular runtime layer intended to shrink `GameApp`

## Current Solution Map

Main code areas:

- `Core`
  - deterministic simulation rules, entities, buildings, world state, pathfinding, economy
- `Runtime`
  - bootstrap, runtime systems, orchestration seams, automation API
- `UI`
  - menu, debug, settings, selection, Gum helpers
- `Rendering`
  - camera and rendering helpers
- `Audio`
  - audio service, session audio bridging, and audio-specific systems
- `Shared`
  - diagnostics, math, utilities, extensions
  - shared diagnostic models such as tick profiling snapshots used across `Core`, `Runtime`, and UI
  - shared runtime state models used across `Core`, `Runtime`, and UI without making `Core`
    depend on host-specific systems

Use `src/TriloGame.Tests` for unit, runtime, world, UI, performance, simulation, and AI tests.

## Architectural Contract

### Dependency Direction

- `Core` must not depend on MonoGame rendering/input/audio APIs.
- `Core` must not do file IO or own host-specific rendering concerns.
- `Runtime` coordinates `Core` modules and exposes control/query seams.
- `UI` and `Rendering` may depend on `Core` and `Runtime`, but not the reverse.
- `Audio` may observe `Core` state through session/runtime boundaries, but simulation logic must
  not depend on live playback.
- `GameApp` is the MonoGame host and composition root, not the long-term home for new gameplay
  systems.

## Module Placement Guide

When adding code, use this routing:

### Put code in `Core` when it is:

- a simulation rule
- an entity/building/world behavior
- pathfinding logic
- economy/resource logic
- deterministic state transformation

### Put code in `Runtime` when it is:

- startup/bootstrap flow
- orchestration across multiple modules
- runtime state ownership that is not pure simulation
- play/test control or inspection
- scenario setup or future automation hooks

### Put code in `UI` when it is:

- menu state
- panel layout
- debug overlay behavior
- selection UX
- settings UX
- Gum-backed control logic
- screen-space UI text/chrome rendering that should route through Gum rather than raw `SpriteBatch`

### Put code in `Rendering` when it is:

- camera math
- render helpers
- sprite placement/origin concerns

### Put code in `Audio` when it is:

- cue registration
- playback mechanics
- audio state machines driven by runtime state

### Put code in `Shared` when it is:

- diagnostics, math, and utility helpers shared across modules
- shared runtime state models that multiple layers need to read
- reusable data structures that should not pull host concerns into `Core`

### Only leave code in `GameApp` if it is:

- MonoGame lifecycle glue
- input routing between systems
- top-level draw orchestration
- system composition

If a feature can become a module or system, do that instead of adding another large private method to
`GameApp`.

## Golden Path Files

These are the current reference files for the preferred structure:

- `Core/Simulation/TickRunner.cs`
- `Runtime/Bootstrap/GameSessionBootstrapper.cs`
- `Runtime/Systems/GameSimulationClockSystem.cs`
- `Runtime/Systems/GameOverStateSystem.cs`
- `Audio/SessionAudioBridge.cs`
- `Runtime/Automation/GamePlayApi.cs`

Use these as examples when extracting new systems out of `GameApp`.

## Current High-Pressure Files

These files are still structurally important and should be treated carefully:

- `GameApp.cs`
  - still large; host under active decomposition
- `GameApp.MiningOrders.cs`
  - mining selection, dispatch, and order UI glue
- `GameApp.SurfaceFeatures.cs`
  - ant-hole presentation glue
- `UI/Menu/MenuController.cs`
- `UI/Menu/MenuController.Layout.cs`
- `UI/Menu/MenuController.Drawing.cs`
- `UI/Debug/DebugToggleControls.cs`
- `UI/Gum/GumShapePool.cs`

If you change these files, actively look for an extraction opportunity instead of just growing them.

## Data / Content Rules

- Keep gameplay rules out of content loading code.
- If a change adds structured runtime data, prefer typed models over stringly-typed commands.
- Keep content pipeline assumptions documented when assets or audio/content build behavior changes.
- Do not quietly introduce new hidden root assets or build steps without documenting them.
