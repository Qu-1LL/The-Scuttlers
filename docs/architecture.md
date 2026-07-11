## Architecture Overview

The project is currently a single MonoGame game assembly with layered modules inside it.

### Practical layers

- `Core`
  - deterministic simulation rules
  - entities, buildings, world state, pathfinding, economy, events
  - shared item/storage contracts live under `Core/Economy` so trilobites, stockpiles, and
    storage buildings can exchange resources through one typed catalog and common interfaces
  - construction recipes flow through typed resource requirements that can target either exact
    resources or whole resource categories, with scaffolding resolving category matches through
    storage query helpers before builders haul exact items
  - mining-order execution lives in `Core/Simulation` so UI can request orders without owning
    the manual-order state transformation
  - cave generation is isolated in `Core/World/CaveGenerator.cs`; `Cave` owns world state and
    simulation-facing coordination rather than generation policy details
  - cave path queries that do not need to mutate world state live in `Core/Pathfinding`
  - trilobite role dispatch is isolated behind `Core/Entities/TrilobiteRoleBehavior.cs`, with
    role-specific behavior components such as `TrilobiteFighterBehavior.cs` owning extracted
    state machines
- `Runtime`
  - startup/bootstrap flow
  - simulation clock orchestration
  - game-over state
  - round pacing and round-driven ant spawning orchestration
  - resource stockpile aggregation across storage buildings for HUD and tooling surfaces
  - research draft generation and placement orchestration, including round rewards and infinite-draft follow-ups
  - play/test automation API
- `UI`
  - menu, debug, selection, settings, Gum-backed controls
  - the colony menu reports explicit interaction outcomes, such as consumed clicks, requested
    select sounds, and requested building placement, instead of calling back into `GameApp`
  - mining tile selection state lives under `UI/Selection` so the host does not own raw
    tile-key list mutation policy directly
  - mining tile rectangle hit scanning and hover tooltip rendering live under `UI/Selection` so
    the host supplies cave/input/camera context without owning mining-selection UI details
  - mining-order menu state, layout, rendering, and hit-test outcomes live under `UI/Selection`;
    the host still dispatches accepted orders into Core simulation commands
  - settings layout, rendering, and interaction routing live under `UI/Settings` so the host
    delegates menu behavior instead of owning widget-level details directly
  - main-menu, game-over, and round-debug overlay layout/rendering live under `UI` so the host
    performs screen flow orchestration instead of owning overlay chrome and button geometry
  - the resource HUD lives under `UI/Hud` and renders stockpile snapshots through Gum, keeping
    resource aggregation in runtime rather than in screen drawing code
  - shared Gum text/chrome helpers live under `UI/Gum` so new overlays and menus can compose
    reusable rounded panels, action buttons, fitted text, and scrollable text viewports instead of
    cloning local draw helpers
  - the shared Gum viewport helper now owns clipped scroll-surface rendering for card grids and
    other expandable UI collections, including fallback primitive clipping when Gum container
    masking is not sufficient for rounded shapes
  - debug-menu button layout and action hit-testing live under `UI/Debug`; the host executes the
    selected actions because they mutate runtime/game state
  - research-tree viewport pan, zoom, visible bounds, and release snapping live in
    `UI/Research/ResearchTreeViewportState.cs` so draft interaction and rendering do not own
    viewport math directly
  - shared research node info/text formatting lives under `UI/Research` so draft, tree, and
    info-panel surfaces render the same node metadata through one typed model
- `Rendering`
  - camera and render helpers
  - world-scene rendering, including parallax background and cave tile/entity layers
  - soil-patch rendering draws per-tile crop sprites in the world layer instead of collapsing the
    patch to one building sprite
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
- `Runtime/Systems/RoundManager.cs`
- `Runtime/Systems/AntHandler.cs`
- `Runtime/Systems/ResearchDraftSystem.cs`
- `Audio/SessionAudioBridge.cs`
- `Runtime/Automation/GamePlayApi.cs`

These form the current “golden path” for adding structure without destabilizing the whole game.

## UI Rendering Notes

- Screen-space UI is Gum-first.
- In this MonoGame host, screen-space UI text is also Gum-first.
- World rendering is layered: parallax cave background first, then floor tiles, then world overlays such as walls, ore, and cave crystals.
- `Rendering/WorldSceneRenderer.cs` owns the reusable world-scene draw details so `GameApp`
  can stay focused on MonoGame lifecycle and top-level pass orchestration.
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
