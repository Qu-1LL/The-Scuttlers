# AGENTS.md

## Purpose

This file is the local contract for screen-space UI work under `src/TriloGame.Game/UI`.
Read it after the repository root [AGENTS.md](../../../AGENTS.md) and the game-project
[AGENTS.md](../AGENTS.md) when touching menus, debug overlays, selection UX, settings UX, or
Gum-backed control logic.

## UI Placement

Put code in `UI` when it is:

- menu state
- panel layout
- debug overlay behavior
- selection UX
- settings UX
- Gum-backed control logic
- screen-space UI text/chrome rendering that should route through Gum rather than raw `SpriteBatch`

## UI Rendering Rule

- All screen-space UI, including text, should be rendered through Gum (`GumUiRenderer` or Gum-backed
  controls).
- In the MonoGame host, this applies to labels, fitted text, wrapped text, menu text, settings text,
  debug menu text, and game-over/main-menu overlay text as well.
- Do not introduce new `SpriteBatch.DrawString`-driven screen UI.
- World-space debug labels and world rendering overlays may still use the world render path when
  they are part of the scene rather than the UI layer.
- Gum text should generally use fixed integer `FontSize` values instead of fractional `FontScale`.

## UI High-Pressure Files

These files are still structurally important and should be treated carefully:

- `Menu/MenuController.cs`
- `Menu/MenuController.Layout.cs`
- `Menu/MenuController.Drawing.cs`
- `Debug/DebugToggleControls.cs`
- `Gum/GumShapePool.cs`

If you change these files, actively look for an extraction opportunity instead of just growing them.

## Additional UI Guidance

See [../../../docs/agents.md](../../../docs/agents.md) for the current player-facing UI style
guidance, especially the rounded colony UI expectations and the intentional debug-menu exception.
