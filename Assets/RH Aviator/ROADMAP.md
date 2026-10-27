# RH Aviator — Roadmap

**This document's one job:** what to do next for the AI pilot, and what is done. Why each piece exists is in `BLUEPRINT.md`; the decisions it carries (W36–W51) are in Air Wing's register.

**Rules (as Air Wing, General Quarters, Guided Fury and Radar Suite):**
- Slices MAY be reordered for dependency.
- Unchecked items MUST NOT be deleted; mark them `~~skipped~~` with the reason.
- Completed items get a date and their commit.
- Every slice is tested headless in EditMode against limits **fixed before tuning**, then smoked once in Dogfight (W50).
- Every performance claim carries its A/B number (`unity-perf-ab`).
- Before a slice is built, its items are checked against the safe limits (BLUEPRINT §6.5).

**Branch:** `feat/aircraft`, kept level with `main` (CLAUDE.md, Branches), commits through `commit-to-branch`.

**Where things stand (2026-10-26):** the blueprint is written (W36–W51 locked; A1–A6 proposed); nothing is built. Next is slice 3a, on SIX's go.

## DONE — finished and committed

| Done | What you get | Date | Commit | Tests |
|---|---|---|---|---|
| Blueprint and roadmap | Phase 3's 16 decisions carried for the AI side; slices 3a–3e with their pass criteria | 2026-10-26 | see the change log | — |

## STILL TO DO

### Next
- **Slice 3a — the AI seat, the hands and the gates** (W51), on SIX's go.

### Waiting on SIX
- **BLUEPRINT Q1:** confirm A1–A6 (the picture's snapshot, the hands in Air Wing, the go-home order's path, an AI budget, the old plan superseded, legacy mining).

---

# Slices (Pilot L0, then Pilot L1; W49)

