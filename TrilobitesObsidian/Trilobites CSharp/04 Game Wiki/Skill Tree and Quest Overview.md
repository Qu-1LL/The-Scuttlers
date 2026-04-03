The game will have a collection of "skill lines" that provide a variety of upgrade paths for the player to encounter during a game. Each skill line will have a collection of upgrades and new abilities that the player can unlock by unlocking each skill. The lines are ordered by prerequisite, allowing for quick readability.

![[SkillLineExample.drawio.svg]]

When the player is playing a run however, the lines will be presented to them much differently. Individual skills will show up one at a time as the player is presented with branches made of random skills. When a player selects a branch they may place it anywhere in their unique skill tree, even if it goes against the order presented by the skill lines. The game will just simply prevent the player from collecting that skill from the tree until it's prerequisite skill is also collected.

![[SkillBranchExample.drawio.svg]]

## Runtime class draft

The current C# runtime model for this work lives in [[Progression and Feature Trees]].

For now it only defines the uninstantiated data types:

- `TriloDex` for the global hard-coded catalog that will eventually expose every authored feature tree
- `FeatureTree` for tree-level metadata, root ownership, and any-number-of-children prerequisite structures
- `SkillTree` for the local per-run binary tree that mixes copied skills from different feature trees
- `SkillNode` for upgrade nodes with a parent-based prerequisite and one-shot game effect
