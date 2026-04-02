## UI Port Notes

- Rounded UI in the MonoGame port is drawn manually with pixel-safe rounded-rect helpers
  instead of browser/CSS border radius.
- Text fitting is handled in code so names and button labels stay inside their cards,
  buttons, and prompts at different window sizes.
- The trilobite radial role picker clamps its button/title layout to the visible
  gameplay area so it does not clip off-screen.
- `.wav` assets are now routed through MGCB as `SoundEffect` content and played through
  the shared in-game audio service so UI and gameplay cues reuse the same volume setting.
- The top-left settings button opens a rounded side panel for volume control; changing
  volume snaps to fixed increments and previews the volume cue at the new setting.
- Unhandled managed crashes now emit a text report in the runtime `CrashReports` folder
  so camera/input/menu/session state is preserved for debugging after a failure.
- The debug menu is the one deliberate exception and keeps square-edged debug styling.
