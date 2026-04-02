## UI Rendering Notes

- MonoGame UI is drawn in immediate mode through shared shape/text helpers rather than a
  retained widget system.
- Player-facing surfaces should route through rounded-rectangle and fitted-text helpers
  so menu panels, radial role buttons, settings controls, hints, and restart prompts
  keep consistent edges and avoid text clipping.
- Short UI sound cues are routed through a shared `AudioService`, while gameplay systems
  request sounds indirectly through `GameSession.AudioCueRequested`.
- Managed crash handling is routed through a shared crash reporter that writes timestamped
  reports with exception text plus a live `GameApp` snapshot.
- The debug menu overlay intentionally remains a simpler sharp-edged renderer so it stays
  visually separate from the main in-world UI.
