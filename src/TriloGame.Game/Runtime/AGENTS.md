# AGENTS.md

## Purpose

This file is the local contract for orchestration work under `src/TriloGame.Game/Runtime`.
Read it after the repository root [AGENTS.md](../../../AGENTS.md) and the game-project
[AGENTS.md](../AGENTS.md) when touching bootstrap flow, runtime systems, automation, or other
cross-module orchestration.

## Runtime Ownership

`Runtime` coordinates `Core` modules and owns state that is not pure simulation.

Use `Runtime` for:

- startup/bootstrap flow
- orchestration across multiple modules
- runtime state ownership that is not pure simulation
- play/test control or inspection
- scenario setup or future automation hooks

## Current Event / Runtime Timing Stance

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
- Projectile travel/impact timing belongs in runtime state/systems so hits can resolve between
  ticks without moving timing math into simulation rules.

Do not push those responsibilities back into `GameApp`.

## Current Play/Test API Contract

The current automation seam is:

- host contract: `Automation/IGamePlayHost.cs`
- API entry point: `Automation/GamePlayApi.cs`
- live host exposure: `GameApp.PlayApi`

Use the play/test API for:

- runtime inspection
- scripted scenario setup
- automation-style tests
- future tool adapters

Do not bolt new ad-hoc test-only helpers straight into simulation classes when this API is the
better seam.

## Runtime Extraction Direction

The target direction for this codebase is:

- thinner MonoGame host
- stronger deterministic `Core`
- more runtime systems under `Runtime`

As the refactor continues, more orchestration should move out of `GameApp` and into runtime
systems, especially:

- selection/command orchestration
- debug command handling
- scenario bootstrapping
- automation bridges

## Documentation Comments

- add short one-line `//` comments before non-trivial runtime methods when they coordinate timing,
  orchestration, or state ownership
- keep comments focused on lifecycle intent and cross-system contracts, not line-by-line narration
- skip obvious accessors and tiny forwarders unless they hide important runtime meaning
- add brief notes before complicated time-budget loops or branching state machines

## Golden Path Files

These are the current reference files for the preferred structure:

- `Bootstrap/GameSessionBootstrapper.cs`
- `Systems/GameSimulationClockSystem.cs`
- `Systems/GameOverStateSystem.cs`
- `Automation/GamePlayApi.cs`

Coordinate with [../Audio/AGENTS.md](../Audio/AGENTS.md) for audio runtime state machines and
[../Shared/AGENTS.md](../Shared/AGENTS.md) for shared profiler/runtime state data models.
