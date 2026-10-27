---
name: guided-fury-authoring
description: "Extend the Guided Fury missile framework: add a new missile archetype profile (SR-IR missile, MR-radar missile, area SAM, anti-ship or land-attack cruise missile, SR air-to-ground missile), guidance law, seeker, damage, integrator LOD, or range target/launcher. Use when building anything new in Assets/Guided Fury, or when asked to author a specific missile: author it as its game-level archetype with AUTHORED numbers. Covers the game-level grade, the SO-Profile + Pure-Core + MB-Adapter pattern, the interface+factory+kind-enum shape, the programmatic archetype-builder editor command, and the regression-test template."
allowed-tools: Bash, Read, Write, Edit, Glob, Grep
---

# Guided Fury Authoring

How to add to the missile framework without breaking its patterns. Guided Fury is the reference implementation of the project's Pat1, so hold the line here.

## The grade: video-game level, generic (SIX, 2026-10-27)

New work is planned and built at game level (`Assets/Guided Fury/ROADMAP.md`, "The grade"), inside the safe limits (`Assets/General Quarters/BLUEPRINT.md` §6, standing constraint 8). Before building, check the item against both.
- **Archetypes, not real weapons.** A new profile is named for its role (SR-IR missile, area SAM) and every number is AUTHORED, with a one-line reason in the provenance. No real designators, blocks or variants, no real-system performance tables or citations. A request that names a real weapon is authored as its archetype, and the reply says so.
- **Guidance is textbook** (pursuit, PN with an authored gain). **A seeker is a cone, a range and a lock chance.** No seeker, datalink, fuze, warhead or autopilot internals.
- **Damage is abstract:** a lethal radius, an AUTHORED kill chance per target class, hit points (ROADMAP Stream B). Nothing derived from warhead or fragment data.
- **New archetypes fly at L0–L2.** L3 and L4 stay as built and are not extended.
- **Stays as built:** the one real-system profile, the L4 stack and their tests are listed in ROADMAP "Stays as built" (S1–S8). They keep working; never take them as a template for new fidelity.
- **Definition of done is gameplay-facing:** what the player sees, how long an engagement takes to resolve, and that it resolves the same at any frame rate and on every run with the same seed.

## The load-bearing pattern (Pat1): SO-Profile + Pure-Core + MB-Adapter

Three layers, always:
1. **`MissileProfileSO`** (`ScriptableObjects/Profiles/`) — every tuning value as a serialized field. `.Bake()` returns an unmanaged `MissileProfileData` struct. **No magic numbers in code — they live here.**
2. **Pure-C# core** — `MissileEntity` owns `MissileState` and steps the sim via `entity.Step(dt)`. Guidance, seeker, and fuze read `entity.State`, never the transform. This layer is testable without Unity.
3. **`MissileBehaviour`** — the MonoBehaviour adapter: pumps `entity.Step(Time.fixedDeltaTime)` in FixedUpdate, mirrors state to the transform, and (Pat5) renders an interpolated pose in Update.

A vehicle module that carries missiles (Air Wing, General Quarters) builds the entity headless and steps it on its own tick; it never reimplements missile behaviour (module lines, SIX 2026-10-27).

## Adding a new axis of variation — interface + factory + kind-enum (Pat3)

Every pluggable subsystem follows the same three-edit shape, so the compiler flags a missing case:
1. Implement the interface (stateless singleton where possible): `IGuidanceLaw`, `ISeeker`, `IAeroModel`, `IAutopilot`, `IThrustModel`.
2. Add a value to its `*Kind` enum (`GuidanceLawKind`, `SeekerKind`, `AeroModelKind`, `AutopilotKind`, `ThrustModelKind`). **Adding values is non-breaking; removing is not.**
3. Add the `case` to the factory (`GuidanceFactory`, `SeekerFactory`, `AutopilotFactory`, `ThrustModelFactory`).
4. If the subsystem needs new tuning, add fields to `MissileProfileSO` **and** the `MissileProfileData` struct **and** the `.Bake()` mapping **and** `TestStub()`. Thread any new value through `MissileBehaviour.BuildTargetSource` / integrator construction as needed.

