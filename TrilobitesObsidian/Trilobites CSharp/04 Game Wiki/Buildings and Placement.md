---
tags:
  - trilobites/csharp
  - trilobites/csharp/wiki
type: wiki
area: buildings
aliases:
  - Buildings
  - Building Placement
---
# Buildings and Placement

Buildings define the colony’s infrastructure. They shape where workers go, what jobs can be performed efficiently, and how safe the colony feels. For the implementation details, see [[Buildings]].

## How building placement works

1. Open the colony menu with `Tab` if needed.
2. Go to the `Buildings` tab.
3. Click a building card.
4. Move the preview over a valid tile area.
5. Press `R` if you want to rotate the footprint.
6. Click to place it.

Successful placement creates `Scaffolding`, not the final building immediately. Builders then have to finish it.

## Placement rules that matter to the player

- The footprint must be valid.
- The building cannot overlap blocked or invalid tiles.
- It cannot be placed on top of occupied trilobite footprint tiles.
- It cannot sever previously reachable colony space.
- It cannot fully trap another building’s usable access.

In practice, this means the game pushes you toward readable colony paths instead of letting you accidentally soft-lock your own base.

## Main buildings in the current unlocked set

### `Mining Post`

Use it to:

- organize miner work
- store mined materials
- cover a useful mining radius

This is the backbone of expansion and resource flow. It works directly with [[Trilobite Roles|miners]] and [[Resources Mining and Danger]].

### `Algae Farm`

Use it to:

- create harvestable food
- support farmers
- keep the queen supplied

Without farms, queen feeding becomes a long-term bottleneck.

### `Barracks`

Use it to:

- stage fighters
- keep defense roles organized
- give the colony a natural defensive anchor

### `Radar`

Use it to:

- reveal more of the cave
- widen what the colony can see

Radar affects map awareness more than direct economy.

## Other implemented building types

These exist in the current codebase and wiki because they are part of the project, even if they are not always part of the default unlocked opening set:

- `Queen`
- `Scaffolding`
- `Storage`
- `Smith`

## `Scaffolding`

Every placed structure goes through `Scaffolding` first.

That means a new building has two phases:

1. placement and material delivery
2. construction completion

This is why `builder` trilobites matter once you start expanding.

## Building menu behavior

The `Buildings` tab has two major parts:

- a preview area that shows the hovered or selected building
- a build grid where you actually click building cards

That menu behavior connects to [[Controls and Shortcuts]] and is implemented under [[UI and Input]].

## Good placement habits

- Put `Mining Post` buildings where they can cover lots of useful mining.
- Keep room for trilobites to move around important structures.
- Avoid over-building tight corridors if you expect danger to rise.
- Expand with the queen’s long-term access in mind, not just immediate convenience.

## Related notes

- [[Core Loop and Colony Growth]]
- [[Trilobite Roles]]
- [[Resources Mining and Danger]]
- [[Buildings]]
