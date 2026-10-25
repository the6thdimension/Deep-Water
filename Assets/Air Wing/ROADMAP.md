# Air Wing — Roadmap

**This document's one job:** what to do next, and what is done. Why each piece exists is in `BLUEPRINT.md`.

**Rules (same as General Quarters, Guided Fury and Radar Suite):**
- Phases MAY be reordered for dependency.
- Unchecked items MUST NOT be deleted; mark them `~~skipped~~` with the reason.
- Completed items get a date and their commit.
- Every part's level ships with EditMode tests.
- Every performance claim carries its A/B number (`unity-perf-ab`).
- Before a slice is built, its items are checked against the safe limits (BLUEPRINT §6, constraint 8).

**Branch:** `feat/aircraft`, cut from `main` at 522ca2f, and kept in step with `main` (CLAUDE.md, Branches). HEAD stays wherever the shared checkout is, and commits go in through `commit-to-branch`.
- **Git (SIX, 2026-10-24):** Claude manages git without asking. Verified work is committed, pushed and brought up to `main`, and every branch is kept level; `Guided-Fury` takes it through a PR.
- **Never pushed:** the archive branch, DAVFX and the MCP unlock script. There are no force pushes.

**Status (2026-10-25): slice 0 done; no flight code yet.** The blueprint is locked and on `main`. Slice 1 starts on SIX's go, once no other session is driving the Unity Editor.

---

## Order of streams

1. **Slice 0:** clean-up.
2. **Phase 1:** first flight at L0, in Dogfight (slice 1).
3. **Phase 2:** jet L1, a 6-DOF airframe with fly-by-wire and landing gear; runway take-off and landing.
4. **Phase 3:** AI pilots (RH Aviator) L0 and L1, with switchable seats.
5. **Streams after jet L1, in any order:** the helicopter (W20); Airframe L2 (Mach, ground effect); L3 (fuel and CG); Powerplant L2–L3.
6. **Carrier ops last** (W16): parked until Phase 3 is done (BLUEPRINT §7).

---

## Slice 0 — Clean-up (S) (done 2026-10-25, 63650de: 44/44 GUIDs kept; 0 aircraft errors in the player-script compile)

