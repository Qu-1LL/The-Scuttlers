# AGENTS.md

## Purpose

This file is the local contract for test work under `src/TriloGame.Tests`.
Read it after the repository root [AGENTS.md](../../AGENTS.md) when adding or updating tests.

## Test Coverage Expectations

Minimum expectations for behavior changes:

- add or update unit tests for the affected rule/module
- add or update runtime tests when orchestration changes
- add replay/performance coverage when a deterministic or hot-path system changes

Minimum expectations for refactors:

- preserve behavior unless the change is explicitly requested
- lock behavior with tests before or during the refactor
- update docs when structure, ownership, or runtime flow changes

## Performance Contract Exception

`GamePlayApi`, tests, and tooling code may use LINQ when it materially improves clarity, because
they are not part of the real-time simulation hot path.
