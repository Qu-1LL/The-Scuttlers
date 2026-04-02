---
tags:
  - trilobites/csharp
  - trilobites/csharp/wiki
type: wiki
area: economy-danger
aliases:
  - Resources and Danger
---
# Resources Mining and Danger

This note describes what the colony is collecting, why mining matters, and how danger affects the flow of play. For the underlying data and event layer, see [[Resources Events and Stats]].

## Main resource names

The current resource set is:

- `Algae`
- `Sandstone`
- `Malachite`
- `Magnetite`
- `Perotene`
- `Ilmenite`
- `Cochinium`

## Why mining matters

Mining is the main way to:

- open new paths
- gain construction materials
- gather ore
- widen the colony footprint

This is why miners are such a central part of [[Core Loop and Colony Growth]].

## Wall mining vs ore mining

When miners work, they can break:

- walls, which become empty space and yield `Sandstone`
- ore tiles, which become empty space and yield their ore type

Mining also changes the shape of the cave, which means it affects movement and future building space, not just inventory.

## Food as a colony-growth resource

`Algae` is special because it feeds the queen. That makes it both a resource and a colony-growth requirement.

Farmers collect algae through [[Buildings and Placement|Algae Farms]] and deliver it to the queen through the workflows described in [[Trilobite Roles]].

## Danger

Danger is the combat-pressure state of the colony.

When danger is active:

- enemies are present
- fighters become more important
- shared combat pathfinding becomes more active
- colony defense becomes part of the normal loop

When the last enemy is removed, the pressure drops and the colony can settle back into its regular work rhythm.

## Enemies

Enemies:

- move toward colony targets
- attack nearby trilobites or buildings
- pressure the player into using fighters and cleaner colony layouts

They matter because they interrupt the economy loop rather than existing as a disconnected side activity.

## What players should watch for

- too few miners means stalled expansion
- too little food means the queen stops being supported well
- too few builders means scaffolding piles up
- too little defense means enemies can break core colony flow

## Related notes

- [[Core Loop and Colony Growth]]
- [[Trilobite Roles]]
- [[Buildings and Placement]]
- [[Resources Events and Stats]]
