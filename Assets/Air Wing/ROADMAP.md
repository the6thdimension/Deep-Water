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

**Status (2026-10-25): Phase 1 done (slices 0 and 1a–1e), except one item.** The F/A-18E flies at L0 in the rebuilt Dogfight and in the flight-test scene (see `README.md`). The budget is met in a player build. Not done: a live run of Dogfight from the stick and the mouse. Phase 2 (jet L1) started 2026-10-26: W32–W35 locked, the reset (Q6) done; Q5 answered (no instructor floor).

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
- [~] Play-mode check per device class. **What is proven:**
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
- [~] *Done when:*
  - all Air Wing tests pass: **met**, 59/59;
  - the replay test is bit-identical: **met**, `SeatTests.ReplayingTheInputLog_ReproducesTheFixBitForBit`;
  - Dogfight flies from all three device classes in the Editor: **partly met**. Bindings are proven in EditMode; SIX flew it with a gamepad; Claude's live run was not done, and the stick and the mouse have not been flown live in Dogfight;
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

### Slice 2a — Airframe L1: 6-DOF and the NASA tables, headless (W32, W33)
- [ ] Source: NASA TM-107601's F/A-18 aero tables and inertias, transcribed with page references into `Calibration~/` with a Python mirror (C17), then scaled to the E (AUTHORED, every factor named).
- [ ] `Core/Airframe/AirframeL1`: a rigid body in body axes; quaternion attitude; Euler's equations with the full inertia (Ixz); forces and moments from the tables over α and β, the surfaces and the body rates; thrust along the body.
- [ ] `ControlDemand` gains surface deflections; a direct law (stick to surfaces) is enough for 2a's tests. The ladder pairs Airframe L1 only with Flight Controls ≥ L1, and says so when a sheet asks otherwise.
- [ ] Tests: trim at several speeds and heights; the short-period and Dutch-roll modes against the linearised tables; a stall at Cl_max; energy with thrust and drag off; the Fix bit-identical; the banned-API scan.

### Slice 2b — Flight Controls L1: Hornet-style fly-by-wire (W23, W26)
- [ ] G-command pitch, roll-rate roll, AoA and G limiters, a yaw damper; structure after NASA's HARV control-law papers, gains AUTHORED. The stick keeps its meaning from L0 (W26).
- [ ] Tests: step responses (G and roll rate), the limiters holding, a departure when abused at the corners (W33).

### Slice 2c — Powerplant L1, L1 in the scenes, the budget (W21, W35)
- [ ] Spool-up and spool-down time constants (AUTHORED from public figures where they exist).
- [ ] Dogfight and the flight-test scene switch to L1; L0 stays on the sheet. The HUD names the level.
- [ ] Budget ≤ 0.05 ms per aircraft per step in a player build.

### Slice 2d — Landing Gear L1: runway start, take-off and landing (W4, W29)
- [ ] Struts, wheels, brakes and nosewheel steering through PhysX contacts (variant B); ground phases flagged in the Fix.
- [ ] A runway start on 09/27; take-off and landing tests.

### Slice 2e — Phase 2 review
- [ ] Each device class in Dogfight at L1; the budget; METHODOLOGY; archive what L1 supersedes.

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
- **2026-10-25** — W25–W31 locked by SIX. Slice 1a done (44d5bfd): the core contracts, 19/19 tests.
- **2026-10-25** — Slice 1b done (8b34492): the L0 flight core and the F/A-18E profile; ceiling CALIBRATED (50,001 ft), top speed VALIDATION residual recorded; 33/33 tests.
- **2026-10-25** — Slice 1c done (75f836e): seats, arbitration, the mouse instructor, replay and the device reader; 45/45 tests.
- **2026-10-25** — Slice 1d done (d05ae0d): the jet in Unity, the chase camera, the HUD and, at SIX's request, the flight-test scene; 49/49 tests; flown in play with a virtual Xbox pad.
- **2026-10-25** — Slice 1e done: Dogfight rebuilt in place by its builder, with land, runway 09/27, four gates and the DDG landmark; the old scene and the dead jet archived; the budget met in a player build (2.7 µs per aircraft per step, ×3.6 under); 59/59 tests. Found: the mouse instructor flies into the sea when aimed low (BLUEPRINT Q5), and `RunwayStrip` would have saved as a missing script (fixed as `FlatGround`). Not done: a live Dogfight run from the stick and the mouse. Stopped at SIX's word.
- **2026-10-26** — SIX: Q6 reset (done, 61/61), Q5 no floor; Phase 2 begun with W32–W35 and slices 2a–2e.
