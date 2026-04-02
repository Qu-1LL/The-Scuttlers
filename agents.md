# TriloGame C# port agent notes

## Scope
This document defines the required functional-parity target for the C# / MonoGame
adaptation of the current JavaScript codebase.

The source of truth for gameplay behavior is the implemented JavaScript version in
`src/` and `public/`. This file exists to guide the C# port without
changing gameplay, controls, content scope, data flow, or player-facing behavior.

## Porting guardrails
- Keep all gameplay behavior functionally identical to the current JavaScript version.
- Do not add new features, new gameplay rules, new systems, or new content.
- Do not remove existing features, workflows, or UI behavior.
- Preserve all player-visible hotkeys, assignment names, resource names, building names,
  event names, menu flow, tick speeds, and behavior timing semantics.
- Internal implementation details may change only when doing so is necessary to adapt to
  C# / MonoGame or to avoid an inefficient literal JavaScript-to-C# translation.
- When an internal implementation changes, the resulting behavior must still match the
  JavaScript version exactly.

## Documentation maintenance
- When a new building is implemented in the C# port, add its data model and workflow to
  this file in the same change.
- When a new custom game event is implemented in the C# port, add the event name and
  exact emission timing to this file in the same change.
- When creature, tile, building, rendering, input, or UI state/workflow changes under
  `TriloGame.CSharp/src/`, update the relevant sections of this file in the same change.
- Keep sections consistent, factual, and explicit about triggers, runtime behavior,
  failure handling, and invariants.

## C# / MonoGame porting rules
- Use MonoGame's `Game` lifecycle for startup, update, draw, and content loading.
- Do not try to recreate browser event wiring literally. In MonoGame, input must be
  driven from per-frame keyboard and mouse state snapshots, with explicit transition
  detection for pressed, held, released, hover, and wheel behavior.
- Do not port the JavaScript `setTimeout` loop literally. Use a simulation accumulator
  inside `Update(GameTime)` so the colony tick still runs at the same speeds and with the
  same pause / single-step semantics as the JavaScript version.
- Use MonoGame content loading and the content pipeline for textures, fonts, audio, and
  other assets.
- Prefer standard generic .NET collections instead of recreating JavaScript `Map`, `Set`,
  or linked-list utilities directly:
  - `Map<K, V>` -> `Dictionary<TKey, TValue>`
  - `Set<T>` -> `HashSet<T>`
  - array-like mutable collections -> `List<T>`
  - FIFO queued behavior -> `Queue<T>`
- Preserve existing string-visible identifiers such as `miner`, `builder`, `enemy`,
  `Algae`, `Sandstone`, and event names. Stronger internal typing is allowed only if it
  does not change external behavior, saved names, logs, debug output expectations, or
  content naming.
- Follow standard C# naming conventions for C# symbols while preserving the JavaScript
  gameplay contract.
- Use standard .NET event patterns or a domain event bus with equivalent semantics for
  the existing gameplay events.
- Separate gameplay state from MonoGame rendering primitives when practical. MonoGame does
  not provide a PIXI-like retained scene graph, so the port should not force `Texture2D`,
  `SpriteBatch`, or input state objects to become the gameplay model.

## Trilobite roles and workflows

### Role: `unassigned`
- Trigger: default state, or any state that falls back from miner/farmer/builder/fighter.
- Workflow:
1. Release `AssignedBuilding` if it currently points to a mining post, algae farm,
   scaffolding, or barracks.
2. Clear any stored fighter target.
3. Do no autonomous work until reassigned or given manual movement.

### Role: `miner`
- Trigger:
1. Select a trilobite.
2. Open creature menu.
3. Press `Mine`.
- Workflow (`Trilobite` step chain):
1. Find viable mining posts: post must have inventory space and queued mineable tiles.
2. Prioritize posts by current assignment load, then approach distance.
3. Navigate to selected post area via the mining post's cached BFS field.
4. If carrying resources, deposit to post inventory first.
5. Request and reserve a mineable tile from the post queue.
6. Validate reservation is still valid.
7. Navigate to mining target:
   - Ore tiles: stand on tile.
   - Wall tiles: stand on an adjacent passable tile.
8. Mine tile:
   - `wall` becomes `empty`, new wall perimeter may be generated, trilobite gains
     `Sandstone`.
   - Ore tile becomes `empty`, trilobite gains that ore type.
9. Notify nearby mining posts that mineable tile queues are stale.
10. Loop back to step 1.
- Failure handling:
1. Navigation/mine failure clears queued actions.
2. Reservation is reset/requeued when needed.
3. Behavior restarts from step 1.

### Role: `farmer`
- Trigger:
1. Select a trilobite.
2. Open creature menu.
3. Press `Farm`.
- Workflow (`Trilobite` step chain):
1. If carrying non-`Algae`, clear inventory.
2. If carrying `Algae`, skip to queen delivery.
3. Find viable algae farms (must have an approach tile).
4. Prioritize farms by current assignment load, then approach distance.
5. Store the target farm in `AssignedBuilding` and navigate to farm via that farm's
   cached BFS field.
6. Build a route that visits passable farm tiles and returns to origin.
7. Move along farm route and attempt harvest at each step.
8. Harvest succeeds when `Random < growth / period`; success gives fixed `HarvestYield`.
9. After harvest, follow the queen building's cached BFS field one tile at a time until
   standing on a passable queen tile.
10. Feed queen all carried algae.
11. If queen quota is reached, queen may spawn broodlings and increase next quota.
12. Loop back to step 1.
- Failure handling:
1. If farm/queen unavailable or pathing fails, release assignment and restart selection.
2. Role checks enforce miner/farmer exclusivity through the shared `AssignedBuilding`
   slot.

