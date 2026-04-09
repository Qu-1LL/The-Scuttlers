## Architecture Overview

The project is currently a single MonoGame game assembly with layered modules inside it.

### Practical layers

- `Core`
  - deterministic simulation rules
  - entities, buildings, world state, pathfinding, economy, events
- `Runtime`
  - startup/bootstrap flow
  - simulation clock orchestration
  - game-over state
  - play/test automation API
- `UI`
  - menu, debug, selection, settings, Gum-backed controls
- `Rendering`
  - camera and render helpers
- `Audio`
  - cue registration, playback, and audio-specific runtime systems
- `Shared`
  - diagnostics, math, and utilities

### Current host rule

`GameApp` is the MonoGame host and composition root. It should wire modules together, but it
should not remain the long-term home for new gameplay rules or reusable orchestration.

### Runtime systems added in the current refactor

- `Runtime/Bootstrap/GameSessionBootstrapper.cs`
- `Runtime/Systems/GameSimulationClockSystem.cs`
- `Runtime/Systems/GameOverStateSystem.cs`
- `Audio/OpalAudioSystem.cs`
- `Runtime/Automation/GamePlayApi.cs`

These form the current “golden path” for adding structure without destabilizing the whole game.

## UI Rendering Notes

- Screen-space UI is Gum-first.
- In this MonoGame host, screen-space UI text is also Gum-first.
- Player-facing surfaces should route through `UI/Gum/GumUiRenderer.cs` or Gum-backed controls
  so panels, buttons, toggles, hints, and text all share the same rendering path.
- New screen UI text should not be added through raw `SpriteBatch.DrawString`; text should flow
  through the existing Gum-backed fitted/wrapped text helpers so layout and layering stay
  consistent.
- Prefer fixed integer Gum `FontSize` values for normal UI text. Avoid fractional `FontScale`
  for routine sizing because it softens text and makes nearby surfaces look inconsistent.
- Treat MonoGame `SpriteBatch.DrawString` as a world-space/debug-only tool unless the text is
  intentionally attached to the game world rather than the UI.
- Short UI sound cues are routed through a shared `AudioService`, while gameplay systems
  request sounds indirectly through `GameSession.AudioCueRequested`.
- Managed crash handling is routed through a shared crash reporter that writes timestamped
  reports with exception text plus a live `GameApp` snapshot.
- The debug menu overlay intentionally remains visually simpler than the rounded colony menu, but
  it should still render through the same Gum-based screen UI path.
