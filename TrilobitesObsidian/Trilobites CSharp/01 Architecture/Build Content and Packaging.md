---
tags:
  - trilobites/csharp
  - trilobites/csharp/architecture
  - trilobites/csharp/reference
type: architecture
area: build
aliases:
  - Build and Packaging
---
# Build Content and Packaging

Linked notes: [[Trilobites CSharp Home]] - [[File Structure]] - [[Rendering]] - [[Audio]]

Framework docs for MGCB, content loading, and Gum setup are collected in [[External Docs - MonoGame and Gum]].

## Project file

`src/TriloGame.Game/TriloGame.Game.csproj` defines the runtime build.

### Important settings

- `OutputType`: `WinExe`
- `TargetFramework`: `net9.0`
- `RollForward`: `Major`
- `PublishReadyToRun`: `false`
- `TieredCompilation`: `false`

### Package references

- `Gum.MonoGame`
- `Gum.Shapes.MonoGame`
- `MonoGame.Framework.DesktopGL`
- `MonoGame.Content.Builder.Task`

## Content pipeline

The game loads authored assets through `Content.mgcb`.

### Content folders

- `Content/Audio/`
  - `BuildingFinished.wav`
  - `BuildingPlace.mp3`
  - `TrilobiteBirth.wav`
  - `TrilobiteSelected.wav`
  - `UiSelect.wav`
  - `VolumeSound.wav`
- `Content/Fonts/`
  - `DebugFont.spritefont`
  - `SmallFont.spritefont`
  - `UiFont.spritefont`
- `Content/Icons/`
  - `Trilobite.ico`
- `Content/Textures/`
  - world, building, UI, and overlay textures such as `Queen.png`, `MiningPost.png`, `Selected.png`, and `Path.png`
- `Content/UI/`
  - UI frame textures such as `window_3x1.png`, `window_4x1.png`, `window_5x4.png`

## Runtime loading path

- `GameApp.LoadContent()` creates the `SpriteBatch` and `GumBatch`, which are part of [[Boot and Game Root]] and [[Rendering]]
- textures are registered into `SpriteFactory` for [[Rendering]]
- sprite fonts are loaded into `RenderingContext` for [[Rendering]] and [[UI and Input]]
- sound effects are loaded into `AudioService` for [[Audio]]

## Tooling

- local tool manifest: `src/TriloGame.Game/.config/dotnet-tools.json`
- VS Code launch settings: `src/TriloGame.Game/.vscode/launch.json`
- content tooling helper area: `tools/content-pipeline/README.md`

For official external references, see the MonoGame content pipeline and MGCB links in [[External Docs - MonoGame and Gum]].

## Common commands

### Run the game from source

```powershell
dotnet run --project TriloGame/TriloGame.CSharp/src/TriloGame.Game/TriloGame.Game.csproj
```

### Run tests

```powershell
dotnet test TriloGame/TriloGame.CSharp/src/TriloGame.Tests/TriloGame.Tests.csproj
```

### Publish a Windows build

```powershell
dotnet publish TriloGame/TriloGame.CSharp/src/TriloGame.Game/TriloGame.Game.csproj -c Release -r win-x64 --self-contained true
```

## What packaging produces

- build outputs go under `src/TriloGame.Game/bin/`
- publish outputs go under `src/TriloGame.Game/bin/Release/net9.0/win-x64/`
- crash reports are written at runtime under the game output folder inside `CrashReports/`, which is part of [[Diagnostics and Crash Reports]]

## Design implications

- content is part of the runtime architecture because texture and audio keys are hard-wired into `GameApp`, building classes, and the audio system, which connects this note directly to [[Rendering]], [[Buildings]], and [[Audio]]
- Gum is used code-first rather than through a `.gumx` authoring project, which matters most in [[UI and Input]] and [[Rendering]]
- build output folders should not be edited by hand when documenting the architecture

## Related notes

- [[Rendering]]
- [[Audio]]
- [[Diagnostics and Crash Reports]]
- [[External Docs - MonoGame and Gum]]
- [[File Inventory - TriloGame.Game]]