The gate: nothing anyone uses changes, a player build compiles, and the legacy aircraft code has a checklist of what replaces it.
- [x] Archive (P8) `Assets/Scripts/FighterJet Scripts/` → `Assets/_Archive/FighterJet Scripts_legacy/`, with its `.meta` files (moved with `AssetDatabase.MoveAsset`).
- [x] Archive `Assets/Scripts/Aircraft_Handlers/` → `Assets/_Archive/Aircraft_Handlers_legacy/`, with its `.meta` files, including the MouseFlight copy (MIT headers kept).
  - **Found on the way:** three audio files in the folder are live. `newF85 Sounds/afterburner.mp3`, `mid55.wav` and `TRANSPORTATION_JET_MILITARY_Fighter_Turbine_Steady_Soft_04.wav` are referenced by CVN-78 FORD, Dogfight and OutdoorsScene (the F-18E's audio). They moved to `Assets/_AIRCRAFT/Audio/` rather than the archive.
  - **Measured:** all 44 GUIDs unchanged (41 files and folders, the two folder roots, the audio folder).
- [x] **Fix found 2026-10-24:** `FixedWing_Handlers/Editors/` renamed to `Editor/`. `FixedWingController_Editor.cs` uses `UnityEditor`, and "Editors" is not a special folder.
  - **Measured (player-script compile, StandaloneWindows64):** before, the file's errors were in the list; after, 0 errors from any aircraft file.
- [x] A `README.md` in each archive folder with the supersession checklist (C25), one row per file: carried, superseded (named, with the slice that brings it) or dropped deliberately (with the reason).
- [x] `FighterJetController` and the `F-18 (1) Variant` prefab are **not** archived here: Dogfight still references them. They go in slice 1e, with the rebuild.
- [x] Fixed stale words:
  - `Assets/_SCENES/README.md`: the Dogfight, CVN-78 FORD and Helo rows now say what is wired.
  - `Assets/RH Aviator/README.md`: no more "PuppetMaster F-18"; the legacy paths point to the archive; the `IAircraftActuator` contract is struck through, superseded by Air Wing's `ISeat` + `PilotInputs` (W5, W6). The matching RH Aviator ROADMAP item is marked ~~skipped~~ with the reason.
- [x] **Q2 (SIX, 2026-10-25):** `RH Aviator.asmdef` renamed `RH.Aviator` → `DeepWater.Aviator` (name and root namespace). Nothing referenced it by name; it has no scripts yet.
- [x] *Done when:*
  - the moved GUIDs all resolve (scan above);
  - the Editor compiles clean, with every moved type resolved from its new folder (compile-clean RunCommand);
  - every archived file has a checklist row;
  - a player build of one scene compiles: at first **not met, for reasons outside slice 0** (next item); Air Wing's part was met (zero aircraft errors). **Met for player scripts since 0d24231** (next item). A full player build with content has not been run yet; slice 1e does it for W21.
- [x] **Player build blocked outside Air Wing** (found 2026-10-25, BLUEPRINT Q4; fixed the same day on SIX's word, 0d24231). Each file got an `Editor/` folder (GUID kept) or an `#if UNITY_EDITOR` guard:
  - `AerialArcade/Code/Scripts/F18/F18ControllerEditor.cs` → `F18/Editor/` (vendor; SIX's word);
  - `RH Utilities/RH_SceneManager.cs`, `SceneManagerCore.cs`, `SceneValidationSystem.cs` → `RH Utilities/Editor/` (only editor code calls them);
  - `RH Testing Suite/Core/TestRunner.cs`: guards around its play-mode plumbing only. `TestResultExporter` (runtime) still calls `GetAllTestResults()`, and in a player every test simply runs. `TestDiscovery.cs`: the whole file is guarded (AssetDatabase; only the editor window calls it). It never showed in the error list: the compiler stops at declaration errors and skips method bodies, so a body-only use hides behind them;
  - `Scripts/Destroyer Scripts/DestroyerEditor.cs` → `Destroyer Scripts/Editor/`;
  - `_Archive/Carrier_legacy/Carrier Controller/CarrierEditor.cs` → `Carrier Controller/Editor/` (an earlier archive had lifted it out of its `Editor/` folder);
  - `UnityMcpUnlock.cs`: wrapped in `#if UNITY_EDITOR` **locally only**; it stays git-ignored and is never committed.
  - **Measured (player-script compile, StandaloneWindows64):** 0 errors, 91 assemblies (it had failed with 0 assemblies). The Editor recompiled clean, and every moved type resolves (`Assembly-CSharp-Editor` for the inspectors and RH Utilities, `Assembly-CSharp` for the test-suite core).
- *METHODOLOGY:* added as AP24 (editor API compiled into the player).
- *Anti-goals:* held for slice 0 itself (no new code, no vendor folder touched). The follow-up moved one AerialArcade file into an `Editor/` folder on SIX's word; no vendor file's content changed.

---

## Phase 1 — First flight at L0 in Dogfight

The gate: the F/A-18E flies in the rebuilt Dogfight at L0, from each device class. The numbers on the HUD are right by test, a replay from the input log reproduces the Fix bit for bit, and one aircraft costs ≤ 0.01 ms per fixed step in a player build.

### Slice 1a — Module skeleton and contracts
- [ ] Folders per P3 (`Core`, `Modules`, `ScriptableObjects`, `Prefabs`, `Examples`, `Tests`) and asmdefs (`DeepWater.AirWing`, `DeepWater.AirWing.Tests.Editor`). No reference to `DeepWater.GeneralQuarters` (W31).
- [ ] `PilotInputs` (stick pitch and roll, pedals, throttle with the afterburner detent, switches) and `ISeat`, with the issuer and tick.
- [ ] `InputLog`: append-only, one entry per seat per tick.
- [ ] `AirframeState` (C4): position, velocity, attitude, body rates, engine state. SI units (W22).
- [ ] Fidelity-ladder contract (C14): a level per part, factories, and fallback out loud. `FidelitySheetSO` (W25).
- [ ] Fixed substep clock: N substeps per 50 Hz tick, N from the fidelity sheet (W24).
- [ ] `FlightFix`: a per-tick FNV-1a fingerprint of every aircraft's state.
- [ ] Seeded RNG (pattern copied from General Quarters, W31).
- [ ] Tests: banned-API source scan of `Core/` (constraint 1); ladder fallback; substep clock; input log; Fix stable for identical states.

### Slice 1b — Airframe, Powerplant, Flight Controls and Atmosphere at L0, headless
- [ ] `Atmosphere` L0: ISA standard atmosphere (density, temperature, speed of sound to at least 20 km). Tested against the published ISA table.
- [ ] `Airframe` L0, a point-mass energy model (BLUEPRINT §4):
  - thrust minus drag along the path; drag = parasite + induced;
  - lift up to Cl_max and the G limit sets the achievable load factor;
  - turn rate g·√(n²−1)/V; climb and zoom from energy;
  - crashed when below the truth-fed surface height (sea from Sea State, runway from the scene).
- [ ] `Powerplant` L0: throttle and afterburner to thrust, with a density lapse (shape AUTHORED).
- [ ] `FlightControls` L0: commanded G and roll rate (W26), tracked by a first-order lag and clipped to the profile limits.
- [ ] `AirframeProfileSO` for the F/A-18E, with each number's source in its provenance: weights, wing area, thrust (military and afterburner), G limits, maximum speed and ceiling from the Navy fact file and Boeing's published figures. Drag, Cl_max and lag time constants are CALIBRATED to one public figure each, or AUTHORED and flagged (C16). Sources go in `Calibration~/F18E-SOURCES.md`. An unsourced value stays empty (W8).
- [ ] Tests:
  - determinism: two seeded runs give identical Fix sequences;
  - substep independence: 2, 4 and 8 substeps agree within a stated tolerance on a 60 s manoeuvre;
  - straight and level: trim holds altitude and speed;
  - sustained turn rate matches g·√(n²−1)/V at the trimmed speed;
  - the G limit holds at full stick;
  - a zoom climb with thrust and drag off conserves energy within tolerance;
  - published figures each marked CALIBRATED or VALIDATION.

### Slice 1c — Seats and devices (W10, W15, W18, W27, W28)
- [ ] Player seat: devices sampled once per tick, at the tick boundary, through `DeepWater.Input` (one reader, many adapters). Action maps for keyboard and mouse, the Xbox pad and the VelocityOne. Extends the existing `VelocityOne.inputactions` without breaking the MissileRange and carrier adapters.
- [ ] Mouse-aim instructor: you aim the camera with the mouse, and it issues stick pitch and roll that bring the velocity vector onto the aim point. Throttle is untouched. Any stick axis or flight key overrides it at once (W15).
- [ ] Device arbitration: the last device moved past its deadzone owns the seat; the HUD shows which (W27).
- [ ] Seat swap plumbing (W18): a seat can be handed over mid-flight. A `HoldSeat` that keeps the last inputs stands in until RH Aviator's AI seat exists (Phase 3).
- [ ] Tests:
  - C18 virtual-device tests, one per device class: keyboard and mouse, an XInput pad, and a generic HID stick (`Joystick` layout with a hat);
  - the instructor converges on a fixed aim point at L0;
  - override takes effect within one tick;
  - **replay:** feeding the input log back reproduces the Fix bit for bit (W28).

### Slice 1d — The jet in Unity: adapter, art, camera, HUD
- [ ] `AirWingAircraft` adapter: pumps `AirWing.Step` from `FixedUpdate`; render interpolation (Pat5); truth feed (Sea State `ISeaSurface.Sample` under each aircraft, never HDRP water, AP11).
- [ ] The Air Wing F/A-18E prefab, built by an editor builder from a copy of the F-18E model (W30). No AerialArcade, NWH or vendor scripts on it (AP8). Control surfaces animated from the core's state, presentation only.
- [ ] Chase camera (W11), decoupled from the airframe so the mouse instructor can aim it.
- [ ] HUD (W11, W22): airspeed in kt, altitude in ft, AoA in degrees, G, Mach, throttle with the afterburner shown. Every number carries its unit; the HUD also names the active device, the seat and each part's level (W26, W27).
- [ ] Tests: unit conversions at the edge; HUD values match core state.

### Slice 1e — Dogfight rebuilt (W12, W13, W14) and the phase gate
- [ ] Back up first: copy `Assets/_SCENES/Dogfight.unity` to `Assets/_Archive/Dogfight_legacy/` (P8); git keeps history.
- [ ] Builder, Deep Water → Air Wing → Build Dogfight: overwrites `Dogfight.unity` in place, so its GUID survives (C24). It builds:
  - the Sea State sea, replacing the HDRP water;
  - a land runway at the sea's edge;
  - speed and altitude gates;
  - the DDG as a still landmark;
  - the Air Wing F/A-18E starting airborne (a runway start waits for L1, W29);
  - the input root and the HUD.
- [ ] Out of the scene: the `F-18 (1) Variant` (`FighterJetController`), the two switched-off FDS F-18Es, and the `RH_RadarModule` instance (Radar Suite doesn't use it here; the script stays for Rocket and SENTINAL).
- [ ] Archive `Assets/_SCENES/Rocket/FighterJetController.cs` and `Assets/_AIRCRAFT/F-18 (1) Variant.prefab` with checklist rows (W19), once the GUID scan shows nothing else references them.
- [ ] Play-mode check in the Editor, with each device class: fly the gates; the numbers agree with the tests; crashing into the sea registers. Labelled verified only once done in the Editor.
- [ ] Budget (W21): `unity-perf-ab`, one aircraft at L0 and twenty, in a player build. Target ≤ 0.01 ms per aircraft per fixed step. Record the A/B numbers in this file.
- [ ] METHODOLOGY: append what the phase taught, with provenance, to Candidates or Principles.
- [ ] *Done when:* all Air Wing tests pass; the replay test is bit-identical; Dogfight flies from all three device classes in the Editor; the budget is measured (met or reported as missed); the slice 0 and 1e archives carry their checklists.
- *Anti-goals for Phase 1:* no landing gear or ground handling; no 6-DOF; no AI pilot; no carrier; no weapons; no cockpit view; no helicopter; no change to the project's fixed timestep; no edits to vendor code or to the original F-18E model and prefab.

---

## Phase 2 — Jet L1 (outline)
- [ ] `Airframe` L1: 6-DOF rigid body with mass, inertia and aerodynamic coefficients from NASA's public F/A-18 data, scaled to the E (AUTHORED, W8).
- [ ] `FlightControls` L1: Hornet-style fly-by-wire (W23).
- [ ] `LandingGear` L1: struts, wheels, brakes and nosewheel steering through PhysX contacts (variant B). Runway start (W29), take-off and landing in Dogfight.
- [ ] `Powerplant` L1: spool time.
- [ ] Budget ≤ 0.05 ms per aircraft per step (W21).

## Phase 3 — AI pilots (outline; RH Aviator, W5, W17, W18)
- [ ] RH Aviator Phase 0 (its own roadmap): an AI seat against Air Wing's `ISeat` and `PilotInputs`; named, logged rules (C19).
- [ ] AI L0: waypoints, wingman formation, return to base with an approach and landing on the Dogfight runway.
- [ ] Switchable seats with a real AI seat in place of `HoldSeat`.
- [ ] AI L1: fighting for position against another jet, scored by time behind it. No weapons.

## Parked: carrier ops (W16)
Revisit when Phase 3 is done. The questions for SIX are in BLUEPRINT §7.

---

## Change log
- **2026-10-24** — Roadmap written with the blueprint (W1–W31). Nothing built.
- **2026-10-25** — Slice 0 done (63650de). Found: live audio in the legacy folder (moved to `_AIRCRAFT/Audio/`) and eight player-build blockers outside Air Wing (BLUEPRINT Q4).
