# Autonomous Routine — Missile Module (Guided Fury)

You are the daily routine for **Guided Fury**, the missile module. Read
`.claude/autoroutines/_common.md` first, then this file.

## Identity

- **Module folder:** `Assets/Guided Fury/`
- **Branch:** `auto/missile-dev`
- **ROADMAP:** `Assets/Guided Fury/ROADMAP.md`
- **Verification:** `pwsh tools/ci/run-checks.ps1 -Module GuidedFury`

## Context

This is the most mature module in Deep Water. L0–L4 fidelity ladder, ESSM authored,
test suite live. The pattern (Pat1: SO-Profile + Pure-Core + MB-Adapter, plus
orthogonal sub-systems Pat3 candidate) is load-bearing. New work continues that
pattern — do not invent new shapes.

The ROADMAP has three parallel streams:

- **Stream A** — L5 + new missile profiles
- **Stream B** — Damage / warhead / fragmentation
- **Stream C** — Test coverage + scenario library

## Per-iteration rule: alternate streams round-robin

When you pick the next task, alternate streams: A → B → C → A → ... If the topmost
item in the next stream blocks (dependency on the previous stream), advance within
that stream until you find one that doesn't. Document the choice in your commit body.

The first iteration starts on Stream A unless an unfinished stream B/C item has been
explicitly blockered.

## Module-specific guardrails

- **No breaking changes to `MissileEntity`/`MissileBehaviour`** public surface without
  bumping a major. The contract is consumed by the test runner, salvo runner, LOD
  comparison, and Carrier Ops (recovery hooks). Plan changes as additive.
- **Profile struct discipline.** Adding a field to `MissileProfileData` means touching
  `MissileProfileSO.cs`, `MissileProfileData.cs`, and `MissileProfileSO.Bake()`.
  No reflection (AP1).
- **Determinism.** Stream C tests rely on deterministic ballistic outputs. Never
  introduce `Random` / `Time.time` / non-fixed-step state into Core/.
- **Stream B (damage) must preserve detonation events.** The `MissileBehaviour.OnTerminalState`
  pipeline (spawn explosion prefab, play SFX) is consumed by gameplay. Add behind /
  alongside, not replace.

## What to write to METHODOLOGY.md

Whenever you finish a non-trivial chunk, append a one-line changelog entry under
`## Changelog` at the bottom. If you discover a generalizable principle (e.g. "every
new missile gets an EditorCommand builder, not hand-authored YAML"), add a Candidate
under `## Candidates`.

## First iteration

If `Assets/Guided Fury/FAILURES.md` does not exist, create it with a header line.
Then pick the first unchecked Stream A item (L5 design doc) and begin.
