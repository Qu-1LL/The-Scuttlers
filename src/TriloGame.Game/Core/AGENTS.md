# AGENTS.md

## Purpose

This file is the local contract for deterministic simulation work under `src/TriloGame.Game/Core`.
Read it after the repository root [AGENTS.md](../../../AGENTS.md) and the game-project
[AGENTS.md](../AGENTS.md) when touching simulation rules, entities, buildings, world state,
pathfinding, economy, or progression.

## Core Contract

`Core` is for deterministic simulation rules and state transformation.

- Keep simulation decisions independent from draw cadence.
- Do not use wall-clock timing inside simulation decisions.
- Avoid host-specific rendering, input, audio, and file IO concerns here.
- Prefer explicit state transitions and typed models over hidden side channels.

## Deterministic Simulation Rules

- Simulation progresses by integer ticks through `TickRunner`.
- Keep simulation decisions independent from draw cadence.
- Do not use `DateTime.Now`, `Stopwatch`, or wall-clock timing inside simulation decisions.
- Keep event timing explicit. Never add "fire whenever" logic to the sim.
- Randomness for gameplay should continue to use the project's established deterministic/random
  utility patterns rather than ad-hoc sources.

Projectile firing decisions belong in `Core`, while projectile travel/impact timing belongs in
runtime state/systems so hits can resolve between ticks without moving timing math into
simulation rules.

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

## Creature Tracking And Building Projections

When adding buildings that care about nearby moving units, use the current projection/tracking
pattern instead of adding per-tick radius scans.

- `Tile` owns a `Projections` list of buildings watching that tile.
- `Building` owns a `ProjectedTiles` list for the tiles it currently watches.
- Radius-based buildings should populate `ProjectedTiles` during `OnBuilt` from the building
  center using coordinate iteration plus distance checks, and clear those registrations during
  removal.
- Tile occupancy transitions in `Cave` are the projection trigger point:
  - when a creature leaves a tile, buildings that are no longer covered should receive
    `TargetNoLongerInRadius(creature)`
  - when a creature enters a tile, newly covered buildings should receive
    `TargetInRadius(creature)`
- Do not re-scan a full radius every tick when tile-transition projection hooks can answer the
  same question incrementally.

For creature lifecycle cleanup, use the explicit tracking seam:

- `Creature` owns a `TrackedBy` set of buildings currently tracking that creature.
- Buildings must add themselves to `TrackedBy` when they begin owning/targeting a creature and
  remove themselves when they stop.
- On creature death/removal, tracked buildings receive `TrackedCreatureDied(creature)` so they can
  clear targets or assignments immediately.
- Current examples:
  - `Turret` tracks only its current hostile target
  - `MiningPost`, `AlgaeFarm`, and `Barracks` track creatures that are in their assignment lists
- Future assignment-owning or proximity-reactive buildings should follow the same pattern.

## Current Gameplay Invariants

These are current live rules. If a task changes them, update tests and docs in the same patch.

- Starter colony spawns four trilobites with roles:
  - `Jeffery` = miner
  - `Quinton` = builder
  - `Yeetmuncher` = farmer
  - `Sigma` = fighter
- Default runtime starts unpaused at `100 ms` tick speed.
- Trilobites carry one item at a time.
- Ore has finite yield, darkens as it depletes, and takes `5` hits (`0.5` seconds of
  simulation game-time) per yielded unit.
- Walls take `3` hits and do not yield resources to trilobites.
- Cave crystals render with the ore overlay layer, block placement/creature occupancy, take `3`
  hits, and do not yield resources.
- Queen death triggers a screen overlay rather than closing the app.
- Ant-hole systems are active world features.
- Natural ant-hole spawning follows the current ambient spawn rules.

## Subsystem Reading Guide

When work is concentrated in a specific Core area, read the exact files in that subsystem and the
nearest tests first. Coordinate with:

- [../Runtime/AGENTS.md](../Runtime/AGENTS.md) for runtime timing ownership and automation seams
- [../Shared/AGENTS.md](../Shared/AGENTS.md) for shared state/data models consumed by Core
- [../../../src/TriloGame.Tests/AGENTS.md](../../../src/TriloGame.Tests/AGENTS.md) for testing expectations
