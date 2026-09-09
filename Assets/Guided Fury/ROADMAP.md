# Guided Fury — Routine Roadmap

Three parallel work streams (chosen Batch-2 answers): **L5 + new profiles**, **Damage / warhead**,
**Test coverage + scenario library**. The routine alternates streams round-robin
unless dependencies force serial work.

**Instruction:** the routine MAY reorder for dependency. It MUST NOT delete unchecked
items without marking `~~skipped~~` + reason. New profiles / weapons get added to the
end of the appropriate stream as you learn them.

---

## Stream A — L5 + New Missile Profiles

### L5 (highest fidelity)

- [ ] **L5 design doc.** What's above L4 (full-aero 6DOF)? Candidates: thrust vectoring,
  staging, GPS/INS error models, aero coupling (pitch–yaw cross terms), control-loop
  rate-limit refinement. Pick 2–3 deltas, document in `Core/Integrators/_L5_design.md`.
- [ ] **L5 integrator skeleton.** `FullSixDofL5Integrator : IPhysicsIntegrator`.
- [ ] **Thrust vectoring subsystem.** `IThrustVector` interface, `GimballedNozzle` impl.
  Plumb into L5.
- [ ] **Staging subsystem.** `IStage` interface, profile carries stage list. Boost
  separator at burnout.
- [ ] **INS error model.** Drift + Kalman fusion if a GPS update arrives. New `IGuidanceSensor`.
- [ ] **Bump `HighestImplementedLod`** + auto-downgrade banner.
- [ ] **L5 EditMode tests.** Thrust-vector authority, stage separation timing, INS drift
  bounded under ProNav.

### New profiles (real weapons, EssmProfileBuilder pattern — Pat4 candidate)

- [ ] **AIM-9X Sidewinder (L3 / L4).** IRH seeker, short-range AAM. Editor command.
- [ ] **AIM-120D AMRAAM (L4).** ARH seeker, BVR AAM.
- [ ] **AGM-84 Harpoon (L4).** Sea-skimmer, anti-ship. Add `seaSkimAltitudeM` field.
- [ ] **BGM-109 Tomahawk (L4 + GPS waypointing).** Land-attack cruise. Needs IGuidanceSensor.
- [ ] **AGM-114 Hellfire (L3).** Air-to-ground, SALH variant.
- [ ] **RIM-66 SM-2 (L4).** Long-range SAM (counterpart to ESSM).

## Stream B — Damage / Warhead / Fragmentation

- [ ] **Warhead profile authoring.** New SO `WarheadProfileSO` (mass, charge yield in TNT-eq,
  fragmentation count, fragment mass + velocity distribution). Attach to MissileProfileSO.
- [ ] **Blast model.** Overpressure vs distance — Sadovsky / Kingery-Bulmash. Pure function
  in `Core/Damage/BlastModel.cs`. EditMode test against published curves.
- [ ] **Fragmentation model.** Cone of fragments at detonation, ray-cast `n` fragments,
  apply damage to IHittable per fragment hit. Statistical sampler (deterministic via
  P7 RNG).
- [ ] **Damage classes.** Define soft / hard / armored target classes. Kill probability
  per warhead × class published table.
- [ ] **`IDamageable` upgrade.** Extend `IHittable` (or successor) with damage type + amount.
  Carriers, aircraft, ground veh implement.
- [ ] **HUD / stats integration.** Show kill / mission-kill / damage in `LodComparisonStats`.
- [ ] **Damage EditMode tests.** Blast radius lethal cutoff, fragmentation kill prob,
  hard-target underpenetration.

## Stream C — Test Coverage + Scenario Library

- [ ] **Test-coverage audit.** Count test classes per Core/ subfolder. Identify gaps.
  Write report to `_audit/tests-<date>.md`.
- [ ] **Property tests** for `StableLookRotation` — fuzz random forwards, assert quaternion
  is valid + forward axis matches input.
- [ ] **Scenario library.** Convert `EngagementScenarios` enum to SO-driven scenario
  presets (`ScenarioSO`). Each scenario = world setup + expected outcome.
- [ ] **Crossing / head-on / tail-chase / pop-up scenarios** as SOs.
- [ ] **Salvo coordination scenarios.** N missiles vs M targets, time-overlap, weapon
  pairing optimality.
- [ ] **Replay support.** Record `MissileState` per step at L1+ to a binary log. Editor
  tool to scrub/replay. Foundation for regression tests (snapshot-compare).
- [ ] **Snapshot regression harness.** Run a canonical scenario, compare summary
  statistics (impact time, miss distance) to a golden value. CI gate.
- [ ] **PlayMode smoke scenes.** One scene per fidelity tier (L0..L5). Routine adds
  these to the verification gate.

---

## Skip / fail log
