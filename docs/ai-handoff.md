# The Scuttlers — AI Project Hand-off

> Snapshot date: 2026-07-28. This document describes the current C# / MonoGame repository, not
> the older browser prototype or the full long-term design plan. Source code and tests are the
> final authority when this document conflicts with them.

## 1. Project identity

`The-Scuttlers` is the current C# implementation of `Trilobites`, a 2D colony-sim / tower-defense
/ roguelike. The player grows a colony of trilobites inside a procedurally generated cave, mines
resources, farms algae, constructs a base, researches upgrades, and defends the queen from ants
and ant holes.

The intended run is approximately one to two hours and culminates in increasingly difficult
defense rounds; an endless mode is planned. The current repository is an active playable slice,
not a finished commercial game. The older browser version is linked from `README.md`, but this
repository targets desktop MonoGame and is not currently the browser build.

## 2. Technology and build surface

- Language/runtime: C# on .NET 9 (`net9.0`).
- Host: MonoGame DesktopGL, desktop `Game` lifecycle, `SpriteBatch`, `GraphicsDeviceManager`.
- UI: Gum.MonoGame and Gum.Shapes.MonoGame. Screen-space UI, including text, is Gum-first.
- Content: MonoGame Content Builder (`Content/Content.mgcb`) produces textures, fonts, audio, and
  the radiance-cascade effect. Do not bypass MGCB or hand-author hidden runtime assets.
- Tests: xUnit in `src/TriloGame.Tests`, referencing the game project directly.
- Solution: `TriloGame.sln` / `TriloGame.slnx`.

Useful commands from the repository root:

```powershell
dotnet build TriloGame.sln
dotnet test src/TriloGame.Tests/TriloGame.Tests.csproj
dotnet run --project src/TriloGame.Game/TriloGame.Game.csproj
```

The checked-in `launch` / `start` wrappers restore, build, and run, but `dotnet launch` is not a
reliable way to discover a repo-local custom verb. On macOS, the DesktopGL content pipeline may
require a 64-bit Wine prefix configured through `MGFXC_WINE_PATH`; see `docs/mac-setup.md`.

## 3. Repository map

The game is one assembly with explicit internal layers rather than separate projects:

| Area | Responsibility |
| --- | --- |
| `src/TriloGame.Game/Core` | Deterministic simulation, entities, buildings, cave/world state, economy, pathfinding, movement, combat, traits, vehicles, progression, and research. |
| `src/TriloGame.Game/Runtime` | Bootstrap, fixed-step clock, rounds, ant spawning, game-over, research offers, resource aggregation, unlock commands, profiling, and automation. |
| `src/TriloGame.Game/UI` | Menus, settings, debug surfaces, selection, building placement, research screens, HUD, Gum helpers, and input-facing state. |
| `src/TriloGame.Game/Rendering` | Camera, world-scene draw helpers, sprites, particles, screen shake, world effects, and presentation-only lighting. |
| `src/TriloGame.Game/Audio` | Audio cue catalog, playback services, music, focus cues, and session-to-audio bridging. |
| `src/TriloGame.Game/Shared` | Fixed-point math, deterministic PRNG, runtime state, diagnostics, profiler models, and presentation request records. |
| `src/TriloGame.Tests` | Unit, integration-style, UI, runtime, AI, world, combat, movement, rendering, audio, progression, and performance tests. |
| `Content` | MGCB source assets and shader/effect inputs. |
| `TrilobitesObsidian` | Expanded design and architecture notes; useful context, but potentially less current than code/tests. |

`GameApp.cs` and its partial files remain the MonoGame composition root. It owns lifecycle, input
routing, top-level update/draw flow, module composition, and some legacy orchestration. It is still
large and under active decomposition. New reusable gameplay or orchestration should generally go
to Core or Runtime instead of adding another large `GameApp` method.

## 4. Layer boundaries and ownership rules

The intended dependency direction is:

```text
Core  <- Runtime <- GameApp
  ^       ^          |
  |       |          +-- UI / Rendering / Audio bridges
  +-------+---------------- Shared host-agnostic models/utilities
```

More precisely:

- Core must remain independent of MonoGame rendering and input APIs, file I/O, and live audio
  playback. It may publish typed events/requests that the outer layers observe.
- Runtime coordinates Core modules and owns non-simulation timing/state.
- UI and Rendering may read Core/Runtime state but must not become dependencies of simulation.
- Audio observes session/runtime events; simulation must never require playback to make progress.
- Shared types must remain host-agnostic when Core consumes them.

Read the root `AGENTS.md` first, then the nearest subsystem guide under `src/TriloGame.Game`,
before changing code. Those files are an operational contract, not merely background notes.

