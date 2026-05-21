# Carrier Ops

Carrier-operations framework for Deep Water — the bridge, deck, catapults, JBDs, elevators, and motion model that make a carrier scene a credible sim instead of a static prop.

Same architectural template as Guided Fury (the reference implementation of methodology principle P6):

- **`CarrierProfileSO`** (authoring) → bakes to **`CarrierProfileData`** (unmanaged struct).
- **`CarrierEntity`** is pure C# — no Unity scene deps. Holds state, runs subsystem cycles on `Step(dt)`.
- **`CarrierBehaviour`** is the MonoBehaviour adapter — pumps the entity on `FixedUpdate`, mirrors state to the transform, drives Animator triggers on the model's shuttle and JBD children.

## Subsystems

| Folder | Purpose |
|---|---|
| `Core/State` | `CarrierState` (live state) + `CarrierProfileData` (baked profile) + per-subsystem state structs |
| `Core/Motion` | Sea-state-driven sway. `ISeaStateMotion` + `SumOfSinesMotion` |
| `Core/Movement` | `ShipKinematics` — speed, heading, turn rate dynamics with realistic Ford-class limits |
| `Core/Catapult` | `CatapultCycle` state machine: Idle → Spotted → Tensioned → Ready → Firing → Retracting |
| `Core/Elevator` | `ElevatorCycle` state machine: Stowed → Moving → Deployed |
| `Core/Carrier` | `CarrierEntity` (pure-C# aggregate) + `CarrierBehaviour` (MB adapter) |
| `ScriptableObjects/Profiles` | `CarrierProfileSO` (Ford-class defaults) |
| `Examples/Scripts` | `CarrierLaunchControlPanel` — in-play IMGUI control panel |
| `Examples/Editor` | Scene-tooling editor scripts (e.g. CVN-78 migration) |
| `Tests/Editor` | EditMode tests: motion determinism, catapult cycle, ship kinematics, entity integration |

## What's modeled (Phase 1 + 2)

- **Movement.** Throttle → speed (first-order lag, ~10 min from stop to full). Rudder → turn rate (scaled by speed — no rudder authority at zero speed). Position integrated on FixedUpdate.
- **Sea state.** Sum-of-sines heave / roll / pitch with deterministic seed (P7). Beaufort 0–6 visual fidelity. Calm / FreshBreeze presets.
- **Catapult cycle.** Six-stage state machine, profile-timed. Firing stage applies acceleration sized to reach `EndSpeed` over `Stroke`, clipped by `PeakG` for realistic overweight clipping. Aircraft Rigidbody is set kinematic during firing, then released with the end-of-run velocity.
- **JBD (Jet Blast Deflector).** Slaved to catapult stage — raises on Tensioned, lowers on Retracting.
- **Elevators.** Simple linear [0..1] travel between stowed and deployed positions.
- **FLOLS (Phase 2).** Optical landing system geometry — given an approaching aircraft, compute glideslope deviation and produce a normalized ball offset [-1..+1]. Wave-off flag when deviation exceeds threshold. Drives a ball-transform's local Y position on the lens housing.
- **Arresting wires (Phase 2).** 4-wire state machine (Idle / Engaged / Decelerating / Retracting). Engagement is proximity-based: hook tip within profile-configured radius of a wire centerline, hook down. Decelerating stage applies constant G-load along the aircraft's velocity vector until the aircraft stops OR runout reaches the wire stroke length.
- **TailhookHook companion.** A small `MonoBehaviour` on the aircraft that implements `IRecoveringAircraft` against any Rigidbody + hook tip transform — works with the AerialArcade F-18 without modifying it.

## What's intentionally NOT modeled yet

- **Bolter / wave-off return logic** — if the aircraft misses all four wires, it just keeps going. Bolter doctrine requires an AI flight model that goes around; Phase 3+.
- **Wind-over-deck** — apparent wind from ship velocity + ambient wind. Affects launch energy and recovery glideslope.
- **Bow wave / stern wake VFX** — driven by speed; would tie to the HDRP water surface.
- **Hull hydrodynamics** — sideways drift, current, leeway. Heading == ground track for now.
- **Twin-shaft differential thrust** — single throttle, no per-shaft control.
- **Spawning aircraft on elevators** — `spawnFromHangar` is legacy stub; the air-wing system is bigger than this module.
- **Stabilised FLOLS** — real lens stabilises against ship pitch so the ball doesn't wander with sea state. Phase 3+.
- **Hydraulic AAG curve** — wire deceleration is constant G in Phase 2; AAG actually delivers a profiled deceleration. Phase 3+.
- **Wire selection logic** — closest wire wins on multi-wire crossings; fleet doctrine prefers the 3rd wire. Phase 3+.

## Trying it (Phase 1 + 2)

1. **Author a profile.** Project window → right-click → Create → Carrier Ops → Carrier Profile. The defaults are Ford-class realistic.
2. **Add a CarrierBehaviour** to your carrier root (or replace the legacy CarrierController via the migration command — see below).
3. **Assign the profile** to the behaviour's `Profile` slot.
4. **Populate Catapult Slots.** One entry per real catapult. For each: assign `ShuttleAnimator`, `JbdAnimator`, `ShuttleStart` and `ShuttleEnd` transforms, and (optionally) `AttachedAircraft` Rigidbody.
5. **Populate Elevator Slots.** For each elevator transform, assign `StowedLocalPosition` (record the elevator's current local position) and `DeployedLocalOffset` (where it goes when deployed).
6. **(Phase 2) Assign FLOLS slots.** `FLOLS Reference` is a transform marking the lens housing on the port side of the angled deck. `FLOLS Ball Transform` is the cosmetic ball — its local Y is driven by ball offset. Optional `FLOLS Cut Lights` GameObject is toggled active on wave-off.
7. **(Phase 2) Populate Wire Slots.** One slot per arresting wire; assign a `WireCenterline` transform at each wire's deck position.
8. **(Phase 2) Add a `TailhookHook` to your aircraft.** Assign the aircraft's Rigidbody and a hook tip transform. Press `H` in play to toggle the hook down. The companion auto-registers with the nearest CarrierBehaviour.
9. **Add the `CarrierLaunchControlPanel`** to any GameObject in the scene (e.g. the Main Camera). Press Play.

## Keyboard (control panel)

| Key | Action |
|---|---|
| `C` / `V` / `B` / `N` | Fire catapults 1 / 2 / 3 / 4 |
| `W` / `S` | Throttle up / down |
| `Q` / `E` | Rudder left / right |

The panel also exposes click-to-fire buttons, throttle / rudder sliders, and per-elevator deploy/stow toggles.

## Running the tests

Window → General → Test Runner → EditMode → Run All. The `CarrierOps.Tests.Editor` suite covers:

- **MotionTests** — determinism across runs, calm-sea zero output, amplitude envelope.
- **CatapultCycleTests** — Idle ignores Step until requested, full cycle returns to Idle, end speed matches profile, JBD slaving, PeakG clip on overweight launch.
- **ShipKinematicsTests** — speed approaches max under full throttle, decel to zero, rudder produces heading change, no rudder authority at zero speed, position advances forward at speed.
- **CarrierEntityTests** — same scenario produces same state (determinism), catapult-launch request begins cycle, elevator request flips command, time-of-sim accumulates correctly.
- **FlolsTests (Phase 2)** — on-glideslope produces zero ball, high produces positive ball, low produces negative, saturation at window edge, behind-FLOLS produces no track, lateral offset is ignored.
- **ArrestingGearTests (Phase 2)** — Idle ignores step, RequestEngage latches into Decelerating, deceleration brings aircraft to a stop, stroke overrun releases the aircraft mid-motion, full cycle returns to Idle, second engagement request on an active wire is rejected.

Pure C# — no scene loaded.

## Migration from the legacy `CarrierController`

The legacy `CarrierController.cs` (under `Assets/FDS Assets/Naval Vessels/CVN-78/Scripts/`) lives until you swap it. Run **Deep Water → CVN-78 → Migrate Carrier to New Framework** to get a checklist-style report on what to wire up.

## Methodology cross-references

- **P1** (SO-driven config) — all tuning in `CarrierProfileSO`.
- **P2** (FixedUpdate determinism) — `CarrierBehaviour.FixedUpdate`. Verified by `CarrierEntityTests.Entity_ProducesSameStateAcrossIdenticalRuns`.
- **P3** (`Core/Modules/SO/Prefabs/Examples` layout).
- **P5** (vendor read-only) — no vendor deps.
- **P6** (pure-C# core + MB adapter) — `CarrierEntity` (pure) + `CarrierBehaviour` (adapter). Same template as Guided Fury.
- **P7** (deterministic RNG via SO) — sum-of-sines motion uses `SeaState.Seed`.
- **Pat1** (SO-Profile + Pure-Core + MB-Adapter) — second system to adopt the pattern, validating it across domains.
- **AP2 avoided** — no physics in `Update()`. All work on FixedUpdate.
