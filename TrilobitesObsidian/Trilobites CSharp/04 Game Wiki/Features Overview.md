---
tags:
  - trilobites/csharp
  - trilobites/csharp/wiki
type: wiki
area: features
aliases:
  - Features
  - Current Feature Set
---
# Features Overview

This note summarizes the current C# build as a playable feature set rather than a codebase.

## Core gameplay features

- real-time colony simulation with pause and manual tick stepping
- controllable trilobite role assignment
- mining, farming, building, and fighting workflows
- building placement with rotation and scaffolding completion
- underground cave expansion through mining
- enemy pressure and danger-state defense
- queen feeding and brood growth

These systems form the main loop described in [[Core Loop and Colony Growth]].

## Player interaction features

- single selection for trilobites and buildings
- right-click radial role assignment
- right-drag box select for trilobite groups
- focus and follow behavior with `F`
- colony side panel with `Buildings`, `Assignments`, and `Selected`
- top-left settings panel with shared volume control

These are explained in [[Controls and Shortcuts]] and [[Getting Started and How to Play]].

## UI and feedback features

- rounded player-facing UI chrome
- focused debug overlay
- live selection markers and path previews
- building preview placement feedback
- off-screen `F to focus` hint
- game-over overlay with `Play Again`

The implementation side of those systems lives in [[UI and Input]] and [[Rendering]].

## Audio features

- UI selection sound
- trilobite selection sound
- building placement sound
- building completion sound
- trilobite birth sound
- volume preview sound
- randomized pitch variation on repeated sounds

See [[Audio]] for the runtime system behind those cues.

## Technical support features that matter to testing

- debug tick speed controls
- BFS field debug views
- live performance readout
- crash report generation

These are not “gameplay features” in the same sense, but they matter a lot for pre-playtest stability and iteration.

## What this current build emphasizes

The current build is strongest as:

- a colony-management sandbox
- a role-assignment strategy game
- a systems-heavy prototype with visible simulation behavior

It is less about scripted missions and more about managing a living colony well.

## Related notes

- [[Game Wiki Home]]
- [[Core Loop and Colony Growth]]
- [[Controls and Shortcuts]]
- [[Getting Started and How to Play]]
