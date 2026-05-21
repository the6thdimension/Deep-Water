# Guided Fury

Modular, LOD-switchable missile / munition framework for Deep Water.

The design goal: simulate any bomb, rocket, or missile in this project at any fidelity from a kinematic point all the way to full 6DOF aerodynamics, **with the same authored asset**. A designer authors a missile profile once; per-launch the engine picks the LOD that fits the scenario.

This is also the first system in the project built to current standards under the [McMahon Simulation Methodology](../../METHODOLOGY.md).

## LOD ladder

| LOD | Name | Models | Use case |
|---|---|---|---|
| L0 | Kinematic trajectory | Position + cruise-speed (or ballistic) | Distant salvos, cinematic |
| L1 | 3DOF point mass | + gravity, drag, thrust along velocity | Far engagements, large salvos |
| L2 | 3DOF + rate limit | + g-limited turn rate, realistic guidance constraints | Most non-hero engagements |
| L3 | Pseudo-6DOF | + full rigid-body orientation, simplified aero | Close engagements, hero missiles without 6DOF |
| L4 | Full 6DOF | + aero coefficient tables, autopilot, atmosphere coupling | Hero missile, AAR replay |
| L5 | HWIL fidelity | + seeker dynamics, INS drift, fuze detector models | R&D, seeker tuning |

**Current implementation: L0, L1, L2, L3 (Phase 4).** Full rigid-body orientation with body-axis thrust, angle-of-attack lift, weather-vane stability, and an inline rate autopilot. Guidance (Pursuit / ProNav), cone-seeker acquisition, g-limit / turn-rate limit. L4+ ships in subsequent phases.

## Architecture

```
GuidedFury/
  Core/
    State/         MissileState, MissileCommand                          (universal interchange structs)
    Atmosphere/    IAtmosphere, AtmosphereSample, StandardAtmosphere     (ISA model)
    Integrators/   MissileLod, IPhysicsIntegrator,
                   KinematicL0Integrator, PointMass3DofL1Integrator,
                   RateLimited3DofL2Integrator, PseudoRb6DofL3Integrator (physics per LOD)
    Guidance/      IGuidanceLaw, GuidanceContext, GuidanceFactory,
                   ITargetSource, TargetTrack, TransformTargetSource,
                   NullGuidanceLaw, PursuitGuidance,
                   ProportionalNavigation                                 (guidance + targeting)
    Damage/        IHittable                                              (Phase 2.5 minimal damage seam)
    Missile/       MissileProfileData, MissileEntity, MissileBehaviour   (aggregate + adapter)
    Seekers/       ISeeker, SeekerKind, SeekerProfile, SeekerFactory,
                   SimpleConeSeeker, SeekerTargetSource                   (FOV/range-based acquisition)
    Autopilot/     (Phase 5 — L4 inner loop)
    Fuzing/        (Phase 3)
  Modules/         (out-of-band extensions: countermeasures, telemetry)
  ScriptableObjects/
    Profiles/      MissileProfileSO                                      (per-missile data)
    Atmospheres/   (Phase 4+)
    LodPolicy/     (Phase 6+)
  Prefabs/         (one per missile, profile pre-assigned)
  Tests/
    Editor/        IntegratorTests, GuidanceTests, EntityTests           (EditMode tests, no scene)
  Examples/
    Scripts/       GuidedFury_TestRunner.cs                              (L0/L1 demo runner)
    Scenes/        (assemble manually — see "Trying it" below)
    Profiles/      (sample SO assets, to come with L3)
```

### The three-layer pattern

Per methodology **Pat1** (SO-Profile + Pure-Core + MB-Adapter):

1. **`MissileProfileSO`** — authored asset, lives in `ScriptableObjects/Profiles/`. Designer-facing.
2. **`MissileEntity`** — pure C# class, no Unity dependencies. The simulation lives here. Testable without a scene.
3. **`MissileBehaviour`** — `MonoBehaviour` adapter. Pumps the entity on `FixedUpdate`. Mirrors state to transform.

`MissileProfileSO` bakes into `MissileProfileData` (unmanaged struct) at launch; the entity carries that struct copy. Live edits to the SO do not retroactively change in-flight missiles.

### Universal state struct

`MissileState` is the lingua franca for every integrator. Every LOD reads and mutates the same struct. A higher LOD can pick up a lower-LOD state mid-flight and continue without translation. (Mid-flight LOD switching itself ships in Phase 6+, but the state contract is already designed for it.)

### Why `FixedUpdate` only

