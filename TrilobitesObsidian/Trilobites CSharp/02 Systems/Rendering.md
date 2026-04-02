---
tags:
  - trilobites/csharp
  - trilobites/csharp/system
type: system
area: rendering
---
# Rendering

Linked notes: [[Trilobites CSharp Home]] - [[Runtime Flow]] - [[UI and Input]] - [[World Tiles and Cave]] - [[Entities and Roles]]

For official framework references on `SpriteBatch`, `Game`, MGCB-backed content loading, Gum shapes, and `GumBatch`, use [[External Docs - MonoGame and Gum]].

## Primary files

- `src/TriloGame.Game/Rendering/CameraController.cs`
- `src/TriloGame.Game/Rendering/RenderingContext.cs`
- `src/TriloGame.Game/Rendering/SpriteFactory.cs`
- `src/TriloGame.Game/GameApp.cs`
- `src/TriloGame.Game/UI/Gum/GumShapePool.cs`

## Camera

`CameraController` is the world-to-screen transform layer.

It is used constantly by [[Boot and Game Root]] and by every interaction path in [[UI and Input]].

### Responsibilities

- store current zoom as `CurrentScale`
- store the camera origin in world space
- store the current viewport center in screen space
- support viewport resize compensation
- convert world coordinates to screen coordinates
- convert screen points back into world coordinates

### Key methods

- `SetViewport(...)`
- `SetOrigin(...)`
- `HandleViewportResize(...)`
- `PanByScreenDelta(...)`
- `WorldToScreen(...)`
- `ScreenToWorld(...)`

## Rendering context

`RenderingContext` bundles the runtime rendering dependencies:

- `SpriteBatch`
- `UiFont`
- `SmallFont`
- `DebugFont`
- `WhitePixel`
- `SpriteFactory`
- `Camera`

It lets UI and menu code receive a compact rendering bundle rather than long parameter lists.

That makes it the handoff point between [[Rendering]] and [[UI and Input]].

## Sprite registry

`SpriteFactory` is a keyed registry of loaded textures.

### Responsibilities

- register `Texture2D` objects under stable string keys
- retrieve textures by key
- expose `TryGet(...)` for safe lookup

This is how `GameApp` maps authored MGCB assets from [[Build Content and Packaging]] to game texture names such as `Queen`, `MiningPost`, `Selected`, and `Path`.

## Draw order in `GameApp`

### World pass

- tiles from [[World Tiles and Cave]]
- buildings from [[Buildings]]
- creatures from [[Entities and Roles]]
- role labels
- selection markers
- selection box
- floating build preview
- lightweight world debug overlay

### UI background pass

- Gum-backed rounded rectangles for menu, settings, role radial, focus hint, and game-over overlays from [[UI and Input]]

### UI foreground pass

- menu text and buttons from [[UI and Input]]
- debug menu from [[Diagnostics and Crash Reports]]
- settings text and controls
- radial labels and buttons
- focus hint
- game-over foreground

## Why the UI uses two styles

- the main player-facing UI uses rounded Gum-backed chrome
- the debug menu stays sharper and simpler on purpose

## Rendering helpers still living in `GameApp`

`GameApp.cs` still contains many draw helpers because it owns the immediate-mode orchestration:

- world texture helpers
- screen-line and border helpers
- debug-card helpers
- text fitting and wrapping helpers
- selection and overlay draw helpers

## Related notes

- [[UI and Input]]
- [[Boot and Game Root]]
- [[World Tiles and Cave]]
- [[Audio]]
- [[External Docs - MonoGame and Gum]]
