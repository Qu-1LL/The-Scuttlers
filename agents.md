# AGENTS.md

## Purpose

This repository contains the current C# / MonoGame version of `Trilobites` (`The-Scuttlers`).
This file is the root operational contract for coding agents working in the repo.

Use it as:

- a project briefing
- an architecture boundary contract
- a performance and determinism checklist
- a placement guide for where new code should go

This file is intentionally compact enough to stay useful as root agent context. For deeper detail,
follow these docs after reading this file:

- [README.md](README.md)
- [docs/architecture.md](docs/architecture.md)
- [docs/runtime-systems.md](docs/runtime-systems.md)
- [docs/playtest-api.md](docs/playtest-api.md)
- [docs/agents.md](docs/agents.md)
- [docs/port-notes.md](docs/port-notes.md)

## Agent Persona

You are acting as a senior C# gameplay/systems engineer for a deterministic colony-sim roguelike.

Optimize in this order:

1. Correctness and invariant safety
2. Determinism and replay safety
3. Frame pacing and allocation discipline
4. Clear module boundaries and testability
5. CPU performance
6. Extensibility and tooling

Do not invent missing constraints silently. If a hard contract is missing, add an explicit placeholder
or note the assumption in the final response.

## What The Game Is

`Trilobites` is a 2D colony-sim / roguelike where trilobites:

- mine and haul resources
- farm algae and feed the queen
- build through scaffolding and recipes
- defend against ants and ant holes
- manage opal pressure and other world hazards

The project already contains:

- a deterministic tick-based simulation
- a MonoGame desktop host
- a Gum-backed screen-space UI rendering path for panels, sprites, controls, and text
- Gum text should generally use fixed integer `FontSize` values instead of fractional `FontScale`
- a runtime play/test API
- a growing modular runtime layer intended to shrink `GameApp`

## Required Reading Order For Agents

Before making substantial changes, build context in this order:

1. This file
2. [README.md](README.md)
3. [docs/architecture.md](docs/architecture.md)
4. [docs/runtime-systems.md](docs/runtime-systems.md)
5. The exact files in the subsystem you are changing

For feature work, also inspect the nearest tests first.

## Repository Commands

Run commands from the repo root:

- Build: `dotnet build TriloGame.sln`
- Test: `dotnet test src/TriloGame.Tests/TriloGame.Tests.csproj`
- Run: `dotnet run --project src/TriloGame.Game/TriloGame.Game.csproj`
- Format: `dotnet format`
- Compile only the game project when the apphost is locked by a running game window:
  - `dotnet msbuild src/TriloGame.Game/TriloGame.Game.csproj /t:Compile /p:UseAppHost=false /p:UseSharedCompilation=false`

Do not bypass MGCB or the MonoGame content pipeline. Content still flows through
`src/TriloGame.Game/Content/Content.mgcb`.

## Current Solution Map

Main projects:

- `src/TriloGame.Game`
- `src/TriloGame.Tests`

Main code areas:

- `src/TriloGame.Game/Core`
  - deterministic simulation rules, entities, buildings, world state, pathfinding, economy
- `src/TriloGame.Game/Runtime`
  - bootstrap, runtime systems, orchestration seams, automation API
- `src/TriloGame.Game/UI`
  - menu, debug, settings, selection, Gum helpers
- `src/TriloGame.Game/Rendering`
  - camera and rendering helpers
- `src/TriloGame.Game/Audio`
  - audio service, session audio bridging, and audio-specific systems
- `src/TriloGame.Game/Shared`
  - diagnostics, math, utilities, extensions
  - shared diagnostic models such as tick profiling snapshots used across `Core`, `Runtime`, and UI
  - shared runtime state models used across `Core`, `Runtime`, and UI without making `Core`
    depend on host-specific systems
- `src/TriloGame.Tests`
  - unit, runtime, world, UI, performance, simulation, and AI tests

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

### Deterministic Simulation Rules

- Simulation progresses by integer ticks through `TickRunner`.
- Keep simulation decisions independent from draw cadence.
- Do not use `DateTime.Now`, `Stopwatch`, or wall-clock timing inside simulation decisions.
- Keep event timing explicit. Never add "fire whenever" logic to the sim.
- Randomness for gameplay should continue to use the project's established deterministic/random
  utility patterns rather than ad-hoc sources.

### Current Event / Runtime Timing Stance

- `TickRunner` is the authoritative sim-tick entry point.
- `GameSimulationClockSystem` owns pause state, tick speed, accumulator logic, and runtime
  profiler recording around `TickRunner`.
- `Shared/Diagnostics/TickProfiler.cs` holds reusable profiler data structures consumed by runtime
  systems and debug surfaces without putting stopwatch logic back into `Core`.
- `Shared/State/GameSessionRuntimeState.cs` groups runtime/debug state that simulation can read
  without scattering host-only toggles directly across `GameSession`.
- `GameOverStateSystem` owns queen-loss state.
- `OpalAudioSystem` owns opal warning audio state transitions.
- `Audio/SessionAudioBridge.cs` owns session audio cue subscription so `GameApp` does not need to
  manually subscribe and relay cue events.

Do not push those responsibilities back into `GameApp`.

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

### Only leave code in `GameApp` if it is:

- MonoGame lifecycle glue
- input routing between systems
- top-level draw orchestration
- system composition

If a feature can become a module or system, do that instead of adding another large private method to
`GameApp`.

## Golden Path Files

These are the current reference files for the preferred structure:

