---
tags:
  - trilobites/csharp
  - trilobites/csharp/reference
type: reference
area: docs
aliases:
  - Existing Project Docs
---
# Behavior Contract and Existing Docs

Linked notes: [[Trilobites CSharp Home]] - [[File Structure]] - [[Testing Strategy]]

## Primary behavior contract

The main behavior contract for the C# port is:

- `TriloGame/TriloGame.CSharp/agents.md`

That file documents the parity target, role names, input semantics, building behaviors, event names, UI expectations, and other gameplay rules that should remain stable.

Those rules are then implemented across [[Entities and Roles]], [[Buildings]], [[UI and Input]], [[Pathfinding and BFS]], and [[Simulation and Ticks]].

## Supporting docs inside the repository

- `TriloGame/TriloGame.CSharp/docs/architecture.md`
  - short architecture notes from the porting process
- `TriloGame/TriloGame.CSharp/docs/port-notes.md`
  - focused port notes, especially around UI and runtime behavior
- `TriloGame/TriloGame.CSharp/tools/content-pipeline/README.md`
  - support-file location for content-pipeline notes

## How this Obsidian vault relates to those files

- `agents.md` tells you what behavior must stay true
- the Obsidian notes explain how the C# project is organized and how the code flows through [[Runtime Flow]], [[System Map]], and the system notes
- the tests confirm whether key parts of that behavior still hold through [[Testing Strategy]]

## Good order for maintainers

1. Read `agents.md` when changing behavior
2. Read the relevant system note in this vault
3. Check the matching tests in `src/TriloGame.Tests/`

## Related notes

- [[Trilobites CSharp Home]]
- [[Testing Strategy]]
- [[File Inventory - TriloGame.Tests]]
