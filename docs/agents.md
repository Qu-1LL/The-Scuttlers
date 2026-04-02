## UI Guidance

- Treat all player-facing UI as polished colony UI, not debug chrome.
- Use rounded frames and buttons for the shared menu, build cards, radial role picker,
  focus hints, restart/game-over prompts, and the left-side settings panel.
- Fit text to its box by shrinking first and ellipsizing only when needed; do not allow
  labels to clip outside their bounds.
- Keep iconography and labels centered inside their hit targets.
- The top-left settings button opens a rounded settings panel with volume controls from
  `0` to `100` in stepped increments and audio feedback on change.
- The only intentional exception is the backtick debug menu, which keeps its sharper,
  utilitarian presentation.