- `src/TriloGame.Game/Core/Simulation/TickRunner.cs`
- `src/TriloGame.Game/Runtime/Bootstrap/GameSessionBootstrapper.cs`
- `src/TriloGame.Game/Runtime/Systems/GameSimulationClockSystem.cs`
- `src/TriloGame.Game/Runtime/Systems/GameOverStateSystem.cs`
- `src/TriloGame.Game/Audio/OpalAudioSystem.cs`
- `src/TriloGame.Game/Runtime/Automation/GamePlayApi.cs`

Use these as examples when extracting new systems out of `GameApp`.

## Current High-Pressure Files

These files are still structurally important and should be treated carefully:

- `src/TriloGame.Game/GameApp.cs`
  - still large; host under active decomposition
- `src/TriloGame.Game/GameApp.MiningOrders.cs`
  - mining selection, dispatch, and order UI glue
- `src/TriloGame.Game/GameApp.SurfaceFeatures.cs`
  - opal / ant-hole presentation glue
- `src/TriloGame.Game/UI/Menu/MenuController.cs`
- `src/TriloGame.Game/UI/Menu/MenuController.Layout.cs`
- `src/TriloGame.Game/UI/Menu/MenuController.Drawing.cs`
- `src/TriloGame.Game/UI/Debug/DebugToggleControls.cs`
- `src/TriloGame.Game/UI/Gum/GumShapePool.cs`

If you change these files, actively look for an extraction opportunity instead of just growing them.

## Performance Contract

Hot-path rules inside simulation/update code:

- no LINQ
- no reflection
- no `async` / `await`
- no `GC.Collect`
- no per-tick string building
- no hidden closure allocations
- no repeated list growth without capacity planning

Prefer:

- stable lists for iteration
- hash sets/maps for lookup
- numeric ids and cached coordinates over string churn in hot paths
- one-pass scoring over repeated sort-heavy selection
- pooling only when justified by real hot-path pressure

Allowed exception:

- `GamePlayApi`, tests, and tooling code may use LINQ when it materially improves clarity, because
  they are not part of the real-time simulation hot path.

## Testing Contract

Minimum expectations for behavior changes:

- add or update unit tests for the affected rule/module
- add or update runtime tests when orchestration changes
- add replay/performance coverage when a deterministic or hot-path system changes

Minimum expectations for refactors:

- preserve behavior unless the change is explicitly requested
- lock behavior with tests before or during the refactor
- update docs when structure, ownership, or runtime flow changes

## Current Play/Test API Contract

The current automation seam is:

- host contract: `src/TriloGame.Game/Runtime/Automation/IGamePlayHost.cs`
- API entry point: `src/TriloGame.Game/Runtime/Automation/GamePlayApi.cs`
- live host exposure: `GameApp.PlayApi`

Use the play/test API for:

- runtime inspection
- scripted scenario setup
- automation-style tests
- future tool adapters

Do not bolt new ad-hoc test-only helpers straight into simulation classes when this API is the
better seam.

## Current Gameplay Invariants

These are current live rules. If a task changes them, update tests and docs in the same patch.

- Starter colony spawns four trilobites with roles:
  - `Jeffery` = miner
  - `Quinton` = builder
  - `Yeetmuncher` = farmer
  - `Sigma` = fighter
- Default runtime starts unpaused at `100 ms` tick speed.
- Trilobites carry one item at a time.
- Ore has finite yield, darkens as it depletes, and takes `1-5` hits per yielded unit.
- Walls take `3` hits, drop sandstone, and miners haul dropped stone.
- Queen death triggers a screen overlay rather than closing the app.
- Opal and ant-hole systems are active world features.
- Natural ant-hole spawning is governed by the opal grace/warning system.

## Data / Content Rules

- Keep gameplay rules out of content loading code.
- If a change adds structured runtime data, prefer typed models over stringly-typed commands.
- Keep content pipeline assumptions documented when assets or audio/content build behavior changes.
- Do not quietly introduce new hidden root assets or build steps without documenting them.

## UI Rendering Rule

- All screen-space UI, including text, should be rendered through Gum (`GumUiRenderer` or Gum-backed
  controls).
- In the MonoGame host, this applies to labels, fitted text, wrapped text, menu text, settings text,
  debug menu text, and game-over/main-menu overlay text as well.
- Do not introduce new `SpriteBatch.DrawString`-driven screen UI.
- World-space debug labels and world rendering overlays may still use the world render path when
  they are part of the scene rather than the UI layer.

## Output Contract For Agents

When making changes:

- keep touched file lists clear in the final response when the task is substantial
- mention tests run and anything blocked
- update relevant docs when architecture or behavior changes
- do not leave partial placeholders like `TODO implement`

## Practical Refactor Direction

The target direction for this codebase is:

- thinner MonoGame host
- stronger deterministic `Core`
- more runtime systems under `Runtime`
- clearer seams for UI, audio, and automation
- eventual easier extraction into separate host/core assemblies if desired

Good changes move the project toward:

- modules over monoliths
- systems over giant host classes
- explicit ownership of state transitions
- reusable runtime seams
- measurable hot-path discipline

Bad changes move the project toward:

- more logic in `GameApp`
- render code owning simulation rules
- duplicated orchestration
- hidden allocations in tick paths
- ad-hoc testing hooks spread through unrelated classes

## Before Coding Checklist

Before substantial implementation, confirm:

1. Which layer owns this change?
2. Is the behavior deterministic?
3. Does this belong in an existing runtime system?
4. Is there already a test or API seam that should be reused?
5. If touching `GameApp`, can part of the change be extracted first?
