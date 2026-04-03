---
tags:
  - trilobites/csharp
  - trilobites/csharp/reference
type: reference
area: inventory
aliases:
  - Runtime File Inventory
---
# File Inventory - TriloGame.Game

Linked notes: [[Trilobites CSharp Home]] - [[File Structure]] - [[System Map]]

Use this inventory alongside [[System Map]] and [[File Structure]]. The list below is intentionally file-first, while the system notes such as [[Boot and Game Root]], [[Simulation and Ticks]], [[World Tiles and Cave]], [[UI and Input]], and [[Rendering]] explain behavior and ownership.

## Root files

- `src/TriloGame.Game/Program.cs` - process entry point and top-level crash boundary
- `src/TriloGame.Game/GameApp.cs` - MonoGame runtime root and integration hub
- `src/TriloGame.Game/TriloGame.Game.csproj` - project manifest and package references
- `src/TriloGame.Game/app.manifest` - Windows app manifest
- `src/TriloGame.Game/Icon.ico` - executable icon
- `src/TriloGame.Game/Icon.bmp` - alternate icon asset
- `src/TriloGame.Game/.config/dotnet-tools.json` - local tool manifest
- `src/TriloGame.Game/.vscode/launch.json` - VS Code launch settings

## Audio

This section maps to [[Audio]].

- `Audio/AudioService.cs` - sound loading, playback, and shared volume
- `Audio/ClickPitchVariation.cs` - randomized pitch variation
- `Audio/GameAudioCue.cs` - cue-name enum

## Content

This section supports [[Build Content and Packaging]], [[Rendering]], and [[Audio]].

### Pipeline root

- `Content/Content.mgcb` - MGCB content manifest

### Audio assets

- `Content/Audio/BuildingFinished.wav`
- `Content/Audio/BuildingPlace.mp3`
- `Content/Audio/TrilobiteBirth.wav`
- `Content/Audio/TrilobiteSelected.wav`
- `Content/Audio/UiSelect.wav`
- `Content/Audio/VolumeSound.wav`

### Fonts

- `Content/Fonts/DebugFont.spritefont`
- `Content/Fonts/SmallFont.spritefont`
- `Content/Fonts/UiFont.spritefont`

### Icons

- `Content/Icons/Trilobite.ico`

### Textures

- `Content/Textures/AlgaeFarm.png`
- `Content/Textures/AlgaeTile.png`
- `Content/Textures/BackArrow.png`
- `Content/Textures/Barracks.png`
- `Content/Textures/CaveWall.png`
- `Content/Textures/CochiniumTile.png`
- `Content/Textures/EmptyTile.png`
- `Content/Textures/Enemy.png`
- `Content/Textures/HowDoIGetHimOff.png`
- `Content/Textures/IlmeniteTile.png`
- `Content/Textures/MagnetiteTile.png`
- `Content/Textures/MalachiteTile.png`
- `Content/Textures/MenuBlock.png`
- `Content/Textures/MiningPost.png`
- `Content/Textures/OrePath.png`
- `Content/Textures/Path.png`
- `Content/Textures/PeroteneTile.png`
- `Content/Textures/Queen.png`
- `Content/Textures/Radar.png`
- `Content/Textures/SandTile.png`
- `Content/Textures/Scaffold.png`
- `Content/Textures/Selected.png`
- `Content/Textures/SelectedEdge.png`
- `Content/Textures/Smith.png`
- `Content/Textures/Storage.png`
- `Content/Textures/Trilobite.png`
- `Content/Textures/window_3x1.png`
- `Content/Textures/window_4x1.png`
- `Content/Textures/window_5x4.png`

### UI textures

- `Content/UI/window_3x1.png`
- `Content/UI/window_4x1.png`
- `Content/UI/window_5x4.png`

## Core - Buildings

This section maps to [[Buildings]].

- `Core/Buildings/AlgaeFarm.cs` - farm growth and farmer assignments
- `Core/Buildings/Barracks.cs` - fighter staging building
- `Core/Buildings/Building.cs` - base building model
- `Core/Buildings/Factory.cs` - build-menu metadata wrapper
- `Core/Buildings/MiningPost.cs` - mining radius, queues, assignments, and inventory
- `Core/Buildings/Queen.cs` - queen feeding and birth logic
- `Core/Buildings/Radar.cs` - reveal-growth building
- `Core/Buildings/Scaffolding.cs` - construction site and completion handoff
- `Core/Buildings/Smith.cs` - smith building
- `Core/Buildings/Storage.cs` - storage building

## Core - Constants

