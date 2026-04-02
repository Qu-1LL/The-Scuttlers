---
tags:
  - trilobites/csharp
  - trilobites/csharp/system
type: system
area: ui
aliases:
  - Input and UI
---
# UI and Input

Linked notes: [[Trilobites CSharp Home]] - [[Boot and Game Root]] - [[Rendering]] - [[Audio]] - [[Buildings]]

For official MonoGame input APIs and Gum control references, use [[External Docs - MonoGame and Gum]].

## Primary files

- `src/TriloGame.Game/UI/Input/InputController.cs`
- `src/TriloGame.Game/UI/Input/DoubleClickTracker.cs`
- `src/TriloGame.Game/UI/Menu/MenuController.cs`
- `src/TriloGame.Game/UI/Menu/MenuController.Drawing.cs`
- `src/TriloGame.Game/UI/Menu/MenuController.Layout.cs`
- `src/TriloGame.Game/UI/Selection/RoleRadialLayout.cs`
- `src/TriloGame.Game/UI/Selection/RoleSelectionState.cs`
- `src/TriloGame.Game/UI/Selection/SelectionFocusLayout.cs`
- `src/TriloGame.Game/UI/Settings/SettingsMenuLayout.cs`
- `src/TriloGame.Game/UI/Debug/DebugMenuLayout.cs`
- `src/TriloGame.Game/UI/Gum/GumShapePool.cs`
- `src/TriloGame.Game/UI/ViewModels/AssignmentEntryViewModel.cs`
- `src/TriloGame.Game/UI/ViewModels/BuildOptionViewModel.cs`

## Input polling model

`InputController` replaces browser event listeners with explicit frame snapshots.

That polling model is coordinated from [[Boot and Game Root]] and then exercised every frame through [[Runtime Flow]].

### Captured state

- current and previous keyboard state
- current and previous mouse state
- mouse point
- mouse delta
- wheel delta
- button transitions
- drag start point
- drag active state

### Important methods

- `BeginFrame()`
- `KeyPressed(...)`
- `KeyReleased(...)`
- `KeyHeld(...)`
- `BeginDrag()`
- `UpdateDrag(...)`
- `EndDrag()`

## Double click handling

`DoubleClickTracker` supports the manual movement interaction:

- arm a tile key on first click
- consume the key if the same tile is clicked again inside the threshold
- expire stale pending clicks

That interaction ultimately issues world actions against [[World Tiles and Cave]] and selected units from [[Entities and Roles]].

## Main colony menu

`MenuController` owns the right-side colony panel.

The panel is orchestrated by [[Boot and Game Root]], rendered through [[Rendering]], and constantly changes state in response to [[Buildings]] and [[Entities and Roles]].

### Main responsibilities

- open, close, and toggle the panel
- compute panel width
- manage active tabs
- update hover state
- handle clicks and wheel scroll
- sync Gum-backed background shapes
- draw the menu foreground

### Tabs and content

- `Buildings`
- `Assignments`
- `Selected`

### Important methods

- `OpenPanel(...)`
- `ClosePanel()`
- `TogglePanel()`
- `ResetState()`
- `SetSelectedObject(...)`
- `CoversScreenPoint(...)`
- `HandleWheel(...)`
- `HandleClick(...)`
- `SyncGumBackgrounds(...)`
- `Draw(...)`

## Selection overlays and helpers

- `RoleRadialLayout.cs`
  - radial menu geometry for selected trilobites from [[Entities and Roles]]
- `RoleSelectionState.cs`
  - shared-role or mixed-role logic for selected trilobite groups from [[Entities and Roles]]
- `SelectionFocusLayout.cs`
  - off-screen focus hint layout used with the camera from [[Rendering]]
- `SettingsMenuLayout.cs`
  - volume/settings panel layout
- `DebugMenuLayout.cs`
  - debug menu card and button geometry used by [[Diagnostics and Crash Reports]]

## Gum in the UI layer

The UI is not a full Gum-authored `.gumx` project. Instead, it uses code-first Gum-backed rounded shapes for retained background chrome.

That means this note overlaps heavily with [[Rendering]] and [[Build Content and Packaging]] even though the interaction behavior still lives in runtime code.

`GumShapePool.cs`:

- owns a `ContainerRuntime`
- pools `RoundedRectangleRuntime` objects
- exposes `BeginFrame()` and `EndFrame()`
- lets `GameApp` and `MenuController` request rounded rectangles each frame

## Interaction rules worth knowing

- `Tab` toggles the colony menu
- `F` focuses or follows the selected trilobite or building
- left drag pans the camera
- right drag creates a trilobite selection box
- right click on a trilobite auto-selects it and opens the radial role menu
- world clicks and UI clicks are intentionally routed through `GameApp` so menus, selection, build mode, settings, and debug state can all arbitrate first

## UI style notes

- rounded main UI chrome uses Gum-backed rounded rectangles from the same code-first Gum layer shared with [[Rendering]]
- debug menu intentionally stays sharp-edged
- text fitting and wrapping are still manually controlled in the foreground draw pass

## Related notes

- [[Rendering]]
- [[Audio]]
- [[Boot and Game Root]]
- [[Buildings]]
- [[External Docs - MonoGame and Gum]]
