---
tags:
  - trilobites/csharp
  - trilobites/csharp/system
type: system
area: world
aliases:
  - World and Cave
---
# World Tiles and Cave

Linked notes: [[Trilobites CSharp Home]] - [[Simulation and Ticks]] - [[Pathfinding and BFS]] - [[Entities and Roles]] - [[Buildings]]

## Primary files

- `src/TriloGame.Game/Core/World/Tile.cs`
- `src/TriloGame.Game/Core/World/Graph.cs`
- `src/TriloGame.Game/Core/World/Cave.cs`
- `src/TriloGame.Game/Core/World/CaveGenerator.cs`
- `src/TriloGame.Game/Core/World/RevealSystem.cs`
- `src/TriloGame.Game/Core/World/ReachabilitySystem.cs`
- `src/TriloGame.Game/Core/Constants/TileConstants.cs`

## Core model

### `Tile`

- the smallest world unit
- has a stable numeric `Id`
- stores key, neighbors, tile type, reachability, reveal state, and occupancy-relevant facts

### `Graph`

- generic tile graph container
- stores the tile dictionary and graph-level access helpers

### `Cave`

- the live world instance for a running session
- extends `Graph`
- owns creature/building collections, reveal state, reachability, BFS fields, occupancy caches, typed building registries, and danger-state transitions that connect directly to [[Entities and Roles]], [[Buildings]], and [[Pathfinding and BFS]]

## Key `Cave` collections

- `Trilobites`
- `Enemies`
- `Buildings`
- `RevealedTiles`
- `ReachableTiles`

## Cached registries and occupancy helpers

`Cave` caches frequently used subsets to avoid repeated broad scans.

- live trilobite list
- live enemy list
- live building list
- mining post list
- algae farm list
- barracks list
- scaffolding list
- queen reference
- enemy occupancy map

These caches are important because trilobite and enemy AI query them constantly.

That is one of the core links between [[World Tiles and Cave]] and [[Entities and Roles]].

## Generation responsibilities

`CaveGenerator` and `Cave` collaborate to build the playable cave:

- create cave volume
- degrade and vary the cave shape
- place ore types
- create perimeter walls and interior reachable space
- seed the starting environment used by `GameApp.StartNewGame()` in [[Boot and Game Root]]

## Reveal and reachability

- `RevealSystem` handles what the colony can currently see
- `ReachabilitySystem` maintains which tiles the colony can actually traverse
- radar, building placement, mining, and growth from [[Buildings]], [[UI and Input]], and [[Entities and Roles]] all feed back into reveal and reachability

## Movement and occupancy

### Key methods

- `Spawn(Creature, Tile)`
- `MoveCreature(Creature, GridPoint)`
- `RemoveCreature(Creature, object?)`
- `SyncTrilobiteTileOccupancy(...)`
- `GetTrilobiteAtTileKey(...)`
- `GetEnemyAtTileKey(...)`

### Why this matters

- creature AI in [[Entities and Roles]] reads occupancy to avoid illegal moves
- building logic in [[Buildings]] depends on passable and blocked tiles
- selection and hit-testing in [[UI and Input]] rely on world positions staying in sync with actual occupancy

## World state owned by `Cave`

- live BFS fields
- danger-state handling
- reveal and reachability
- building placement validation
- tile mining and wall mining side effects
- creature removal cleanup

## Tile scale

- `TileConstants.TileSize = 80`
- `TileConstants.TileHalfSize = 40`

Those values drive rendering, hit-testing, and camera transform math.

That is why tile scale shows up again in [[Rendering]] and [[UI and Input]].

## Best next notes

- [[Pathfinding and BFS]]
- [[Entities and Roles]]
- [[Buildings]]
- [[Rendering]]
