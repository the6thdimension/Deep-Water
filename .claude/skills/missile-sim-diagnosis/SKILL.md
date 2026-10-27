---
name: missile-sim-diagnosis
description: "Diagnose and tune Guided Fury missile flight, guidance, seeker, and intercept behavior. Use when a missile misses, overshoots, won't track, climbs ballistically, breaks lock, or when tuning an archetype's authored numbers (g limit, nav gain, seeker cone, lock chance) — anything about why a shot did or didn't hit. Covers the deterministic pure-core diagnosis harness (no play mode), the scenario battery, reading lock/off-boresight/min-distance traces, and the design-envelope tuning rule."
allowed-tools: Bash, Read, Write, Edit, Glob, Grep
---

# Missile Sim Diagnosis (Guided Fury)

How to figure out why a missile did what it did, and tune it without breaking other cases. Distilled from the 2026-09-08 terminal-guidance investigation.

**The grade (SIX, 2026-10-27):** missiles are game-level archetypes with AUTHORED numbers, flown at L0–L2 (`Assets/Guided Fury/ROADMAP.md`, "The grade"). Tuning means changing those authored numbers inside the archetype's envelope. It never means deepening the model: no new aero tables, seeker or fuze internals, or real-system data. The L3/L4 stack and the one real-system profile stay as built (ROADMAP S1–S8); diagnose them, don't extend them.

## Diagnose in the pure core, not play mode

The winning technique: build the missile stack from the asset in a Unity `RunCommand` (see `unity-mcp-workflow`), step it deterministically, and log a trace. No rendering, no PhysX, no time-scale games — just the sim. Reproduces flight behavior exactly and isolates guidance from everything else.

Skeleton (fill target motion per scenario):
```csharp
var so = AssetDatabase.LoadAssetAtPath<MissileProfileSO>(ProfilePath);   // the archetype under test
MissileProfileData data = so.Bake();
// Archetypes fly at L2. For the L4 profile that stays as built, build the L4 integrator the way
// TerminalGuidanceRegressionTests does, from the profile's own curves.
IPhysicsIntegrator integrator = new RateLimited3DofL2Integrator();
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
    // log: tof, pos, speed, dist-to-target, off-boresight angle, seeker.HasLock, commanded vs delivered accel
    if (st.Phase == MissilePhase.Detonated || st.Phase == MissilePhase.Failed || st.Position.y < 0f) break;
}
```
Note `MissileCommand` lives in `GuidedFury.Core.State` — `using GuidedFury.Core.State;` (a missing one silently breaks the whole assembly; see `unity-mcp-workflow`).

## What to log, and what it tells you

- **min distance + tof** — the miss. Compare to the profile's fuze radius (`FuzeProximityRadiusM`), its prop-impulse radius (`blastRadiusM`), and, once ROADMAP B1 is built, its lethal radius. (The point-defence SAM that stays as built uses 10 m and 25 m.)
- **seeker.HasLock transitions** — a LOST at the terminal LOS swing means the body-fixed cone broke lock and guidance went blind. Fix with `seekerCoastTimeS` (track-memory coast). Once the lock chance exists (ROADMAP A2), log each lock draw with its seed and draw index, so a "no lock" can be told from a bad draw.
- **off-boresight angle** — large + growing near the end = overflying, not homing.
- **commanded vs delivered acceleration** — at L2, a command clipped at the g limit means the archetype's authored g limit binds: the shot needs more agility than the archetype was given, not a guidance fix. (On the L3/L4 stack that stays as built, the same symptom shows as control deflections pinned at their limit.)
- **apogee on a vertical shot** — climbing past target altitude = pure ProNav is degenerate off-boresight; needs pitch-over (blended law + `seekerMidcourseDatalink`).

## Scenario battery

Sweep a set of engagements in one command and print a one-line summary per config. Cover the archetype's DESIGN ENVELOPE (its authored range, cone and g limit): head-on, tail-chase, crossing (slow + fast), high-alt, dive. Count how many land inside the fuze. Use it to compare law/gain choices without eyeballing single traces.

## The design-envelope tuning rule (learned the hard way)

**Tune an archetype's numbers against its design envelope. Out-of-envelope shots get bounded-behavior tests, not gain chased to zero.**

A below-horizon ground target is outside a surface-to-air missile's envelope (ProNav's collision course runs through terrain). Chasing nav-gain to make that shot hit (N=5.5 → 2.6 m) blew up a real crossing intercept (13 m → 216 m). If the user flags a test case as unrealistic, believe them and re-scope to the envelope.

## Common fixes (all ScriptableObject-driven, no magic numbers in code)

- **The archetype can't make the turn** → its authored g limit binds. Raise the g limit, lower the navigation gain, or accept the shot is out of envelope. On the L4 profile that stays as built, a g shortfall lives in its aero curves; those are not re-authored (ROADMAP S1, S3), so record the finding instead.
- **Break-lock in the endgame** → `seekerCoastTimeS` > 0.
- **Target starts outside the seeker cone / vertical launch** → `seekerMidcourseDatalink = true` and `GuidanceLawKind.PursuitThenProNav`.
- **Crossing near-misses inside blast but outside fuze** → the archetype's authored lethal radius decides them (ROADMAP B1, K8). No deeper guidance or seeker model.

## Lock it down with a regression test

Add a deterministic pure-core test to `Assets/Guided Fury/Tests/Editor/` (the existing terminal-guidance battery is `TerminalGuidanceRegressionTests.cs`): assert a bounded miss for envelope cases (≤ fuze) and out-of-envelope cases (bounded, not "hit"), and that the same seed gives the same result. Distinguish real behavior from the failure mode with a secondary bound (e.g. VLS apogee < 8 km separates "pitched over" from "10 km ballistic climb").

## Change log
- **2026-10-27** — Generalised to game-level archetypes (SIX: "Just simplify the plans and generalize them to make them safer"): the harness loads any archetype at L2, with the L4 profile as the stays-as-built exception; tuning means the archetype's authored numbers; crossing near-misses go to the authored lethal radius; lock draws get logged. Checked against the safe limits, General Quarters `BLUEPRINT.md` §6 standing constraint 8. Real-system detail removed.
