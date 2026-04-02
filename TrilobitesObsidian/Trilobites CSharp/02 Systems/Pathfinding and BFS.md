---
tags:
  - trilobites/csharp
  - trilobites/csharp/system
type: system
area: pathfinding
aliases:
  - BFS and Pathfinding
---
# Pathfinding and BFS

Linked notes: [[Trilobites CSharp Home]] - [[World Tiles and Cave]] - [[Simulation and Ticks]] - [[Entities and Roles]] - [[Buildings]]

## Primary files

- `src/TriloGame.Game/Core/Pathfinding/BfsField.cs`
- `src/TriloGame.Game/Core/Pathfinding/PathBuilder.cs`
- `src/TriloGame.Game/Core/World/Cave.cs`

## Design

The C# port keeps the JS-style BFS field model instead of replacing it with A*.

That choice is part of the behavior expectations captured in [[Behavior Contract and Existing Docs]] and it feeds directly into the timing model in [[Simulation and Ticks]].

That means the game uses:

- shared colony and enemy fields
- building-seeded fields
- destination-seeded point fields
- incremental dirty-field refresh logic where possible

## `BfsField`

### What it stores internally

- coverage state
- blocked state
- queued state
- numeric values per tile id
- seed tile ids
- dirty tile tracking
- cached external dictionary form for consumer code

### Why numeric tile ids matter

The port moved the hot path away from string-keyed BFS storage and toward arrays indexed by `Tile.Id`. The public API can still hand out a `Dictionary<string, int>` view when needed, but the expensive work happens on indexed arrays.

## Important methods

- `GetField(bool refresh = true)`
- `Refresh()`
- `Rebuild()`
- `Rebalance(IEnumerable<string>? dirtyKeys = null)`
- `GetFieldValue(GridPoint location, bool refresh = true)`

## How `Cave` manages BFS fields

### Common responsibilities

- create fields on demand
- rebuild or refresh fields
- mark shared fields dirty
- rebalance one or all fields
- expose next-step lookups for creatures

Those responsibilities make [[World Tiles and Cave]] the owner of pathfinding state, even though the algorithms live here.

### Important `Cave` methods

- `GetBfsField(...)`
- `RefreshBfsField(...)`
- `RebuildBfsField(...)`
- `MarkSharedBfsFieldsDirty(...)`
- `RebalanceBfsField(...)`
- `RebalanceAllBfsFields(...)`
- `GetBfsFieldValue(...)`
- `GetBfsFieldNextStep(...)`

## Who uses BFS fields

- trilobites use them for mining posts, algae farms, scaffolding, barracks, queen delivery, combat, and manual point movement in [[Entities and Roles]] and [[Buildings]]
- enemies use the `colony` field for attack movement in [[Entities and Roles]]
- buildings such as mining posts rely on field distance and local area logic in [[Buildings]]
- the debug menu from [[UI and Input]] can visualize specific fields when the game is paused

## Path reconstruction

`PathBuilder.cs` reconstructs concrete step-by-step routes from field values instead of storing full paths everywhere up front.

That keeps the high-level navigation model consistent:

- fields answer "what direction gets me closer"
- `PathBuilder` turns that into a list of `GridPoint` steps when a creature needs a concrete path

## Performance notes

- BFS timing is one of the measured phases in `TickProfiler` from [[Simulation and Ticks]]
- danger mode increases BFS refresh work because enemy and colony fields are both active
- the numeric-id rewrite reduced repeated dictionary churn in large colonies, and the effect is surfaced again in [[Diagnostics and Crash Reports]]

## Best next notes

- [[Entities and Roles]]
- [[Buildings]]
- [[Simulation and Ticks]]
- [[Diagnostics and Crash Reports]]
