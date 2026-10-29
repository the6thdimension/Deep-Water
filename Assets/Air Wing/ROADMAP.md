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

**Where things stand (2026-10-27):** Phases 1, 2 and 3 are finished. The F/A-18E flies at L1 in Dogfight from every device class, takes off from and lands on runway 09/27, and costs 18 µs a step in a player against a 50 µs budget. The AI pilots (RH Aviator) fly routes, formation, the overhead break and the dogfight at up to eight aircraft; let go of the keyboard or pad and the jet holds level (W54). Next on SIX's word (line 42).

## DONE — finished and committed

Everything in this table is built, tested and on `main`. The detail behind each row is under *Slice records*, further down.

| Done | What you get | Date | Commit | Tests |
|---|---|---|---|---|
| Slice 0 — Clean-up | Legacy aircraft scripts archived with checklists; the player build compiles | 2026-10-25 | 63650de, 0d24231 | — |
| Slice 1a — Skeleton and contracts | The Air Wing module, Pilot Inputs, seats, the input log, the Fix, the fidelity ladder | 2026-10-25 | 44d5bfd | 19 |
| Slice 1b — The L0 jet, headless | Atmosphere, a point-mass airframe, engines, controls; the F/A-18E profile from public numbers | 2026-10-25 | 8b34492 | 33 |
| Slice 1c — Seats and devices | Xbox pad, flight stick, keyboard, mouse aim (the instructor), replay | 2026-10-25 | 75f836e | 45 |
| Slice 1d — The jet in Unity | The jet in a scene, chase camera, HUD, the flight-test scene | 2026-10-25 | d05ae0d | 49 |
| Slice 1e — Dogfight rebuilt | Sea, land and runway 09/27, four gates, the DDG landmark; the L0 budget met (2.7 µs) | 2026-10-25 | 22f5a2a0 | 59 |
| Reset after a crash | R or the Xbox View button restarts the flight (BLUEPRINT Q6) | 2026-10-26 | 608e1288 | 61 |
| Slice 2a — Airframe L1 | A 6-DOF jet on NASA's F/A-18 HARV flight data | 2026-10-26 | 59c9059c | 70 |
| Slice 2b — Fly-by-wire L1 | Hornet-style flight controls: G and roll-rate commands, the AoA and G limits | 2026-10-26 | 7c77b0e1 | 82 |
| Slice 2c — Engines L1, L1 in the scenes | Engine spool; the scenes fly L1; the L1 budget met (17 µs) | 2026-10-26 | 6edafea9 | 87 |
| Slice 2d — Landing gear L1 | Wheels on spring-damper struts over PhysX contacts, brakes, nosewheel steering, gear up and down, a runway start, take-off and landing; hard and gear-up landings crash | 2026-10-26 | 8343959b | 99 |
| Slice 2e — Phase 2 review | Every device class flown live at L1 in Dogfight; a landing on runway 09/27 in the scene; the budget re-measured with the gear, in the air and on the wheels; the phase written up | 2026-10-26 | a108970b | 103 |
| Slice 2d follow-up — the gear moves; softer struts | The model's gear retracts and extends with the core (legs fold, doors close); the struts stiffen at the end of travel, so a hands-off touchdown survives | 2026-10-26 | d9cedd4c | 103 |
| The arena, the spectator camera, explosions (W59–W64) | For RH Aviator's training squadrons: a jet that goes down respawns at its spawn point after 3 s, protected 5 s, with seeded jitter; jets inside 15 m both go down; a 40 km box. A spectator camera that follows any jet or flies free, and explosions for kills, collisions and impacts | 2026-10-29 | `3dde2e84` | 9 here (ArenaTests) |

## STILL TO DO

### Next
- **Phase 3 — AI pilots: COMPLETE (2026-10-27).** Next on SIX's word: RH Aviator's A9 (Q3) and the proposed slice 3f (the fight in the vertical); or the helicopter stream (W20); carrier ops stay parked (W16).

