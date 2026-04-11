# AGENTS.md

## Purpose

This file is the local contract for shared cross-layer helpers under `src/TriloGame.Game/Shared`.
Read it after the repository root [AGENTS.md](../../../AGENTS.md) and the game-project
[AGENTS.md](../AGENTS.md) when touching diagnostics, math, utilities, extensions, or shared state
models.

## Shared Placement

Put code in `Shared` when it is:

- diagnostics, math, utilities, or extensions shared across modules
- shared diagnostic models such as tick profiling snapshots used across `Core`, `Runtime`, and UI
- shared runtime state models used across `Core`, `Runtime`, and UI without making `Core`
  depend on host-specific systems

## Shared Data Contracts

- `Diagnostics/TickProfiler.cs` holds reusable profiler data structures consumed by runtime
  systems and debug surfaces without putting stopwatch logic back into `Core`.
- `State/GameSessionRuntimeState.cs` groups runtime/debug state that simulation can read without
  scattering host-only toggles directly across `GameSession`.
- If a change adds structured runtime data, prefer typed models over stringly-typed commands.
- Keep reusable shared data host-agnostic when `Core` also needs to consume it.