### Role: `builder`
- Trigger:
1. Select a trilobite.
2. Open creature menu.
3. Press `Builder`.
- Workflow (`Trilobite` step chain):
1. Pick or keep an in-progress scaffolding by lowest current builder assignment count,
   then by building-BFS distance.
2. If carrying a resource the assigned scaffolding still needs, navigate into scaffold
   work range and deposit it.
3. If carrying a resource the assigned scaffolding no longer needs, return it to the
   nearest mining post with free space.
4. If the scaffolding still needs resources after accounting for builder-held
   reservations, sort mining posts by the BFS value of the trilobite's current tile in
   each post's cached field.
5. Check those posts shortest-to-longest for a resource whose unreserved inventory can
   satisfy one of the scaffolding's still-unreserved recipe needs.
6. Immediately reserve that material/amount on both the scaffolding and the chosen
   mining post.
7. Navigate to the reserved mining post, withdraw the reserved material into inventory,
   and clear the mining-post-side reservation.
8. Navigate back into scaffold work range and deposit the carried material into the
   scaffolding.
9. Builders only apply construction work after the full recipe has been deposited into
   that scaffold.
10. If a scaffold has no actionable next step for the current builder, that trilobite
    releases it and can retarget another in-progress scaffold instead of idling
    permanently on the old one.
11. Scaffolding completes only when both recipe deposits and rarity-weighted construction
    work are finished.
12. Loop back to step 1 while any scaffolding remains in progress.
- Failure handling:
1. Invalid or completed scaffolding releases builder assignments and scaffold-side
   reservations.
2. Invalid mining-post reservations are cleared and the builder restarts target
   selection.
3. If a scaffold is fully supplied/worked but still present because the final building
   swap failed, builders retry the scaffold completion path instead of treating it as
   permanently done.
4. Navigation failure clears queued builder steps and restarts from step 1.

### Role: `fighter`
- Trigger:
1. Select a trilobite.
2. Open creature menu.
3. Press `Fight`.
- Workflow (`Trilobite` step chain):
1. If `Game.Danger` is `false`, clear the fighter target and prefer returning to an
   assigned barracks.
2. If no barracks is assigned, pick a barracks by lowest assignment load, then approach
   distance, and store it in `AssignedBuilding`.
3. If already on a passable barracks tile, idle there until danger rises.
4. If `Game.Danger` is `true` and the stored target tile is adjacent, attack the enemy
   on that tile.
5. If a neighboring tile contains an enemy, set that tile as the fighter target and
   attack.
6. Otherwise, read the current `enemy` BFS field and move one tile to a neighboring
   passable tile with a lower value.
7. Fighters recompute their adjacent-enemy checks before and after each combat move so
   movement can still be interrupted for attacks.
8. If no reachable enemy exists while danger is active, navigate back to the least-loaded
   barracks using its cached BFS field and rejoin its assignment set.
- Failure handling:
1. Losing the target enemy clears the fighter target and triggers a fresh enemy search.
2. Failed combat movement clears queued fighter steps and restarts from step 1.
3. Role checks enforce that only barracks remain in `AssignedBuilding` while fighter
   behavior is active.

### Enemy behavior
- Trigger: `Enemy` creatures spawn with `Assignment = "enemy"`.
- Workflow (`Enemy` step chain):
1. If the stored hostile tile is adjacent, attack the trilobite or building on that tile.
2. Otherwise, check neighboring tiles for a trilobite first, then for a building; if
   found, store that tile as the target and attack.
3. Otherwise, read the current `colony` BFS field, which is seeded from trilobites plus
   adjacent tiles around non-passable colony-building tiles and the outside perimeter of
   algae farms, and move one tile to a neighboring passable tile with a lower value.
4. Enemy movement re-checks target validity and adjacent trilobites/buildings before and
   after moving so travel can still be interrupted for combat.
5. If no lower-value colony tile is reachable, do nothing.
- Failure handling:
1. Losing the target trilobite/building clears the stored target and triggers a fresh
   hostile search.
2. Failed combat movement clears queued enemy steps and restarts from step 1.

### Manual movement (role-agnostic)
- Trigger:
1. Select trilobite.
2. Press `Move`.
3. Hover tiles to preview path.
4. Click destination tile to queue path.
- Runtime:
1. Hover/click path generation uses a temporary destination-seeded distance field rather
   than direct source-to-goal BFS.
2. Each simulation tick executes one queued action.

## Building types, data types, and workflows

## Shared building data model (`Building`)
- `Name: string`
- `Size: { x: int, y: int }`
- `OpenMap: int[][]`
- `Game: GameApp` or equivalent gameplay root
- `TileArray: List<Tile>`
- `Description: string`
- `Sprite`: JavaScript `PIXI.Sprite` equivalent render-side reference; in MonoGame this
  may be represented by texture references plus draw state rather than a single retained
  scene object type
- `HasStation: bool`
- `Location: { x: int?, y: int? }`
- `Health: int` (starts 100)
- `MaxHealth: int` (starts 100)
- `BfsField: BfsField`
- `BfsField.Field: Dictionary<string, int>`
- `BfsField.UpdatedTiles: HashSet<string>`
- `BfsField.UpdatedBuildings: HashSet<Building>`
- `BfsField.UpdatedCreatures: HashSet<Creature>`
- `BfsField.TrackedBuildings: HashSet<Building>`
- `BfsField.TrackedCreatures: HashSet<Creature>`
- `DisplayBaseSize: { x: int, y: int }` (unrotated display footprint used for pivot /
  origin math)
- `DisplayRotationTurns: int` (quarter-turn clockwise rotation state for the building's
  visual display)
- `Recipe: Dictionary<string, int> | null` (construction cost for scaffolded buildables)
- `Selectable: bool` (`false` only for runtime buildings that should ignore normal
  building selection/menu flow)

