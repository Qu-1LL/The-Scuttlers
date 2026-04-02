---
tags:
  - trilobites/csharp
  - trilobites/csharp/meta
type: meta
area: documentation
aliases:
  - Trilobites C# Documentation Guide
---
# Using This Vault

Linked notes: [[Trilobites CSharp Home]] - [[Runtime Flow]] - [[System Map]]

## Documentation design goals

- Make the runtime readable from the graph view, not only from folder names.
- Keep one hub note per major concern instead of dumping every file into a single giant page.
- Connect notes by real execution and data flow: [[Boot and Game Root]] -> [[Runtime Flow]] -> [[Simulation and Ticks]] -> [[World Tiles and Cave]] -> [[Entities and Roles]] -> [[UI and Input]] -> [[Rendering]].
- Separate "how the system works" from "which files exist" so readers can choose the right depth.
- Provide a second, player-facing layer through [[Game Wiki Home]] so the vault can also be read as a game wiki instead of only an engineering map.

## Obsidian practices used here

- **Properties**: every note has tags and a note type so the vault can be grouped and filtered consistently.
- **Internal links**: notes use wikilinks for every neighboring system they depend on.
- **Aliases**: major hub notes expose alternate names such as `Trilobites C# Home` to make linking easier.
- **Maps of content**: hub notes gather related notes instead of trying to replace them.
- **Reference notes**: file inventories, tests, and contract notes are intentionally separated from the higher-level system explanations.

## Why the note graph should read cleanly

- The home note links to the architecture notes and every system note through [[Trilobites CSharp Home]].
- Each architecture note links to the systems it explains, such as [[Runtime Flow]] pointing into [[Boot and Game Root]], [[Simulation and Ticks]], [[UI and Input]], and [[Rendering]].
- Each system note links to the systems it consumes and produces data for, such as [[Buildings]] linking into [[Entities and Roles]], [[Resources Events and Stats]], and [[Pathfinding and BFS]].
- Reference notes point back to the system notes instead of existing as isolated file dumps, especially [[Testing Strategy]], [[File Inventory - TriloGame.Game]], and [[File Inventory - TriloGame.Tests]].
- External framework links live in [[External Docs - MonoGame and Gum]] so future work can jump from our vault into official MonoGame and Gum docs when needed.
- Game-design notes in [[Game Wiki Home]] and its child pages point sideways into each other and back into the engineering notes when a reader wants implementation depth.

## Note types used in this vault

- `moc`: a hub note that organizes a topic
- `architecture`: a cross-system note such as flow or structure
- `system`: a subsystem explanation
- `wiki`: a player-facing or design-facing explanation of how the game works
- `reference`: inventories, contracts, and support material
- `tests`: notes focused on verification and coverage
- `meta`: notes about the documentation itself

## Documentation conventions

- Repo paths are written as code, for example `src/TriloGame.Game/Core/Simulation/TickRunner.cs`.
- Player-facing names are kept exactly as they appear in code, such as `miner`, `builder`, `Algae`, and `tileMined`.
- Generated output folders such as `bin/` and `obj/` are called out, but they are not treated as core architecture.
- Runtime ownership is documented before helper methods and layout helpers, which is why notes like [[Boot and Game Root]], [[World Tiles and Cave]], and [[UI and Input]] start with ownership and responsibilities first.
- When a system is built around a few key methods, those methods are named directly in the note.

## Recommended graph-view usage

- Start from [[Trilobites CSharp Home]] for the full architecture cluster.
- Start from [[Game Wiki Home]] if you want player-facing documentation first.
- Open a local graph from any system note to see immediate neighbors.
- Hide attachments if you only want architectural notes.
- Use tag or path filters when you want only `system` notes, only `wiki` notes, or only `reference` notes.
- Open [[External Docs - MonoGame and Gum]] when you need official framework guidance alongside project notes.

## Official Obsidian guidance used to shape this vault

- Properties: https://help.obsidian.md/properties
- Internal links: https://help.obsidian.md/links
- Aliases: https://help.obsidian.md/aliases
- Graph view: https://help.obsidian.md/plugins/graph
- Vault and `.obsidian` behavior: https://help.obsidian.md/data-storage

## Practical takeaways from that guidance

- Use YAML properties for structured note metadata.
- Use internal links as the primary relationship mechanism because graph nodes are built from links.
- Use aliases when the same note needs a short or alternate name.
- Keep related notes in one vault and avoid nested vaults.
- Treat `.obsidian/workspace.json` as a convenience file rather than architectural content.

## Related notes

- [[Trilobites CSharp Home]]
- [[Game Wiki Home]]
- [[Runtime Flow]]
- [[File Structure]]
- [[Behavior Contract and Existing Docs]]
- [[External Docs - MonoGame and Gum]]
