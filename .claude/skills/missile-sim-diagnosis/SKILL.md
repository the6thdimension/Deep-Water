---
name: missile-sim-diagnosis
description: "Diagnose and tune Guided Fury missile flight, guidance, seeker, and intercept behavior. Use when a missile misses, overshoots, won't track, climbs ballistically, breaks lock, or when tuning aero/guidance/nav-gain — anything about why a shot did or didn't hit. Covers the deterministic pure-core diagnosis harness (no play mode), the scenario battery, reading lock/off-boresight/min-distance traces, and the design-envelope tuning rule."
allowed-tools: Bash, Read, Write, Edit, Glob, Grep
---

# Missile Sim Diagnosis (Guided Fury)

How to figure out why a missile did what it did, and tune it without breaking other cases. Distilled from the 2026-09-08 terminal-guidance investigation.

## Diagnose in the pure core, not play mode

The winning technique: build the missile stack from the asset in a Unity `RunCommand` (see `unity-mcp-workflow`), step it deterministically, and log a trace. No rendering, no PhysX, no time-scale games — just the sim. Reproduces flight behavior exactly and isolates guidance from everything else.

Skeleton (fill target motion per scenario):
```csharp
var so = AssetDatabase.LoadAssetAtPath<MissileProfileSO>(EssmAssetPath);
MissileProfileData data = so.Bake();
IAeroModel aero = new TabulatedAeroModel(so.cdVsMach, so.clAlphaVsMach, so.cmAlphaVsMach, so.cmDeltaVsMach);
var integrator = new FullAero6DofL4Integrator(aero, AutopilotFactory.Create(data.Autopilot), ThrustModelFactory.Create(data.ThrustModel));
var guidance = GuidanceFactory.Create(data.GuidanceLaw);
var seeker = new SimpleConeSeeker(new SeekerProfile { FovDeg = data.SeekerFovDeg, MaxRangeM = data.SeekerMaxRangeM,
    AcquisitionTimeS = data.SeekerAcquisitionTimeS, CoastTimeS = data.SeekerCoastTimeS });
var entity = new MissileEntity(in data, integrator, guidance, StandardAtmosphere.Instance,
    new SeekerTargetSource(seeker, new TransformTargetSource(targetGo.transform), data.SeekerMidcourseDatalink));
entity.Launch(launchPos, launchRot);
for (int i = 0; i < 3000; i++) {
    targetGo.transform.position = /* target motion at t = i*0.02f */;
    entity.Step(0.02f);
    var st = entity.State;                       // Position, Velocity, Orientation, Phase, TimeOfFlight
    // log: tof, pos, speed, dist-to-target, off-boresight angle, seeker.HasLock, deflections
    if (st.Phase == MissilePhase.Detonated || st.Phase == MissilePhase.Failed || st.Position.y < 0f) break;
}
```
Note `MissileCommand` lives in `GuidedFury.Core.State` — `using GuidedFury.Core.State;` (a missing one silently breaks the whole assembly; see `unity-mcp-workflow`).

## What to log, and what it tells you

- **min distance + tof** — the miss. Compare to fuze radius (`FuzeProximityRadiusM`, ESSM 10 m) and blast radius (`blastRadiusM`, ESSM 25 m).
- **seeker.HasLock transitions** — a LOST at the terminal LOS swing means the body-fixed cone broke lock and guidance went blind. Fix with `seekerCoastTimeS` (track-memory coast).
- **off-boresight angle** — large + growing near the end = overflying, not homing.
- **surface deflections at max** (PitchDeflectionRad/YawDeflectionRad near `maxControlDeflectionDeg`) = the autopilot is commanding hard but the airframe can't deliver → aero/lift ceiling, not a guidance bug.
- **apogee on a vertical shot** — climbing past target altitude = pure ProNav is degenerate off-boresight; needs pitch-over (blended law + `seekerMidcourseDatalink`).

## Scenario battery

Sweep a set of engagements in one command and print a one-line summary per config. Cover the weapon's DESIGN ENVELOPE: head-on, tail-chase, crossing (slow + fast), high-alt, dive. Count how many land inside the fuze. Use it to compare law/gain choices without eyeballing single traces.

## The design-envelope tuning rule (learned the hard way)

**Tune weapon data against the weapon's design envelope. Out-of-envelope shots get bounded-behavior tests, not gain chased to zero.**

A below-horizon ground target is outside a surface-to-air missile's envelope (ProNav's collision course runs through terrain). Chasing nav-gain to make that shot hit (N=5.5 → 2.6 m) blew up a real crossing intercept (13 m → 216 m). If the user flags a test case as unrealistic, believe them and re-scope to the envelope.

## Common fixes (all ScriptableObject-driven, no magic numbers in code)

- **Airframe can't pull its rated g** → the lift-slope curve is body-alone theory. Use effective whole-airframe (body+strake+tail) normal-force values on `clAlphaVsMach`.
- **Break-lock in the endgame** → `seekerCoastTimeS` > 0.
- **Target starts outside the seeker cone / vertical launch** → `seekerMidcourseDatalink = true` and `GuidanceLawKind.PursuitThenProNav`.
- **Crossing near-misses inside blast but outside fuze** → APN (target-accel augmentation) or a gimbaled seeker — roadmap, not tuning.

## Lock it down with a regression test

Add a deterministic pure-core test to `Assets/Guided Fury/Tests/Editor/TerminalGuidanceRegressionTests.cs`: assert a bounded miss for envelope cases (≤ fuze) and out-of-envelope cases (bounded, not "hit"). Distinguish real behavior from the failure mode with a secondary bound (e.g. VLS apogee < 8 km separates "pitched over" from "10 km ballistic climb").
