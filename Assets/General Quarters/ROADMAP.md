# General Quarters — Roadmap

**This document's one job:** what to do next, and what is done. Why each piece exists is in `BLUEPRINT.md`.

**Rules (same as Guided Fury and Radar Suite):**
- Phases MAY be reordered for dependency.
- Unchecked items MUST NOT be deleted; mark them `~~skipped~~` with the reason.
- Completed items get a date and their commit.
- Every element level ships with EditMode tests.
- Every performance claim carries its A/B number (`unity-perf-ab`).
- Hull changes pass `hull-stability-check`.

Branch: `feat/general-quarters`, cut from `feat/environment-ocean`. HEAD stays wherever the shared checkout is; commits go in through `commit-to-branch`. Nothing is pushed without SIX's word.

---

## Phase 1 — L0 engagement on the range (G20)

The gate: a scripted raid against the destroyer plays end to end on the Sea State range.
- She floats and steers; the CIC detects, assigns, fires and kills or is hit.
- Every engagement is explained in the log.
- It costs ≤ 1 ms of combat-core time per fixed step at 24 threats, and looks and sounds like a fight.

### Slice 1 — Module skeleton and core contracts (done 2026-10-23, 0c577de: 10/10 tests)
- [x] Folders per P3 (`Core`, `Modules`, `ScriptableObjects`, `Prefabs`, `Examples`, `Tests`) and asmdefs (`DeepWater.GeneralQuarters`, `.Tests.Editor`).
- [x] Orders (one flat `Order` value type: Helm, Engage, CeaseFire, Doctrine, Roe) with issuer and tick, and an append-only `OrderLog`.
- [x] `DeterministicRng` (seeded; every draw counted, so a chance root can name its seed and draw).
- [x] Watch schedule: a whole-number tick period per system, and the fixed system order (BLUEPRINT §3.2).
- [x] Fidelity ladder contract: `FidelityLevel`, the per-element factory pattern, fallback-with-note when a level isn't implemented.
- [x] `EngagementLog`: notable events with rule, numbers, causes and roots (Order / scenario / chance).
- [x] `CombatFix`: a per-tick FNV-1a fingerprint of the combat state.
- [x] Tests: RNG reproducibility, Watch schedule, order log, ladder fallback, banned-API source scan of `Core/`.

### Slice 2 — Combat core L0, headless (done 2026-10-23, 6745ce4: 21/21 tests; 8-missile raid all killed, 9 ESSM; 24-missile raid 0.043 ms/tick)
- [x] Truth feed: own-ship state and truth targets (id, kind, position, velocity, identity, signature).
- [x] Radar L0 (SPY-1D): instrumented range and radar horizon, on its Watch.
- [x] Tracks L0: perfect correlation, stable ids, drop on loss.
- [x] Doctrine / ROE / TEWA L0: rank hostile tracks by time to closest approach; range bands per weapon class; doctrine modes; every assignment logged with its reason.
- [x] VLS L0: cells, quad-packed ESSM, launch interval; Guided Fury `MissileEntity` built headless at the loadout's missile level; track-backed `ITargetSource`; intercept by closest approach over the step; seeded Pk draw.
- [x] Guns L0 (Mk 45, CIWS, Mk 38): rate of fire, magazine, arcs, time of flight, seeded Pk(range) per burst (AUTHORED, flagged).
- [x] Damage Control L0: hit points per system and hull; systems at zero go offline; leakers that reach the ship are hits.
- [x] Scripted raid L0: sea-skimmers and aircraft on set paths.
- [x] *Done when:* a headless raid test resolves with a cause chain for every kill, two seeded runs give identical Fix sequences, and the step stays within budget.

### Slice 3 — Hull from Blender (done 2026-10-23: 27/27 tests; PhysX roll 11.18 s vs 11.14 s damped theory)
- [x] Copy the DDG `.blend` (`ArtSource/General Quarters/DDG51_GQ.blend`; the original is untouched, hash checked). It is a Flight I/II: two CIWS, Mk 141 Harpoon quads aft, no hangar, 32 + 64 VLS cells (BLUEPRINT Q1, answered).
- [x] Collision hull: 17 convex pieces (11 hull segments, 6 superstructure blocks, ≤ 196 faces each). Compartments: 12 watertight sections at AUTHORED 12 m stations, full depth. ~~Split by deck~~: deferred to Damage Control L1, where flooding first needs decks.
- [x] Articulated parts: Mk 45 (train / elevate), CIWS ×2 (base / train / elevate), Mk 38 ×2, SPG-62 ×3, 96 VLS and 8 Harpoon launch points. Exported flat (the FBX axis bake applies to first-level objects only); `Editor/Ddg51HullBuilder.cs` re-nests elevate under train and remaps the original HDRP materials.
- [x] Export into `Prefabs/Models` (FBX ×2, `DDG51_GQ_Layout.json`). Hydrostatics MEASURED on a converged 61 × 0.5 m column grid: 8 717 t, draft 6.52 m (9.41 m at the sonar dome), KB 3.92 m, BM 4.79 m, waterline 141 × 17.7 m. Mass = displacement. GM 1.5 m and the gyradii (0.38 B, 0.25 / 0.26 L) are AUTHORED: DDG-51's KG is not public. Sea State floats it on 2 183 columns (25 × 1.5 m), chosen from a grid study (METHODOLOGY AP16).
- [x] `hull-stability-check`:
  - **EditMode:** righting moment at 5° gives GM 1.510 m against 1.500 m. Free roll is 10.92 s against 11.04 s theory (IMO 10.86 s, context).
  - **PhysX:** 11.18 s damped (ζ 0.13 from the decay) against 11.14 s. Pitch is 4.27 s against 4.16 s. At rest it trims 8 cm over its length and sits 3 cm high: the probe grid's centre of buoyancy is 15 cm forward of the hull's, and the probes displace 0.3 % more.
  - **Cost:** the DDG's buoyancy costs 0.30 ms per fixed step in the demo sea (Editor, play mode).

