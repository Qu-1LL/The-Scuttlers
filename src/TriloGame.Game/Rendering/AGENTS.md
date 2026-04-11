# AGENTS.md

## Purpose

This file is the local contract for rendering helpers under `src/TriloGame.Game/Rendering`.
Read it after the repository root [AGENTS.md](../../../AGENTS.md) and the game-project
[AGENTS.md](../AGENTS.md) when touching camera math, render helpers, sprite placement/origin
concerns, or world-space overlays.

## Rendering Placement

Put code in `Rendering` when it is:

- camera math
- render helpers
- sprite placement/origin concerns

`Rendering` may depend on `Core` and `Runtime`, but the reverse should not happen.

World-space debug labels and world rendering overlays may still use the world render path when
they are part of the scene rather than the screen-space UI layer.