Per **P2** (deterministic physics on FixedUpdate). The MonoBehaviour adapter only ticks on `FixedUpdate`; the integrator only reads `dt`, never `Time.deltaTime`. Two runs at different frame rates produce the same engagement outcome. The legacy system's anti-pattern (AP2 — physics in `Update`) is the diagnosis this rule was written against.

## The Missile Range scene

The fastest path to seeing Guided Fury work end-to-end: generate the canonical test range.

**Build it:** `Guided Fury → Build Missile Range Scene` from the Unity menu.

The scene the command creates:

- **Ground plane** (10 km × 10 km, muted green) at world Y = 0.
- **Distance markers** along +Z at 100 m, 250 m, 500 m, 1 km, 2 km, 5 km — each is a pole + sign + TextMesh label so you can eyeball range during flight. 1 km marks are yellow; intermediate marks white.
- **Static boxes** scattered downrange (orange, ~200 kg each). They take an impulse on impact and tumble — they don't disappear, so the result of each hit stays visible.
- **A crate stack** near 750 m (lighter 50 kg crates) — fun to topple.
- **Two moving targets** (cyan cubes) at 1.5 km and 3.0 km, drifting sinusoidally across the firing axis at different speeds. The 1.5 km one is slow, the 3.0 km one fast — exercises both Pursuit and ProNav lead behavior.
- **Launcher** at the origin, with `GuidedFury_TestRunner` pre-wired to the default profile, L1 LOD, slight upward initial pitch, 1 s launch delay.
- **LodComparisonLauncher** at +15 m on X, with `LodComparisonRunner` configured to fire one missile per LOD (L0–L3) in a color-coded salvo. **Disabled by default** — enable in the Inspector to fire the 4-missile comparison instead of (or alongside) the single launcher.
- **HUD overlay** on the Main Camera: a top-left panel listing every active missile with name, LOD, phase, time-of-flight, speed, range-to-target, and lock state.
- **Camera controller** on the Main Camera. Starts in Overview mode; auto-chases the first missile that launches. `F` toggles Chase / Overview; `Space` cycles to the next active missile when chasing.
- **Control panel** (top-right): on-demand fire buttons for every launcher in the scene, time scale slider with 0.1× / 0.5× / 1× / 2× / 5× presets, pause/resume, and "reload scene" — all without leaving Play mode.

The command also creates `Assets/Guided Fury/Examples/Profiles/Range_Default.asset` if it doesn't already exist. ProNav, gain 3, 22 kN boost for 3 s, 0.5 s safe-and-arm delay, 8 m proximity fuze.

**Run it:** open the scene (`Assets/Guided Fury/Examples/Scenes/MissileRange.unity`), press Play. The single launcher fires once after a brief delay; the camera latches onto the missile and chases it; the HUD shows live telemetry; the missile impacts the first box / crate in its path.

**To make it home:** drag any target Transform (a box, moving target, etc.) into the `Target` slot on `Launcher` → `GuidedFury_TestRunner` in the Inspector. The missile homes via ProNav.

**To compare LODs side-by-side:** in the Hierarchy, enable `LodComparisonLauncher`. On press Play, it fires one missile per LOD simultaneously — yellow (L0), green (L1), blue (L2), magenta (L3). Each missile carries a colored trail so you can see their flight paths and compare them.

**In-Play controls:**

| Key | Action |
|---|---|
| `L` | Fire one missile from the single-missile launcher |
| `K` | Fire the LOD comparison salvo |
| `1` / `2` / `3` / `4` / `5` | Time scale: 0.1× / 0.5× / 1× / 2× / 5× |
| `P` | Pause / Resume |
| `R` | Reload the scene |
| `F` | Toggle camera Chase / Overview |
| `Space` | Cycle to next active missile (Chase mode) |

The control panel in the top-right has buttons for every action — keyboard shortcuts are mirrors, not replacements.

---

## Trying it (Phase 2, L0 + L1 + guidance)

1. **Create a profile asset.** In the Project window: right-click → Create → Guided Fury → Missile Profile. Name it `Test_L1_Homer`. Defaults are sane (250 m/s cruise, 3 s boost, ProNav with gain 3).
2. **Make a scene.** Open `OutdoorsScene` (or any scene with open space).
3. **Add a target.** Create an empty GameObject `Target` and place it ~1 km away. Optionally attach a `Rigidbody` or a simple movement script if you want a moving target.
4. **Add a runner.** Create an empty GameObject `GF_TestRunner`. Add the `GuidedFury_TestRunner` component.
5. **Assign:** profile → your profile asset; target → the Target GameObject; LOD → `L1_PointMass3Dof`. Leave Missile Prefab empty.
6. **Press Play.** The runner spawns a primitive stand-in, boosts forward, and homes on the target via ProNav.