## 5. Session bootstrap and starter state

`Runtime/Bootstrap/GameSessionBootstrapper.cs` creates a fresh `GameSession`, initializes the root
research node (`Hive Core`), registers starter building factories, creates a `Cave`, places the
queen and starter mining post, and spawns four named trilobites:

- `Jeffery` — miner
- `Quinton` — builder
- `Yeetmuncher` — farmer
- `Sigma` — fighter

The queen/post placement first tries varied placements and then falls back to a stable scan. The
new game reveals the initial cave after bootstrap. The default runtime starts unpaused at
`GameConstants.TickSpeedFast` (100 ms per simulation tick). Available debug speeds are 500, 250,
100, and 50 ms.

The current starter building catalog includes soil patch, garage, silo, algae farm, barracks,
turret, wall, mining post, and radar. Other building classes already exist, including queen,
storage, scaffolding, ranch, smith, and factory abstractions.

## 6. Deterministic simulation model

`Core/Simulation/TickRunner.cs` is the authoritative integer-tick entry point. Draw cadence must
not change simulation decisions. `Runtime/Systems/GameSimulationClockSystem.cs` owns pause state,
tick speed, wall-time accumulation, interpolation alpha, projectile travel, and tick profiling.
Wall-clock timing is therefore an outer-runtime concern, not a Core gameplay rule.

The current tick phases are, in order:

1. increment tick and notify observers;
2. trait updates;
3. surface-feature updates;
4. danger-state refresh;
5. enemy BFS when danger is active;
6. trilobite planning/movement decisions;
7. colony BFS and enemy planning/movement when danger is active;
8. unified creature movement/collision resolution;
9. combat resolution against final post-movement poses;
10. independent mining-strike resolution;
11. building ticks, ranch work, and vehicle work.

Creature movement and reservation/commit logic are intentionally ordered and deterministic. Do
not introduce wall-clock checks, unseeded gameplay randomness, asynchronous updates, reflection, or
parallel mutation into Core.

### Coordinates and bodies

- The cave topology is cell-based; `Tile` stores terrain/building topology, not moving creatures.
- `WorldPoint` / `WorldVector` are authoritative integer fixed-point positions with 16 subunits per
  world pixel. A tile is 512 pixels wide.
- `Creature.Position` is authoritative. `Location` and `CurrentCell` are read-only coarse-cell
  projections.
- Creatures have circular collision bodies and collide with environmental blockers only; creature
  bodies may overlap one another.
- Routes, desired velocity, wall avoidance, impulses, formations, and movement cohorts live in
  `Core/Movement`. Group moves use deterministic hexagonal formation slots.
- Point-route construction is capped at 32 routes per tick. Deferred navigation remains in
  `Planning` and retries in stable creature-ID order.
- Buildings watching moving units use tile projection registrations and cell-transition callbacks,
  not full-radius scans every tick. Creatures expose a `TrackedBy` lifecycle seam for assignment or
  target-owning buildings.

### Role behavior

Roles are typed (`Unassigned`, `Miner`, `Builder`, `Farmer`, `Fighter`, `Enemy`) and activities are
typed as well. Important state machines include:

- miners: select post, acquire a claim, reach/mine ore, deposit, wait for work/storage;
- farmers: a valid ranch assignment is highest priority; otherwise select farm, reach a soil slot, harvest, move to queen, feed, then fall back to stored algae only when no ranch or algae-farm work is available;
- builders: select scaffold, select storage source, withdraw, haul, deliver, construct;
- fighters: select/hold station, acquire and pursue targets, attack, regroup, retreat, recover;
- enemies: acquire target, move to colony, attack/breach, recover.

Idle behavior is shared: mostly stationary pauses followed by short deterministic anchor-biased
local moves. Role tasks resume after explicit movement commands complete.

### Economy and construction

Resources are represented by typed `ResourceName` values, including algae, sandstone, magnetite,
malachite, perotene, ilmenite, cochinium, lumenite, chitinstone, and mycocore. `Core/Economy`
contains the shared item catalog, storage interfaces, inventories, resource requirements, and
category-aware construction requirements. `Runtime/ResourceStockpileSystem` aggregates storage for
HUD/tooling and withdraws in deterministic building order.

Trilobites normally carry up to five items and can carry multiple resource types. Compatibility
callers of the older single-type inventory API observe the first carried resource. Scaffolding
uses typed requirements, reservations, and builder staffing based on remaining recipe volume and
carry capacity. `Build First` scaffolds have priority; stable creation order resolves ties.

