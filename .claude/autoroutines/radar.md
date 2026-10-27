# Autonomous Routine — Radar Module (RH Radar)

> **RETIRED 2026-10-27 (SIX).** Never ran: no scheduler, no `auto/*` branch, no output since they were imported
> (2026-09, 8181c10a). As written they would also `git checkout` in the shared checkout, push to branches outside the
> 2026-10-24 branch table, and point the radar routine at the struck `Assets/RH Radar` plan. Doctrine guard: "a
> routine without a scheduler is a wish". Kept for the record; do not schedule them as written. A new routine starts
> from the doctrine's routine-design procedure (worktree, hard gate, revert path, escalation, a real schedule).
> Kill log: `.claude/autoroutines/KILLED.md`.

You are the daily routine for **RH Radar**. Read `.claude/autoroutines/_common.md`
first, then this file.

## Identity

- **Module folder:** `Assets/RH Radar/`
- **Branch:** `auto/radar-dev`
- **ROADMAP:** `Assets/RH Radar/ROADMAP.md`
- **Verification:** `pwsh tools/ci/run-checks.ps1 -Module RHRadar`

## Context

Greenfield rebuild. Pre-McMahon predecessors live at
`Assets/Scripts/Radar Scripts/`. Phase 0's first task is to archive them under
`Assets/_Archive/Radar Scripts_legacy/` per Pat C5.

The fidelity ladder mirrors Guided Fury's LOD pattern:

| Tier | Name | Purpose |
|---|---|---|
| L0 | Cone scan | Boolean visibility, no noise. Baseline. |
| L1 | Range-gated detection | Radar equation + SNR threshold |
| L2 | Tracking | α-β / Kalman filter on detections |
| L3 | Doppler / MTI | Closing-velocity gate, clutter rejection |
| L4 | TWS + ECCM | Multi-target track-while-scan, jam detection |

## Reference architecture (mandatory)

Mirror Guided Fury's Core/ layout. Concretely, by end of Phase 0:

```
Assets/RH Radar/Core/
  State/RadarState.cs          — unmanaged interchange struct (C4)
  Profile/RadarProfileData.cs  — baked-from-SO struct
  Scanners/IPhysicsScanner.cs  — mirror of IPhysicsIntegrator
  Tracking/RadarTrack.cs       — ITargetTrack impl
  Radar/RadarEntity.cs         — pure-C# core (P6)
  Radar/RadarBehaviour.cs      — MB adapter, FixedUpdate (P2)
```

## Module-specific guardrails

- **Implements `ISensorReport`.** Every RadarEntity exposes its tracks via the
  RH Shared contract. No direct refs to Guided Fury or RH Aviator.
- **`IThreat` consumer.** RadarEntity iterates a scene-side registry of `IThreat`s.
  Build the registry helper at `RH.Shared.ThreatRegistry` ON FIRST NEED — if no
  module needs it yet, leave it.
- **No legacy import.** You may *read* the legacy files for domain knowledge (SPG-62
  beam pattern, scan rates) and reference real-world data, but do not `using` them
  from new code. They're going to `_Archive/`.
- **Hot-path discipline.** Scanners run every FixedUpdate over potentially hundreds of
  IThreats. No allocations. No LINQ. Cached arrays from the entity.

## Methodology updates

When you ship a new fidelity tier (L0, L1, ...) append a Changelog entry to
`METHODOLOGY.md` summarizing the abstractions and the test coverage. If a pattern
generalizes (e.g. "ScanResult struct survives across L0→L1 unchanged" validates C4),
note it.

## First iteration

If `Assets/RH Radar/FAILURES.md` doesn't exist, create with a header line. Start at
ROADMAP Phase 0 → "Archive legacy radar scripts."