### Left open in finished slices
- **Fly by hand** from real devices. Every device class is proven live at L1 with virtual devices (slice 2e), and SIX has flown it with the pad and flown L1 by hand (2026-10-26); SIX's L1 feedback is still to be written in. A real VelocityOne, a real mouse, and take-off and landing by hand are not yet flown.
- **One fly-by-wire target missed** (slice 2b): G onset 1.2 s against 1.0. Reported, not fixed.
- ~~**The G-limit overshoot**~~ (8.08 G in the 2b test; 8.43 G live from a keyboard's instant full pull at about 470 kt, slice 2e): **accepted by SIX, 2026-10-26** ("g limit overshoot is fine"). Not a defect; see *Parked* for what it may feed.
- **Not built in the fly-by-wire:** the revised Hornet law's sideslip feedback to the ailerons, the differential stabilator, sensor models.
- **Landing gear (slice 2d), open:**
  - Not flown by hand: a runway start, take-off and landing from a real device. They are tested in EditMode and the take-off was seen in play from a scripted seat.
  - The 24 ft/s sink limit is AUTHORED: the figure is commonly quoted for Navy jets, but its source is not pinned.
  - The gear animation is AUTHORED off the model (it has no retraction clip). Stowed, a narrow slit stays open between the nose doors, and the struts do not compress on the model.

### ~~Phase 3 — AI pilots (RH Aviator)~~ done 2026-10-27
- ~~AI L0: waypoints, wingman formation, return to base with a landing on the Dogfight runway.~~ Done (3a–3c).
- ~~Switchable seats with a real AI seat.~~ Done (3b).
- ~~AI L1: fighting for position against another jet. No weapons.~~ Done (3d), up to eight aircraft.
- **The level hold for mouse and keyboard** (W54): done 2026-10-27.

### After Phase 2, in any order
- The helicopter (its own stream).
- Airframe L2: Mach effects and ground effect (L1's top speed is not right until then).
- L3: fuel burn and the centre of gravity moving with it; Powerplant L2–L3.

### Parked
- **Carrier operations:** waiting until Phase 3 is done (W16).
- **G-induced loss of consciousness (the pilot passing out):** SIX's idea, 2026-10-26: the fly-by-wire's G overshoot could feed a pilot model that greys out and blacks out under sustained G. Nothing decided; revisit when the pilot or the cockpit view is modelled.

---

# Slice records (the detail behind the two lists above)

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

### Slice 1a — Module skeleton and contracts (done 2026-10-25, 44d5bfd: 19/19 tests; player scripts compile)
- [x] Asmdefs `DeepWater.AirWing` (no references at all, so none to `DeepWater.GeneralQuarters`; a test reads the asmdef to keep it that way, W31) and `DeepWater.AirWing.Tests.Editor`. Folders `Core/`, `ScriptableObjects/`, `Tests/`. **`Modules/`, `Prefabs/` and `Examples/` come with their first content** (slices 1d, 1e). Git keeps no empty folders, so an empty folder's `.meta` would arrive without its folder.
- [x] `Core/Inputs/PilotInputs.cs`: stick pitch and roll, pedals, throttle 0 … 1 with an afterburner flag, `PilotSwitches` flags. `Clamped()` clamps device overshoot and throws on NaN or infinity (a seat bug, not an input). `Core/Inputs/Seat.cs`: `ISeat.Issue(tick, aircraftId, in state)` and `SeatKind` (Player, Ai, Hold, Scenario, Replay).
- [x] `Core/Inputs/InputLog.cs`: append-only, ticks in order, at most one entry per aircraft per tick (anything else throws); each entry records the seat; `TryGet(tick, aircraft)`.
- [x] `Core/State/AirframeState.cs` (C4): position, velocity, attitude, body rates, load factor, mass, fuel, engine spool, status, in-contact. SI units, Unity's axes.
- [x] `Core/Fidelity/FidelityLadder.cs`: `PartLadder<TProfile, TPart>`, one builder per built level. A request resolves to the highest built level at or below it and says so; a ladder with nothing at or below the request throws. `FidelitySheet` (baked) and `ScriptableObjects/FidelitySheetSO.cs` (W25).
- [x] `Core/Simulation/SubstepClock.cs` (W24): N fixed substeps (1 … 16) per tick, time from the tick count (never accumulated).
- [x] `Core/Simulation/FlightFix.cs`: FNV-1a over exact bits, aircraft in ascending id order, a contact flag per tick (W7); `FixLog.FirstDivergence`.
- [x] `Core/Simulation/DeterministicRng.cs`: SplitMix64 with counted draws (pattern copied from General Quarters, W31).
- [x] Tests (`Tests/Editor`, 19, all passing in the Editor):
  - banned-API source scan of `Core/`;
  - Air Wing never references General Quarters;
  - SplitMix64 reference value and reproducibility;
  - ladder fallback, no running above the request across a gap, nothing built throws, duplicates refused;
  - the sheet bakes every level, the substeps and the seed, and refuses 0 substeps;
  - substeps tile the tick, and time comes from the tick count. **Measured:** a float accumulator is 269 s off after 10⁶ ticks; the clock is exact;
  - the input log's order and uniqueness, overshoot clamped, NaN refused;
  - two seats issue through the same door;
  - the Fix is stable for identical states and sees one ulp of altitude; contact flagged; id order enforced; `FirstDivergence` names the tick.
- [x] Gate (AP24): player-script compile (StandaloneWindows64) has 0 errors, 92 assemblies, `DeepWater.AirWing` built; no warnings from Air Wing.

### Slice 1b — Airframe, Powerplant, Flight Controls and Atmosphere at L0, headless (done 2026-10-25, 8b34492: 33/33 tests)
- [x] `Core/Atmosphere`: the ISA (U.S. Standard Atmosphere 1976 to 32 km, clamped outside −610 m … 32 km), `AtmosphereL0`. Matches the standard's layer values (0, 11, 20 and 32 km).
- [x] `Core/Airframe/AirframeL0`: a point mass in a wind frame that rolls about the velocity.
  - Thrust minus drag (parasite + induced, plus flaps and speed brake) acts along the path.
  - Lift is the demanded load factor, clipped to what Cl_max allows at this dynamic pressure; gravity acts down; semi-implicit Euler.
  - The body is drawn α = C_L/C_Lα above the path. At L0, `BodyRates` are the path's wind-axis rates.
  - No Mach effect, ground effect or sideslip; the pedals do nothing at L0.
- [x] `Core/Powerplant/PowerplantL0`: idle to military power with the throttle, maximum afterburner with its switch, T = T_SL·σ^x; no spool, ram effect or fuel burn.
- [x] `Core/FlightControls/FlightControlsL0` (W23, W26): the stick commands n (neutral 1 g, linear to +7.5 / −3.0) and a roll rate, each through an exact discrete first-order lag.
- [x] `Core/Aircraft` and `AircraftFactory`: parts built through their ladders (L0 registered for all five; landing gear L0 is "none").
  - Surface check after every substep: meeting the sea or a runway with no gear is a crash, frozen in place (AP23).
  - `FlightCore`: the tick of BLUEPRINT §3.2 (seat → input log → substeps → Fix); `ISurfaceTruth` is the truth feed's ground.
- [x] **Renamed:** the profile is `AircraftProfileSO` (it carries every part's numbers), not `AirframeProfileSO`.
  - `ScriptableObjects/FA18E_AirWing.asset` stores public figures in their quoted units (lb, ft, lbf) and converts in `Bake()` (W22); every field has a provenance line.
  - `FA18E_FidelitySheet.asset`: all parts at L0, 4 substeps, seed 1442.
- [x] `Calibration~/F18E-SOURCES.md`: every number tagged PUBLIC, CALIBRATED, VALIDATION or AUTHORED.
  - **Official public figures** (Navy fact file 2021, Boeing, GE datasheet): span, two engines, 22,000 lb static thrust per engine, maximum takeoff weight, Mach 1.8+, 50,000+ ft.
  - **Conflicts kept, not hidden:** Boeing gives 17,000 lb per engine and Mach 1.6.
  - **AUTHORED:** wing area, empty weight, internal fuel, dry thrust and the G limits have no official page; they are AUTHORED, with the secondary sources' spread.
- [x] **Calibration (C16, C17):** one number. The thrust lapse exponent x = 1.2726, fitted in the mirror `Calibration~/l0_calibration.py` to the 50,000 ft service ceiling (empty + half fuel, maximum afterburner, climb speeds capped at Mach 0.9). The C# model reproduces it: **50,001 ft**.
- [x] **Validation (not fitted):** maximum level speed is Mach 1.41 at 40,000 ft and Mach 1.54 at sea level, against Mach 1.8+ (Navy) / 1.6 (Boeing). The residual is L0's missing Mach effects, a **known gap until L2**: it is too slow at altitude and too fast at sea level. C# matches the mirror to 0.01.
- [x] Tests (`AtmosphereL0Tests`, `FlightL0Tests`; 33/33 with slice 1a's):
  - the ISA table;
  - the factory builds L0 and says so when L2 is asked;
  - two runs give the same Fix on all 3,000 ticks, two jets, both airborne;
  - substeps 2 / 4 / 8 move a 13 km path by 11.6 m / 3.8 m (limits 1 % / 0.5 %, set before measuring), and the error shrinks with the substep;
  - trim holds 5,000 m and 200 m/s within 5 m and 0.5 m/s for 60 s;
  - a level turn at 60° bank turns at g√(n²−1)/V within 1 %, to the right;
  - full aft stick holds 7.5 g when fast and n = qS·CLmax/W when slow;
  - with thrust and drag off, a zoom climb keeps energy within 0.2 %;
  - full right stick rolls right at 95 % of the limit in 1 s, and the jet turns right;
  - a crash freezes on the surface;
  - the ceiling (CALIBRATED) and top speed (VALIDATION) match the mirror;
  - every profile field has a provenance line.
- [x] **Found while testing, the scenario not the model:** the first 60 s test manoeuvre rolled past 90° (0.6 stick plus the roll lag's tail) and spiralled into the sea at 2.3 g. A trace confirmed the physics; the script now rolls to about 60°.
- [x] Gate (AP24): player-script compile has 0 errors, 92 assemblies.

### Slice 1c — Seats and devices (W10, W15, W18, W27, W28) (done 2026-10-25, 75f836e: 45/45 tests)
- [x] **Deviation, with the reason:** Air Wing reads its devices from its own `Modules/Input/AirWingFlight.inputactions`, with **one action map per source** (Keyboard, Mouse, Gamepad, Stick).
  - The plan was to extend `VelocityOne.inputactions`. W27 (the last device moved owns the seat) needs each device's values on their own, and one action bound across devices mixes them.
  - The VelocityOne and the MissileRange and carrier adapters are untouched.
  - The joystick paths match `VelocityOne.inputactions` (stick y inverted, z twist, rz lever, buttons 2–4), so a stick reads the same here as elsewhere.
- [x] `Modules/Input/FlightDeviceReader` (plain C#): `Poll()` every frame gathers presses (XOR, so two presses between ticks flip twice) and mouse travel; `Read()` once per tick returns a `DeviceFrame`.
  - **Found while testing:** an `InputAction` reads 0 whenever its control sits at the default value, so a lever at mid-travel (raw 0) read as idle. The lever is now read from its control, and its first read syncs the seat to where the lever sits.
  - `PlayerSeatInput` (MonoBehaviour, execution order −60) owns the reader and the seat.
- [x] `Core/Seats/PlayerSeat` (pure, W27):
  - the last source moved past its deadzone owns stick and pedals (ties: the owner keeps it, else stick > pad > keyboard);
  - the mouse takes over only on a tick when nothing else moved, with the aim starting on the flight path, so nothing jumps; any flight axis or key overrides it on the same tick (W15);
  - one shared throttle lever, with the afterburner past a detent; switch toggles from any source flip the switch; `Owner` is exposed for the HUD.
- [x] `Core/Seats/MouseInstructor` (W15): rolls the lift toward the aim, pulls by the error in the lift plane, levels the wings within 5°, never touches the throttle. Gains AUTHORED in `PlayerSeatSettings.asset`; the idea is credited to MouseFlight (MIT).
- [x] `Core/Seats/HoldSeat` (W18, until the AI seat exists) and `ReplaySeat` (W28; a gap in the log throws). Hand-over is `FlightCore.SetSeat`.
- [x] Tests (45/45 with 1a and 1b):
  - `SeatTests`:
    - arbitration and override in one tick;
    - the mouse takes over with no jump;
    - the shared lever, the afterburner detent, and toggles from any source;
    - **the instructor brings the path from 60.5° off the aim to 0.06° in 20 s** at L0;
    - **replay reproduces the Fix bit for bit over 1,500 ticks**;
    - a seat changes hands mid-flight and the log keeps its shape;
    - a replay gap throws.
  - `DeviceBindingTests` (C18, `InputTestFixture`), one test per device class:
    - keyboard and mouse;
    - an XInput controller;
    - a generic HID flight stick (custom layout with a stick, z twist, rz lever and buttons).
    - Then the reader and the seat together: the stick overrides the mouse on the tick it moves.
  - **Found while testing:** Unity's stick deadzone on these sticks is per axis (0.125 … 0.925, rescaled): a raw 0.7 reads 0.71875. The tests now expect that, and the seat's own deadzone default is 0.05 on top of it, not 0.1.
- [x] Gate (AP24): player-script compile has 0 errors, 92 assemblies.
- *Not verified:* a physical VelocityOne, Xbox pad or mouse in the Editor. The bindings are proved with virtual devices only, and the lever's direction on the real stick may need `invertLever`. Play-mode flying comes in slice 1e.

### Slice 1d — The jet in Unity: adapter, art, camera, HUD, and a flight-test scene (done 2026-10-25, d05ae0d: 49/49 tests; flown in play mode with a virtual Xbox pad)
- [x] `Modules/Aircraft/AirWingRunner` (execution order −50): builds every `AircraftView`'s aircraft from the profile and the sheet, logs each part's level and any fallback, seats it (player or hold), and steps the `FlightCore` once per FixedUpdate.
- [x] `Modules/Aircraft/AircraftView`: the pose is interpolated between the last two ticks (Pat5; measured in play, the drawn jet trails the core by 0.5 to 4 m at about 210 m/s, one tick). Moving parts follow the last Pilot Inputs (presentation only). No Rigidbody, no vendor script (AP8).
- [x] `Modules/World/SceneSurface` + `RunwayStrip` (renamed `FlatGround` in slice 1e, in its own file): the truth feed's ground is the Sea State simulation's height (`ISeaSurface`, never HDRP water, AP11) or a runway's top; with no sea, a flat fallback that says so. The runner refuses a director that is not ready.
- [x] The Air Wing jet (W30): **Deep Water → Air Wing → Build F-A-18E Prefab** writes `Prefabs/FA18E_AirWing.prefab`, an `AircraftView` over an instance of the F-18E model (503 renderers); the model and the FDS prefab are only read.
  - `FA18E_Visuals.asset` (`AircraftVisualsSO`) maps 8 surfaces (ailerons, stabilators, rudders, flaps), copied from the FDS prefab's AerialArcade settings. The stabilators run on −stick pitch, because AerialArcade's pitch was positive pushing. Rudder travel AUTHORED at 30°.
- [x] `Modules/Cockpit/ChaseCamera` (W11): behind and above the jet, eased; looks along the mouse aim while the instructor flies.
- [x] `Modules/Cockpit/FlightHud` (W11, W22, W26, W27): TAS kt (no calibrated airspeed is claimed at L0), Mach, α °, G, ft, ft/min, heading, throttle % or AB, `SEAT PLAYER · <device>`, every part's level, a velocity marker and the aim ring.
  - **Decision (Claude, for review):** drawn with IMGUI. No assets, works at once; a UI Toolkit HUD can replace it once the layout settles. It allocates a little per frame.
  - `Core/Cockpit/HudReadout` does the conversions, pure and tested.
- [x] **Added at SIX's request (2026-10-25):** a bare flight-test scene. **Deep Water → Air Wing → Build Flight Test Scene** writes `Examples/Scenes/AirWing_FlightTest.unity`: the Sea State demo's sea, sky and sun, the player seat, the runner, one F/A-18E at 1,500 m and 400 kt, the chase camera and the HUD. How to fly it, and the controls, are in `README.md`.
- [x] Tests (49/49):
  - `CockpitTests`: HUD conversions (10,000 ft, 1,000 ft/min, 500 kt, heading, Mach) and the exact unit definitions;
  - `PrefabTests`: only Air Wing scripts and no Rigidbody on the jet; every moving part names a transform in the model.
- [x] **Play-mode smoke run** (Editor, flight-test scene, a virtual XInput pad added in play mode, C18's live check):
  - the seat went `None` → `Gamepad` on the tick the stick moved;
  - right stick banked right and turned right; the pull read 4.86 g, the stick-to-G map after Unity's deadzone;
  - the runner used the Sea State sea; the camera held 26.7 m; no Air Wing errors or warnings;
  - a screenshot shows the jet, the sea and the HUD (437 kt, M 0.67, α 11.0°, 4.9 G).
- [x] Gate (AP24): player-script compile has 0 errors, 92 assemblies; the editor assembly is not in the player.
- *Not verified:*
  - a physical Xbox pad or VelocityOne: SIX's flight test;
  - the stabilator deflection sign by eye.
- *Known:* the gear shows down in flight (the model's rest pose; animated with Landing Gear L1).

### Slice 1e — Dogfight rebuilt (W12, W13, W14) and the phase gate (done 2026-10-25: 59/59 tests; budget met in a player build, 2.7 µs per aircraft per step)
- [x] Back up first: `Assets/_SCENES/Dogfight.unity` copied to `Assets/_Archive/Dogfight_legacy/Dogfight.unity` (P8, a new GUID for the copy). The builder refuses to run while no copy exists.
- [x] Builder, **Deep Water → Air Wing → Build Dogfight** (`Editor/DogfightBuilder.cs`), overwrites `Dogfight.unity` in place: GUID `6981d1a2…` kept (C24, tested). It builds:
  - the Sea State sea, sky and sun, replacing the HDRP water;
  - land at the sea's edge (8 × 4 km, top at 8 m) with **runway 09/27** (2,438 × 61 m) along the coast. Both are truth-feed ground through `FlatGround`; meeting them is a crash at L0;
  - **four gates** flown in order, from `Examples/Dogfight/Dogfight_Gates.asset` (`GateCourseSO`, all AUTHORED): 5,000 ft at 350–450 kt; 1,000 ft over the DDG; a climb to 6,000 ft at ≥ 300 kt; a 500 ft pass along the runway at 250–350 kt. The next ring is orange, the HUD names it with its bands and marks it `[ n ]`, and it reports what the jet held through the last one. Each gate flown is logged;
  - the DDG-51 model (`General Quarters/Prefabs/Models/DDG51_GQ.fbx`) as a still landmark under gate 2: 16 renderers, no colliders, no scripts;
  - the Air Wing F/A-18E airborne at 5,000 ft and 400 kt heading 000, 2.5 km off the coast (a runway start waits for L1, W29); the player seat, the chase camera and the HUD (the rig shared with the flight-test scene, `AirWingBuilders.BuildRig`).
- [x] `Core/Gates/GateCourse.cs` (pure, in the banned-API scan): `Gate` (an upright ring flown one way), `Band` (any, at least, a range), `GatePass`. The path's crossing of the gate's plane is solved inside the tick, and the speed and altitude are taken there. The course only reads state, so it cannot change the flight or its Fix. `Modules/World/GateCourseView` draws the rings and feeds the course after the runner steps (order −40).
- [x] **Found and fixed:** slice 1d's `RunwayStrip` was a MonoBehaviour declared inside `SceneSurface.cs`, so a scene would have saved it as a missing script. It moved to its own file as `FlatGround`, which also serves land (METHODOLOGY C32).
- [x] Out of the scene: the `F-18 (1) Variant` (`FighterJetController`), the two switched-off FDS F-18Es, the `RH_RadarModule`, the AerialArcade waypoints, `Cat 1` and the old engine audio. `DogfightSceneTests` reads the saved scene: only Air Wing, Sea State and Unity scripts, none missing, one runner, one seat, one course, one sea, two pieces of ground, one Air Wing jet.
- [x] Archived (W19) to `Assets/_Archive/Dogfight_legacy/` with the C25 checklist (`README.md` there), GUIDs kept: `FighterJetController.cs` and `WaypointManager.cs` (from `_SCENES/Rocket/`; the second needs the first to compile), `WaypointManager (1).prefab` (unused), `F-18 (1) Variant.prefab`. GUID scan: nothing else references them.
- [x] Play-mode check per device class. **Closed 2026-10-26 by slice 2e:** every device class flown live in Dogfight at L1 with virtual devices (pad, HID stick, keyboard, mouse instructor). Before that, **what was proven:**
  - in EditMode, every device class through its bindings (`DeviceBindingTests`, slice 1c);
  - Dogfight's start, course asset and gate logic through the flight core (`GateCourseTests.TheDogfightStart_FliesThroughGate1`: gate 1 at 22.0 s, 479 kt, 5,000 ft, 0.0 m off centre);
  - SIX flew Dogfight in the Editor with a gamepad. Seen read-only: the seat owner was `Gamepad` at tick 372; no Air Wing errors in the log; no gate flown before SIX stopped.

  **Not run:** Claude's own play run in Dogfight with virtual keyboard, pad and mouse. The Editor was in use: General Quarters' range was open in it, apparently in play, and Claude's Stop at about 13:58 may have ended that run. After that, Claude left the Editor alone. Crashing into the sea registering is proven in EditMode (`FlightL0Tests.MeetingTheSurfaceWithNoGear_IsACrash_AndTheWreckStaysPut`), not yet seen in Dogfight.
- [x] **Finding, open for SIX (BLUEPRINT Q5, METHODOLOGY AP25):** the mouse instructor can fly the jet into the sea.
  - Aimed at each next gate, it dives at gate 2. It comes through sinking 93 m/s at 641 kt, pushes −2.7 G while rolling toward gate 3 behind it, and meets the sea 2.8 s later.
  - Lined up 3 km out, it flies gates 1–3, then meets the land short of gate 4.
  - With the dive limited to 8°, it still banks past 90° toward any aim below the horizon.
  - A heading-and-altitude-hold test pilot does not crash, but at 500 kt and 60° of bank it turns too wide to line up on gate 2. Flying the whole course is left to SIX and to the AI pilot (Phase 3).
- [x] **Budget (W21), unity-perf-ab.** The instrument is `Modules/Diagnostics/FlightBenchmark`: the flight core alone, N aircraft straight and level on hold seats, 500 warm-up ticks discarded, 5,000 ticks timed, the median of 3. It leaves out the sea sample, the views and the HUD.

  | Where | 1 aircraft | 20 aircraft | Budget |
  |---|---|---|---|
  | **Player** (Windows, Mono, release; i9-12900H; two runs) | **2.69, 2.70 µs** per step | **2.76, 2.73 µs** per aircraft per step (55.3, 54.6 µs per step) | ≤ 10 µs: **met** (×3.6) |
  | Editor (Debug code optimisation; two runs) | 8.24, 8.83 µs | 8.35, 8.72 µs per aircraft | not the budget's instrument |

  - The player is `Builds/AirWingBenchmark/` (git-ignored): **Deep Water → Air Wing → Build Benchmark Player**, then `AirWingBenchmark.exe -batchmode -nographics -airwingBenchOut <file>`. The first player build of the project took 16.5 min (HDRP shaders): 356 MB, 0 errors, 140 warnings.
  - Allocation is **not measured**: `GC.GetAllocatedBytesForCurrentThread` returns 0 in Unity's Mono, even for a 100 kB array, so the harness prints no figure rather than a false zero.
- [x] METHODOLOGY: AP25 (pure pursuit near the ground, as a pilot or an instructor) and C32 (one MonoBehaviour per file, and a scene test that fails on a missing script); C25 exercised again.
- [x] *Done when* (**Phase 1 closed 2026-10-26** at SIX's word, its last item met by slice 2e):
  - all Air Wing tests pass: **met**, 59/59;
  - the replay test is bit-identical: **met**, `SeatTests.ReplayingTheInputLog_ReproducesTheFixBitForBit`;
  - Dogfight flies from all three device classes in the Editor: **met** (slice 2e, live with virtual devices; SIX flew it with a gamepad). The stick and mouse by hand on real hardware are left to SIX;
  - the budget is measured: **met**;
  - the slice 0 and 1e archives carry their checklists: **met**.
- *Anti-goals for Phase 1:* no landing gear or ground handling; no 6-DOF; no AI pilot; no carrier; no weapons; no cockpit view; no helicopter; no change to the project's fixed timestep; no edits to vendor code or to the original F-18E model and prefab.

---

## Phase 2 — Jet L1 (W32–W35, locked 2026-10-26)

The gate: the F/A-18E flies at L1 (6-DOF, fly-by-wire, spool time) in Dogfight from each device class, takes off from and lands on runway 09/27, and can depart when abused (W33). Replay stays bit-identical in the air; ground phases are same-machine only (W7). One aircraft costs ≤ 0.05 ms per fixed step in a player build (W21).

### Before Phase 2: reset after a crash (BLUEPRINT Q6) (done 2026-10-26: 61/61 tests)
- [x] `PilotSwitches.Reset`: momentary, one tick. The aircraft returns to the state it was built with before that tick flies, crashed or not (`Aircraft.Start`, `Aircraft.Resets`). It travels in the Pilot Inputs, so the input log records it.
- [x] Keyboard **R** and the Xbox **View** button (`AirWingFlight.inputactions`); the stick has none bound, because stick buttons differ by model. Two presses between ticks are still one reset; two sources on one tick still reset.
- [x] The seat re-aims the mouse along the new flight path; the view and the chase camera snap rather than sweep; the gate course starts again at gate 1 and keeps its record; the HUD shows `R / VIEW BUTTON: RESET` under `CRASHED`.
- [x] Tests: `SeatTests.Reset_IsMomentary_FromAnySource_AndLeavesTheSwitchesAlone`; `SeatTests.AReset_PutsTheJetBackAtItsStart_EvenAfterACrash_AndTheReplayRepeatsIt` (crash from 300 m, reset, 200 ticks more, replayed with no divergence); R and View in `DeviceBindingTests`; `GateCourse.Restart`.
- *Not verified:* a reset pressed live in Dogfight.

### Slice 2a — Airframe L1: 6-DOF and the HARV model, headless (W32, W33) (done 2026-10-26: 70/70 tests)
- [x] **Source, found the hard way.** NASA TM-107601 describes the F/A-18 simulation model (structure, geometry, inertia in Table 3.5) but does not print its aero tables; they come from MDC A7247, which is not public. SIX re-decided W32 to the published HARV fits: Chakraborty, Seiler and Balas (*J. Guidance* 2011, Tables A1–A2), checked against the authors' public `F18AeroData.mat` and `f18full.m` (University of Minnesota, `F18Analysis.zip`).
- [x] `Calibration~/l1_harv_mirror.py` (C17): a line-for-line port of `f18full.m`, dependency-free, with its own trim (Newton) and RK4. At 500 ft/s and 25,000 ft the HARV trims at α 7.614°, stabilator −2.550°, thrust 4,018 lbf.
- [x] **Found in the published model:** `f18full.m`'s β̇ adds the thrust term T·cosα·sinβ/(mV) where body kinematics (Stevens and Lewis) subtract it. The mirror keeps the authors' line and checks it a second way (`betad_kinematic`); the two differ by exactly 2·T·cosα·sinβ/(mV), to seven digits. Air Wing derives its motion from body kinematics and does not inherit it. V̇ and α̇ agree to every digit.
- [x] `Core/Profiles/AirframeL1Profile` (the polynomials, reference geometry, inertia, α range) and `Core/Airframe/AirframeL1`:
  - the six coefficients term for term as the HARV model forms them, with α clamped to −10 … 60° for the polynomials (AUTHORED: the fits hold 0–60°);
  - wind-axis lift, drag and side force to body axes; Euler's equations with Ixz; thrust along the body; gravity down;
  - the NASA body frame (x forward, y right, z down) inside, Unity's outside; `BodyRates` holds Unity's local angular velocity, (−q, r, −p);
  - **the midpoint method**, two force evaluations a substep. Semi-implicit Euler gained 1.2 % of rotational energy in 5 s of torque-free tumbling at 200 Hz; the midpoint method gains 1.7 × 10⁻⁷.
- [x] `ControlDemand` carries `ControlSurfaces` (stabilator, aileron, rudder, in the HARV's sign convention). Flight Controls L0 sets none, so **Airframe L1 is not registered on the ladder yet**: it pairs with Flight Controls L1 in 2b. Until then the tests reach it directly.
- [x] The F/A-18E's L1 data: **Deep Water → Air Wing → Apply HARV L1 Data to F-A-18E** writes the polynomials, a mean chord of 12.0 ft (AUTHORED) and an inertia scaled as m·r² from TM-107601's HARV row to the E at 60 % fuel (40,721 lb; roll by span 44.9/37.42 ft, pitch and yaw by length 60.3/56 ft, Navy fact files): Ixx 40,482, Iyy 214,448, Izz 240,886, Ixz −4,693 slug·ft² (AUTHORED). The provenance names every field.
- [x] Tests (`AirframeL1Tests`, 9; 70/70 in all):
  - the coefficients at three points equal the mirror's to 2 × 10⁻⁶;
  - at the mirror's trim every derivative is still (V̇ 7 × 10⁻⁷ ft/s², α̇ and q̇ under 10⁻⁸);
  - at a skewed state (β 3°, α 10°, p, q, r, φ 20°, all three surfaces, 9,000 lbf) V̇, β̇ (corrected), α̇, ṗ, q̇, ṙ match to 2 × 10⁻⁴ relative;
  - a stabilator doublet tracks the mirror's RK4 for 3 s to 0.0007 ft/s and 4 × 10⁻⁷ rad;
  - C_L peaks at 1.698 near 38.2° and falls past it (a stall);
  - torque-free rotation keeps its angular momentum (7 × 10⁻⁶) and energy (2 × 10⁻⁷);
  - two runs are bit-identical; the polynomials are held flat outside their range; the E asset carries the paper's numbers.
- [x] **A trap on the way:** the doublet first missed by 0.08° of α. The cause was the test, not the physics: `(k − 1) · 0.005f` is 0.49999998 at the switch, so a float clock held the −2° one step too long. Scheduled by step index, the miss is 4 × 10⁻⁷ rad (METHODOLOGY AP26).
- *Known:* the bare HARV is statically unstable in pitch at low α (C_mα +0.51 per radian at α = 0): it needs the fly-by-wire (2b), as the real jet does. Not measured yet: the L1 cost per step (2c's budget).

### Slice 2b — Flight Controls L1: Hornet-style fly-by-wire (W23, W26, W33) (done 2026-10-26: 82/82 tests; two criteria missed and reported)
- [x] **Criteria fixed before tuning**, on the F/A-18E's own L1 data (`Calibration~/l1_fbw_mirror.py` measured each first):

  | Case | Criterion | Measured (C#; the mirror agrees to the third decimal) | |
  |---|---|---|---|
  | Hands off 10 s, 350 kt, 15,000 ft | \|Δn\| < 0.02 g, \|Δh\| < 30 m | 0.0047 g, 2.3 m | met |
  | 4 G step, 350 kt | 90 % within 1.0 s; overshoot ≤ 15 % | 1.20 s; 4.16 G (5 %) | **onset missed** |
  | Full aft stick, 500 kt, 10,000 ft | peak ≤ 8.0 G | 8.08 G | **missed by 1 %** |
  | Full aft stick, 160 kt | α ≤ 30° + 4° | 32.98° | met |
  | Half roll stick, 350 kt | p within 10 % at 1 s | 73.3 against 75°/s | met |
  | Rudder pulse | β after 4 s < 20 % of its peak | 0.26 % | met |
  | Roll into 60° of bank and pull 2 G | \|β\| < 2° | 1.06° | met |

  The two misses are guarded in the tests at their measured values and named as misses. The binding constraint is the stabilator's 40°/s rate limit (TM-107601): a faster G onset saturates it and overshoots. A stronger G integral holds G better but pushes the limit to 8.8 G.
- [x] **How the law was found, by elimination:**
  1. a fixed-gain PI law (G error, pitch-rate damping, gains scheduled on q̄): no set of a 27-point grid met the criteria; the AoA limiter overshot to 42°;
  2. nonlinear dynamic inversion (NDI) with the G error driving pitch rate: the lateral axis met every criterion at once, but pitch overshot (5.7 G for 4; 13 G at the limit), because G follows pitch rate only through α;
  3. **NDI commanding α**: invert the lift curve for the α that gives the commanded G, clamp it at the AoA limit, drive pitch rate from the α error about the path's turn rate *with the G the jet has now*. An early version used the commanded G there and spiked α to 47°.
- [x] `Core/FlightControls/FlightControlsL1`: NDI after Snell, Enns and Garrard (*J. Guidance* 15(4), 1992, designed on this HARV model):
  - pitch: the stick's L0 G map (W26), prefiltered (0.3 s); α command from the inverted lift curve less the stabilator's lift, plus a slow G integral (0.02 rad/(g·s)), clamped at **30°**; q̇ = 6·(q_cmd − q) inverted for the stabilator;
  - roll and yaw: p_cmd from the stick (150°/s at full); r_cmd = the coordinated-turn rate + p·tan α + 2·(β − β_cmd), the pedals commanding β (8° at full); both equations inverted together for aileron and rudder (bandwidths 5 and 3/s);
  - actuators PUBLIC (TM-107601 via the 2011 paper's Table 2): stabilator 30 rad/s, 40°/s, −24 … +10.5°; aileron 48 rad/s, 100°/s, −25 … +45°; rudder 40 rad/s, 61°/s, ±30°.
- [x] The law's state (surfaces, G integral, prefiltered command) is `AirframeState.Controls`: in the Fix, cleared by a reset. `IFlightControls.Demand` now takes the state by reference and the air data.
- [x] **The ladder pairs them** (C14): Airframe L1 and Flight Controls L1 build only together and only from a type with L1 data; otherwise both fall back to L0 and the resolution says why (`FidelityResolution.Reason`). `AircraftFactory.LevelStart(profile, sheet, …)` trims an L1 start (α, stabilator) by `AirframeL1.TrimLevel`. Nothing in a scene asks for L1 yet (2c).
- [x] **Found while tuning: the HARV fits have almost no drag at low α** (C_D 0.004 at α 0; the authors say their low-α data are thin). The E trimmed at 500 kt on 781 lbf. Airframe L1 now takes the larger of the fit and the L0 polar (C_D0 0.025 + K·C_L², AUTHORED, `dragFloorCd0`/`dragFloorK`); at 500 kt and 10,000 ft the trim thrust is 8,334 lbf. The pure-HARV mirror tests run with the floor off.
- [x] **W33, departure, confirmed:** at 120 kt, idle, 20,000 ft, full aft stick with full pedal and full opposite stick saturates all three surfaces; α reaches 63.6° (limit 30°) and |β| 35.5°, and the jet loses 394 m. At 160 kt the AoA limit holds but, with rudder and aileron saturated, β still reaches 21–30°.
- [x] Tests (`ControlsL1Tests`, 12; 82/82 in all): the actuator; the pairing and its reasons; the seven criteria; the departure; bit-identical Fix and replay at L1 through the flight core; the E asset carries the law and the floor. Player scripts compile: 0 errors, 81 assemblies.
- *Not done here:* differential stabilator, the revised law's β and β̇ to the aileron (k_β 0.5, k_β̇ 2), sensors (the law reads the state directly), the speed brake and flaps in the law. The law has not been flown by hand: L1 reaches the scenes in 2c.

### Slice 2c — Powerplant L1, L1 in the scenes, the budget (W21, W35) (done 2026-10-26: 87/87 tests; budget met, 17 µs per aircraft per step)
- [x] `Core/Powerplant/PowerplantL1`: the core spool (idle 0 … military 1) follows the throttle with a first-order lag, 1.2 s up and 0.8 s down; the afterburner lights only above 90 % core spool and fades in over 0.4 s, out over 0.25 s (all AUTHORED: no public F414 spool figures were found). L0's density lapse is kept. `AirframeState.AfterburnerSpool` joins the Fix. Powerplant L1 falls back to L0 when a type has no spool data, and says so.
- [x] **The scenes fly L1 (W35):** `FA18E_FidelitySheet.asset` asks for Airframe, Flight Controls and Powerplant L1; L0 stays one setting away. Dogfight and the flight-test scene read that sheet, so neither needed a rebuild. `AirWingRunner` starts an L1 jet trimmed (`AircraftFactory.LevelStart(profile, sheet, …)`), and the HUD's level line shows `Airframe: L1 …`. At L1 the moving parts follow the actuators, not the stick.
- [x] **Seen at runtime, hands off, in Dogfight:** the levels were L1 on all three parts; after 23.6 s the jet held 1.000 G, 37 m above its start, accelerating from 400 to 520 kt on the 89 % throttle; no seat owner, flying.
- [x] Tests (`PowerplantL1Tests`, 5; 87/87 in all): the spool's lags up and down; the afterburner lights at 2.77 s from idle (2.76 s predicted by the 90 % rule); the fallback without spool data; the scenes' sheet resolves to L1; **the mouse instructor flies the Dogfight start through gate 1 at L1** (22.0 s, 476 kt, 4,983 ft, 5.1 m off centre).
- [x] **Budget (W21): ≤ 50 µs per aircraft per fixed step at L1, player build, met.** unity-perf-ab, the instrument of slice 1e (Windows, Mono, release; i9-12900H; median of 3; two runs each):

  | | 1 aircraft | 20 aircraft |
  |---|---|---|
  | L1, first build | 35.65, 39.43 µs | 35.26, 34.60 µs per aircraft |
  | **L1, the lift-curve peak cached** | **16.60, 16.62 µs** | **17.14, 16.72 µs per aircraft** (×2.9 under budget) |
  | L0 (the same build) | 2.85–3.00 µs | 2.82–3.00 µs per aircraft |

  - The first L1 build re-scanned the lift curve for its peak (141 polynomials) on every substep. It depends only on the profile, so the law now finds it once: ×2.2, with every test's numbers unchanged (fidelity pinned).
  - Editor, Debug code: L1 44–50 µs, L0 4.5 µs (not the budget's instrument). Twenty L1 jets: 0.34 ms a step in the player.
  - The rebuilt player took 0.6 min with the shader cache warm (16.5 min the first time).
- *Not verified:* L1 flown by hand from any device; the reset at L1 by hand (it is tested in EditMode). Not built: Mach effects (L2), so L1's top speed is not yet right either.

### Slice 2d — Landing Gear L1: runway start, take-off and landing (W4, W29) (done 2026-10-26, 8343959b: 99/99 tests; a scripted take-off seen in Dogfight on PhysX)
SIX's picks: contacts as W4 says, with spring-damper struts; a brake button (Space, Xbox D-pad down, the stick's trigger).
- [x] **Contacts (W4):** the core asks `IContactProbe` for each wheel: a ray from the top of its strut down the strut axis, answered with the distance and the ground's normal. The scenes answer with PhysX raycasts (`PhysXContactProbe`) against the land's and the runway's colliders; tests and scenes without colliders use the truth feed's level ground (`SurfaceProbe`). The core stays free of the physics engine (the banned-API scan passes).
- [x] **`LandingGearL1`:** three wheels, each a linear spring and damper along its strut, with 0.3 m of travel. The tyre's rolling, braking and side friction each rise smoothly to μ·N over a 0.5 m/s slip. The pedals steer the nosewheel ±20°, fading out between 40 and 80 kt. The brakes work the mains. The gear comes up or down over 4 s and stays down with weight on the wheels. Gear drag ΔC_D 0.02. A wheel meeting the ground faster than 24 ft/s, or a strut driven through its travel, breaks the gear and ends the flight. Hard points (belly, tail, nose, wingtips) end it if they touch. `GearDeployed` joins the Fix; contact ticks were already flagged.
- [x] **Found and fixed while testing:**
  - The sink rate was first measured along the strut, which tilts with the jet: at 8° nose up and 150 kt it saw 10 m/s of forward speed and broke the gear on every take-off. It is now the tyre's speed along the ground's normal (METHODOLOGY AP29).
  - Flight Controls L1's lift inversion left out the flaps' ΔC_L, which the airframe adds, so flaps ballooned the jet. The law now subtracts it (METHODOLOGY C39).
  - The runway start began with the airborne start's throttle lever (0.80, military power), so the jet rolled off on its own. A ground start sets the lever to idle (`PlayerSeat.SetStart`); a held jet on the runway waits with its brakes on.
- [x] **Ground mode in the fly-by-wire:** on the wheels the stick moves the stabilator directly over its travel, the ailerons get half travel, the rudder follows the pedals, and the G law waits at 1 G.
- [x] **Runway start (W14: one setting):** the jet's `AircraftView` has **Start Mode** (Airborne by default, or Runway) and its runway. `AircraftFactory.GroundStart` puts it 150 m in from the runway's end, at its static strut compression, gear down, engines at idle. Dogfight's builder keeps colliders on the land and the runway (markings have none) and gives the jet runway 09-27.
- [x] **The F/A-18E's gear data** (`HarvL1Data`; all AUTHORED, listed in F18E-SOURCES): wheel positions off the model, with the centre of mass 1.6 m aft of the model's origin (`AircraftVisualsSO.modelOffsetM`); springs sized so the static weight takes about half the travel; dampers about half critical.
- [x] **HUD:** `GEAR DOWN / UP / nn %`, `WOW` (weight on wheels) and `BRAKES`; after a crash, why.
- [x] **Tests** (`LandingGearL1Tests`, 10; one brake-binding test; one seat test; 99/99 in all):

  | Test | Measured |
  |---|---|
  | A ground start settles | at rest in 10 s, level (−0.11°), the nose takes 13.5 % of the weight, the struts carry 100.0 % |
  | Take-off in afterburner, rotating at 150 kt | lift-off 441 m from brake release at 181 kt; 9.7° on the wheels at most (a tail strike is about 15°) |
  | Landing on a 3° path, flaps down | touchdown at 3.6 m/s (11.9 ft/s against 24); the brakes stop it in 1,049 m; on the centreline |
  | A 9° dive onto the runway | the gear breaks: nose gear at 11.4 m/s (37 ft/s) |
  | A gear-up landing | the belly hard point meets the runway: a crash |
  | Gear transit and the weight-on-wheels lock | half down at 2 s, down at 4 s, up again in 4 s; it will not come up on the ground |
  | Nosewheel steering at 17 kt, full right pedal | turns right, 7.1° in the first second, 202° in 8 s |
  | On the ground the core is bit-identical, and the input log replays it | no divergence |
  | The PhysX probe against a box collider | distance to 1e-4 m, the normal, out of reach, off the edge |

- [x] **Follow-up, SIX's asks (2026-10-26):**
  - *Softer struts:* SIX chose to soften the hands-off touchdown. Each strut now stiffens over its last 30 % of travel, as an oleo's air spring does: past 0.7 of the travel the spring adds 30 × k × (x − x₀)² / (L − x₀), so at full travel a strut pushes 10× a linear one. A hands-off touchdown now survives: the nose slaps at 15.8°/s and its strut takes 86 % of its travel. The 9° dive still breaks the gear, and the firm landing is unchanged (11.9 ft/s, 1,050 m).
  - *The gear moves on the model:* `GearRig` (Modules/Aircraft) turns 19 parts of the F-18E's gear from the core's `GearDeployed`, interpolated between ticks. The legs fold over the first 75 % of the retraction, and the doors close over the last 30 %; lowering runs it backwards. The model had gear bones but no retraction clip, and the vendor rig named only two parts. So each hinge was solved off the geometry and checked in captures. The nose leg folds forward 105°; each main leg swings aft, up and in to lie its wheel flat in the bay its doors cover; each door turns about its own long edge until it lies over the bay. The legs, braces and door links hide once stowed, behind the closed doors. Values are in `HarvL1Data.GearRig()`, data in `FA18E_Visuals.asset`.
  - Tests (103/103): the end stop's formula; the hands-off touchdown; every gear part found and gear down equal to the rest pose; with the gear up, nothing visible below the closed doors (lowest −0.86 m in the model's frame, the right inner door's thick skin). Seen in play in Dogfight: the airborne jet starts with its gear stowed; on gear down the doors open, the leg swings, and it ends on the model's rest pose.
- [x] **Seen at runtime in Dogfight** (Start Mode set to Runway for the run, then the scene reloaded from disk): at idle the jet held its strut compression on the PhysX runway and crept at 3–4 kt (idle thrust beats rolling friction; the brakes hold it). A scripted seat then took off in afterburner: rotation at 150 kt, lift-off near 183 kt, about 470 m from brake release, the gear up above 80 m, climbing out. The player-script compile passes.

### Slice 2e — Phase 2 review (done 2026-10-26, a108970b: 103/103 tests; the gate met, by scripted and virtual pilots)
- [x] **Every device class, live in Dogfight at L1** (play mode, virtual devices added at runtime, one after another, each held 3 s; C18's live check, the item slice 1e left open):

  | Device | What it did | Seen |
  |---|---|---|
  | Xbox pad, left stick right | took the seat (`Gamepad`) | rolled at 150°/s, the L1 roll limit |
  | HID flight stick, stick back | took the seat from the pad (`Stick`) | pulled 7.5 G |
  | Keyboard, S then D | took the seat (`Keyboard`) | pulled 8.43 G (a G-limit overshoot, see *Left open*); rolled at 150°/s |
  | Mouse, moved right | the instructor took the seat (`Mouse`) | flew to the aim |
  | Keyboard G | flipped the gear switch while the mouse held the seat | gear 75 % down after 3 s |

  - **Found (METHODOLOGY AP30):** with the Editor unfocused, as it is when a session drives it, the virtual keyboard and mouse read nothing; the pad and the stick still worked. The Input System resets keyboards and mice when the application loses focus (its default background behaviour). The keyboard and mouse were rerun with an in-memory copy of the input settings set to ignore focus, restored afterwards; the settings asset was not touched. A player focused on the Game view is not affected.
- [x] **Take-off and landing on runway 09/27 in Dogfight** (PhysX contacts, scripted seats): take-off seen in slice 2d (lift-off near 183 kt, about 470 m from brake release). Landing: put on a 3° final at 145 kt with gear and flaps down, the pilot held the path and the centreline, touched down at 3.7 m/s (12.2 ft/s against 24), lowered the nose, braked, and stopped 1,048 m on, on the centreline and on the runway.
- [x] **Budget (W21) re-measured with the gear**, unity-perf-ab's instrument (`FlightBenchmark`, now with a ground case: N jets parked on their wheels, brakes on at half throttle, every wheel in contact on every substep). Player (Windows, Mono, release; i9-12900H; median of 3; two runs):

  | Case | 1 aircraft | 20 aircraft | Budget |
  |---|---|---|---|
  | L1, airborne (gear stowed) | 17.84, 18.15 µs | 17.82, 18.13 µs per aircraft | 50: **met** (×2.8) |
  | L1, on the wheels, PhysX contacts | 13.32, 12.89 µs | 13.79, 13.09 µs per aircraft | 50: **met** |
  | L1, on the wheels, the truth feed's ground | 10.86, 11.00 µs | 10.61, 10.84 µs per aircraft | 50: **met** |
  | L0 | 3.27, 3.23 µs | 3.10, 3.14 µs per aircraft | 10: **met** |

  - Against slice 2c, the airborne L1 cost rose from 16.6–17.1 to 17.8–18.2 µs: Landing Gear L1's step (its transit, and the hard-point check over five points instead of one). On the wheels it is cheaper than flying, because the aero is small at low speed; PhysX's twelve raycasts per aircraft per step add about 2.5 µs.
  - The player rebuilt in 0.1 min (302 MB, 0 errors).
- [x] **What L1 superseded (C25):** nothing first-party. L1 adds levels and L0 stays one setting away on the fidelity sheet (W35). The vendor's AerialArcade F-18E (`FDS Assets/Aircraft/F-18E.prefab`) is superseded as the jet Air Wing flies, but it stays where it is: `CVN-78 FORD.unity` still uses it, it is vendor content (read-only), and carrier operations are parked until Phase 3 (W16). Revisit it with the carrier stream.
- [x] METHODOLOGY: AP30 (driving keyboard and mouse input in an unfocused Editor); the unity-mcp-workflow skill gains it as trap 12.
- [x] *Done when* (the Phase 2 gate):
  - the F/A-18E flies at L1 in Dogfight from each device class: **met** with virtual devices, live; real devices by hand are left to SIX;
  - it takes off from and lands on runway 09/27: **met** in the scene, by scripted pilots;
  - it can depart when abused (W33): **met** (slice 2b: 120 kt at idle, α 63.6°, β 35.5°);
  - replay is bit-identical in the air and same-machine on the ground (W7): **met** (`ControlsL1Tests.AtL1_TheFlightCore_IsBitIdentical_AndReplays`, `LandingGearL1Tests.OnTheGround_TheCoreIsBitIdentical_AndReplays`);
  - one aircraft costs ≤ 0.05 ms per fixed step in a player: **met**, 18 µs airborne, 13 µs on the wheels.
- *Anti-goals for Phase 2, held:* no Mach effects or ground effect (L2); no fuel burn or moving centre of gravity (L3); no AI pilot; no carrier; no weapons; no cockpit view; no helicopter; no vendor code edited; no change to the project's fixed timestep.

## Phase 3 — AI pilots — COMPLETE 2026-10-27 (RH Aviator, W5, W17, W18; W36–W54; tracked in RH Aviator's roadmap, W48)
The AI work is tracked in `Assets/RH Aviator/BLUEPRINT.md` and `ROADMAP.md` (W48, written 2026-10-26), against W36–W51 here; the pass criteria for each slice are fixed there. Proposed slices, Pilot L0 then Pilot L1 (W49), each tested headless and then smoked in Dogfight (W50):
- [x] **3a** (W51), done 2026-10-26: RH Aviator's AI seat, the shared flight-path layer (W37), the mode machine with logged rules (W36) on a 4 Hz watch (W44); the AI flies Dogfight's four gates at L1. **Air Wing side:** `Core/Seats/FlightPathHands` (the hands, A2, an L1 path-following law, not the instructor's `Steer`), `FlightCore.Picture` (A1) and `FlightCore.Surface`, `CoreBannedApiTests.Scan` made public; `FlightPathHandsTests` (4). Detail in `Assets/RH Aviator/ROADMAP.md`.
- [x] **3b** (W40, W42), done 2026-10-27: an AI wingman joins and holds fingertip; the player swaps seats with it mid-flight. **Air Wing side:** `Core/Seats/SeatRoster` (one player seat, each aircraft's own seat, RH Aviator A8), `PlayerSeat.TakeOver`, `ISeatStatus`, the `CycleSeat` action (Tab, the Xbox D-pad right), the chase camera and HUD following the player's aircraft, the HUD's pick list; the hands take feed-forward and `Resume`, and their throttle integral is held to ±1 (RH Aviator AP33); `SeatRosterTests` (4). Detail in `Assets/RH Aviator/ROADMAP.md`.
- [x] **3c** (W41, W47), done 2026-10-27: the AI goes home on the player's order (RH Aviator's own `AviatorOrders`, A3) or at the bingo stand-in and lands on 09/27 after the overhead break. **Air Wing side:** `FlightPathRequest.Landing` (the ground guard stands down for a landing). Detail in `Assets/RH Aviator/ROADMAP.md`.
- [x] **3d**, Pilot L1 (W38, W43 at eight, W45, W46, W52, W53), done 2026-10-27: the fight on what each pilot sees, late; rookie, line, ace; red hunts the player; rear-cone scoring. No weapons. **Air Wing side:** `FlightPathRequest.GLimit` and `BankLimitDeg`. Detail in `Assets/RH Aviator/ROADMAP.md`.
- [x] **W54, the level hold** (SIX 2026-10-27: mouse and keyboard "can't hold level flight"), done 2026-10-27: keyboard or pad centred 0.4 s → the jet flies level on the heading and height it had, on the AI's bounded hands (the instructor first: let go at 121° of bank it dived to −16° and wandered 6,000 ft, AP25); the HUD shows LEVEL HOLD; the mouse aim snaps to the horizon within 2° once the mouse stops; the throttle stays the player's; settings in `PlayerSeatSettings.asset`; `LevelHoldTests` (3): let go at 121° of bank, level in 15 s, then within 10 ft and 0.5° for a minute.
- [x] **3e**: Phase 3 review, done 2026-10-27. **PHASE 3 COMPLETE.** Detail in `Assets/RH Aviator/ROADMAP.md`.

## Parked: carrier ops (W16)
Revisit when Phase 3 is done. The questions for SIX are in BLUEPRINT §7.

---

## Change log
- **2026-10-24** — Roadmap written with the blueprint (W1–W31). Nothing built.
- **2026-10-25** — Slice 0 done (63650de). Found: live audio in the legacy folder (moved to `_AIRCRAFT/Audio/`) and eight player-build blockers outside Air Wing (BLUEPRINT Q4).
- **2026-10-25** — W25–W31 locked by SIX. Slice 1a done (44d5bfd): the core contracts, 19/19 tests.
- **2026-10-25** — Slice 1b done (8b34492): the L0 flight core and the F/A-18E profile; ceiling CALIBRATED (50,001 ft), top speed VALIDATION residual recorded; 33/33 tests.
- **2026-10-25** — Slice 1c done (75f836e): seats, arbitration, the mouse instructor, replay and the device reader; 45/45 tests.
- **2026-10-25** — Slice 1d done (d05ae0d): the jet in Unity, the chase camera, the HUD and, at SIX's request, the flight-test scene; 49/49 tests; flown in play with a virtual Xbox pad.
- **2026-10-25** — Slice 1e done: Dogfight rebuilt in place by its builder, with land, runway 09/27, four gates and the DDG landmark; the old scene and the dead jet archived; the budget met in a player build (2.7 µs per aircraft per step, ×3.6 under); 59/59 tests. Found: the mouse instructor flies into the sea when aimed low (BLUEPRINT Q5), and `RunwayStrip` would have saved as a missing script (fixed as `FlatGround`). Not done: a live Dogfight run from the stick and the mouse. Stopped at SIX's word.
- **2026-10-26** — SIX: Q6 reset (done, 61/61), Q5 no floor; Phase 2 begun with W32–W35 and slices 2a–2e.
- **2026-10-26** — Slice 2a done: Airframe L1 on the published HARV model, the midpoint method, the E's L1 data; 70/70 tests. Found: TM-107601 has no aero numbers (W32 re-decided by SIX) and the published f18full.m has a sign slip in β̇.
- **2026-10-26** — Slice 2b done: Flight Controls L1 by nonlinear dynamic inversion commanding α, paired with Airframe L1 on the ladder; the drag floor; 82/82 tests. Missed and reported: G onset (1.2 s against 1.0) and the G limit (8.08 against 8.0). W33's departure confirmed.
- **2026-10-26** — Slice 2c done: Powerplant L1 (spool), the scenes fly L1, the budget met in a player (17 µs per aircraft per step after caching the lift peak, ×2.2); 87/87 tests.
- **2026-10-26** — Roadmap split into DONE and STILL TO DO at SIX's request (e322f5a7).
- **2026-10-26** — Slice 2d done (8343959b): Landing Gear L1 on spring-damper struts over PhysX contacts, brakes, nosewheel steering, a runway start, take-off and landing; 99/99 tests. Found and fixed: the sink rate measured along a tilted strut, the flaps missing from the law's lift inversion, the runway start's throttle. Open for SIX: a hands-off touchdown breaks the nose gear.
- **2026-10-26** — Phase 3 slice 3a done (RH Aviator): the AI pilot flies Dogfight's four gates; Air Wing gains the hands and the picture; 113/113 tests in both modules (`92eaaaff`).
- **2026-10-26** — SIX: Phase 1 closed (its live device check met by 2e); the G-limit overshoot accepted, and G-LOC parked as an idea. Phase 3 interview, round 1: W36–W39 locked, all as Claude leaned. Round 2: W40–W43 locked; W41 (the overhead break) is SIX's pick over Claude's straight-in leaning. Round 3: W44–W47 locked, all as Claude leaned. Round 4: W48–W51 locked, all as Claude leaned; the interview is complete. Proposed slices 3a–3e.
- **2026-10-26** — Slice 2e done (a108970b): **Phase 2 complete.** Every device class flown live at L1 in Dogfight; a landing on runway 09/27 in the scene; the budget met with the gear (18 µs airborne, 13 µs on the wheels on PhysX, per aircraft per step in a player); nothing first-party superseded. Found: an unfocused Editor reads no keyboard or mouse (AP30); a keyboard step pull overshoots the G limit to 8.43 G.
- **2026-10-26** — (d9cedd4c) Slice 2d follow-up at SIX's word: the struts stiffen over their last 30 % (a hands-off touchdown survives) and the model's gear retracts and extends with the core (`GearRig`, hinges solved off the geometry); 103/103 tests.
- **2026-10-27** — Phase 3 slice 3b done (RH Aviator): formation and the seat cycle; Air Wing gains the seat roster, the cycle key, the pick list and a camera and HUD that follow the player; 122/122 tests in both modules (`a2ae171d`).
- **2026-10-27** — Phase 3 slice 3c done (RH Aviator): going home and the overhead break; Air Wing gains `FlightPathRequest.Landing`; 125/125 tests in both modules.
- **2026-10-27** — Phase 3 slice 3d done (RH Aviator): the fight at up to eight aircraft; Air Wing gains per-request G and bank limits; 130/130 tests in both modules.
- **2026-10-27** — W54 done: the level hold for keyboard and pad, the mouse aim's level detent; 134/134 tests in both modules.
- **2026-10-27** — Phase 3 complete (RH Aviator 3a–3e and W54). Next for Air Wing: on SIX's word (carrier ops are parked, W16; the helicopter is its own stream, W20).
- **2026-10-29** — The arena (W59–W61), the spectator camera (W62) and explosions (W63), built for RH Aviator's learned-pilot training (SIX: two squadrons that fight each other to train, respawning at points opposite each other). `ArenaTests` 9/9: a jet shot down is back 150 ticks (3 s) later at its slot, whole; protected 5 s (no hits on it, its trigger dead); jitter within ±300 m / ±15° and repeatable from its seed; head-on jets at one height both go down (40 m apart they pass); out of the box down and back; a four-jet run repeats Fix for Fix with one input entry per jet per tick. Air Wing gains `FlightCore.UseArena`, `Aircraft.Respawn` (internal), `SeatKind.Learned` (W64, proposed), `SpawnPoint`, `AirWingArena`, `ArenaSO`, `SpectatorCamera`, `ExplosionView`, `AircraftVisualsSO.explosion`, and the builders' public `BuildRunner`, `BuildSpectatorCamera` and `DogfightBuilder.BuildWorld` (Dogfight builds as before).