Ranch soil is a special case: a 2x2 `SoilPatch` owns four independently growing `SoilTile` objects.
`SoilArea` groups patches for placement and selection: clicking a soil patch selects its complete area,
and clicking that area’s patch again selects the individual patch. An assigned farmer waits below the
garage, then stations on a visible plow. The ranch rebuilds a deterministic serpentine sweep over legal,
all-soil 2x2 plow footprints whenever membership changes; each completed row advances by two tiles before
the plow returns to its garage-side start. It follows straight route segments with continuous fixed-point
movement at 1.5x the normal vehicle speed and only stops for a turn. Each 90-degree plow turn lasts
0.5 seconds of game time. Each movement or completed turn works every tile in the 2x2 footprint.

## 7. World generation and mining

`Core/World/CaveGenerator.cs` and the map-generation helpers create the cave; `Cave` owns the live
world and topology. The world includes walls, open/revealed tiles, ore deposits, cave crystals,
biomes, decorations, ant holes, reachability, BFS navigation fields, and surface features.

Mining has two separate paths:

- player/manual/autonomous mining orders are planned/executed through `MineOrderPlanner`,
  `MineOrderExecutor`, and mining-post claims;
- `MiningStrikeSystem` resolves timed mining strikes independently from combat hitboxes.

Depleting ore updates tile resource state, building-navigation fields, BFS fields, reachability,
and mineable-target notifications. Walls reveal adjacent cave and can change topology and
reachability. Cave crystals block placement/occupancy, require mining hits, and are resourceless.

When in doubt about numeric rules, inspect `Core/Constants/GameConstants.cs` and the relevant tests.
The current source constants state three ore hits per yield and ten wall hits; some older `AGENTS.md`
text still describes five ore hits and three wall hits. Treat that as documentation drift to verify,
not as an instruction to silently change gameplay.

## 8. Combat, hazards, rounds, and game over

`Core/Combat/CombatWorld` owns fixed-tick attack commands, centered creature hitboxes/hurtboxes,
circle/AABB/capsule narrow-phase tests, a uniform-grid broad phase, faction filtering, stable hit
events, and combat diagnostics. Combat resolves after final creature movement. Structure attacks
retain a blocked-tile reach envelope; creature combat uses centered body shapes.

`CombatAgentController` creates automatic 8x8 threat-sector directives and assigns live ants to
fighters in stable ID order using least-load, then distance, then ant-ID tie-breaks. Fighters move
to deterministic stand-off points based on the assigned ant's live world pose. Ants acquire nearby
trilobites through the combat grid rather than doing an army-wide scan during hit resolution.

`RoundManager` and `AntHandler` are Runtime-owned. A round has a three-minute in-game grace/wait
phase followed by a 30-second spawn window. The base ant count is five plus three per round; early
rounds spawn singly, while later rounds can batch spawn events through ant-hole abstractions.
Ambient ant-hole spawning is also active. `GameOverStateSystem` owns queen-loss state and the game
shows a game-over overlay rather than closing the process.

Projectile travel occurs between ticks in Runtime state (`ProjectileFlightSystem` and
`Shared/State/ProjectileFlight`); projectile firing decisions remain in Core.

## 9. Progression and research

The session owns a global `TriloDex`, feature-tree catalog, live `SkillTree`, and `GlobalResearch`.
The bootstrap root is always-unlocked `Hive Core`. After a completed round, when the queen survives,
`ResearchDraftSystem` generates three candidate branches and retains the pending offer until the
player places a branch. Infinite-draft mode can generate follow-up offers. Unlock cost/path/resource
validation is Runtime-owned (`SkillTreeUnlockSystem`), while tree data and deterministic generation
are Core-owned.

## 10. Rendering, UI, and audio

The frame is a presentation pipeline, not a simulation driver. `WorldSceneRenderer` handles the
world pass: parallax background, floor, walls/ore/crystals, buildings, soil crops, creatures,
vehicles, particles, and world-space debug overlays. `CameraController` handles world/screen
conversion and zoom/pan.

`Rendering/Lighting` contains a presentation-only radiance-cascade pipeline. It derives blocker,
reveal, and intact-ore emission data from a camera-cull tile grid, keeps creature alpha silhouettes
as dynamic occluders, and applies lighting before world debug/selection overlays. It must not alter
ticks, replay state, or gameplay rules. Gum UI is rendered separately and is never lit by the world
shader.

All new screen-space UI—including text—must use Gum helpers/controls under `UI/Gum`. Use fixed
integer Gum `FontSize` values for normal text. Raw `SpriteBatch.DrawString` is reserved for
intentional world-space/debug text. Player-facing UI uses rounded colony-style panels/buttons;
the backtick debug menu is intentionally sharper and utilitarian.

