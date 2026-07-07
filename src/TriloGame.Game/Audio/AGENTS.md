# AGENTS.md

## Purpose

This file is the local contract for audio work under `src/TriloGame.Game/Audio`.
Read it after the repository root [AGENTS.md](../../../AGENTS.md) and the game-project
[AGENTS.md](../AGENTS.md) when touching cue registration, playback mechanics, session audio
bridging, or audio state machines driven by runtime state.

## Audio Placement

Put code in `Audio` when it is:

- cue registration
- playback mechanics
- audio state machines driven by runtime state

## Audio Runtime Contract

- `SessionAudioBridge` owns session audio cue subscription so `GameApp` does not need to manually
  subscribe and relay cue events.
- Audio may observe `Core` state through session/runtime boundaries, but simulation logic must not
  depend on live playback.
- Short UI sound cues are routed through the shared `AudioService`, while gameplay systems request
  sounds indirectly through `GameSession.AudioCueRequested`.

Keep live playback policy out of simulation rules.

## Documentation Comments

- add short one-line `//` comments before non-trivial audio methods when playback ownership or cue
  timing is not obvious from the signature
- keep comments focused on state-machine intent, routing, and lifecycle behavior
- skip trivial accessors and obvious forwarding methods unless audio policy would otherwise be hidden
- add brief notes before dense branching that coordinates loop start/stop behavior
