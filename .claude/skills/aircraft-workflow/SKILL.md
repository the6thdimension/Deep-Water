---
name: aircraft-workflow
description: "Work on Deep Water's aircraft: Air Wing (the flight core, its fidelity ladder, the F/A-18E's profile, seats, devices, the HUD, the Dogfight scene, and the jets' gun: the trigger, rounds, hit points, kills, the pipper, tracers) and RH Aviator (the AI pilot: route flying, fingertip formation, the seat cycle, going home and the overhead break, the fight on rookie, line and ace profiles, trigger discipline and gun tracking). Use it to modify, configure, build, tune or test anything that flies: a new mode or rule for the AI, a new part level (airframe, controls, powerplant, gear), a new aircraft type, pilot profiles, the player's controls (mouse, keyboard, pad, stick, the level hold, Fire), scene setups (wingman, 4 v 4), headless pass criteria, traces and gain sweeps, fight surveys and judging a fight (position against pointed cone time, before and after a rule change), slice 3f (the fight in the vertical), the AI budget benchmark, and Dogfight smokes. Also when a jet crashes, stalls, porpoises, overshoots a slot, lands off the centreline, an AI fight stalemates or goes nowhere, or the AI's gun misses. Not for missiles: Guided Fury and Air Wing W58 are SIX's or Codex's work, not Claude's."
allowed-tools: Bash, Read, Edit, Write, Grep, Glob
---

# Aircraft workflow (Air Wing + RH Aviator)

Two modules and a shared gun, one rule between them: **an aircraft changes only through Pilot Inputs from a seat** (Air Wing W6). The player's devices, the mouse instructor, the level hold and every AI pilot issue the same `PilotInputs` through `ISeat`; the trigger is one more input (`PilotInputs.Trigger`, W55). **Air Wing never references RH Aviator** (W5), and Gunnery references no vehicle (`Gunnery_ReferencesNoVehicleOrMissileModule`).