Major UI areas are `Menu`, `Selection`, `Settings`, `Research`, `Debug`, `Hud`, `MainMenu`,
`Overlays`, and reusable `Gum` primitives. UI controllers should return typed interaction results
or requests; they should not call back into `GameApp` for simulation mutations.

Audio flows through `GameSession` cue events, `SessionAudioBridge`, `AudioService`, and
`MusicService`. Simulation requests cues indirectly and remains valid if playback is unavailable.
Managed crashes are reported by `Shared/Diagnostics/CrashReporter` with a live host snapshot.

## 11. Automation and testing

`GameApp.PlayApi` exposes the in-process `Runtime/Automation/GamePlayApi` through `IGamePlayHost`.
It supports:

- restart, pause/resume, tick-speed changes, and exact tick advancement;
- snapshots of session state, creatures, buildings, combat directives, hitboxes, hurtboxes, and
  recent hit events;
- assigning a named trilobite role and moving one to a continuous world position;
- spawning trilobites, enemies, and ant holes;
- placing supported building types by alias, location, and rotation.

Use this seam for scenario setup, runtime inspection, and automation tests instead of adding hidden
test-only helpers directly to Core. `GamePlayApi` is intentionally in-process; future external
adapters should sit above it.

Tests are organized by subsystem: `AI`, `Buildings`, `Core/Combat`, `Entities`, `Movement`,
`Pathfinding`, `Performance`, `Progression`, `Rendering`, `Runtime`, `Simulation`, `Traits`, `UI`,
`Vehicles`, and `World`. Deterministic movement/replay, combat, mining, role assignment, placement,
research, UI layout, lighting, audio, and crash diagnostics already have dedicated coverage.

For strict performance gates, set `TRILO_ENFORCE_PERF_BUDGETS=1`; ordinary test runs report the
benchmark without applying hardware-dependent timing assertions. Hot-path Core code must avoid
LINQ, reflection, async/await, per-tick strings, hidden closures, and uncontrolled list growth.

## 12. Controls and player-facing behavior

`CONTROLS.md` is the authoritative quick reference. In brief: WASD or middle drag pans, wheel
zooms/scrolls, left click selects, left drag box-selects, right click issues exact-point moves,
`F` focuses, `Tab` cycles, `Escape` cancels/closes, `R` rotates building placement, and the role
radial/mining-order controls issue colony commands. Backtick opens debug, F3 shows metrics, Space
pauses in debug, Enter advances one tick while paused, and P spawns a debug enemy.

## 13. Current risks and hand-off instructions

1. Preserve the existing worktree. At the snapshot date there are many uncommitted edits across
   architecture docs, Core entities/buildings, `GameApp`, rendering/lighting, Gum UI, content, and
   tests, plus new lighting assets/tests. Do not use `git reset --hard`, `git checkout --`, or broad
   cleanup commands. Treat those edits as user-owned unless explicitly told otherwise.
2. `GameApp` is still a high-pressure file. Prefer extracting a focused Runtime/UI/Rendering system
   when adding behavior.
3. Keep simulation timing deterministic. Runtime may use elapsed wall time for accumulation,
   profiling, and presentation effects; Core may not use wall-clock state for decisions.
4. Preserve authoritative-vs-projection boundaries: mutate `Creature.Position`, not `Location`; do
   not restore creature collections to `Tile`; use projections and tracking seams for observers.
5. Add or update tests for every behavior change. For deterministic movement/combat/hot-path changes,
   include replay/performance evidence where practical.
6. Read the nearest `AGENTS.md` plus `README.md`, `docs/architecture.md`, and
   `docs/runtime-systems.md` before substantial work. Consult `docs/playtest-api.md` for automation
   and `docs/agents.md` for player-facing UI style.
7. If source, tests, and prose disagree, first determine which behavior the task intends, then update
   the affected tests and documentation together. Do not silently normalize historical drift.

### Recommended first files for orientation

- `src/TriloGame.Game/Core/Simulation/TickRunner.cs`
- `src/TriloGame.Game/Core/Simulation/GameSession.cs`
- `src/TriloGame.Game/Core/World/Cave.cs`
- `src/TriloGame.Game/Core/Entities/Creature.cs`
- `src/TriloGame.Game/Runtime/Bootstrap/GameSessionBootstrapper.cs`
- `src/TriloGame.Game/Runtime/Systems/GameSimulationClockSystem.cs`
- `src/TriloGame.Game/Runtime/Systems/RoundManager.cs`
- `src/TriloGame.Game/Runtime/Automation/GamePlayApi.cs`
- `src/TriloGame.Game/GameApp.cs`