These constants are consumed heavily by [[Simulation and Ticks]], [[Rendering]], and [[UI and Input]].

- `Core/Constants/GameConstants.cs` - tick speeds, zoom limits, drag threshold, double-click timing
- `Core/Constants/TileConstants.cs` - tile pixel size constants

## Core - Economy

This section maps to [[Resources Events and Stats]].

- `Core/Economy/Inventory.cs` - single-type carried inventory
- `Core/Economy/OreType.cs` - ore-name definitions
- `Core/Economy/ResourceReservation.cs` - reserved-material record
- `Core/Economy/StatsTracker.cs` - event-driven stat counters

## Core - Entities

This section maps to [[Entities and Roles]].

- `Core/Entities/Creature.cs` - base unit class
- `Core/Entities/Enemy.cs` - enemy AI
- `Core/Entities/Trilobite.cs` - colony AI and role workflows

## Core - Events

This section maps to [[Resources Events and Stats]].

- `Core/Events/GameEventBus.cs` - domain event dispatcher
- `Core/Events/GameEvents.cs` - event-name constants and payload

## Core - Pathfinding

This section maps to [[Pathfinding and BFS]].

- `Core/Pathfinding/BfsField.cs` - BFS field engine
- `Core/Pathfinding/PathBuilder.cs` - path reconstruction helpers

## Core - Progression

This section maps to [[Progression and Feature Trees]].

- `Core/Progression/BinarySkillNode.cs` - binary per-run skill-tree node copied from a feature-tree skill
- `Core/Progression/FeatureTree.cs` - feature-tree metadata and traversal helpers
- `Core/Progression/SkillTree.cs` - session-local binary skill tree built from feature-tree node copies
- `Core/Progression/SkillNode.cs` - upgrade-node model with prerequisite and one-shot effect behavior
- `Core/Progression/TriloDex.cs` - global hard-coded feature-tree catalog accessor

## Core - Simulation

This section maps to [[Simulation and Ticks]].

- `Core/Simulation/GameSession.cs` - persistent session state
- `Core/Simulation/TickProfiler.cs` - tick timing history and summaries
- `Core/Simulation/TickRunner.cs` - one-step tick executor

## Core - World

This section maps to [[World Tiles and Cave]].

- `Core/World/Cave.cs` - live world graph and registries
- `Core/World/CaveGenerator.cs` - cave generation helpers
- `Core/World/Graph.cs` - graph base class
- `Core/World/ReachabilitySystem.cs` - reachable-tile updates
- `Core/World/RevealSystem.cs` - reveal-state updates
- `Core/World/Tile.cs` - tile model

## Rendering

This section maps to [[Rendering]].

- `Rendering/CameraController.cs` - world/screen transforms
- `Rendering/RenderingContext.cs` - batched render dependencies
- `Rendering/SpriteFactory.cs` - texture registry

## Shared

This section is most relevant to [[Diagnostics and Crash Reports]].

- `Shared/Diagnostics/CrashReporter.cs` - crash report writer
- `Shared/Extensions/CollectionExtensions.cs` - collection helpers
- `Shared/Math/GridPoint.cs` - grid coordinate type
- `Shared/Math/GridRect.cs` - grid rectangle type
- `Shared/Utilities/RandomUtil.cs` - shared RNG helpers

## UI

This section maps to [[UI and Input]].

### Debug

- `UI/Debug/DebugMenuLayout.cs` - debug overlay geometry

### Gum

- `UI/Gum/GumShapePool.cs` - pooled Gum rounded-rectangle container

### Input

- `UI/Input/DoubleClickTracker.cs` - manual move double-click state
- `UI/Input/InputController.cs` - input polling and drag state

### Menu

- `UI/Menu/MenuController.cs` - menu state and orchestration
- `UI/Menu/MenuController.Drawing.cs` - menu foreground drawing helpers
- `UI/Menu/MenuController.Layout.cs` - menu layout helpers

### Selection

- `UI/Selection/RoleRadialLayout.cs` - radial menu geometry
- `UI/Selection/RoleSelectionState.cs` - selection role summarization
- `UI/Selection/SelectionFocusLayout.cs` - focus-hint geometry

### Settings

- `UI/Settings/SettingsMenuLayout.cs` - settings panel geometry

### View models

- `UI/ViewModels/AssignmentEntryViewModel.cs` - assignments-tab view model
- `UI/ViewModels/BuildOptionViewModel.cs` - build-card view model

## Best companion notes

- [[System Map]]
- [[File Structure]]
- [[Runtime Flow]]
- [[Testing Strategy]]