### What to vary

- **LOD** on the runner: switch between `L0_Kinematic` (cheap, animation-tier) and `L1_PointMass3Dof` (real physics with drag and gravity). Same profile, different fidelity.
- **Guidance law** on the profile: `None` (unguided ballistic), `Pursuit` (tail-chase baseline), `ProportionalNavigation` (the workhorse).
- **Navigation Gain**: ProNav typically 3..5. Low gain = lazy turns; high gain = aggressive but oscillation-prone.
- **Drag Coefficient** / **Reference Area**: increase for a draggier round; the missile decelerates faster after burnout.
- **Boost Thrust** / **Boost Duration**: tune for short-burn high-thrust vs long-burn cruise behavior.
- Move the target during flight — ProNav will lead it; Pursuit will lag behind it.

## Running the tests

Open Unity → Window → General → Test Runner → EditMode tab → "Run All". The `GuidedFury.Tests.Editor` suite covers:

- **IntegratorTests** (L0 + L1) — straight-line cruise, ballistic arc, max-lifetime; L1 gravity-only fall, boost acceleration, linear mass burn, boost→cruise phase transition, drag deceleration.
- **L2IntegratorTests** — g-limit clips excessive command, turn-rate limit binds at low speed, small commands pass through unclipped, base physics regression check.
- **L3IntegratorTests** — boost regression, body-axis thrust accelerates pitched missile, weather-vane reduces AoA, no-torque drift check, AoA produces lift, autopilot tracks command, end-to-end L3 + ProNav intercept.
- **GuidanceTests** — Pursuit / ProNav both handle `HasTrack=false`; Pursuit commands toward off-axis target; ProNav zero command head-on; ProNav lateral accel off-axis; ProNav leads moving targets.
- **SeekerTests** — cone seeker acquires in FOV after dwell; rejects outside FOV; rejects out of range; respects acquisition time; drops lock when target exits FOV; SeekerTargetSource gates observation correctly; end-to-end L2 + ConeSeeker + ProNav closes on target.
- **EntityTests** — Detonated state freezes the entity; deterministic trajectory; homing closes range; no target = ballistic flight.

All tests run without loading a scene. This is the payoff of P6.

## What's intentionally missing in Phase 4

- **Signal-aware seekers** — the cone seeker still doesn't distinguish IR vs radar vs SARH. Phase 5+.
- **Gimbaled seekers / lock hysteresis** — body-fixed and instant-break-lock for now.
- **Aero coefficient tables** — L4 (next physics tier). Cd and Cl are scalars; no Mach dependence.
- **Control surfaces** — L4. The L3 autopilot applies torque directly, pretending the airframe has perfect attitude control.
- **Extractable IAutopilot interface** — L4 will pull the L3 inline autopilot out into a swappable contract once we have multiple autopilot strategies worth differentiating.
- **Pooling, effects, damage system beyond IHittable impulses** — Phase 5+.
- **Mid-flight LOD switching** — Phase 6+ (state struct already supports it).
- **Authored hero missile profiles** — first one targeted for the post-Phase-4 polish pass.

## Methodology cross-references

This module exercises and validates:

- **P1** (SO-driven config) — all tuning in `MissileProfileSO`.
- **P2** (FixedUpdate determinism) — `MissileBehaviour.FixedUpdate`. Determinism verified by `EntityTests.Entity_DeterministicTrajectory_SameInputsProduceSameOutputs`.
- **P3** (`Core/Modules/SO/Prefabs/Examples` layout) — yes.
- **P5** (vendor read-only) — no vendor deps.
- **P6** (pure-C# core + MB adapter) — `MissileEntity` (pure C#) + `MissileBehaviour` (adapter). The EditMode test suite proves the value: every test constructs entities directly without a scene.
- **P7** (deterministic RNG via SO) — N/A at L0/L1 (no randomness yet); will be exercised at L5.
- **Pat1** (SO-Profile + Pure-Core + MB-Adapter) — the three-layer pattern.
- **C4** (universal state struct) — `MissileState`. Round-trip from L0 to L1 demonstrated; promotion to Principle deferred until the full L0→L4 round-trip is proven.

The legacy code archived to `Assets/_Archive/Guided Fury_legacy/` is the diagnosis source for anti-patterns **AP1–AP4** in METHODOLOGY.md. Read it as a counter-example, not a foundation.
