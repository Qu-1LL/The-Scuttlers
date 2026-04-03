---
tags:
  - trilobites/csharp
  - trilobites/csharp/wiki
type: wiki
area: controls
aliases:
  - Controls
  - Keyboard Shortcuts
---
# Controls and Shortcuts

This note describes the current playable controls in the C# build. For the lower-level routing and menu ownership behind these inputs, see [[UI and Input]].

## Camera controls

- `W / A / S / D`: pan the camera
- Left drag on world space: pan the camera
- Mouse wheel: zoom in and out
- `F`: focus the selected trilobite or selected building once
- Hold `F`: keep following the selected trilobite or selected building

## Selection controls

- Left click a trilobite: select it
- Left click a building: select it
- Left click non-interactable or off-map space: clear selection
- Right drag: box-select trilobites
- Right click a trilobite: auto-select it and open the role radial
- Right click with a multi-selection: open the group role radial

These controls are the main entry into [[Trilobite Roles]] and [[Buildings and Placement]].

## Main menu

- On launch, the game opens on a main menu instead of starting a colony immediately
- `Start Game`: create a fresh colony using the normal startup generation flow
- `Quit Game`: close the game window
- The title reads `Welcome to The Scuttlers`
- The line `Trilo-dex coming soon!` appears below the two buttons

## Orders and world interaction

- Left click a wall: mine it
- Left click empty space: clear current selection
- Double left click the same reachable tile: move selected trilobite(s) there
- `R`: rotate the current building preview during placement
- `Escape`: clear active selection, cancel active placement, and close the colony panel

## Colony menu and settings

- `Tab`: open or close the colony panel
- Top-left gear button: open or close the settings menu
- Top-right gear button: reopen the colony panel when it has been collapsed
- In-panel arrow button: collapse the colony panel

The actual panel flow and tab logic are described in [[Buildings and Placement]] and [[UI and Input]].

## Tick and debug shortcuts

- `Space`: pause or unpause the simulation
- `Enter`: advance one simulation tick
- While running:
  - `1`: set tick speed to `500 ms`
  - `2`: set tick speed to `250 ms`
  - `3`: set tick speed to `100 ms`
  - `4`: set tick speed to `50 ms`
- While paused:
  - `1`: show queen BFS values
  - `2`: show enemy BFS values
  - `3`: show colony BFS values
- `P`: spawn a debug enemy
- `` ` ``: open or close the debug panel

These are more relevant to testing and diagnostics than normal play, but they are still part of the live build.

## Focus hint behavior

If a selected trilobite or selected building drifts off-screen, the game shows `F to focus`. That hint disappears when:

- you press or hold `F`
- the selection is cleared
- the selected target returns close to the gameplay center

## Settings menu

The settings menu currently exposes:

- `Volume` from `0` to `100`
- `-` and `+` buttons
- clickable volume bar changes in `5`-point increments
- `Return to Main Menu`, which closes the current colony and goes back to the startup menu

## Related notes

- [[Getting Started and How to Play]]
- [[Trilobite Roles]]
- [[Buildings and Placement]]
- [[UI and Input]]
