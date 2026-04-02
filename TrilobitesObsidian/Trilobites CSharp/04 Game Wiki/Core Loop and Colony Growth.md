---
tags:
  - trilobites/csharp
  - trilobites/csharp/wiki
type: wiki
area: gameplay-loop
aliases:
  - Core Loop
  - Colony Growth
---
# Core Loop and Colony Growth

The current build of Trilobites is built around a colony-management loop. At a high level, the player is turning worker control into more resources, more buildings, more food, and better defense.

## The main loop

1. Mine walls and ore to create space and gather materials.
2. Place buildings that widen what the colony can do.
3. Assign trilobites into roles through [[Trilobite Roles]].
4. Feed the queen through farmed `Algae`.
5. Respond to `Danger` when enemies are present.
6. Expand again with better infrastructure and more workers.

This loop is supported mechanically by [[Resources Mining and Danger]] and spatially by [[Buildings and Placement]].

## Why mining matters first

Mining does more than gather resources:

- it opens new space
- it reveals more of the cave
- it creates room for future buildings
- it supplies construction and storage needs

Without miners, the rest of the colony stalls.

## Why food matters

The queen depends on `Algae`. Farmers harvest from `Algae Farm` buildings and then deliver the food to the queen. That makes food production a colony-growth system, not just a survival bar.

The queen is the center of the run:

- she anchors the colony
- she receives farm output
- she can produce new trilobites
- her death causes `Game Over`

## Why buildings matter

Buildings are how the player translates raw resources into stronger colony structure:

- `Mining Post` supports miner workflows
- `Algae Farm` supports food production
- `Barracks` supports defense staging
- `Radar` expands reveal pressure
- `Scaffolding` is the temporary state that turns materials plus work into a finished building

See [[Buildings and Placement]] for the structure-by-structure breakdown.

## Why assignments matter

The colony does not become efficient on its own. Trilobites need to be told what kind of job they should specialize in:

- `miner`
- `farmer`
- `builder`
- `fighter`
- `unassigned`

That job structure is the heart of [[Trilobite Roles]].

## Pressure and failure

As the colony grows, enemies become more important. When danger is active:

- fighters leave idle staging and engage
- enemy pathing starts to matter more
- colony layout becomes a gameplay decision, not just a visual one

This is explained in [[Resources Mining and Danger]].

## What success looks like in the current build

There is no grand campaign structure documented in this vault. Success in the current build looks more like:

- stable mining output
- stable queen feeding
- usable building coverage
- fast reassignment when something changes
- surviving danger without losing the queen

## Related notes

- [[Getting Started and How to Play]]
- [[Trilobite Roles]]
- [[Buildings and Placement]]
- [[Resources Mining and Danger]]
