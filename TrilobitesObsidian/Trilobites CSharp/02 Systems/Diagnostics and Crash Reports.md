---
tags:
  - trilobites/csharp
  - trilobites/csharp/system
type: system
area: diagnostics
aliases:
  - Crash Reporting
---
# Diagnostics and Crash Reports

Linked notes: [[Trilobites CSharp Home]] - [[Simulation and Ticks]] - [[Boot and Game Root]] - [[UI and Input]]

## Primary files

- `src/TriloGame.Game/Shared/Diagnostics/CrashReporter.cs`
- `src/TriloGame.Game/GameApp.cs`
- `src/TriloGame.Game/Core/Simulation/TickProfiler.cs`

## Crash reporting flow

### Setup

- `Program.cs` calls `CrashReporter.InstallProcessHandlers()` from [[Boot and Game Root]]
- `Program.cs` registers `GameApp.BuildCrashDiagnostics` as a snapshot provider

### On crash

- `CrashReporter.Report(...)` writes a timestamped text file into `CrashReports/` under the app base directory
- the report includes exception information plus the current snapshot text from `GameApp` in [[Boot and Game Root]]

## What `GameApp.BuildCrashDiagnostics()` captures

- camera position and zoom
- currently pressed keys
- menu/debug/settings/game-over state
- selected object and selected trilobites
- floating building state
- role radial state
- selection-box state
- session resources
- stats snapshot
- tick profiler output
- building, trilobite, and enemy summaries

## Tick diagnostics

`TickProfiler` from [[Simulation and Ticks]] gives both live debug output and crash-time evidence.

### Useful outputs

- last tick timing snapshot
- rolling average timing snapshot
- dominant-work description
- GC and allocation data
- entity counts

## Debug menu relationship

The debug menu in [[UI and Input]] is the live face of the diagnostics system:

- shows current and average tick timings
- shows allocation and GC counts
- reports the dominant tick work
- exposes tick-speed and BFS debug controls

## Test coverage

Relevant tests live in the verification flow documented by [[Testing Strategy]]:

- `src/TriloGame.Tests/Diagnostics/CrashReporterTests.cs`
- `src/TriloGame.Tests/Simulation/TickProfilerTests.cs`

## Related notes

- [[Simulation and Ticks]]
- [[Boot and Game Root]]
- [[Testing Strategy]]
- [[File Inventory - TriloGame.Tests]]
