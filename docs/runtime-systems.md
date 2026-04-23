## Runtime Systems

This project now treats runtime orchestration as a first-class layer instead of leaving all state
management inside `GameApp`.

### Current runtime modules

- `Runtime/Bootstrap/GameSessionBootstrapper.cs`
  - creates a fresh game session
  - unlocks starter buildings
  - places the queen and starter mining post
  - spawns the starter trilobites and their roles
- `Runtime/Systems/GameSimulationClockSystem.cs`
  - owns paused state
  - owns tick speed and tick accumulator
  - advances the deterministic simulation through `TickRunner`
  - records runtime tick profiling around the simulation phases
- `Shared/Diagnostics/TickProfiler.cs`
  - stores tick timing snapshots and rolling averages
  - is shared so runtime systems, crash diagnostics, and debug UI can read the same data model
- `Runtime/Systems/GameOverStateSystem.cs`
  - owns queen-loss detection and continue/restart state
- `Runtime/Systems/RoundManager.cs`
  - owns round number, round elapsed game time, round start/end, and round-zero grace timing
  - raises the round-end draft hook used by the research draft flow
- `Runtime/Systems/AntHandler.cs`
  - receives round spawn requests
  - schedules ant spawns across the round spawn window
  - routes actual spawning through the ant-hole abstraction instead of `GameApp`
- `Runtime/Systems/ResearchDraftSystem.cs`
  - generates three candidate research branches after a completed round when the queen survives
  - preserves pending drafts until the player places one branch onto the live skill tree
  - owns draft-offer state so the UI can reopen the research menu without rerolling branches
- `Audio/OpalAudioSystem.cs`
  - owns the opal red-phase audio state machine
- `Audio/SessionAudioBridge.cs`
  - attaches audio playback to session cue events
  - keeps the host from manually relaying simulation audio cues
- `Runtime/Automation/GamePlayApi.cs`
  - exposes a programmatic play/test command surface
- `Shared/State/GameSessionRuntimeState.cs`
  - groups runtime/debug state such as tick profiling, debug enemy naming, and simulation toggles

### Why this layer exists

The main goal is to make the game more scalable and more maintainable:

- startup rules should not live in the render host
- fixed-step orchestration should be reusable in tests
- game-over state should not be a loose collection of booleans
- round pacing and enemy-wave orchestration should not live in the render host
- research-branch drafting and between-round progression offers should not live in the render host
- automation and test hooks should be explicit, not hidden in UI code

### Current dependency direction

- `GameApp` composes runtime modules
- runtime modules depend on `Core`
- `Core` stays free of MonoGame host concerns
- runtime profiling and stopwatch-based tick diagnostics stay outside `Core`

### Preferred future direction

As the refactor continues, more orchestration should move out of `GameApp` and into runtime
systems, especially:

- selection/command orchestration
- debug command handling
- scenario bootstrapping
- automation bridges