| Module | Owns | Read first |
|---|---|---|
| `Assets/Air Wing` (`DeepWater.AirWing`) | The flight core (50 Hz tick, substeps, the Fix, the input log, replay), the fidelity ladder (Airframe, Powerplant, Flight Controls, Landing Gear, Atmosphere at L0/L1), `AircraftProfileSO` (the F/A-18E, NASA HARV L1 data, public sources), seats (`PlayerSeat`, `HoldSeat`, `ReplaySeat`, `SeatRoster`), **the hands** (`FlightPathHands`, the AI's shared flight-path layer, A2), devices, HUD, camera, Dogfight; **the airframe's side of the gun** (the trigger; `Rounds`, `GunCarry`, `HitPoints` in the Fix; `FlightStatus.Killed`; the guns stage and `FlightCore.Kills`; `GunMount`, `CombatProfile`; the pipper; `TracerView`) | `BLUEPRINT.md` (W1–W58; the gun W55–W57), `ROADMAP.md` |
| `Assets/RH Aviator` (`DeepWater.Aviator`) | The AI seat (`AviatorSeat`), the brain (modes, named rules, `RuleLog`, the 4 Hz watch), Navigate, Formation, Pattern (home and the break), Perception and Fight, **the AI's trigger** (discipline and gun tracking, A12), pilot profiles, scene adapters (`AviatorPilot`, `AviatorOrders`, `AviatorFight`), the AI benchmark | `BLUEPRINT.md` (A1–A12), `ROADMAP.md` |
| `Assets/Gunnery` (`DeepWater.Gunnery`) | Round flight for every platform (K1): `Core/Guns` (`GunProfile`, `RoundFlight`, `RoundsInFlight`, `LeadSight`), `Core/Ballistics`. Damage crosses as RH Shared's `HitReport`; each vehicle applies its own (K3) | `BLUEPRINT.md` (K1–K6), `README.md`, `ROADMAP.md`; a gun on another vehicle: the `gunnery-integration` skill |

Decisions are numbered and dated; a locked one is not re-litigated, and a new one goes in the register before it is built (W43's reversal shows how: strike the old text, write the new, mark "reversed"). The gun's record is in the blueprints, Gunnery's `ROADMAP.md` (slice 2) and commit 3e34e232; neither aircraft ROADMAP has a gun entry yet.

## 1. Standing constraints (from the blueprints; check every change)

1. `Core/` touches no Unity runtime state (no `Time`, `Random`, `Physics`, `Object.Find*`, `GameObject`, `DateTime.Now`). A banned-API scan enforces it for both cores (`CoreBannedApiTests.Scan`) and for Gunnery's (`GunneryCore_UsesNoBannedApis`).
2. Determinism: time from the tick count, never a float clock (AP26; the watch rounds `0.02f` to the microsecond). Two runs give the same Fix and the same rule log; there is a test for it in each slice. Hits draw from the core's own seed (`FlightCore(..., gunnerySeed: n)`), so a guns-hot fight replays kill for kill.
3. Tuning lives in ScriptableObjects with provenance naming every field; the profile tests check it. AUTHORED values are labelled; an unsourced number stays empty. Pilot profiles are written by **Apply Pilot Profiles**, never by hand, so numbers and sources cannot drift apart.
4. **Safe limits** (Air Wing §6.8): a game simulation. The jets carry a game gun because SIX asked (W55–W57, A12): rounds as shot groups, abstract hit points, a game pilot's trigger. No real fire-control code, no real targeting, no real-hardware control code; generic opponents; public data only. The fight scores kills first, rear-cone seconds beside (K6); the cone is scored by position alone (A9 declined).
5. Vendor code is read-only; never edit the original F-18E `.blend` or the FDS prefab.
6. Never save a scene you did not mean to change. The Dogfight builder owns `Dogfight.unity`; AI setups are added **unsaved** by the RH Aviator menus; tracers are added by the runner, so the gun needs no scene change.
7. **Missiles are not Claude's work** (SIX, 2026-10-27): Guided Fury missile work, Air Wing W58 included, is done by SIX or Codex. Leave W58 alone.

## 2. Configure

| What | Where | How |
|---|---|---|
| The jet's numbers | `Air Wing/ScriptableObjects/FA18E_AirWing.asset` | **Deep Water → Air Wing → Apply HARV L1 Data to F-A-18E** (`Editor/HarvL1Data.cs`, every number cited) |
| The gun | the same asset (its gun fields, `AircraftProfileSO`) | **Deep Water → Air Wing → Apply Gun Data to F-A-18E** (`Editor/GunData.cs`: the M61A2's public numbers, the rest AUTHORED) |
| Which level each part flies | `FA18E_FidelitySheet.asset` | Airframe/Controls L1 come as a pair; Gear L1 needs Airframe L1; a missing level falls back and says so |
| The player's devices and assists | `PlayerSeatSettings.asset` | deadzone, mouse, lever, the instructor, **the level hold** (W54: keys or pad centred 0.4 s → level on the release heading; mouse detent 2°), the hold's limits |
| Bindings | `Modules/Input/AirWingFlight.inputactions` | one map per source; `CycleSeat` = Tab / D-pad right; `Fire` = left mouse, left Ctrl, pad A, stick button 5 (the stick's trigger stays the brakes, W56); RH Aviator's own "go home" = H / D-pad up (`AviatorOrders`, A3) |
| Pilots | `RH Aviator/ScriptableObjects/Pilot_{Rookie,Line,Ace}.asset` | **Deep Water → RH Aviator → Apply Pilot Profiles** (`AviatorTools.ApplyPilotProfiles`); rookie and ace are the line pilot plus named changes, trigger discipline included (`GunRangeM`, `TriggerMils`, A12) |

A new `PilotProfile` field needs six edits: the struct (+ `Validate`), the SO field, `Bake`, `AviatorTools` (value **and** provenance line), and a re-apply. Miss the provenance and `TheLineProfile_NamesTheSourceOfEveryNumber` fails.

## 3. Build scenes

- **Deep Water → Air Wing → Build Dogfight** rebuilds the scene (sea, land, runway 09/27 at (0, 8.05, −5,500) heading 090, 2,438 × 61 m, the four gates, the DDG landmark).
- Unsaved additions (RH Aviator menu): **Add AI Jet** (flies the gates), **Add AI Wingman** (joins your right wing; your jet gets an AI too, for the seat cycle), **Add 4 v 4 Dogfight** (three blue wingmen, four red: rookie, line, line, ace; the fight with its score panel; the orders).
- Benchmarks: **Build Benchmark Player** (Air Wing and RH Aviator each have one); run the exe with `-batchmode -nographics -aviatorBenchOut <file>`. Judge budgets in a player only (Air Wing: 0.05 ms per aircraft at L1; RH Aviator A4: 10 µs per pilot per tick).

## 4. Modify

### A new AI mode or rule
1. `PilotMode` in `Core/Brain.cs`; a status line in `AviatorSeat.Status()` (the pick list shows it).
2. An order method on `AviatorSeat` that fires a rule (`Fire(tick, id, "ordered: …", mode, reason)`): every mode change is logged with its tick and a reason in words.
3. Rules on the watch in `Decide` (cheap, 4 Hz); anything that must react inside a quarter second (formation slot, flare, touchdown, the fight's plan, gun tracking) runs **every tick** in `Issue` (A7).
4. The plan as a static function returning a `FlightPathRequest` (`Navigate.Plan`, `Formation.Plan`, `Pattern.Plan`, `Fighting.Plan`). Only ground handling works the controls directly (`Pattern.Rollout`).
5. Pick the hands: route, formation (tighter height), pattern (slow-speed gains, C43), or fight. A change of hands calls `Resume(throttle)` so nothing jumps.
6. Pass criteria **in the ROADMAP before tuning**, then a headless test, a trace helper, then a Dogfight smoke. A change to the fight also gets a fight survey, before and after (§5).

### The hands (`Air Wing/Core/Seats/FlightPathHands.cs`)
Aim → L1 law (a = 2V² sin η / L, L capped at the turn radius) → bank; altitude → flight-path angle → G; speed → throttle P+I (integral ±1, AP33). Optional request fields, all zero for a plain route: `PathDeg`/`PathRateDegS` and `BankDeg`/`BankRateDegS` (feed-forward), `Landing` (guard off), `GLimit`/`BankLimitDeg`. **Any new field must default to "no change" so earlier slices fly bit for bit**; rerun their tests.

### The gun (W55–W57, Gunnery K1–K6, A12)
- Round flight, drag, the swept hit test and the lead sight are Gunnery's: change them there, with Gunnery's `GunTests`, never a copy in Air Wing.
- `FlightCore`'s guns stage: rounds already in flight fly first against every jet's path over the tick, then this tick's rounds leave, so the firing order never decides who is hit.
- The AI's trigger is judged at 10 Hz (`AviatorSeat.TriggerEveryTicks`) on what the pilot sees, with the same `LeadSight` as the player's pipper; inside 1.5× its gun range the pilot flies the pipper, not the flight path (A12).
- Position-fight tests fly guns safe (`TriggerMils = 0` in `FightTests`' `Pilot`); the armed fight is `GunFightTests`. A jet shot down falls and crashes by design: crash checks skip it (`KilledBy != 0`, as `GunFightTests.BadCrash` does).

### A new part level or aircraft
Follow Air Wing's ladder contract (BLUEPRINT §4): interface + one class per level, built by the factory, falling back out loud, one `AirframeState` across levels, tests at every level. A second aircraft type is a new `AircraftProfileSO` with its own provenance file; the AI's gains were tuned on the F/A-18E at L1 and **need their sweeps re-run** (the formation and pattern gains sit close to their stability edges: see the ROADMAP's "left open").

## 5. Test

- **Headless first** (W50): build a `FlightCore` with `FlatSurface`, add aircraft and seats, `Step()` in a loop. Every slice's tests are the template: `FlightPathHandsTests`, `AviatorSeatTests`, `FormationTests`, `HomeTests`, `FightTests`, `GunFightTests`, `LevelHoldTests`, `SeatRosterTests`, Air Wing's `GunTests`.
- **Pass criteria are fixed before tuning**, written in the ROADMAP; a box added later is added before the tuning that would use it, and said so. Never move the scenario to pass (C42).
- **Traces, not guesses:** each slice has a static trace helper that writes a CSV once a second (`HomeTests.Trace`, `FightTests.TraceMerge`, the formation hold's `Temp/aviator_3b_trace.csv`). Call it from a RunCommand (`templates/run-direct.cs`) and read it with `column -s, -t`.
- **Gain sweeps** (C42): an explicit test that runs the fixed scenario over a grid (`FormationTests.Sweep`), logged with a tag; pick inside the stable region and record the margin.
- **When a scene is dirty**, the Test Runner blocks on a save dialog. Don't save it: call the test methods directly from a RunCommand (`templates/run-direct.cs`); they are plain public methods.
- **Smokes in Dogfight** (`templates/smoke-recorder.cs`): open Dogfight, add the setup with the menu, Play, record per fixed step from an `EditorApplication.update` hook (sent *after* Play), then Stop, put `PlayerSettings.runInBackground` back, and reopen the scene that was open before (usually `GQ_Range`). Ask before stopping a play session you did not start.

### The fight survey (judging a fight; slice 3f)
A change to the fight is judged on many merges, before and after, never on one. Detail, the runners and the before numbers: [references/fight-survey.md](references/fight-survey.md).
1. **Criteria first**, in the ROADMAP item, before building. Slice 3f's (A11): line v line and ace v line each produce a winner in some of 20 merges **by pointed cone time**, and no fight sinks to the floor. Write "the floor" as a number then (`SinkMarginM`).
2. **Before:** clear the report in its own command, send `templates/fight-survey.cs`, then `wait_file.sh <out> 900 '^# end'`. It flies one fight per Editor update, so it never holds the main thread (unity-mcp-workflow trap 12). Its position columns must equal `FightTests.Survey`'s `[Aviator survey]` lines; if not, the template has drifted.
3. **After:** one rule changed, the build confirmed by a new symbol (trap 2), the same template with the same `Starts`, `N`, `GunsHot` and `SinkMarginM`. Other starts make another survey, not an "after".
4. **Report** a table per matchup, before beside after: wins by position and by pointed, mean seconds a fight both ways, the pointed share, the lowest height, fights under the floor, crashes (kills, guns hot). Name the runner and the starts behind every number.

## 6. Traps (each cost real time)

1. **The results file you read may be the last run's.** Clear it in its own command before starting a run; never chain the `rm` after a wait that can time out (unity-mcp-workflow trap 17). Check its timestamp if numbers look unchanged.
2. **A DLL timestamp does not prove a rebuild.** The `ScriptAssemblies` copy keeps its old time when the output is byte-identical, and auto-refresh often compiles before you ask. Wait for a new *field or method* name (`wait_build.sh`), never a local variable or a comment (trap 9).
3. **Heredocs eat backslashes.** A `"\\n"` in a Python heredoc lands as a real newline in C#. Write edit scripts to a scratch file with the Write tool, or use `chr(92)`.
4. **`??` on a Unity object lies in the Editor:** `GetComponent` returns a fake null; test with `== null`.
5. **Fixed gains are speed-dependent:** a G-per-degree path gain is 2.4× hotter at 145 kt than at 350 kt (C43). Slow phases get their own hands.
6. **Feedback alone is late:** a follower on position error overshoots; feed forward what the leader does (bank, roll rate, path rate, turn rate) and leave feedback the remainder (AP32). Read rates partly from rotation (leads) and partly from G (steady).
7. **An integrator clamp narrower than the trim's distance** to the operating point is a hidden steady error (AP33).
8. **The rear cone scores position alone** (A9 declined, SIX 2026-10-27), so a head-on pass scores a few seconds each way while the jets fly apart: line v line went from 0 / 0 to 6.1 / 5.6 s a fight with nothing built. Judge a fight by pointed cone time (`templates/fight-survey.cs`), and leave `RearCone.Inside` as SIX set it.
9. **An unseen contact must not be stored:** `Perception.TryGet` returns only contacts actually seen.
10. **The mouse instructor has no bank limit** (Air Wing AP25): never use it as an autopilot. The level hold flies the bounded hands.
11. **The runway's surface sits at 8.05 m and the land at 8 m;** headless landing tests use `FlatSurface(8.05)` everywhere, the strict case for the ground guard.
12. **In a RunCommand, FlightSimLite's global `Units` and `Aircraft` beat your `using`s.** The vendor's `Assets/Plugins/FlightSimLite/Scripts/` declares both in the global namespace, which C# searches before `using` directives, and RunCommand compiles against that assembly; tests don't (asmdefs can't see it). `Units.KnotToMetrePerSecond` fails with CS0117. Write `DeepWater.AirWing.Core.Units` and use `var` for aircraft.
13. **Gun tracking through the height loop porpoised** (A12: misses 4–187 m). Ask the hands for the flight-path angle that puts the bore on the lead point (`PathDeg`); misses settled under 1 m.

## 7. Record

- Every slice: tick the ROADMAP (date, commit, tests), "Found and fixed while tuning" with the numbers, the pass-criteria table as flown, a survey's before and after tables in the item they judge, a change-log line; the blueprint's register for new decisions (proposed ones as questions for SIX); METHODOLOGY for anything that generalises; the README's "Try it".
- Commit with `commit-to-branch` to `feat/aircraft` (deny `UnityMcpUnlock|/DAVFX/|\.claude/worktrees`), record the hash in a follow-up, then `level_branches.sh` with Guided-Fury by PR.
- This skill grows: fold what each slice teaches into §4–§6.

Worked example of every file and number for the F/A-18E, its gun and the three pilots: [references/fa18e.md](references/fa18e.md).
