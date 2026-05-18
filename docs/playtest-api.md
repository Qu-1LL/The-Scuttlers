## Play/Test API

The project now includes a small runtime automation API intended for scenario setup, programmatic
testing, and future tooling.

### Core files

- `src/TriloGame.Game/Runtime/Automation/IGamePlayHost.cs`
- `src/TriloGame.Game/Runtime/Automation/GamePlayApi.cs`
- `src/TriloGame.Game/Runtime/Automation/GamePlaySnapshot.cs`

### Live host

`GameApp` implements `IGamePlayHost` and exposes:

- `GameApp.PlayApi`

That API works against the live running game state.

### Current commands

- restart the game
- pause and resume
- set tick speed
- run simulation ticks
- inspect a snapshot of the current session
- assign a trilobite role by name
- move a trilobite by name
- spawn trilobites
- spawn enemies
- place buildings directly by type and tile

### Current scope

This is intentionally an in-process API first. It is meant to provide a stable seam for:

- tests
- scripted scenarios
- future external adapters

If a future network or tool-facing API is added, it should adapt to this layer instead of reaching
straight into `GameApp` or `Core` objects.