### Slice 4 — Engine Room and Helm L0
- [ ] Shafts and thrust curve, twin rudders (lift and stall), speed-power resistance.
- [ ] `HelmOrder`: course and speed hold, waypoint routes with turn anticipation.
- [ ] Checks against public figures: top speed, acceleration, tactical diameter, heel in a turn.

### Slice 5 — The range
- [ ] Range scene on the Sea State sea, with the destroyer prefab (new) and the raid.
- [ ] Tactical display (track picture, weapon assignment, doctrine and ROE controls), helm panel, orbit and chase cameras.
- [ ] Captain / TAO seats, with the AI captain filling the empty one (named rules L0).
- [ ] Play verification against the Phase 1 gate. Measure the budget.

### Slice 6 — Presentation (G18)
- [ ] VLS hatch and plume, Mk 45 muzzle and shell splashes, CIWS tracers and bursts, missile trails.
- [ ] Splashes into the foam field (needs BLUEPRINT Q2).
- [ ] Hit effects, fire and smoke; layered audio.
- [ ] All unreckoned; no committed DAVFX references.

### Slice 7 — Phase review
- [ ] Legacy supersession checklist (BurkeVLSSystem, SPG62FireControlRadar, VLSManager, BurkeMissile). Archive only after the range proves the replacement (G13).
- [ ] Update this roadmap and BLUEPRINT; METHODOLOGY entries; a handoff.

---

## Element streams (after Phase 1; one level at a time, each with tests)

### Stream A — Sensors and CIC
- [ ] Radar L1: radar equation (RCS in dBsm, Pd/Pfa), sea clutter from `SeaConditions`.
- [ ] Illuminators L1: three SPG-62 channels gate terminal homing.
- [ ] Tracks L1: measurement noise, α-β/Kalman filter, track quality.
- [ ] TEWA L1: Pk-weighted pairing, shoot-look-shoot.
- [ ] Radar L2: beam and energy scheduling.

### Stream B — Weapons
- [ ] Guns L1: point-mass ballistics with drag, dispersion, mount slew and arcs, firing on the roll.
- [ ] VLS L1: hatch and launch sequence timing, midcourse uplink.
- [ ] SM-2 profile; Harpoon and Tomahawk profiles (Guided Fury Stream A items).

### Stream C — The living ship
- [ ] Engine Room L1: the plant (4 × LM2500, CPP, fuel).
- [ ] Helm L1: GPS/INS drift and fixes.
- [ ] Damage Control L1: compartments and flooding into buoyancy probes (list, trim, sinking).
- [ ] Damage Control L2: fire spread and damage-control parties.
- [ ] Engine Room L2: casualties from damage.

### Stream D — Opponents and AI
- [ ] The peer ship on the same combat system (BLUEPRINT Q3).
- [ ] AI captain L1: ML-Agents trained captain.

### Stream E — Reckoning seam
- [ ] Hull L2: Reckoning ship motion (variant B) replaces PhysX motion behind the seam.

---

## Change log
- **2026-10-23** — Roadmap created from the locked blueprint (G1–G20). Phase 1 started.
- **2026-10-23** — Slice 1 (0c577de) and slice 2 done.
  - **Built:** the combat core at L0: radar to the 4/3-earth horizon (27.6 km for a 5 m sea-skimmer from a 20 m antenna), tracks, TEWA, doctrine and ROE, the VLS with Guided Fury ESSM at L2, guns, damage, and a scripted raid.
  - **8 sea-skimmers:** all 8 killed, 9 ESSM launched; every kill's cause chain is complete and ends in roots, with an identical Fix two runs in a row.
  - **24 sea-skimmers:** all killed, at 0.043 ms per tick (Editor), against the 1 ms budget.
  - **Observation, not tuned:** at missile L2, ESSM averaged about 720 m/s over 20 km. That is slower than published ESSM speeds; the cause is the missile level, not the combat core. A Guided Fury stream item.
- **2026-10-23** — Slice 3 done: the hull from Blender, with a prefab (`Prefabs/DDG51_GQ.prefab`), the hull profile and the buoyancy assets.
  - **Grid fix:** the first probe grid (9 × 4 m, 300 columns) measured its own hydrostatics and passed every upright check. At 5° of heel it lost 7.6 % of GM, and the roll test caught it. The hydrostatics are now measured on a converged grid and the probe grid is gated against them (AP16).
  - **Prefab:** `ShipHull` sets the inertia tensor from the profile. Unity's automatic tensor from 17 convex pieces would not be a ship's.

## Skip / fail log
