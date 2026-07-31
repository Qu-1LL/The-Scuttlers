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
  - creature role work is represented by typed `CreatureTask` values; role and activity are typed
    state, with the combat role controller and battle plan owning explicit fighter transitions
    and deterministic army-level cohorts
  - `Core/Movement` owns fixed-point continuous routes, environment-aware locomotion,
    deterministic formations, and explicit impulse displacement
  - terrain, buildings, topology, and coarse BFS remain cell based; moving bodies are not stored
    on `Tile` and walking does not invalidate static terrain fields
  - `Core/Interaction` owns rotated rectangular building zones, physical slots, and deterministic
    30-tick reservation leases
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

### Navigation ownership

Per-building traversal navigation is split from the legacy synchronous `BfsField` path:

- `Core` declares explicit building metadata for whether a building maintains a traversal field,
  how its open-map seeds are selected, and whether maintenance is synchronous or asynchronous.
- `Core` creates immutable tile/building topology snapshots and publishes immutable per-building
  field snapshots for O(1) distance and next-step reads.
- `Runtime/Systems/BuildingBfsFieldMaintenanceSystem` owns the single long-lived worker, its
  topology mirror, incremental repair state, and command/result queues.
- `enemy`, `colony`, `wall`, and the queen remain synchronous. Other navigable building fields are
  asynchronously maintained in production; non-navigable buildings do not participate in the
  per-building traversal-field set.
- Mining-post movement uses the general per-building field path. Its compatibility telemetry is
  retained, but there is no separate mining-post movement-field cache.
- Smooth creature routes consume bounded coarse path chunks from those published snapshots, then
  follow their existing fixed-point continuous movement. Generic building navigation terminates on
  a snapshot tile with distance `0`, including exterior access tiles for solid-footprint buildings.
- A building field seeds every walkable interaction-zone slot at distance `0` by default.
  `NavigateToInteractionZone` reserves capacity in the requested zone, then follows that shared
  building field instead of creating a destination-specific point BFS. Spawn-only and hosted-only
  slots explicitly opt out of field seeding.
- Building selection no longer maintains separate ownership BFS fields. Nearest-building queries
  compare the corresponding per-building traversal fields, and assignment candidate lists are
  ordered directly by those distances.

Worker results are applied only at runtime pump points outside `TickRunner.RunTick`. Session and
building runtime ids guard publication so detached sessions and replaced buildings cannot publish
stale mutable state into the current simulation.

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

## Continuous World Model

- `WorldPoint` and `WorldVector` are authoritative deterministic coordinates with 16 subunits per
  world pixel. Floating-point conversion is a rendering and external-API boundary only.
- Every creature has a stable numeric ID, circular body, previous/current position, velocity,
  desired velocity, typed role/activity, and persistent desired route.
- `Creature.Location` is a read-only projection of `Position`; it is not mutable authority.
- Creature locomotion collides with environment blockers only; creature bodies may overlap one
  another. `ApplyImpulse` displaces only the target creature, and unresolved displacement remains
  pending for later ticks.
- Hosted creatures use the same authoritative position at a kinematic station anchor. Vehicles
  remain separate world objects but block creature circles through the same clearance rules.
- Point-route construction is capped at 32 per tick. Excess typed navigation tasks remain in
  `Planning` and retry in stable creature-ID order.
- Route following, arrival, and wall avoidance produce preferred velocity only; swept environment
  collision remains authoritative.
- `Core/Combat/CombatWorld` owns fixed-tick attack commands, centered creature-body hitboxes and
  hurtboxes, circle, AABB, and capsule narrow phase tests, a uniform-grid broadphase, stable hit
  events, and faction filtering. It resolves against final post-movement poses and emits shared
  damage/audio events. Structure attacks retain a blocked-tile reach envelope, while creature
  combat uses the same centered body shape for both sides.
- `Core/Combat/CombatAgentController` consumes automatic 8x8 threat-sector directives and routes
  fighters through the once-per-tick shared enemy field for long travel, then directly to a
  deterministic stand-off point on the assigned enemy's live world pose before attacking. This
  avoids destination-specific point BFS fields while preserving momentum during nearby retargeting.
  Fighters keep only the colony `fighter`
  profession; named tactical subroles are not part of simulation state. When danger is clear,
  fighters use the same deterministic idle-wander routine as every other mobile trilobite.
- Enemies expose the same explicit combat lifecycle through `EnemyCombatState`, separating target
  acquisition, colony pursuit, attacks, breaches, and recovery for runtime inspection and replay
  diagnostics.
- Combat target selection uses deterministic live-ant load balancing: fighters are processed by
  stable creature ID, assigned to the least-loaded ant, and use distance then ant ID as tie-breaks.
  Threat sectors remain the fallback advance plan, while the spatial grid handles actual target
  acquisition and hit resolution. No attacker performs an army-wide scan during hit resolution.
- Creature death publishes a shared render request; the session particle bridge turns it into a
  red two-second burst using the existing particle system's high-velocity friction and tile-collision
  support.
- `MiningStrikeSystem` owns the unchanged mining claim/reach/timing path. Mining strikes are
  point-sampled at their rendered magenta point and never share combat hitbox state.
- `MiningClaimAllocator` gives miners deterministic claims while allowing multiple miners to share
  a mineable target, and each post rotates its mineable queue so autonomous and manual mining
  orders keep round-robin pressure on the available work.
- Trilobite role state machines start from an explicit idle state. All mobile trilobite professions
  use the same deterministic layered idle routine: mostly stationary pauses followed by short,
  anchor-biased local moves that prefer clear direct steering and bound fallback pathfinding.
- Creature carrier inventory may contain multiple resource types; older `Inventory.Type` callers
  observe the first carried resource for compatibility, while deposit paths drain all carried
  stacks into compatible storage.

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