### `OpenMap` semantics during placement
- `0`: tile occupied by building, not passable.
- `1`: tile occupied by building, passable for creatures.
- `>1`: tile skipped by `Cave.CanBuild` and occupancy write in `Build` (reserved
  behavior).

### Building placement workflow (all building types)
1. Top-right `Menu` button opens the shared main menu panel while it is closed.
2. Player opens the `Buildings` tab directly.
3. Clicking a building card creates the real target instance via `Factory.Build()`, then
   wraps it in a `Scaffolding` instance.
4. If a previous placement preview is already active, it is destroyed before the new
   building card starts placement.
5. Game enters `BuildMode` and shows the floating final-building sprite while keeping the
   scaffolding instance as the object that will actually be placed.
6. Mouse move updates the floating preview position.
7. `R` rotates the floating final-building preview and the underlying
   scaffolding/target-building footprint together.
8. Floating preview pivot / origin math uses the target building's unrotated display
   footprint so the cursor stays bound to the same top-left placement tile across
   quarter-turn rotations, including non-square buildings.
9. Click empty tile to attempt placement.
10. `Cave.CanBuild` validates all non-`>1` footprint tiles, rejects placement if any
    trilobite is currently occupying one of those footprint tiles, and, once the queen
    exists, requires every occupied footprint tile to be in the queen-connected
    reachable-tile set.
11. On player placement click, build validation also runs a simulated reachability pass:
    it duplicates the current `ReachableTiles` set, removes every `0`/`1` footprint tile
    of the candidate placement from that simulated set, then flood-fills outward from the
    queen footprint; placement is rejected if any previously reachable tile would become
    disconnected.
12. The same simulated placement check also requires every currently accessible existing
    building to retain at least one reachable interior tile or reachable adjacent
    approach tile, so new construction cannot fully box in an older building.
13. `Cave.Build` writes building occupancy/passability, stores `TileArray`, reapplies the
    building's stored quarter-turn display rotation, and makes the building renderable
    using origin-based placement for both single-texture buildings and multi-cell
    scaffold views.
14. Optional `OnBuilt(cave)` runs only for the building instance actually being placed.
    Scaffolding placement does not trigger the target building's `OnBuilt`.
15. When scaffolding reaches its recipe requirement, it removes itself and places the
    stored target building at the same top-left tile and preserved quarter-turn rotation;
    the final target's `OnBuilt(cave)` runs at that moment.
16. Successful placement exits build mode and clears the current selection/preview state
    without automatically closing the shared menu panel; `Escape` exits active UI state
    and also closes the panel.
17. Buildings can take damage through `TakeDamage`; at `0` health they are removed from
    the cave, their footprint becomes passable again, and any affected trilobites clear
    stale assignment/reservation state, release destroyed-building links such as
    `AssignedBuilding` or `BuilderSourcePost`, and restart their current role behavior.

### `Factory`
- Purpose: lightweight blueprint wrapper for unlocked buildings.
- Data copied from sample instance:
  - `Name`, `Sprite`, `OpenMap`, `Size`, `Description`, `HasStation`.
- Workflow: `Factory.Build()` returns a new runtime building instance.

### `Scaffolding`
- Type:
  - `Size`: mirrors the stored target building.
  - `OpenMap`: rebuilt from the target building so every `0` or `1` target cell becomes
    scaffold `0`, while target cells `>1` stay skipped.
  - `HasStation: false`
  - `Selectable: true`
- Runtime data:
  - `TargetBuilding: Building`
  - `RecipeRequired: Dictionary<string, int>`
  - `RecipeDeposited: Dictionary<string, int>`
  - `RecipeComplete: bool`
  - `MaterialReservations: Dictionary<Creature, ReservedMaterial>`
  - `Assignments: HashSet<Creature>`
  - `ConstructionProgress: int`
  - `ConstructionRequired: int` (recipe-weighted by material amount and ore rarity)
  - `ConstructionComplete: bool`
  - `CompletionPending: bool` (true when recipe/work are complete but the final building
    swap still needs to succeed)
  - `Sprite`: rendered as one `Scaffold` cell texture per occupied scaffold cell
- Workflow:
1. Constructed automatically from a real target building chosen in the build menu; it is
   not part of `Game.UnlockedBuildings`.
2. Rotating the floating scaffold also rotates the stored target building and regenerates
   the scaffold footprint / display from the rotated target `OpenMap`.
3. Scaffolding stores the target building's quarter-turn visual rotation separately from
   its own rebuilt tile footprint so the final building sprite keeps the same orientation
   when construction completes.
4. While placed, scaffolding blocks the full final occupied footprint, even where the
   eventual target would be passable.
5. Builders reserve outstanding recipe needs on scaffolding through
   `MaterialReservations` so multiple carriers cannot over-claim the same missing
   material.
6. `Deposit()` only accepts resources that still have remaining requirement, clamps the
   accepted amount to that remainder, and clears that builder's scaffold-side reservation.
7. `GetRecipeProgress()` returns cloned required/deposited/remaining counts plus reserved
   totals and construction-progress state.
8. `ApplyConstructionWork()` advances rarity-weighted build progress separately from
   material deposits.
9. `CompleteConstruction()` only succeeds once both recipe deposits and construction
   progress are complete; then it removes the scaffold and places the stored target at the
   same top-left location and preserved rotation through normal `Cave.Build` logic.
10. If scaffolding is destroyed before completion, the stored target is discarded and no
    final building is placed.
11. If final placement fails during completion, the scaffold rebuilds itself at the same
    location, preserves progress and rotation state, and remains retryable instead of
    becoming a dead completed scaffold.

### `Queen`
- Type:
  - `Size: 3x3`
  - `OpenMap: [[1,1,1],[1,0,1],[1,1,1]]`
  - `HasStation: true`
- Runtime data:
  - `AlgaeQuota: int` (starts 20)
  - `AlgaeCount: int`
  - `BroodlingCount: int`
