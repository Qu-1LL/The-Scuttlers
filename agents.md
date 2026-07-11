# AGENTS.md

## Purpose

This repository contains the current C# / MonoGame version of `Trilobites` (`The-Scuttlers`).
This file is the root operational contract for coding agents working in the repo.

Use it as:

- a project briefing
- an architecture boundary contract
- a performance and determinism checklist
- a placement guide for where new code should go
- the index for the narrower local `AGENTS.md` files in the repo

Read the root file first, then the most specific local guide for the area you are changing:

- [src/TriloGame.Game/AGENTS.md](src/TriloGame.Game/AGENTS.md)
- [src/TriloGame.Game/Core/AGENTS.md](src/TriloGame.Game/Core/AGENTS.md)
- [src/TriloGame.Game/Runtime/AGENTS.md](src/TriloGame.Game/Runtime/AGENTS.md)
- [src/TriloGame.Game/UI/AGENTS.md](src/TriloGame.Game/UI/AGENTS.md)
- [src/TriloGame.Game/Audio/AGENTS.md](src/TriloGame.Game/Audio/AGENTS.md)
- [src/TriloGame.Game/Rendering/AGENTS.md](src/TriloGame.Game/Rendering/AGENTS.md)
- [src/TriloGame.Game/Shared/AGENTS.md](src/TriloGame.Game/Shared/AGENTS.md)
- [src/TriloGame.Tests/AGENTS.md](src/TriloGame.Tests/AGENTS.md)

Then read the supporting docs you need:

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
- manage other world hazards

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
2. The nearest local `AGENTS.md` for the subsystem you are changing
3. [README.md](README.md)
4. [docs/architecture.md](docs/architecture.md)
5. [docs/runtime-systems.md](docs/runtime-systems.md) when runtime ownership or timing matters
6. The exact files in the subsystem you are changing

For feature work, inspect the nearest tests first.

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

## Local Guide Map

Repository-level guidance is split so agents can read the narrowest relevant contract first.

### Root and project-level guides

- `AGENTS.md`
  - global operating contract
  - shared architectural boundaries
  - repo-wide performance, testing, and output expectations
- `src/TriloGame.Game/AGENTS.md`
  - game-project solution map
  - placement guide across `Core`, `Runtime`, `UI`, `Rendering`, `Audio`, and `Shared`
  - `GameApp` decomposition direction
- `src/TriloGame.Tests/AGENTS.md`
  - test coverage expectations
  - refactor-safety expectations

### Subsystem guides

- `src/TriloGame.Game/Core/AGENTS.md`
  - deterministic simulation rules
  - hot-path guidance
  - creature/building projection and tracking patterns
  - gameplay invariants
- `src/TriloGame.Game/Runtime/AGENTS.md`
  - runtime timing ownership
  - automation and play/test API seams
  - runtime extraction direction
- `src/TriloGame.Game/UI/AGENTS.md`
  - Gum-first screen UI rules
  - UI-specific high-pressure files
  - player-facing UI polish guidance entry points
- `src/TriloGame.Game/Audio/AGENTS.md`
  - cue registration and playback boundaries
  - session audio bridge ownership
- `src/TriloGame.Game/Rendering/AGENTS.md`
  - camera/render helper placement
  - world-space versus screen-space rendering boundary
- `src/TriloGame.Game/Shared/AGENTS.md`
  - shared diagnostics and runtime state models
  - typed shared data guidance

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
  utility patterns rather than ad-hoc sources; prefer `Shared/Utilities/XorShift64.cs` when code
  needs explicit deterministic PRNG state, especially for generation or replay-sensitive logic.

For timing ownership, system extraction seams, solution-map detail, golden-path files, and
high-pressure files, read the nearest local `AGENTS.md` under `src/TriloGame.Game`.

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

## Documentation Comment Contract

- add short one-line `//` comments before non-trivial methods when their system role or invariant is
  not obvious from the signature alone
- keep comments intent-focused rather than narrating each statement
- skip boilerplate accessors, obvious mutators, and tiny forwarding methods unless local context would
  otherwise be unclear
- add brief comments before especially dense loops or conditionals only when they encode important
  selection, timing, or invariant logic
- when updating a subsystem, refresh nearby comments if the behavior or ownership has changed

## Testing Contract

Minimum expectations for behavior changes:

- add or update unit tests for the affected rule/module
- add or update runtime tests when orchestration changes
- add replay/performance coverage when a deterministic or hot-path system changes

Minimum expectations for refactors:

- preserve behavior unless the change is explicitly requested
- lock behavior with tests before or during the refactor
- update docs when structure, ownership, or runtime flow changes

## Data / Content Rules

- Keep gameplay rules out of content loading code.
- If a change adds structured runtime data, prefer typed models over stringly-typed commands.
- Keep content pipeline assumptions documented when assets or audio/content build behavior changes.
- Do not quietly introduce new hidden root assets or build steps without documenting them.

## Output Contract For Agents

When making changes:

- keep touched file lists clear in the final response when the task is substantial
- mention tests run and anything blocked
- update relevant docs when architecture or behavior changes
- do not leave partial placeholders like `TODO implement`
