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

### Slice 1 — Module skeleton and core contracts (done 2026-10-23: 10/10 tests)
- [x] Folders per P3 (`Core`, `Modules`, `ScriptableObjects`, `Prefabs`, `Examples`, `Tests`) and asmdefs (`DeepWater.GeneralQuarters`, `.Tests.Editor`).
- [x] Orders (one flat `Order` value type: Helm, Engage, CeaseFire, Doctrine, Roe) with issuer and tick, and an append-only `OrderLog`.
- [x] `DeterministicRng` (seeded; every draw counted, so a chance root can name its seed and draw).
- [x] Watch schedule: a whole-number tick period per system, and the fixed system order (BLUEPRINT §3.2).
- [x] Fidelity ladder contract: `FidelityLevel`, the per-element factory pattern, fallback-with-note when a level isn't implemented.
- [x] `EngagementLog`: notable events with rule, numbers, causes and roots (Order / scenario / chance).
- [x] `CombatFix`: a per-tick FNV-1a fingerprint of the combat state.
- [x] Tests: RNG reproducibility, Watch schedule, order log, ladder fallback, banned-API source scan of `Core/`.

### Slice 2 — Combat core L0, headless
- [ ] Truth feed: own-ship state and truth targets (id, kind, position, velocity, identity, signature).
- [ ] Radar L0 (SPY-1D): instrumented range and radar horizon, on its Watch.
- [ ] Tracks L0: perfect correlation, stable ids, drop on loss.
- [ ] Doctrine / ROE / TEWA L0: rank hostile tracks by time to closest approach; range bands per weapon class; doctrine modes; every assignment logged with its reason.
- [ ] VLS L0: cells, quad-packed ESSM, launch interval; Guided Fury `MissileEntity` built headless at the loadout's missile level; track-backed `ITargetSource`; intercept by closest approach over the step; seeded Pk draw.
- [ ] Guns L0 (Mk 45, CIWS, Mk 38): rate of fire, magazine, arcs, time of flight, seeded Pk(range) per burst (AUTHORED, flagged).
- [ ] Damage Control L0: hit points per system and hull; systems at zero go offline; leakers that reach the ship are hits.
- [ ] Scripted raid L0: sea-skimmers and aircraft on set paths.
- [ ] *Done when:* a headless raid test resolves with a cause chain for every kill, two seeded runs give identical Fix sequences, and the step stays within budget.

### Slice 3 — Hull from Blender (needs Blender running with the MCP add-on)
- [ ] Copy the DDG `.blend`; read which DDG-51 flight it is (BLUEPRINT Q1).
- [ ] Author the collision hull (compound convex pieces) and compartments (watertight sections × decks).
- [ ] Separate the articulated parts: Mk 45 (base / gun house / barrel), CIWS (mount / gun), VLS hatch positions fore and aft, SPG-62 ×3 (already separate).
- [ ] Export into `Prefabs/Models`. Build buoyancy probes from hull sections; mass, centre of mass and inertia from public DDG-51 figures.
- [ ] `hull-stability-check`: draft, displacement, GM, roll period (vs IMO).

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

## Skip / fail log