### Slice 3a — the AI seat, the hands and the gates (W36, W37, W39, W44, W51)
- [ ] Mine the legacy flight code (A6), read-only: `_Archive/Aircraft_Handlers_legacy/MouseFlight/Scripts/AI_FlightController.cs`, `_Archive/FighterJet Scripts_legacy/FlightControllerManager.cs`, `JMAC/Agents/*.cs`. Pursuit, throttle and turn heuristics into `_notes/legacy-mining.md`.
- [ ] Air Wing side (A2): **the hands**, `Core/Seats/FlightPathHands`, beside `MouseInstructor` and built on its `Steer`: fly an aim point, or a heading, altitude and speed; throttle on speed; for the AI only, altitude and sink-rate guards near the ground (Air Wing AP25).
- [ ] `DeepWater.Aviator` references `DeepWater.AirWing`; `Core/` under a banned-API scan like Air Wing's.
- [ ] The AI seat (`AviatorSeat : ISeat`), the picture (the previous tick's snapshot, A1), the watch at 4 Hz on the tick count (W44), the mode machine with the rule log (W36): modes Navigate and Hold-course for now.
- [ ] `PilotProfileSO` (W45) with its first fields (G it will pull, the hands' gains); one profile, "line".
- [ ] Navigate flies a route: Air Wing's `GateCourseSO`, gate by gate.
- [ ] Scene: an AI F/A-18E in Dogfight beside the player's, flying the course (the Dogfight builder gains the option; the default scene keeps one player jet until 3b).
- [ ] **Pass criteria, fixed now (headless, from Dogfight's start, Airframe/Controls/Powerplant/Gear L1):**
  - flies all four gates in order, each inside its speed and altitude bands;
  - never below 300 ft except over the runway gate; no crash;
  - the rule log names every mode change, with tick and reason;
  - two runs give the same Fix on every tick, and the same rule log;
  - cost ≤ 10 µs per AI pilot per tick in a player build, on top of the flight core (A4).
- [ ] Smoke in Dogfight: the AI jet flies the gates while the player's sits on its seat.

### Slice 3b — formation and seats (W40, W42)
- [ ] Join (rendezvous on a leader from anywhere in the picture) and Hold fingertip, about three wingspans out and slightly behind (W40).
- [ ] Air Wing side: the seat cycle key and pad button, the HUD pick list; the aircraft the player leaves goes to its AI at once (W42, W18).
- [ ] **Pass criteria, fixed now:** joins from 2 nm behind in ≤ 90 s with no overshoot past the leader; holds the slot within ±10 m along, ±5 m across and ±5 m up through a 30° bank turn and a 2 G pull by the leader; hands back and forth mid-flight with the input log keeping its shape and the Fix continuous.
- [ ] Smoke: the AI joins on the player's wing in Dogfight; the player swaps seats with it.

### Slice 3c — going home and the break (W41, W47)
- [ ] Return on the player's order (A3) or at the bingo stand-in (W47, labelled).
- [ ] The break, as segments on the hands (W41): initial (the runway line at 800 ft, 350 kt), the break (a level turn at about 4 G), downwind (gear and flaps down, slowing), the turn to final, the straight-in final, touchdown, rollout and brakes.
- [ ] **Pass criteria, fixed now:** from 10 nm out, lands on runway 09/27 with touchdown sink < 12 ft/s, on the centreline within ±5 m, stopped on the runway; each segment within its own height, speed and heading box; no crash in 20 runs from scattered starts.
- [ ] Smoke: the AI breaks over runway 09/27 and lands in Dogfight.

### Slice 3d — Pilot L1, the fight (W38, W43, W45, W46)
- [ ] Perception: a field of view and a reaction delay from the profile; last-seen memory (W38).
- [ ] Fight: choose lead, pure or lag pursuit from the geometry and energy; keep its own tail clear; respect the profile's G (W45).
- [ ] Rear-cone scoring (about 30°, about 1 km) on the HUD and in a fight log (W46). No weapons, no gun solution (BLUEPRINT §6.5).
- [ ] Profiles: rookie, line, ace.
- [ ] Up to four aircraft (W43).
- [ ] **Pass criteria, fixed now:** over 20 fights from neutral merges, ace beats rookie on rear-cone time in ≥ 15; no AI crashes into the ground or the sea; an AI with the player outside its field of view does not turn toward the player before its delay has passed.
- [ ] Smoke: a 1v1 in Dogfight against each profile.

### Slice 3e — Phase 3 review
- [ ] Each mode in Dogfight; the AI budget re-measured with four aircraft; METHODOLOGY; Air Wing's Phase 3 closed.

---

# Superseded plan (the routine roadmap of 2026-09; A5)

Kept for the record. Air Wing now owns the airframe side (W5), and W17, W49 and the safe limits set what the AI does. Each unchecked item is struck through with its reason.

## Phase 0 — Foundation
- [ ] ~~skipped~~ ~~Mine legacy flight code for domain knowledge.~~ *Carried 2026-10-26 into slice 3a (A6).*
- [ ] ~~skipped~~ ~~State + profile structs (`AviatorState`, `AircraftProfileData`: mass, thrust curve, max-G, fuel, weapons loadout).~~ *Superseded:* the aircraft's numbers are Air Wing's `AircraftProfile`; the pilot's are `PilotProfileSO` (W45); no weapons loadout (safe limits).
- [ ] ~~skipped~~ ~~AircraftProfileSO + RoleProfileSO.~~ *Superseded:* Air Wing owns `AircraftProfileSO`; roles are modes (W36); pilot numbers are `PilotProfileSO`.
- [ ] ~~skipped~~ **IAircraftActuator contract.** *Skipped 2026-10-25:* Air Wing's `ISeat` and `PilotInputs` are the contract (Air Wing BLUEPRINT W5, W6). RH Aviator implements AI seats against them.
- [ ] ~~skipped~~ ~~IDecisionMaker contract (world snapshot in, `FlightCommand` out).~~ *Superseded:* the brain reads the picture (A1) and writes a flight-path request to the hands (W36, W37).
- [ ] ~~skipped~~ ~~AviatorEntity pure-C# core.~~ *Superseded:* the AI seat (slice 3a).
- [ ] ~~skipped~~ ~~AviatorBehaviour MonoBehaviour adapter (FixedUpdate pump, binds an actuator).~~ *Superseded:* Air Wing's runner steps every seat inside the flight core; no second pump.
- [ ] ~~skipped~~ ~~EditMode test scaffolding.~~ *Carried into slice 3a.*

## Phase 1 — L0 Waypoint follower
- [ ] ~~skipped~~ ~~WaypointFollowerDecisionMaker (a `WaypointPath` SO).~~ *Carried into slice 3a as Navigate on Air Wing's gate course (W39).*
- [ ] ~~skipped~~ ~~NullActuator + StubAircraft for tests.~~ *Superseded:* tests fly the real flight core headless (W50).
- [ ] ~~skipped~~ ~~EditMode tests: waypoint progression, end of path, distance tolerance.~~ *Carried into slice 3a's pass criteria.*

## Phase 2 — L1 Single-target intercept
- [ ] ~~skipped~~ ~~Sensor input adapter (`ISensorReport`, `ITargetTrack`).~~ *Deferred:* sensors come later (W38).
- [ ] ~~skipped~~ ~~Lead-pursuit intercept geometry.~~ *Carried into slice 3d as positioning for the fight, not an intercept.*
- [ ] ~~skipped~~ ~~Bingo / RTB logic.~~ *Carried into slice 3c (W47).*
- [ ] ~~skipped~~ ~~EditMode tests: intercept convergence, RTB at the right fuel.~~ *Carried into slices 3c and 3d.*

## Phase 3 — L2 Air-combat maneuvering
- [ ] ~~skipped~~ ~~Energy state model.~~ *Carried into slice 3d.*
- [ ] ~~skipped~~ ~~Pursuit selector (lag, pure, lead).~~ *Carried into slice 3d.*
- [ ] ~~skipped~~ ~~Gun snapshot detection.~~ *Dropped:* no weapons and no gun solution (W46, safe limits).

## Phase 4 — L3 BVR + countermeasures
- [ ] ~~skipped~~ ~~Missile launch logic.~~ *Dropped:* safe limits; only if SIX asks.
- [ ] ~~skipped~~ ~~Notch maneuver.~~ *Dropped:* safe limits.
- [ ] ~~skipped~~ ~~Defensive RWR adapter.~~ *Dropped:* safe limits.

## Phase 5 — L4 Multi-ship tactics
- [ ] ~~skipped~~ ~~Wingman role.~~ *Carried into slice 3b as formation, with no shots.*
- [ ] ~~skipped~~ ~~Element / division over a `DoctrineSO`.~~ *Deferred:* W43 caps Phase 3 at four aircraft.

## Phase 6 — L5 Mission-level AI
- [ ] ~~skipped~~ ~~Strike package with SEAD escort.~~ *Dropped:* outside W17 and the safe limits.
- [ ] ~~skipped~~ ~~Tasking interface.~~ *Deferred:* not asked for.

---

## Change log
- **2026-09** — The routine roadmap (Phases 0–6) written; nothing built.
- **2026-10-25** — `IAircraftActuator` skipped: Air Wing's `ISeat` and `PilotInputs` replace it.
- **2026-10-26** — Rewritten from Air Wing's Phase 3 interview (W36–W51): slices 3a–3e with pass criteria fixed before tuning; the old plan kept below, superseded (A5). Nothing built.
