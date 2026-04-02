---
tags:
  - trilobites/csharp
  - trilobites/csharp/wiki
type: wiki
area: roles
aliases:
  - Roles
  - Trilobite Jobs
---
# Trilobite Roles

Trilobites are the workforce of the colony. Their role determines what kind of work they will try to do on their own. For the runtime implementation of these workflows, see [[Entities and Roles]].

## How to assign roles

- Right click a trilobite to open the radial role menu.
- Right drag to box-select multiple trilobites, then right click to assign the whole group.
- You can also manage assignments from the `Assignments` tab described in [[Buildings and Placement]].

## `unassigned`

`unassigned` trilobites are idle by default.

Use this role when:

- you want to keep a worker available
- you are about to retask the colony
- you do not want a trilobite committing to a station-based role yet

## `miner`

Miners:

- work through `Mining Post` buildings
- find mineable tiles in the post radius
- collect wall and ore resources
- deliver those resources back to mining post storage

This is usually the first specialized role you need. It supports both expansion and construction.

## `farmer`

Farmers:

- work through `Algae Farm`
- harvest `Algae`
- carry the harvested food to the queen

Farmers are the role most directly tied to colony growth and queen survival.

## `builder`

Builders:

- target active `Scaffolding`
- collect required materials from storage or mining post inventory
- deliver materials to the scaffold
- apply construction work until the final building completes

Builders become more important once the colony is placing multiple buildings quickly.

## `fighter`

Fighters:

- stage through `Barracks`
- respond when danger is active
- chase and attack enemies near the colony

Fighters are usually less important at the very start of a run, but they become necessary once enemy pressure starts to interfere with the colony loop in [[Core Loop and Colony Growth]].

## Choosing roles well

Good general rule of thumb:

- use `miner` to create momentum
- add `farmer` before food becomes a problem
- add `builder` once construction starts backing up
- add `fighter` once danger starts interrupting the colony

## Role management tips

- Reassign in groups when colony priorities change.
- Use `F` to keep an important worker on-screen.
- If a worker seems unproductive, check whether the colony actually has a matching target building for that role.
- `unassigned` is useful during transitions, but too many idle trilobites means the colony is wasting time.

## Related notes

- [[Getting Started and How to Play]]
- [[Core Loop and Colony Growth]]
- [[Buildings and Placement]]
- [[Entities and Roles]]
