---
tags:
  - trilobites/csharp
  - trilobites/csharp/system
type: system
area: buildings
---
# Buildings

Linked notes: [[Trilobites CSharp Home]] - [[Entities and Roles]] - [[World Tiles and Cave]] - [[Resources Events and Stats]] - [[Pathfinding and BFS]]

## Primary files

- `src/TriloGame.Game/Core/Buildings/Building.cs`
- `src/TriloGame.Game/Core/Buildings/Factory.cs`
- `src/TriloGame.Game/Core/Buildings/Queen.cs`
- `src/TriloGame.Game/Core/Buildings/MiningPost.cs`
- `src/TriloGame.Game/Core/Buildings/AlgaeFarm.cs`
- `src/TriloGame.Game/Core/Buildings/Barracks.cs`
- `src/TriloGame.Game/Core/Buildings/Radar.cs`
- `src/TriloGame.Game/Core/Buildings/Scaffolding.cs`
- `src/TriloGame.Game/Core/Buildings/Storage.cs`
- `src/TriloGame.Game/Core/Buildings/Smith.cs`

## Base building model

`Building.cs` provides the common shape for placeable structures:

- name
- size
- open map / footprint
- texture key
- recipe
- location
- selection rules
- rotation state
- tick behavior and build hooks

`Factory.cs` wraps a builder function and exposes preview metadata used by the UI:

- `Name`
- `TextureKey`
- `OpenMap`
- `Size`
- `Description`
- `HasStation`

That preview metadata is consumed directly by [[UI and Input]].

## Building types

### Queen

- file: `Queen.cs`
- name: `Queen`
- role: colony anchor, receives algae, births new trilobites
- special behaviors:
  - `FeedAlgae(...)`
  - `Birth(...)`
  - broodling naming and spawn logic

### Mining Post

- file: `MiningPost.cs`
- name: `Mining Post`
- role: miner assignment hub, inventory store, local mineable radius and queue manager for [[Entities and Roles]]
- important responsibilities:
  - inventory and reservations from [[Resources Events and Stats]]
  - tile assignment per miner
  - mineable queue invalidation and rebuild
  - incremental queue updates for performance
  - navigation helpers for workers through [[Pathfinding and BFS]]

### Algae Farm

- file: `AlgaeFarm.cs`
- name: `Algae Farm`
- role: farmer assignment hub and food production for [[Entities and Roles]]
- important behaviors:
  - assignment tracking
  - growth and harvest logic
  - passable farm tiles for worker movement

### Barracks

- file: `Barracks.cs`
- name: `Barracks`
- role: fighter staging point for [[Entities and Roles]]
- important behavior:
  - tracks assigned fighters

### Radar

- file: `Radar.cs`
- name: `Radar`
- role: reveal expansion building in [[World Tiles and Cave]]
- important behavior:
  - reveals tiles in a growing radius

### Scaffolding

- file: `Scaffolding.cs`
- role: temporary construction site that turns into another building
- important behaviors:
  - resource reservation and delivery through [[Resources Events and Stats]]
  - construction progress
  - target-building rotation and footprint mirroring
  - completion sound and handoff

### Storage

- file: `Storage.cs`
- name: `Storage`
- role: passive capacity building

### Smith

- file: `Smith.cs`
- name: `Smith`
- role: future crafting station placeholder exposed in the current content set

## Cross-system connections

- workers in [[Entities and Roles]] pick buildings as assignments and targets
- building placement and selection are controlled from [[UI and Input]]
- reveal and occupancy feed through [[World Tiles and Cave]]
- local and shared navigation feed through [[Pathfinding and BFS]]
- recipes, reservations, and stats tie into [[Resources Events and Stats]]

## Files worth reading first

- `Building.cs` for common semantics
- `Factory.cs` for UI-facing metadata used in [[UI and Input]]
- `MiningPost.cs` for the most complex worker-target workflow
- `Scaffolding.cs` for build completion and resource flow
- `Queen.cs` for colony growth

## Related notes

- [[Entities and Roles]]
- [[World Tiles and Cave]]
- [[Resources Events and Stats]]
- [[UI and Input]]
