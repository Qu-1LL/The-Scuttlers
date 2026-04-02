---
tags:
  - trilobites/csharp
  - trilobites/csharp/system
type: system
area: audio
---
# Audio

Linked notes: [[Trilobites CSharp Home]] - [[Resources Events and Stats]] - [[UI and Input]] - [[Build Content and Packaging]]

## Primary files

- `src/TriloGame.Game/Audio/AudioService.cs`
- `src/TriloGame.Game/Audio/ClickPitchVariation.cs`
- `src/TriloGame.Game/Audio/GameAudioCue.cs`
- `src/TriloGame.Game/GameApp.cs`

## Audio cue contract

`GameAudioCue` defines the runtime sound names:

- `BuildingPlace`
- `BuildingFinished`
- `TrilobiteBirth`
- `TrilobiteSelected`
- `UiSelect`
- `VolumeSound`

## `AudioService`

### Responsibilities

- load `SoundEffect` content
- map content to `GameAudioCue`
- track `VolumePercent`
- expose normalized volume
- play cues with randomized pitch variation

`AudioService` is owned by [[Boot and Game Root]] and loaded during the content pass described in [[Build Content and Packaging]].

### Important behavior

- all sounds now get randomized pitch variation
- volume is shared across gameplay and UI sounds
- settings-menu volume changes preview the volume cue

## Pitch variation

`ClickPitchVariation.cs` owns the simple randomized pitch set used when sounds are played repeatedly. This helps repeated clicks and common interactions sound less static.

## Who requests sounds

- `GameApp` plays immediate UI sounds such as menu and selection interactions
- gameplay systems request sounds indirectly through `GameSession.AudioCueRequested` from [[Simulation and Ticks]]
- examples:
  - `Queen.cs` requests `TrilobiteBirth` through [[Buildings]]
  - `Scaffolding.cs` requests `BuildingFinished` through [[Buildings]]
  - successful building placement plays `BuildingPlace` through [[UI and Input]]

## Content source

Audio content is authored under `src/TriloGame.Game/Content/Audio/` and compiled through MGCB, as documented in [[Build Content and Packaging]].

## Related notes

- [[Build Content and Packaging]]
- [[UI and Input]]
- [[Resources Events and Stats]]
- [[Boot and Game Root]]