- Workflow:
1. Farmer feeds algae through `FeedAlgae`.
2. Queen accumulates algae.
3. On each quota threshold:
   - consume quota amount
   - raise next quota by 5
   - attempt brood spawn on a random passable queen tile
4. Spawned brood type matches feeder trilobite class.

### `MiningPost`
- Type:
  - `Size: 3x3`
  - `OpenMap: [[1,1,1],[1,0,1],[1,1,1]]`
  - `HasStation: true`
- Runtime data:
  - `Recipe: { Sandstone: 20 }`
  - `Capacity: int` (1000)
  - `Radius: int` (10)
  - `Inventory: Dictionary<string, int>`
  - `Assignments: Dictionary<Creature, string | null>`
  - `MaterialReservations: Dictionary<Creature, ReservedMaterial>`
  - `MineableQueues: Dictionary<string, Queue<string>>` or equivalent FIFO behavior
  - per-type queue-head or queue state
  - queue state flags
- Workflow:
1. `OnBuilt` initializes mineable queues for in-radius wall/ore tiles.
2. Miners are assigned to post and optionally to reserved tile keys.
3. Post provides filtered, non-conflicting mining targets.
4. Miner deposits resources via `Deposit`.
5. Builders compare post inventory against `MaterialReservations`, reserve material on a
   chosen post without decrementing inventory immediately, and only reduce inventory when
   they call `WithdrawReservedMaterial`.
6. Tile changes invalidate queues; queues lazily rebuild on next use.

### `AlgaeFarm`
- Type:
  - `Size: 2x3`
  - `OpenMap: [[1,1],[1,1],[1,1]]` (fully passable)
  - `HasStation: false`
- Runtime data:
  - `Recipe: { Sandstone: 20 }`
  - `Period: int` (30)
  - `Growth: int`
  - `HarvestYield: int` (5)
  - `Assignments: HashSet<Creature>`
- Workflow:
1. Farmers assign to farm.
2. Farm exposes passable tile graph/path for traversal.
3. `Growth` increments on each harvest attempt.
4. Harvest succeeds probabilistically based on `Growth / Period`.
5. On success, algae is transferred to creature inventory and `Growth` resets.

### `Barracks`
- Type:
  - `Size: 3x3`
  - `OpenMap: [[1,1,1],[1,0,1],[1,1,1]]`
  - `HasStation: true`
- Runtime data:
  - `Recipe: { Sandstone: 20 }`
  - `Assignments: HashSet<Creature>`
- Workflow:
1. Placeable via build menu.
2. Fighters sort barracks by assignment count, then approach distance.
3. A fighter stores its selected barracks in `AssignedBuilding`.
4. When danger is low or no reachable enemies exist, fighters return to a passable
   barracks tile and idle there.

### `Storage`
- Type:
  - `Size: 2x2`
  - `OpenMap: [[0,0],[0,0]]`
  - `HasStation: false`
- Runtime data:
  - `Recipe: { Sandstone: 20 }`
  - `Capacity: int` (20)
- Workflow:
1. Implemented as a building type, but not currently included in the default
   `Game.UnlockedBuildings` build menu list.
2. No active transfer workflow is currently implemented.

### `Smith`
- Type:
  - `Size: 2x2`
  - `OpenMap: [[0,0],[0,1]]`
  - `HasStation: true`
- Runtime data:
  - `Recipe: { Sandstone: 20 }`
  - crafting recipe system not implemented yet
- Workflow:
1. Implemented as a building type, but not currently included in the default
   `Game.UnlockedBuildings` build menu list.
2. Crafting interactions are placeholders only.

### `Radar`
- Type:
  - `Size: 4x4`
  - `OpenMap: [[0,0,0,0],[0,0,0,0],[0,0,0,0],[0,0,0,0]]`
  - `HasStation: false`
- Runtime data:
  - `Recipe: { Sandstone: 20 }`
  - `RadiusMax: int` (starts 50)
  - `CurrentRadius: int` (starts 0)
  - `GrowthChance: float` (`0.1`, or 1 in 10 per tick)
- Workflow:
1. Placeable via build menu and occupies a fully impassable 4x4 footprint.
2. `OnBuilt` reveals the radar footprint tiles immediately.
3. On each simulation tick, if `CurrentRadius < RadiusMax`, the radar rolls a 1 in 10
   growth chance; failed rolls do no reveal work.
4. On a successful roll, `CurrentRadius` increases by 1 and only the newly unlocked ring
   of tiles is revealed.
5. Reveals are tile-by-tile and use Euclidean distance, not cave flood-fill.
6. Distance is checked against the 4 center tiles of the 4x4 footprint, not the top-left
   anchor tile.
7. Once `CurrentRadius` reaches `RadiusMax`, expansion stops.
8. Revealed tiles remain visible permanently; removing the radar later does not hide them
   again.

## Object-oriented structure (high-level)

### World graph layer
- `Graph` owns tile map and edge management.
- `Tile` stores base terrain, building occupancy, passability, current trilobite
  occupants, neighbors, and render linkage / draw state references as needed.
