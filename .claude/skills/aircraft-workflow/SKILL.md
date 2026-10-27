---
name: aircraft-workflow
description: "Work on Deep Water's aircraft: Air Wing (the flight core, its fidelity ladder, the F/A-18E's profile, seats, devices, the HUD, the Dogfight scene) and RH Aviator (the AI pilot: route flying, fingertip formation, the seat cycle, going home and the overhead break, the fight on rookie, line and ace profiles). Use it to modify, configure, build, tune or test anything that flies: a new mode or rule for the AI, a new part level (airframe, controls, powerplant, gear), a new aircraft type, pilot profiles, the player's controls (mouse, keyboard, pad, stick, the level hold), scene setups (wingman, 4 v 4), headless pass criteria, traces and gain sweeps, the AI budget benchmark, and Dogfight smokes. Also when a jet crashes, stalls, porpoises, overshoots a slot, lands off the centreline, or an AI fight goes nowhere. Not for missiles (Guided Fury has its own skill)."
allowed-tools: Bash, Read, Edit, Write, Grep, Glob
---

# Aircraft workflow (Air Wing + RH Aviator)

Two modules, one rule between them: **an aircraft changes only through Pilot Inputs from a seat** (Air Wing W6). The player's devices, the mouse instructor, the level hold and every AI pilot issue the same `PilotInputs` through `ISeat`. **Air Wing never references RH Aviator** (W5).

