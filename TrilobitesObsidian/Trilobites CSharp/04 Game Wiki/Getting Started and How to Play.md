---
tags:
  - trilobites/csharp
  - trilobites/csharp/wiki
type: wiki
area: getting-started
---
# Getting Started and How to Play

This note describes how to play the current C# build of Trilobites as if you were approaching it as a new player. For the exact input contract behind these rules, see [[Controls and Shortcuts]] and [[UI and Input]].

## Your goal

You are managing an underground trilobite colony. A normal session revolves around:

- expanding the cave through mining
- keeping the colony fed by harvesting `Algae`
- building support structures such as [[Buildings and Placement|Mining Posts, Algae Farms, Barracks, and Radar]]
- assigning trilobites into useful jobs through [[Trilobite Roles]]
- surviving enemy pressure described in [[Resources Mining and Danger]]

If the `Queen` dies, the run ends in `Game Over`.

## First things to do in a new run

1. Open or leave open the colony panel with `Tab`.
2. Select a trilobite and get familiar with the role radial described in [[Trilobite Roles]].
3. Place a `Mining Post` near useful walls or ore.
4. Assign miners so the colony starts collecting stone and ore.
5. Build an `Algae Farm` so farmers can start feeding the queen.
6. Add builders as soon as you have multiple construction jobs running.
7. Add `Barracks` and fighters when enemy pressure starts to matter.

## What a normal play session looks like

### Early game

- use miners to open space and collect resources
- place the first support buildings
- begin feeding the queen with `Algae`
- use the [[Controls and Shortcuts|focus and selection tools]] to keep track of workers

### Mid game

- expand with more `Mining Post` coverage
- add more specialized trilobites through [[Trilobite Roles]]
- place more farms and build a stronger colony layout
- respond to danger by assigning fighters and keeping the queen protected

### Late pressure

- enemies push fighters away from idle barracks duty and into defense
- pathfinding, building layout, and access routes matter more
- poorly placed buildings can slow the colony down even if resources are available

## How to issue work

- Select a trilobite with left click.
- Right click a trilobite to open the assignment radial.
- Right drag to box-select multiple trilobites, then right click to assign the whole group.
- Use the colony menu to place buildings and manage assignments.
- Double left click the same reachable tile to send selected trilobites there manually.

Those actions all connect back to [[Controls and Shortcuts]], [[Trilobite Roles]], and [[Buildings and Placement]].

## Common beginner priorities

- Keep at least some trilobites on `miner`.
- Do not ignore food production. `Farmer` trilobites ultimately support the queen.
- Use `builder` trilobites when scaffolding starts piling up.
- When enemies exist, do not leave the colony without `fighter` coverage.
- Use `F` to focus on an important trilobite or building instead of losing it off-screen.

## If you are playtesting

Useful feedback areas:

- Is the first 10 minutes understandable?
- Are [[Controls and Shortcuts|selection, movement, and assignment controls]] readable?
- Do [[Buildings and Placement|building choices]] feel meaningful?
- Does the colony loop in [[Core Loop and Colony Growth]] feel satisfying?
- Is danger readable and fair, based on [[Resources Mining and Danger]]?

## Related notes

- [[Game Wiki Home]]
- [[Controls and Shortcuts]]
- [[Core Loop and Colony Growth]]
- [[Buildings and Placement]]
