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

## Known issues / tracked follow-ups (2026-09-08 session)

Filed during the terminal-guidance investigation; each is real, none is urgent enough
to block the current streams. Pick up opportunistically or when a stream touches the area.

- [x] **~~Vertical-launch pitch-over~~ DONE (2026-09-08).** `BlendedPursuitProNav`
  (closing-ratio-weighted pursuit->ProNav) + `seekerMidcourseDatalink`. VLS closes to 20 m
  (was 3.1 km ballistic). Air battery unregressed. New law is selectable per profile.
- [ ] **Gravity-bias PN + minimum-range floor (surface-attack mode).** Below-horizon
  ground targets remain out of the ESSM's air-defense envelope (the blended law improved
  ground600 to 9.3 m but that is coincidental geometry, not a designed capability). A true
  land-attack profile wants a g-bias term + trajectory shaping + min-range gate. The
  regression test pins ground misses at <= 30 m meanwhile.
- [ ] **Crossing-target near-miss band.** Pure PN + body-fixed 30-degree cone closes
  crossing shots to 12-16 m (fuze 10 m). Blast (25 m) covers it, but APN (target-accel
  augmentation) or a gimbaled seeker would close the gap. Ties into Stream A L5.
- [ ] **Two pre-existing EditMode test failures (predate 2026-09-08, verified via stash
  bisect):** `L3_AoaProducesLift_PitchedBodyPullsToward` (lift pulls velocity DOWN — sign
  or frame bug in the L3 lift path) and `EndToEnd_L2WithConeSeekerAndProNav_AcquiresAndCloses`
  (closes 1213 m vs required 802 m). Both look like Phase-6 collateral in L3/L2 paths.
- [ ] **Proximity fuze fires on ANY collider** (`CheckProximityFuze` has no target/ground
  filtering — the "detonated 150 m past" symptom was the fuze catching the ground plane).
  Phase 3+ item already noted in code; add closing-velocity gate + layer filtering.
- [ ] **Two fullscreen cameras stacked in MissileRange** (TurretCab chase cam depth 0
  renders over Main Camera depth -1; both render full HDRP frames every frame). Decide:
  PIP inset for the turret cam, or disable Main Camera render when chase is active.
- [ ] **Two active AudioListeners** in MissileRange (Main Camera + TurretCab camera).
- [ ] **Strip `BurkeMissile` + embedded "Missile Cam" from the ESSM Shell prefab.**
  NeutralizeForeignComponents disables them per-launch (with a warning each shot);
  removing them from the prefab silences the log and saves the per-launch scan.
- [ ] **GuidedFury_TestRunner auto-launches on play** (`autoLaunchOnStart = true` in the
  scene) — fires a missile at t+0.5 s every time play starts. Fine for a test range,
  surprising for demos; consider defaulting off now that the control panel exists.
- [ ] **`scripting_class_is_subclass_of called with NULL` assert** on every editor
  session (would crash a build per Unity's own message). Bisect which package/asset
  triggers it after the 6000.4 upgrade.
- [ ] **Blast impulse vs light props.** 20 000 N-s throws 100 kg debris at ~120 m/s
  (cinematic but cartoonish). If it bothers, add a per-body mass-cap or scale impulse
  by target mass class on the profile.
- [ ] **HDRP top-down/orthographic hand-rendered captures come out black.** A manually
  created Camera.Render() to a RenderTexture does not pick up HDRP exposure/volume, so
  overhead range renders are unusable. Use in-game play-mode cameras for screenshots, or
  set up a proper HDRP camera + exposure override for a map view.
- [ ] **Range polish backlog:** ship-hulk targets need a HittableBox sizing pass for the
  fuze; naval water is a flat unlit plane (no shader); mesa-gantry A2G geometry not yet
  play-verified end-to-end (pure-sim only); airborne-testbed wing-pylon muzzle assumes
  level flight (fine on the racetrack straights, off in the turns).
- [ ] **Untracked vendor packs** (DAVFX, RH Aviator, RH Radar, RH Shared, _VFX, DeepWater
  Input scripts) sit uncommitted in the working tree — decide commit vs .gitignore.

## Skip / fail log