| Module | Owns | Read first |
|---|---|---|
| `Assets/Air Wing` (`DeepWater.AirWing`) | The flight core (50 Hz tick, substeps, the Fix, the input log, replay), the fidelity ladder (Airframe, Powerplant, Flight Controls, Landing Gear, Atmosphere at L0/L1), `AircraftProfileSO` (the F/A-18E, NASA HARV L1 data, public sources), seats (`PlayerSeat`, `HoldSeat`, `ReplaySeat`, `SeatRoster`), **the hands** (`FlightPathHands`, the AI's shared flight-path layer, A2), devices, HUD, camera, Dogfight | `BLUEPRINT.md` (W1–W54), `ROADMAP.md` |
| `Assets/RH Aviator` (`DeepWater.Aviator`) | The AI seat (`AviatorSeat`), the brain (modes, named rules, `RuleLog`, the 4 Hz watch), Navigate, Formation, Pattern (home and the break), Perception and Fight, pilot profiles, scene adapters (`AviatorPilot`, `AviatorOrders`, `AviatorFight`), the AI benchmark | `BLUEPRINT.md` (A1–A9), `ROADMAP.md` |

Decisions are numbered and dated; a locked one is not re-litigated, and a new one goes in the register before it is built (W43's reversal shows how: strike the old text, write the new, mark "reversed").

## 1. Standing constraints (from both blueprints; check every change)

1. `Core/` touches no Unity runtime state (no `Time`, `Random`, `Physics`, `Object.Find*`, `GameObject`, `DateTime.Now`). A banned-API scan enforces it for both cores (`CoreBannedApiTests.Scan`).
2. Determinism: time from the tick count, never a float clock (AP26; the watch rounds `0.02f` to the microsecond). Two runs give the same Fix and the same rule log; there is a test for it in each slice.
3. Tuning lives in ScriptableObjects with provenance naming every field; the profile tests check it. AUTHORED values are labelled; an unsourced number stays empty. Pilot profiles are written by **Apply Pilot Profiles**, never by hand, so numbers and sources cannot drift apart.
4. **Safe limits** (Air Wing §6.8): a game simulation. No weapons unless SIX asks; the fight is scored by rear-cone seconds only; no real-hardware control code; generic opponents; public data only.
5. Vendor code is read-only; never edit the original F-18E `.blend` or the FDS prefab.
6. Never save a scene you did not mean to change. The Dogfight builder owns `Dogfight.unity`; AI setups are added **unsaved** by the RH Aviator menus.

## 2. Configure

| What | Where | How |
|---|---|---|
| The jet's numbers | `Air Wing/ScriptableObjects/FA18E_AirWing.asset` | **Deep Water → Air Wing → Apply HARV L1 Data to F-A-18E** (`Editor/HarvL1Data.cs`, every number cited) |
| Which level each part flies | `FA18E_FidelitySheet.asset` | Airframe/Controls L1 come as a pair; Gear L1 needs Airframe L1; a missing level falls back and says so |
| The player's devices and assists | `PlayerSeatSettings.asset` | deadzone, mouse, lever, the instructor, **the level hold** (W54: keys or pad centred 0.4 s → level on the release heading; mouse detent 2°), the hold's limits |
| Bindings | `Modules/Input/AirWingFlight.inputactions` | one map per source; `CycleSeat` = Tab / D-pad right; RH Aviator's own "go home" = H / D-pad up (`AviatorOrders`, A3) |
| Pilots | `RH Aviator/ScriptableObjects/Pilot_{Rookie,Line,Ace}.asset` | **Deep Water → RH Aviator → Apply Pilot Profiles** (`AviatorTools.ApplyPilotProfiles`); rookie and ace are the line pilot plus named changes |

A new `PilotProfile` field needs six edits: the struct (+ `Validate`), the SO field, `Bake`, `AviatorTools` (value **and** provenance line), and a re-apply. Miss the provenance and `TheLineProfile_NamesTheSourceOfEveryNumber` fails.

## 3. Build scenes

- **Deep Water → Air Wing → Build Dogfight** rebuilds the scene (sea, land, runway 09/27 at (0, 8.05, −5,500) heading 090, 2,438 × 61 m, the four gates, the DDG landmark).
- Unsaved additions (RH Aviator menu): **Add AI Jet** (flies the gates), **Add AI Wingman** (joins your right wing; your jet gets an AI too, for the seat cycle), **Add 4 v 4 Dogfight** (three blue wingmen, four red: rookie, line, line, ace; the fight with its score panel; the orders).
- Benchmarks: **Build Benchmark Player** (Air Wing and RH Aviator each have one); run the exe with `-batchmode -nographics -aviatorBenchOut <file>`. Judge budgets in a player only (Air Wing: 0.05 ms per aircraft at L1; RH Aviator A4: 10 µs per pilot per tick).

## 4. Modify

### A new AI mode or rule
1. `PilotMode` in `Core/Brain.cs`; a status line in `AviatorSeat.Status()` (the pick list shows it).
2. An order method on `AviatorSeat` that fires a rule (`Fire(tick, id, "ordered: …", mode, reason)`): every mode change is logged with its tick and a reason in words.
3. Rules on the watch in `Decide` (cheap, 4 Hz); anything that must react inside a quarter second (formation slot, flare, touchdown, the fight's plan) runs **every tick** in `Issue` (A7).
4. The plan as a static function returning a `FlightPathRequest` (`Navigate.Plan`, `Formation.Plan`, `Pattern.Plan`, `Fighting.Plan`). Only ground handling works the controls directly (`Pattern.Rollout`).
5. Pick the hands: route, formation (tighter height), pattern (slow-speed gains, C43), or fight. A change of hands calls `Resume(throttle)` so nothing jumps.
6. Pass criteria **in the ROADMAP before tuning**, then a headless test, a trace helper, then a Dogfight smoke.

### The hands (`Air Wing/Core/Seats/FlightPathHands.cs`)
Aim → L1 law (a = 2V² sin η / L, L capped at the turn radius) → bank; altitude → flight-path angle → G; speed → throttle P+I (integral ±1, AP33). Optional request fields, all zero for a plain route: `PathDeg`/`PathRateDegS` and `BankDeg`/`BankRateDegS` (feed-forward), `Landing` (guard off), `GLimit`/`BankLimitDeg`. **Any new field must default to "no change" so earlier slices fly bit for bit**; rerun their tests.

### A new part level or aircraft
Follow Air Wing's ladder contract (BLUEPRINT §4): interface + one class per level, built by the factory, falling back out loud, one `AirframeState` across levels, tests at every level. A second aircraft type is a new `AircraftProfileSO` with its own provenance file; the AI's gains were tuned on the F/A-18E at L1 and **need their sweeps re-run** (the formation and pattern gains sit close to their stability edges: see the ROADMAP's "left open").

## 5. Test

- **Headless first** (W50): build a `FlightCore` with `FlatSurface`, add aircraft and seats, `Step()` in a loop. Every slice's tests are the template: `FlightPathHandsTests`, `AviatorSeatTests`, `FormationTests`, `HomeTests`, `FightTests`, `LevelHoldTests`, `SeatRosterTests`.
- **Pass criteria are fixed before tuning**, written in the ROADMAP; a box added later is added before the tuning that would use it, and said so. Never move the scenario to pass (C42).
- **Traces, not guesses:** each slice has a static trace helper that writes a CSV once a second (`HomeTests.Trace`, `FightTests.TraceMerge`, the formation hold's `Temp/aviator_3b_trace.csv`). Call it from a RunCommand (`templates/run-direct.cs`) and read it with `column -s, -t`.
- **Gain sweeps** (C42): an explicit test that runs the fixed scenario over a grid (`FormationTests.Sweep`), logged with a tag; pick inside the stable region and record the margin.
- **When a scene is dirty**, the Test Runner blocks on a save dialog. Don't save it: call the test methods directly from a RunCommand (`templates/run-direct.cs`); they are plain public methods.
- **Smokes in Dogfight** (`templates/smoke-recorder.cs`): open Dogfight, add the setup with the menu, Play, record per fixed step from an `EditorApplication.update` hook (sent *after* Play), then Stop, put `PlayerSettings.runInBackground` back, and reopen the scene that was open before (usually `GQ_Range`). Ask before stopping a play session you did not start.

## 6. Traps (each cost real time)

1. **The results file you read may be the last run's.** Clear it in its own command before starting a run; never chain the `rm` after a wait that can time out (unity-mcp-workflow trap 17). Check its timestamp if numbers look unchanged.
2. **A DLL timestamp does not prove a rebuild.** The `ScriptAssemblies` copy keeps its old time when the output is byte-identical, and auto-refresh often compiles before you ask. Wait for a new *field or method* name (`wait_build.sh`), never a local variable or a comment (trap 9).
3. **Heredocs eat backslashes.** A `"\\n"` in a Python heredoc lands as a real newline in C#. Write edit scripts to a scratch file with the Write tool, or use `chr(92)`.
4. **`??` on a Unity object lies in the Editor:** `GetComponent` returns a fake null; test with `== null`.
5. **Fixed gains are speed-dependent:** a G-per-degree path gain is 2.4× hotter at 145 kt than at 350 kt (C43). Slow phases get their own hands.
6. **Feedback alone is late:** a follower on position error overshoots; feed forward what the leader does (bank, roll rate, path rate, turn rate) and leave feedback the remainder (AP32). Read rates partly from rotation (leads) and partly from G (steady).
7. **An integrator clamp narrower than the trim's distance** to the operating point is a hidden steady error (AP33).
8. **The rear cone scores position and pointing** (A9, proposed): without the pointing test, two jets just past a head-on pass score while flying apart.
9. **An unseen contact must not be stored:** `Perception.TryGet` returns only contacts actually seen.
10. **The mouse instructor has no bank limit** (Air Wing AP25): never use it as an autopilot. The level hold flies the bounded hands.
11. **The runway's surface sits at 8.05 m and the land at 8 m;** headless landing tests use `FlatSurface(8.05)` everywhere, the strict case for the ground guard.

## 7. Record

- Every slice: tick the ROADMAP (date, commit, tests), "Found and fixed while tuning" with the numbers, the pass-criteria table as flown, a change-log line; the blueprint's register for new decisions (proposed ones as questions for SIX); METHODOLOGY for anything that generalises; the README's "Try it".
- Commit with `commit-to-branch` to `feat/aircraft` (deny `UnityMcpUnlock|/DAVFX/|\.claude/worktrees`), record the hash in a follow-up, then `level_branches.sh` with Guided-Fury by PR.
- This skill grows: fold what each slice teaches into §4–§6.

Worked example of every file and number for the F/A-18E and the line pilot: [references/fa18e.md](references/fa18e.md).