Worked example this repo already has: `BlendedPursuitProNav` (guidance), `SeekerCoastTimeS` + `SeekerMidcourseDatalink` (seeker plumbing through all four points above). The planned lock chance (ROADMAP A2) goes through the same four points, with its draw from the run's seeded source (P7) and logged.

## LOD ladder

`MissileLod`: L0 Kinematic, L1 PointMass3Dof, L2 RateLimited3Dof, L3 PseudoRb6Dof, L4 FullAero6Dof. `CreateIntegratorForLod` maps them; `HighestImplementedLod` gates the auto-downgrade banner and stays at L4: L5 is killed (ROADMAP K1), and `L5_HardwareInTheLoop` stays an unbuilt value. New archetypes are authored for L0–L2; at L3 or L4 they fly on `AeroModelKind.Simple`, never on new tabulated curves.

## Authoring an archetype (Pat4's builder shape): programmatic editor command, not hand-authored YAML

Use the shape of the existing builder, `EssmProfileBuilder` (menu `Guided Fury/Authored Missiles/...`; it stays as built, ROADMAP S1), not its data:
- A `[MenuItem]` editor command per archetype that sets every field from **AUTHORED game numbers as source code, each with a comment giving its reason** (top speed, boost and sustain, flight time, g limit, navigation gain, seeker kind, cone, range and lock chance, lethal radius, kill chance per target class). No real-system citations and no Mach-dependent aero curves.
- Prompt before overwriting an existing asset; rerun resets to authored defaults (known-good baseline on a fresh checkout).
- Wire detonation FX on the profile (aerial + ground explosion prefabs). The prop impulse (`blastRadiusM/blastImpulseNs/blastUpwardsModifier`) only throws range props about; damage is the archetype's lethal radius and kill chance.
- Roadmap archetypes (ROADMAP Stream A): SR-IR missile (first, for Air Wing's hardpoints), MR-radar missile, area SAM (General Quarters B2, G69), anti-ship cruise missile (needs `seaSkimAltitudeM`), land-attack cruise missile (authored waypoints; fictional or generic targets only), SR air-to-ground missile.

## Detonation & emplaced ordnance

Detonation is shared via `DetonationEffects` (`Core/Missile/`): `SpawnExplosionVisuals` + `ApplyBlastImpulse`. Reuse it for non-missile ordnance (see `DemolitionCharge`) — don't duplicate the FX/blast code, and don't wrap a full flight sim around a stationary boom. Hits on vehicles go out as RH Shared `HitReport`s (ROADMAP B2); the vehicle's own module applies them.

## Range targets & launchers

`WeaponsRangeSectorBuilder` builds the multi-weapon range additively (idempotent per sector). Targets are auto-discovered by `TargetSelector` via `HittableBox` — any new target with one integrates for free. Launchers are auto-discovered by `MissileControlPanel` via `GuidedFury_TestRunner` — add a runner and it gets a Fire button. Movers use `WaypointMover` (Pat5, `[DefaultExecutionOrder(-50)]` so missiles sample exact sim poses).

## Always: a regression test

Add a deterministic pure-core test to `Tests/Editor/` (see `missile-sim-diagnosis` for the harness): the archetype meets its own authored numbers, and the same seed gives the same result. Run the EditMode suite (`GuidedFury.Tests.Editor`) and keep it green — know your baseline (2 failures pre-date the guidance work and are tracked in `ROADMAP.md`; don't attribute them to your change without a stash-bisect).

## Change log
- **2026-10-27** — Generalised to game-level archetypes (SIX: "Just simplify the plans and generalize them to make them safer"): the trigger list and roadmap examples name archetypes; a "grade" section; the builder section keeps Pat4's shape with AUTHORED numbers; the ladder notes L5 killed. The Pat1 and Pat3 guidance is unchanged. Checked against the safe limits, General Quarters `BLUEPRINT.md` §6 standing constraint 8. Real-system detail removed. Engineering-grade original preserved outside the repo (from 2121da9e).
