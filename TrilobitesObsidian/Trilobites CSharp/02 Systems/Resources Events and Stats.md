---
tags:
  - trilobites/csharp
  - trilobites/csharp/system
type: system
area: economy
aliases:
  - Resources and Events
---
# Resources Events and Stats

Linked notes: [[Trilobites CSharp Home]] - [[Simulation and Ticks]] - [[Buildings]] - [[Entities and Roles]] - [[Audio]]

## Primary files

- `src/TriloGame.Game/Core/Economy/Inventory.cs`
- `src/TriloGame.Game/Core/Economy/OreType.cs`
- `src/TriloGame.Game/Core/Economy/ResourceReservation.cs`
- `src/TriloGame.Game/Core/Economy/StatsTracker.cs`
- `src/TriloGame.Game/Core/Events/GameEventBus.cs`
- `src/TriloGame.Game/Core/Events/GameEvents.cs`
- `src/TriloGame.Game/Core/Simulation/GameSession.cs`

## Resource names

### Capitalized ore names from `OreType`

- `Algae`
- `Sandstone`
- `Magnetite`
- `Malachite`
- `Perotene`
- `Ilmenite`
- `Cochinium`

### Lowercase session inventory keys from `GameSession.Resources`

- `algae`
- `sandstone`
- `malachite`
- `magnetite`
- `perotene`
- `ilmenite`
- `cochinium`

## Inventory model

`Inventory.cs` is intentionally simple:

- one inventory can hold only one resource type at a time
- `Type` is null when empty
- `Amount` tracks the current amount
- `Add(...)` respects capacity and type locking
- `Remove(...)` clears the type when amount reaches zero

This is used for trilobite carrying behavior.

That carrying workflow belongs to [[Entities and Roles]].

## Resource reservations

`ResourceReservation` is a small record:

- `ResourceType`
- `Amount`

Mining posts and scaffolding use these reservations to prevent workers from over-claiming the same materials.

Those consumers live in [[Buildings]].

## Event bus

`GameEventBus` is the domain-level event hub.

It is owned by `GameSession`, so it sits at the boundary between [[Simulation and Ticks]] and the rest of the gameplay systems.

### Responsibilities

- subscribe to named events
- unsubscribe listeners
- emit payloads to all listeners

### Payload shape

`GameEventPayload` carries:

- `Cave`
- `TileKey`
- `Location`
- `MinedType`
- `ResourceType`
- `Source`

## Event names

- `tileMined`
- `wallMined`
- `AlgaeMined`
- `SandstoneMined`
- `MagnetiteMined`
- `MalachiteMined`
- `PeroteneMined`
- `IlmeniteMined`
- `CochiniumMined`

## Stats tracking

`StatsTracker` subscribes to the event bus and increments counters for the mining-related events above.

Those counters are consumed by [[Simulation and Ticks]] for session state and by [[Diagnostics and Crash Reports]] for crash/debug snapshots.

### Useful methods

- `Get(eventName)`
- `GetAll()`
- `Increment(eventName, amount = 1)`
- `Dispose()`

## Audio connection

`GameSession` also owns the `AudioCueRequested` event. This lets gameplay systems request sounds without taking a direct dependency on MonoGame sound objects, which is the bridge into [[Audio]].

## Related notes

- [[Buildings]]
- [[Entities and Roles]]
- [[Audio]]
- [[Simulation and Ticks]]
