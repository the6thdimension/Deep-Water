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

**Where things stand (2026-10-26):** slice 3a is done: the AI pilot flies Dogfight's four gates on the L1 jet, headless and in the scene, at 3–4 µs per pilot per tick. Next is slice 3b (formation and seats), on SIX's go.

## DONE — finished and committed

| Done | What you get | Date | Commit | Tests |
|---|---|---|---|---|
| Blueprint and roadmap | Phase 3's 16 decisions carried for the AI side; slices 3a–3e with their pass criteria | 2026-10-26 | eaf7280f | — |
| Slice 3a — the AI seat, the hands and the gates | An AI pilot that flies Dogfight's four gates through the player's own stick: the mode machine with its logged rules, the shared flight-path layer, the line pilot's profile, an "Add AI Jet" menu, the AI budget met | 2026-10-26 | see the change log | 6 here, 4 in Air Wing (117 in all) |

## STILL TO DO

### Next
- **Slice 3b — formation and seats** (W40, W42), on SIX's go.

### Left open in finished slices
- **Slice 3a:** the first approach to gates 2 and 3 goes around once (each 90° turn onto a gate's line does not fit the first try); the course takes about 5.5 minutes. Not flown against a player by hand.

---

# Slices (Pilot L0, then Pilot L1; W49)

### Slice 3a — the AI seat, the hands and the gates (W36, W37, W39, W44, W51) (done 2026-10-26: all pass criteria met; 117/117 tests in both modules)
- [x] Mined the legacy flight code (A6), read-only, into `_notes/legacy-mining.md`: **nothing worth carrying.** `AI_FlightController` is MouseFlight's camera rig, `FlightControllerManager` a status-light board, the JMAC agents a homing weapon (out of scope).
- [x] Air Wing side (A2): **the hands**, `Core/Seats/FlightPathHands`, beside `MouseInstructor`. *Not built on its `Steer`* (A2 refined): `Steer` has no bank limit (Air Wing AP25). The hands fly an aim point at an altitude and a speed:
  - the aim → a sideways acceleration by the **L1 path-following law** (a = k·V²·sin η / L, k = 2, L capped at the turn radius) → a bank, capped;
  - the altitude error → a flight-path angle, capped; the path and the bank → G (cos γ / cos φ plus a correction), capped by the pilot's G;
  - the speed → throttle, proportional plus integral (no wind-up), afterburner and speed brake;
  - the ground guard: under 100 m, or heading there within 6 s, it climbs at 8° with the bank held to 30°.
- [x] Air Wing side (A1): **the picture**, `FlightCore.Picture`, every aircraft as the previous tick ended, the same for every seat; and `FlightCore.Surface`. Air Wing's banned-API scan is now a public `Scan(folder)`, which RH Aviator's core runs too.
- [x] `DeepWater.Aviator` references `DeepWater.AirWing`; `Core/` passes the banned-API scan.
- [x] `AviatorSeat : ISeat` (`SeatKind.Ai`): the watch at 4 Hz on the tick count (W44), the mode machine with the rule log (W36). Modes Navigate and HoldCourse. Rules: **next gate**, **course flown**, **go around** and **back on the line**, each logged with its tick and a reason in words; `AviatorPilot` also prints them to the console.
- [x] `PilotProfileSO` (W45) and the **line** pilot (`ScriptableObjects/Pilot_Line.asset`, written by **Deep Water → RH Aviator → Apply Line Pilot Profile** with its provenance; all AUTHORED).
- [x] Navigate flies Air Wing's `GateCourseSO`, gate by gate: a carrot on the gate's line, at least a turn radius and at least the offset ahead; a sticky go-around, back down the line and out to its side, when too close to line up or past the gate; "lined up" means its present track would cross inside 70 % of the ring.
- [x] Scene: **Deep Water → RH Aviator → Add AI Jet to the Open Scene** puts an AI F/A-18E (aircraft 2, the line pilot, the Dogfight course) 80 m right of and behind the player's jet, unsaved. The Dogfight scene on disk keeps one jet (W51).
- [x] **Found and fixed while tuning** (the pass criteria stayed as fixed):
  - A carrot fixed at the gate's entry point cut onto the line at 90° on a 1.7 km turn and missed the 100 m ring; then, past the gate, it **orbited for ever**, its aim point inside its own turning circle (METHODOLOGY AP31).
  - A carrot shorter than the turn radius (400 m against 1.2 km) **swung across the line**: −2,500 → +309 → −124 m. A bank proportional to the heading error kept a ±40–77 m swing that never settled. The L1 law with its look-ahead about one turn radius settled it: gates now within 4 m of the centre (METHODOLOGY C41).
  - The go-around, decided afresh each watch, **flipped** at its boundary; it is sticky now, and logged.
  - "Lined up" first tested a fixed offset at one moment; it now asks where the present track crosses the ring.
  - The watch fell a tick late (1201 for 1200): the core's tick is `0.02f`, 0.0199999995 s as a double; the watch rounds it to the microsecond (METHODOLOGY AP26 again).
  - A far aim (a heading) gave almost no bank once the error fell under 90°: L is now capped at the turn radius.
  - Speed held 23 kt fast on a proportional throttle: an integral term took it out (400.0 kt held, 30 s).
- [x] **Pass criteria, met** (headless, from Dogfight's start, every part L1, land height 8 m everywhere):

  | Gate | Flown | Off centre |
  |---|---|---|
  | 1 · 5,000 ft (350–450 kt) | 399 kt, 5,000 ft | 0 m |
  | 2 · DDG pass (800–1,200 ft) | 353 kt, 1,001 ft (after one go-around) | 3 m |
  | 3 · climb to 6,000 ft (≥ 300 kt) | 361 kt, 6,001 ft (after one go-around) | 4 m |
  | 4 · runway pass (300–700 ft, 250–350 kt) | 306 kt, 512 ft | 4 m |

  - all four in order, each inside its bands; course flown in 335 s; no crash; lowest 659 ft away from gate 4's run (511 ft on it);
  - the rule log names every mode change with its tick and reason, on a watch;
  - two runs: the same Fix on every tick, the same rule log;
  - **the AI's own cost, player build (Windows, Mono, release; i9-12900H; two runs): 2.97 and 2.71 µs per pilot per tick with 1 pilot, 4.01 and 3.55 µs with 4: budget 10, met.** Measured by `AviatorBenchmarkRunner` (**Deep Water → RH Aviator → Build Benchmark Player**, `AviatorBenchmark.exe -batchmode -nographics -aviatorBenchOut <file>`): the same aircraft on hold seats against AI pilots, the difference. *Unexplained:* the hold-seat jets here cost 27–30 µs per aircraft per tick, against 18 µs in Air Wing's own benchmark (5,000 m, a different start); both inside Air Wing's 50 µs.
- [x] Air Wing tests (`FlightPathHandsTests`, 4): the hands hold 400.0 kt, height and track for 30 s; a turn behind stays inside the bank and G caps and comes round; the ground guard keeps it 110 m up when asked for the sea; aircraft 2 sees aircraft 1 where the last tick left it.
- [x] **Smoke in Dogfight** (play mode, the AI jet added unsaved, the sea and land of the scene, PhysX contacts): it flew all four gates clean, 3, 2, 36 and 12 m off centre, with one go-around at gate 2, and went into HoldCourse at 239 s. The scene was then reloaded from disk.

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
- **2026-10-26** — SIX confirmed A1–A6. Slice 3a done: the AI pilot flies Dogfight's four gates through the stick (L1 law hands, sticky go-around, logged rules), 117/117 tests in both modules, the AI budget met (3–4 µs per pilot per tick). Found: an aim point inside the turn orbits for ever (AP31); a look-ahead shorter than the turn radius swings (C41); a float clock moved the watch (AP26).
