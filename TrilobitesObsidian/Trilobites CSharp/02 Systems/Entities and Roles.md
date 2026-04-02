---
tags:
  - trilobites/csharp
  - trilobites/csharp/system
type: system
area: entities
aliases:
  - Creatures and Roles
---
# Entities and Roles

Linked notes: [[Trilobites CSharp Home]] - [[World Tiles and Cave]] - [[Pathfinding and BFS]] - [[Buildings]] - [[Resources Events and Stats]]

## Primary files

- `src/TriloGame.Game/Core/Entities/Creature.cs`
- `src/TriloGame.Game/Core/Entities/Trilobite.cs`
- `src/TriloGame.Game/Core/Entities/Enemy.cs`

## `Creature`

`Creature` is the shared base for trilobites and enemies.

It sits on top of [[World Tiles and Cave]] for location and occupancy, and on top of [[Pathfinding and BFS]] for navigation.

### Shared responsibilities

- name, location, health, damage, assignment
- movement offset and rotation for rendering
- action queue management
- behavior restart
- damage handling and removal
- navigation fallback and reroute helpers
- path preview storage for manual movement and debug visuals

### Useful base methods

- `RestartBehavior(...)`
- `DealDamage(...)`
- `TakeDamage(...)`
- `RemoveFromGame(...)`
- `EnqueueAction(...)`
- `BuildNavigationPathToPoint(...)`
- `BuildNavigationPathToBuilding(...)`

## Trilobites

`Trilobite.cs` is the colony AI center.

### Assignment names

- `unassigned`
- `miner`
- `farmer`
- `builder`
- `fighter`

### Role dispatch

`GetBehavior()` routes to:

- `UnassignedBehavior()`
- `MinerBehavior()`
- `FarmerBehavior()`
- `BuilderBehavior()`
- `FighterBehavior()`

### Role-state helpers

- `EnsureMinerState()`
- `EnsureFarmerState()`
- `EnsureBuilderState()`
- `EnsureFighterState()`

### Notable task chains

- miner:
  - `MinerStep1()` through `MinerStep6()`
- farmer:
  - `FarmerStep1()` through `FarmerStep5()`
- builder:
  - builder step chain plus supply lookup and scaffold interaction
- fighter:
  - `FighterStep1()` through `FighterStepMove(...)`

### Important building and world queries

- `GetMiningPosts()`
- `GetAlgaeFarms()`
- `GetBarracksBuildings()`
- `GetScaffoldingBuildings()`
- `GetQueen()`
- `GetClosestPassableBuildingTile(...)`
- `FeedQueenAlgae(...)`

Those queries are where this system reaches into [[Buildings]], [[World Tiles and Cave]], and [[Resources Events and Stats]].

### Why the file is large

The file owns the full colony workflow ordering for all worker roles. It is not just a data model. It is where most actual colony decision-making lives.

## Enemies

`Enemy.cs` uses the same `Creature` base but a much tighter behavior loop.

### Assignment name

- `enemy`

### Core steps

- `EnemyStep1()`
- `EnemyStep2()`
- `EnemyStep3()`
- `EnemyStepMove(...)`

### Behavior summary

- attack adjacent hostile targets when possible
- otherwise follow colony BFS toward the colony through [[Pathfinding and BFS]]
- use cave occupancy lookups from [[World Tiles and Cave]] to find nearby targets

## Connections to other systems

- reads [[World Tiles and Cave]] for movement, occupancy, reveal state, and removal
- reads [[Pathfinding and BFS]] for all structured navigation
- uses [[Buildings]] as work targets and combat targets
- uses [[Resources Events and Stats]] through inventory, mining, feeding, and event-triggered world changes

## Best next notes

- [[Buildings]]
- [[Pathfinding and BFS]]
- [[Resources Events and Stats]]
- [[UI and Input]]