- `Cave : Graph` adds:
  - cave generation
  - tile/building runtime state plus separate `Trilobites` and `Enemies` creature sets
  - `RevealedTiles`, a live `HashSet<Tile>` tracking every revealed tile including
    revealed walls
  - `ReachableTiles`, a live `HashSet<Tile>` tracking the currently passable tiles
    connected to the queen building's passable footprint
  - wall mining explicitly reveals the newly emptied tile, and if that opening touches a
    passable tile that was previously unreachable, the newly connected cave section is
    reprocessed so its passable tiles join `ReachableTiles` and its newly accessible
    boundary tiles join `RevealedTiles`, even when radar had already made some of that
    section visible
  - temporary destination-seeded distance-field generation for manual movement and
    non-building targets
  - per-building lazy `BfsField` objects over reachable tiles only
  - game-held `BfsField` objects for `enemy` and `colony`, computed only over revealed
    tiles
  - dirty tile/building/creature tracking on every `BfsField`
  - incremental `BfsField` refreshes that rebalance around dirty tiles instead of
    recreating the whole map by default
  - shared combat-field dirtying on creature spawn/move/removal and on
    tile/building/reveal changes
  - reachable-tile recomputation when buildings are placed/removed or wall mining changes
    passable connectivity
  - creature deaths mark shared combat fields dirty, remove that creature from every
    building assignment/material-reservation collection, and are applied on the next field
    refresh/access
  - movement, spawn, and removal rules, including denying spawns onto unreachable tiles
  - danger-state syncing for enemy spawn/death, including clearing `Game.Danger` when the
    last enemy is removed
  - full-party healing for all remaining trilobites when the last enemy is removed
  - trilobite tile-occupancy syncing during spawn/move/removal
  - building placement and reveal logic

### Unit layer
- `Creature` is the base actor:
  - action queue
  - path queue / path preview
  - combat state (`Health`, `MaxHealth`, `Damage`) and basic damage/death handling
  - behavior restart helper used after assignment/target cleanup
  - navigation helpers that reconstruct routes from distance fields instead of direct
    BFS-path searches
  - sprite-placement / draw-state helper that snaps movers to the destination tile center
    and then applies a fresh random 1-15 px radial offset on each completed move
  - generic selection/build interactions
- `Trilobite : Creature`:
  - inventory model
  - role system (`unassigned`, `miner`, `farmer`, `builder`, `fighter`)
  - role-specific multi-step workflows
  - building navigation via the assigned building's lazy cached BFS field
  - shared `AssignedBuilding` state for mining/farming/scaffolding/barracks assignments
  - `BuilderSourcePost` for the currently reserved mining-post pickup
  - `PendingMineTileKey` for reserved mining targets
  - `FighterTargetTileKey` for the current enemy tile target
- `Enemy : Creature`:
  - autonomous combat workflow that attacks adjacent trilobites or buildings and
    otherwise follows the shared colony BFS field toward trilobites, non-passable
    colony-building targets, and the outside perimeter of algae farms
  - `EnemyTargetTileKey` for the current hostile tile target

### Building layer
- `Building` is the base type for all placeables.
- Subclasses: `Queen`, `MiningPost`, `AlgaeFarm`, `Barracks`, `Storage`, `Smith`,
  `Radar`, `Scaffolding`.
- `Factory` wraps buildable classes for menu/unlock usage.

### Rendering / game / UI layer
- `GameApp : Game` owns the MonoGame lifecycle:
  - `Initialize`
  - `LoadContent`
  - `Update`
  - `Draw`
- Rendering uses MonoGame primitives such as:
  - `ContentManager` for loaded assets
  - `Texture2D` for textures
  - `SpriteBatch` for drawing sprites and UI
  - viewport / back-buffer sizing for resize behavior
- The port must preserve the current game-level responsibilities now held by JavaScript
  `Game`:
  - selection
  - drag/zoom
  - build mode
  - floating paths / previews
  - paused BFS-debug overlays
  - the shared top-right menu panel
  - `Danger`
- The MonoGame UI layer must preserve the current shared menu behavior rather than
  replacing it with an unrelated UI framework or a different user flow.

### Supporting data types
- `Ore` remains an enum-like resource definition with the same names and order.
- `NodeQueue` in JavaScript is a linked-list queue used for deferred creature actions.
  In the C# port, preserve the same FIFO behavior; prefer `Queue<T>` backing storage
  instead of re-implementing the JavaScript linked-list utility unless a concrete need
  appears.
- `BfsField` owns tracked-target bookkeeping, dirty update queues, cached distance maps,
  and field/path accessors for building/combat BFS.

## Initial colony setup (`Program.cs` / `GameApp.cs` bootstrap flow)
- Startup placement workflow:
1. The game places the queen first.
2. Startup also places one finished mining post through normal `Cave.Build` logic instead
   of scaffolding.
3. The starter mining post must fit on reachable tiles, stay at least 5 Manhattan tiles
   away from any wall/void across its occupied footprint, and have its center within
   10 Manhattan tiles of the queen center.
4. If a random queen placement does not allow such a mining post, startup retries queen
   placement and falls back to an exhaustive search if needed.
5. After the starter buildings are placed, the four initial trilobites spawn on queen
   tiles.

## Implemented events and input behavior

### Game runtime events
- Preserve the JavaScript event contract and event timing exactly.
- In C#, these events may be implemented using standard .NET events or a domain event bus,
  but emission timing must remain identical.
- `tileMined`:
1. Emitted from the shared mining path in the game's mine-event helper.
2. Fires once whenever a mineable tile is successfully converted to `empty` through the
   shared mining path.
3. Covers both manual wall mining and trilobite mining of walls or ore tiles.
- `wallMined`:
1. Emitted from the shared mine-event helper.
2. Fires alongside `tileMined` when the mined tile's pre-mine base was `wall`.
3. Emission happens after the wall tile becomes `empty` and wall-perimeter
   updates/queue invalidation are applied.
