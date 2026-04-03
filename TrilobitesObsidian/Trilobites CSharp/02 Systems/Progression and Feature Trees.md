---
tags:
  - trilobites/csharp
  - trilobites/csharp/system
type: system
area: progression
aliases:
  - Progression
  - Feature Trees
  - Skill Nodes
---
# Progression and Feature Trees

Linked notes: [[Trilobites CSharp Home]] - [[System Map]] - [[Skill Tree and Quest Overview]] - [[Simulation and Ticks]]

## Primary files

- `src/TriloGame.Game/Core/Progression/BinarySkillNode.cs`
- `src/TriloGame.Game/Core/Progression/FeatureTree.cs`
- `src/TriloGame.Game/Core/Progression/SkillTree.cs`
- `src/TriloGame.Game/Core/Progression/SkillNode.cs`
- `src/TriloGame.Game/Core/Progression/TriloDex.cs`

## `TriloDex`

`TriloDex.cs` is the global catalog entry point for progression content.

### Global accessors

- `TriloDex.Global`
- `TriloDex.GlobalFeatureTrees`

### Catalog accessors

- `FeatureTrees`
- `Count`
- `IsEmpty`
- `ContainsFeatureTree(...)`
- `FindFeatureTree(...)`

### Current catalog state

- the catalog is hard-code-ready but intentionally empty right now
- future authored feature trees will be added inside `BuildGlobalFeatureTrees()`

## `FeatureTree`

`FeatureTree.cs` is the tree-level container for a single upgrade line.

### Tree metadata

- `Name`
- `Description`
- `FeaturesAffected`
- `StartingRound`

### Tree structure and helpers

- `Root`
- `HasRoot`
- `Count`
- `SetRoot(...)`
- `AddChild(...)`
- `RemoveSubtree(...)`
- `Contains(...)`
- `FindByName(...)`
- `TraverseDepthFirst()`
- `TraverseBreadthFirst()`

### Feature-tree semantics

- feature trees use regular `SkillNode` instances
- each `SkillNode` in a feature tree can have any number of children

## `SkillTree`

`SkillTree.cs` is the per-game local progression tree.

### Session-local structure

- `SourceDex`
- `Root`
- `HasRoot`
- `Count`
- `IsEmpty`

### Local tree editing

- `IntakeSkillNode(...)`
- `SetRoot(...)`
- `AddLeftChild(...)`
- `AddRightChild(...)`
- `RemoveSubtree(...)`
- `Contains(...)`
- `FindByName(...)`
- `TraverseDepthFirst()`

### Import helpers

- `ImportRoot(...)`
- `ImportLeftChild(...)`
- `ImportRightChild(...)`
- `GetSourceFeatureTreeName(...)`
- `GetNodesFromFeatureTree(...)`

### Local-tree semantics

- each `GameSession` owns its own `SkillTree`
- the local tree is a true binary tree with one root plus `Left` and `Right` children
- imported nodes are detached `BinarySkillNode` copies of global `FeatureTree` definitions
- the local tree can mix nodes from different feature trees in one run

## `BinarySkillNode`

`BinarySkillNode.cs` is the binary per-run copy type used by `SkillTree`.

### Binary node relationships

- `Parent`
- `Prerequisite`
- `Left`
- `Right`
- `IsRoot`
- `IsLeaf`
- `Depth`

### Source metadata

- `SourceSkillNode`
- `SourceFeatureTreeName`

### Upgrade behavior

- `Name`
- `Description`
- `Effect`
- `IsAcquired`
- `CanAcquire()`
- `TryAcquire(GameSession session)`
- `SetLeft(...)`
- `SetRight(...)`
- `RemoveLeft()`
- `RemoveRight()`

## `SkillNode`

`SkillNode.cs` replaces a generic tree node with an upgrade node.

### Node metadata

- `Name`
- `Description`
- `Effect`

### Tree relationships

- `Parent`
- `Prerequisite`
- `Children`
- `IsRoot`
- `IsLeaf`
- `Depth`

### Upgrade behavior

- `IsAcquired`
- `CanAcquire()`
- `TryAcquire(GameSession session)` runs the node's effect once when its prerequisite chain allows it
- `CreateDetachedCopy()`
- `AddChild(...)`
- `RemoveChild(...)`

## Current scope

- data model only
- includes a global empty `TriloDex` catalog plus `GameSession` accessors for it
- includes an empty per-session binary `SkillTree`
- no gameplay flow currently populates the local skill tree yet
- no menu flow, random branch generation, persistence, or reward wiring is attached yet

## Related notes

- [[Skill Tree and Quest Overview]]
- [[System Map]]
- [[File Inventory - TriloGame.Game]]
