# Autonomous Routine — AI Aircraft Module (RH Aviator)

> **RETIRED 2026-10-27 (SIX).** Never ran: no scheduler, no `auto/*` branch, no output since they were imported
> (2026-09, 8181c10a). As written they would also `git checkout` in the shared checkout, push to branches outside the
> 2026-10-24 branch table, and point the radar routine at the struck `Assets/RH Radar` plan. Doctrine guard: "a
> routine without a scheduler is a wish". Kept for the record; do not schedule them as written. A new routine starts
> from the doctrine's routine-design procedure (worktree, hard gate, revert path, escalation, a real schedule).
> Kill log: `.claude/autoroutines/KILLED.md`.

You are the daily routine for **RH Aviator**. Read `.claude/autoroutines/_common.md`
first, then this file.

## Identity

- **Module folder:** `Assets/RH Aviator/`
- **Branch:** `auto/aviator-dev`
- **ROADMAP:** `Assets/RH Aviator/ROADMAP.md`
- **Verification:** `pwsh tools/ci/run-checks.ps1 -Module RHAviator`

## Context

Greenfield. Legacy fragments at:

- `Assets/Scripts/Aircraft_Handlers/MouseFlight/Scripts/AI_FlightController.cs`
- `Assets/Scripts/FighterJet Scripts/FlightControllerManager.cs`
- `Assets/JMAC/Agents/*.cs`

Phase 0 mines these for **knowledge** (intercept geometry, throttle curves,
target-selection heuristics) but leaves them in place. They become consumers of
the eventual `IAircraftActuator` contract, not part of RH Aviator core.

The capability ladder is **decision-side**, not airframe-side:

| Tier | Name | Decision capability |
|---|---|---|
| L0 | Waypoint follower | Fly a path. No combat. |
| L1 | Single-target intercept | Acquire track, intercept, RTB on bingo. |
| L2 | ACM | Energy management, pursuit selection, gun snapshots. |
| L3 | BVR + countermeasures | Long-range shots, notch, chaff/flare. |
| L4 | Multi-ship tactics | Wingman / element / division. |
| L5 | Mission-level AI | Roles, strike packages, SEAD coordination. |

## Reference architecture (mandatory)

```
Assets/RH Aviator/Core/
  Contracts/IAircraftActuator.cs    — pitch/roll/yaw/throttle command surface
  Contracts/IDecisionMaker.cs       — pure-C# decision driver
  State/AviatorState.cs             — unmanaged (current track id, fuel, energy, role state)
  Profile/AircraftProfileData.cs    — baked struct (mass, thrust curve, max-G, loadout summary)
  Decision/AviatorEntity.cs         — pure-C# core (P6)
  Adapter/AviatorBehaviour.cs       — MB adapter, FixedUpdate (P2)
```

## Module-specific guardrails

- **Consumes `ISensorReport` (RH Shared).** Never directly read radar internals.
- **Implements `IThreat` (RH Shared).** AI aircraft are themselves threats to the
  enemy.
- **No flight-model code in this module.** RH Aviator decides; an
  `IAircraftActuator` impl flies. Vendor F-18 wrapper, FlightControllerManager
  adapter, custom 6DOF — all live OUTSIDE RH Aviator.
- **Decision tick is on FixedUpdate** but throttled — most decisions don't need 50 Hz.
  Configurable per `RoleProfileSO`. Coarse decisions (target selection, RTB) at 2–5 Hz;
  fine commands (pitch/roll output) at full rate.
- **Test scaffolding:** EditMode tests run AviatorEntity with `NullActuator` and
  `StubAircraft` — no Unity physics required (P6 payoff).

## Cross-module integration (later phases)

- **Phase 4+ may need an `IMissileLauncher` contract** to request weapon launches from
  Guided Fury without referencing it. Define in RH Shared when the time comes; don't
  pre-emptively add a contract you don't consume.

## Methodology updates

Append Changelog entries when you finish a tier. The "decision/airframe split"
(IAircraftActuator) is likely a generalizable principle — if it survives two airframe
adapters, promote it to a Principle.

## First iteration

If `Assets/RH Aviator/FAILURES.md` doesn't exist, create with a header line. Start at
ROADMAP Phase 0 → "Mine legacy flight code for domain knowledge."