- `AlgaeMined`:
1. Emitted from the shared mine-event helper.
2. Fires alongside `tileMined` when an `Algae` tile is mined into `empty`.
- `SandstoneMined`:
1. Emitted from the shared mine-event helper.
2. Fires alongside `tileMined` when a `Sandstone` tile is mined into `empty`.
- `MagnetiteMined`:
1. Emitted from the shared mine-event helper.
2. Fires alongside `tileMined` when a `Magnetite` tile is mined into `empty`.
- `MalachiteMined`:
1. Emitted from the shared mine-event helper.
2. Fires alongside `tileMined` when a `Malachite` tile is mined into `empty`.
- `PeroteneMined`:
1. Emitted from the shared mine-event helper.
2. Fires alongside `tileMined` when a `Perotene` tile is mined into `empty`.
- `IlmeniteMined`:
1. Emitted from the shared mine-event helper.
2. Fires alongside `tileMined` when an `Ilmenite` tile is mined into `empty`.
- `CochiniumMined`:
1. Emitted from the shared mine-event helper.
2. Fires alongside `tileMined` when a `Cochinium` tile is mined into `empty`.
- Stats linkage:
1. `Stats` subscribes to `tileMined`, `wallMined`, and every `<OreName>Mined` event.
2. Each event increments the stat with the same key name.

### Global MonoGame input and update behavior
- Mouse scroll wheel:
1. If the cursor is over the shared menu panel or the top-left settings UI, world zoom is
   suppressed.
2. If the cursor is inside a scrollable menu box, the wheel scrolls that specific box
   instead of zooming the world.
3. Otherwise, zoom in/out (`CurrentScale`) with clamped bounds.
4. World zoom repositions non-floating world drawables relative to screen center.
- Resize:
1. The game viewport / back-buffer resizes live with the game window while the game is
   running.
2. The viewport size should exclude non-client UI where applicable and preserve the
   current in-window world layout.
3. `Game.HandleViewportResize` equivalent preserves the current on-screen world layout
   while updating the viewport center, menu layout, and any active BFS debug overlay.
- Mouse press:
1. Capture drag start position.
2. Reset drag flag.
- Mouse move:
1. If left-drag starts over world space and exceeds threshold, pan the camera by the
   per-frame mouse delta instead of firing a click on release.
2. If right-drag starts over world space and exceeds threshold, show a screen-space
   selection rectangle.
3. If in `BuildMode`, move the floating target-building preview to the cursor.
- Mouse release:
1. Releasing a right-drag world selection box selects every trilobite whose on-screen
   center lies inside the box.
2. Releasing a left-drag camera pan ends the drag without triggering world click
   interaction.
3. End drag operation.
- Key handling:
1. `Enter`: run one simulation tick and refresh the active BFS debug overlay if one is
   being shown.
2. Tick order:
   - If `Game.Danger` is `true`, refresh the dirty `enemy` `BfsField`, process every
     trilobite in `Cave.Trilobites` once, refresh the dirty `colony` `BfsField`, process
     every enemy in `Cave.Enemies` once, then run buildings with `Tick()` hooks.
   - If `Game.Danger` is `false`, do not refresh either combat field automatically;
     process every trilobite in `Cave.Trilobites` once so fighters can return to barracks
     or idle, skip enemy processing, then run buildings with `Tick()` hooks.
3. `Space`: call `Game.CleanActive()` equivalent and then toggle auto-tick pause/run.
4. While paused, `1` / `2` / `3` display the queen building field / `enemy` field /
   `colony` field as centered yellow tile labels; finite distances are shown and blocked
   or unreachable revealed tiles are left blank; `4` does nothing.
5. While unpaused, `1` / `2` / `3` / `4` set tick speed to `500` / `250` / `100` /
   `50` ms.
6. `P`: spawn a debug enemy on a random reachable, passable, unoccupied tile if one
   exists, then log tick state (trilobites, enemies, and mining posts).
7. `Escape`: `Game.CleanActive(closeMenu: true)` equivalent (close the shared menu panel,
   clear previews, cancel active mode, clear selection, and clear BFS debug labels).
8. `R`: if building placement is active, rotate the floating display plus the underlying
   building/scaffolding `OpenMap` and update the pivot/orientation state used for
   tile-aligned placement.
9. Hold `W` / `A` / `S` / `D` to continuously pan the world at 800 screen px/s by
   applying the same base-position camera offset updates used by click-and-drag panning.
10. `F`: focus the single currently selected trilobite or selected building once; while
    held, keep the camera centered on that target while accounting for the open menu
    width.
11. `Tab`: toggle the shared colony menu open/closed without clearing the current
    selection.
