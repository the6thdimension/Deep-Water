---
name: guided-fury-authoring
description: "Extend the Guided Fury missile framework: add a new weapon profile, guidance law, seeker, aero/autopilot/thrust subsystem, integrator LOD, or range target/launcher. Use when building anything new in Assets/Guided Fury, or when the user names a real weapon to author (Sidewinder, AMRAAM, Harpoon, SM-2, Hellfire, Tomahawk). Covers the SO-Profile + Pure-Core + MB-Adapter pattern, the interface+factory+kind-enum shape, the programmatic asset-builder editor command, and the regression-test template."
allowed-tools: Bash, Read, Write, Edit, Glob, Grep
---

# Guided Fury Authoring

How to add to the missile framework without breaking its patterns. Guided Fury is the reference implementation of the project's Pat1, so hold the line here.

## The load-bearing pattern (Pat1): SO-Profile + Pure-Core + MB-Adapter

Three layers, always:
1. **`MissileProfileSO`** (`ScriptableObjects/Profiles/`) — every tuning value as a serialized field. `.Bake()` returns an unmanaged `MissileProfileData` struct. **No magic numbers in code — they live here.**
2. **Pure-C# core** — `MissileEntity` owns `MissileState` and steps the sim via `entity.Step(dt)`. Guidance, seeker, and fuze read `entity.State`, never the transform. This layer is testable without Unity.
3. **`MissileBehaviour`** — the MonoBehaviour adapter: pumps `entity.Step(Time.fixedDeltaTime)` in FixedUpdate, mirrors state to the transform, and (Pat5) renders an interpolated pose in Update.

## Adding a new axis of variation — interface + factory + kind-enum (Pat3)

Every pluggable subsystem follows the same three-edit shape, so the compiler flags a missing case:
1. Implement the interface (stateless singleton where possible): `IGuidanceLaw`, `ISeeker`, `IAeroModel`, `IAutopilot`, `IThrustModel`.
2. Add a value to its `*Kind` enum (`GuidanceLawKind`, `SeekerKind`, `AeroModelKind`, `AutopilotKind`, `ThrustModelKind`). **Adding values is non-breaking; removing is not.**
3. Add the `case` to the factory (`GuidanceFactory`, `SeekerFactory`, `AutopilotFactory`, `ThrustModelFactory`).
4. If the subsystem needs new tuning, add fields to `MissileProfileSO` **and** the `MissileProfileData` struct **and** the `.Bake()` mapping **and** `TestStub()`. Thread any new value through `MissileBehaviour.BuildTargetSource` / integrator construction as needed.

Worked example this repo already has: `BlendedPursuitProNav` (guidance), `SeekerCoastTimeS` + `SeekerMidcourseDatalink` (seeker plumbing through all four points above).

## LOD ladder

`MissileLod`: L0 Kinematic, L1 PointMass3Dof, L2 RateLimited3Dof, L3 PseudoRb6Dof, L4 FullAero6Dof. `CreateIntegratorForLod` maps them; `HighestImplementedLod` gates the auto-downgrade banner. Bump it when you implement a new tier.

## Authoring a real weapon (Pat4): programmatic editor command, not hand-authored YAML

Follow `EssmProfileBuilder` (menu `Guided Fury/Authored Missiles/Build RIM-162 ESSM Profile`):
- A `[MenuItem]` editor command that sets every field from **public reference data as source code, with citation comments** (mass, dimensions, motor burn, top speed, range, g-rating; Mach-dependent aero as programmatic `AnimationCurve`s).
- Prompt before overwriting an existing asset; rerun resets to authored defaults (known-good baseline on a fresh checkout).
- Wire detonation FX + blast on the profile (aerial + ground explosion prefabs, `blastRadiusM/blastImpulseNs/blastUpwardsModifier`).
- Roadmap weapons already scoped: AIM-9X, AIM-120D, AGM-84 Harpoon (needs `seaSkimAltitudeM`), BGM-109 Tomahawk (needs GPS waypointing / `IGuidanceSensor`), AGM-114 Hellfire, RIM-66 SM-2.

## Detonation & emplaced ordnance

Detonation is shared via `DetonationEffects` (`Core/Missile/`): `SpawnExplosionVisuals` + `ApplyBlastImpulse`. Reuse it for non-missile ordnance (see `DemolitionCharge`) — don't duplicate the FX/blast code, and don't wrap a full flight sim around a stationary boom.

## Range targets & launchers

`WeaponsRangeSectorBuilder` builds the multi-weapon range additively (idempotent per sector). Targets are auto-discovered by `TargetSelector` via `HittableBox` — any new target with one integrates for free. Launchers are auto-discovered by `MissileControlPanel` via `GuidedFury_TestRunner` — add a runner and it gets a Fire button. Movers use `WaypointMover` (Pat5, `[DefaultExecutionOrder(-50)]` so missiles sample exact sim poses).

## Always: a regression test

Add a deterministic pure-core test to `Tests/Editor/` (see `missile-sim-diagnosis` for the harness). Run the EditMode suite (`GuidedFury.Tests.Editor`) and keep it green — know your baseline (2 failures pre-date the guidance work and are tracked in `ROADMAP.md`; don't attribute them to your change without a stash-bisect).