12. `` ` `` toggles a MonoGame-only debug overlay panel that surfaces the existing debug
    controls already implemented in the JavaScript runtime:
   - pause / resume
   - single-step tick
   - tick speed selection (`500` / `250` / `100` / `50` ms)
   - BFS debug field selection (`queen`, `enemy`, `colony`, or clear)
   - toggle live trilobite role labels that stay attached to moving trilobites on screen
   - debug enemy spawn
   - last-tick and rolling-average performance timing for BFS, trilobites, enemies, and
     building ticks, plus allocation/GC counters, live entity counts, and a dominant
     work / slow-tick-cause diagnosis for the last recorded tick
13. While the debug overlay is open, world/menu click interactions are suppressed, but
    the underlying simulation keeps following the current paused/running state.
14. Right-click role assignment:
   - Right-clicking directly on a trilobite always makes that trilobite the active
     selection and opens a radial role menu centered on that trilobite.
   - If multiple trilobites are currently box-selected, right-clicking world space opens
     the same radial role menu around the cursor for the whole selected group.
   - Mixed-assignment group selections do not crash the runtime; they simply open the
     group radial menu with no assignment button pre-highlighted.
   - Left-clicking a role in that radial menu immediately updates every targeted
     trilobite's runtime `Assignment` and restarts its normal behavior queue.
   - The radial role buttons use rounded corners, fit their labels inside the button
     bounds, and clamp to the visible gameplay area instead of clipping off-screen.
- Key release:
1. Releasing `W` / `A` / `S` / `D` clears that held pan direction.
- Window deactivation / loss of focus:
1. Clears all held `W` / `A` / `S` / `D` pan directions so camera movement cannot stick
   after window focus is lost.

### Tile interaction behavior
- Wall tile left-click release over tile: mine wall via the shared wall-mining path.
- Empty/ore tile left-click release over tile: routed to empty-tile click handling.
- Empty/ore tile hover: routed to empty-tile hover handling for move-path preview.
- Empty/ore tile hover exit: routed to empty-tile hover-exit handling.
- Newly created wall tiles after mining also receive wall-click handling.
- Newly mined wall-to-empty tiles receive empty-tile click/hover handling.

### Creature interaction behavior
- Left-click release on a trilobite:
1. Ignored during drag/build mode.
2. Toggle select/deselect of same creature.
3. Replace existing selection with this creature and open the shared menu panel while
   preserving whichever main tab is already active.
4. Selecting a trilobite does not automatically recenter the camera.
- Left-click release on reachable empty/ore world space:
1. A single click clears the current active selection state instead of issuing movement.
2. If one or more trilobites were selected on that first click, a second left click on the
   same tile within the double-click window issues the movement command to that tile.
3. Wall clicks still use the normal manual mining path.
- Left-click release on non-interactable/off-map world space:
1. Clears the current active selection state, including box-selected trilobite groups,
   as long as building placement is not active.
- Right-drag release over world:
1. Creates a box selection for trilobites only.
2. Box selection does not automatically open the shared menu panel.
- Right-click release on a trilobite:
1. Selects that trilobite for radial-role assignment even if a multi-selection already exists.
2. Opens the radial role buttons around that trilobite without drawing the old traced circle.

### Building interaction behavior
- Hover over a building forwards hover behavior to the underlying footprint tile.
- Left-click release on a building:
1. Ignored while dragging.
2. Forwards click to underlying footprint tile interaction first.
3. If no carry mode is active and the placed building is selectable, selects the
   building.
4. Selecting a building does not automatically recenter the camera.
5. Building focus uses the same explicit `F`/hold-`F` behavior as trilobites.
- Hover exit forwards pointer-out behavior to underlying footprint tile.

### Menu / button events
- Top-left settings button:
1. A rounded settings button is always rendered at the top-left of the screen.
2. Left-click release toggles a rounded settings panel below it without changing the
   colony side-panel state.
3. The settings panel contains a `Volume` control from `0` to `100`.
4. Volume changes snap to fixed `5`-point increments whether the player uses `-`, `+`,
   or clicks the volume bar directly.
5. Left-click release outside the open settings panel closes it without triggering world
   interaction underneath.
- Top-right gear button:
1. Left-click release: open the shared main menu panel after it has been collapsed.
2. The button is only rendered while the panel is closed.
- In-panel collapse arrow:
1. A left-pointing arrow button is rendered in the menu header while the panel is open.
2. Left-click release on that arrow collapses the shared main menu panel.
- Main menu tabs:
1. Left tab is `Buildings`.
2. Middle tab is `Assignments`.
3. A third `Selected` tab is rendered only while a trilobite or building is currently
   selected.
4. Left-click release on any visible tab switches the panel content in place.
- `Buildings` tab:
1. Shows buildable factories from the selected creature when available, otherwise from
   `Game.UnlockedBuildings`.
2. The top half of the tab is a persistent preview card that shows the currently hovered
   or selected building's name, size, sprite, and description.
3. The lower half of the tab is a scrollable building-grid container with a broad
   wheel-hit area covering the whole box, not just the masked inner viewport.
4. The grid is 4 cards wide.
5. Each card is rendered as a square and shows only the building name and sprite preview;
   the card name shrinks to fit without overflowing.
6. Hovering a card updates the preview card immediately.
7. Clicking a card keeps that building selected in the preview, creates the target
   building, wraps it in scaffolding, and enters placement mode with the target building
   sprite attached to the cursor.
8. If a placement preview is already active, the old floating preview plus its pending
   scaffolding state are destroyed before the new one starts.
- `Assignments` tab:
1. Top row contains four assignment filter tabs: `Miner`, `Builder`, `Farmer`, and
   `Fighter`.
2. The upper box shows the count for the currently selected assignment filter.
3. The lower box shows the count for `unassigned` trilobites.
4. Each box is scrollable when the cursor is hovering inside that box.
5. Each box entry shows a trilobite image with its count beside it.
6. Clicking an entry in the upper box moves one trilobite from the selected assignment
   back to `unassigned`.
7. Clicking an entry in the lower box moves one trilobite from `unassigned` into the
   currently selected assignment.
8. Each transfer immediately updates the creature's actual runtime assignment and requeues
   its corresponding behavior.
- `Selected` tab:
1. Only appears while `Menu.SelectedObject` is not `null`.
2. Shows the selected trilobite or building name plus a delete button.
3. Pressing the delete button uses the normal creature/building `RemoveFromGame` flow and
   clears the current selection afterward.

### Hover-specific behavior
- During move mode, hovering a passable tile outside the open menu panel and outside the
  top-right gear button previews BFS path from the selected creature.
- While the `Buildings` tab is open, hovering a building card updates the top preview
  card to that hovered building.
- Leaving a building card reverts the preview to the last clicked building card, or to
  the first available buildable when nothing has been clicked yet.
- Leaving tile hover clears floating preview paths.
- Hover previews are suppressed while dragging/build mode is active.
- If a single selected trilobite or selected building drifts outside the visible gameplay
  area, a `F to focus` hint appears and hides again when `F` is pressed/held, the
  selection clears, or that target returns near the gameplay center.

### UI presentation rules
- Player-facing UI uses rounded panels/buttons and fitted text so labels stay inside
  their boxes instead of clipping.
- The only intentional sharp-edged exception is the debug menu overlay.

## Menu system: implemented menus and flow

### Shared menu structure
1. `Game` owns one persistent `Menu` instance on the UI layer.
2. The menu panel starts open by default on a new run and after reset.
3. While the panel is closed, the menu renders a compact top-right gear button; opening
   the panel replaces that button with a full-height right-side panel and a header
   collapse arrow.
4. The panel always has `Buildings` and `Assignments`, and conditionally adds `Selected`
   while a trilobite or building is selected.
5. If the active tab disappears because the current selection was cleared or deleted, the
   menu falls back to `Buildings`.
6. The panel content is redrawn in place when the active tab or current selection changes
   instead of creating a new menu object per selection.
7. The panel width stays effectively fixed at its standard narrow size, while button
   sizing, padding, section spacing, and internal panel layout scale from screen height.

### Selection-driven menu flow
1. Selecting a creature/building calls the selection setter.
2. Selection visuals are rebuilt first:
   - Creature: single selection indicator centered on the creature plus its queued path
     display.
   - Building: perimeter edge highlight visuals around non-shared boundaries.
3. The selected object is pushed into the shared `Menu`.
4. The shared panel opens while preserving the current main-tab choice.
5. Selecting either a trilobite or a building does not recenter the camera
   automatically.
6. Pressing or holding `F` is the explicit path that recenters/follows a single selected
   trilobite or selected building while accounting for the width of the open right-side
   panel.

### Selection impact on the menu
1. Selection adds the conditional `Selected` tab to the shared panel.
2. A selected creature still affects the `Buildings` tab because that tab prefers
   `Creature.GetBuildable()` when a creature is selected.
3. The `Assignments` tab reads from the colony-wide trilobite set on the cave rather than
   from the currently selected object.
4. Deleting the selected object through the `Selected` tab uses the normal runtime
   removal path and then clears the selection.

### `Buildings` tab flow
1. Lists buildable factories from the selected creature when one is selected; otherwise it
   falls back to `Game.UnlockedBuildings`.
2. The top preview card shows the currently hovered building, or the last clicked
   building when nothing is hovered.
3. Inside that preview card, the name/size/description are on the left side and the
   building image fills the full right half.
4. The bottom build list is rendered as a 4-column scrollable grid of square cards.
5. Each grid card shows only the building name and its sprite preview.
6. Mouse-wheel scrolling is captured while the cursor is anywhere over the building-list
   box, including its padding and scrollbar area.
7. Clicking a card starts placement immediately and reuses the shared panel instead of
   opening a second build-options menu.
8. Placement click on a valid tile commits build; invalid placement, including when any
   occupied `0` / `1` footprint tile currently contains a trilobite, keeps the current
   preview active.

### `Assignments` tab flow
1. Shows four assignment filter tabs across the top: `Miner`, `Builder`, `Farmer`, and
   `Fighter`.
2. The top box lists the count of trilobites currently in the chosen assignment.
3. The bottom box lists the count of unassigned trilobites.
4. Both boxes are independently scrollable, and the wheel is captured anywhere inside
   each framed box.
5. Clicking an upper-box entry moves one trilobite from that assignment back to
   `unassigned`.
6. Clicking a lower-box entry moves one trilobite from `unassigned` into the chosen
   assignment.
7. Transfers mutate the live trilobite objects immediately by changing `Assignment`,
   clearing queued actions, and invoking the corresponding behavior.

### Audio cue behavior
- `.wav` and short `.mp3` content are loaded through the MonoGame content pipeline and played through the
  shared runtime audio service.
- `Building Place`:
1. Plays immediately when the player successfully places a building footprint into the
   cave and the scaffolding is created.
2. Each play uses one of three slight randomized pitch variants.
- `Building Finished`:
1. Plays when scaffolding finishes construction and the target building replaces it in
   the cave.
2. Each play uses one of three slight randomized pitch variants.
- `Trilobite Birth`:
1. Plays when the queen successfully spawns a new trilobite into the cave during normal
   runtime birth behavior.
2. Each play uses one of three slight randomized pitch variants.
- `Trilobite Selected`:
1. Plays when a single trilobite becomes the active selection, including from direct
   left-click selection, and once when a box/radial selection creates a live trilobite
   selection group.
2. Each play uses one of three slight randomized pitch variants to soften repeated clicks.
- `UI Select`:
1. Plays whenever the player clicks a normal UI element such as menu tabs, build cards,
   assignment transfers, delete buttons, settings controls, debug buttons, or the game
   over restart button.
2. Each play uses one of three slight randomized pitch variants.
- `Volume Sound`:
1. Plays after the player lands on a new volume increment in the settings panel.
2. The played preview uses the newly applied volume value, so lowering to `0` results in
   silence by design.
3. Each play uses one of three slight randomized pitch variants.

### Crash reporting behavior
- Unhandled managed exceptions write a timestamped crash report into the runtime
  `CrashReports` folder under the game's output directory.
- Each report includes:
1. exception text and stack trace
2. process/environment information
3. a live game snapshot with camera/input/menu/session state, selected objects, and cave
   entity summaries

### `Selected` tab flow
1. Appears only while a trilobite or building is currently selected.
2. Shows basic details for the current selection.
3. Provides a delete button labelled for the selected object type.
4. Clicking delete calls the selected object's normal `RemoveFromGame` path.
5. Once deletion clears the selection, the `Selected` tab disappears and the menu falls
   back to `Buildings`.

### Menu / game state interaction
- `CleanActive()` is the central reset for active selection state:
1. Destroys floating path visuals and building edge highlights.
2. Clears any active BFS debug overlay labels.
3. Exits move/build modes.
4. Removes and destroys the floating building preview.
5. Clears the selected object and selected-path overlays.
6. Preserves the shared menu panel by default so the player can keep browsing tabs after
   the selection clears.
- `Escape` is the explicit path that both clears active state and closes the shared menu
  panel; the panel can also be collapsed with its header arrow button.
- Several workflows call `CleanActive()` to prevent mode overlap and stale UI.
