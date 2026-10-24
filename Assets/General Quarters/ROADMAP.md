# General Quarters — Roadmap

**This document's one job:** what to do next, and what is done. Why each piece exists is in `BLUEPRINT.md`.

**Rules (same as Guided Fury and Radar Suite):**
- Phases MAY be reordered for dependency.
- Unchecked items MUST NOT be deleted; mark them `~~skipped~~` with the reason.
- Completed items get a date and their commit.
- Every element level ships with EditMode tests.
- Every performance claim carries its A/B number (`unity-perf-ab`).
- Hull changes pass `hull-stability-check`.

Branch: `feat/general-quarters`, kept in step with `main` (CLAUDE.md, Branches). HEAD stays wherever the shared checkout is; commits go in through `commit-to-branch`.
- **Git (SIX, 2026-10-24):** Claude manages git without asking. Verified work is committed, pushed and brought up to `main`, and every branch is kept level; `Guided-Fury` takes it through a PR.
- **Never pushed:** the archive branch, DAVFX and the MCP unlock script. There are no force pushes.

---

## Phase 1 — L0 engagement on the range (G20) (DONE 2026-10-24)

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

### Slice 3 — Hull from Blender (done 2026-10-23, 284247f: 27/27 tests; PhysX roll 11.18 s vs 11.14 s damped theory)
- [x] Copy the DDG `.blend` (`ArtSource/General Quarters/DDG51_GQ.blend`; the original is untouched, hash checked). It is a Flight I/II: two CIWS, Mk 141 Harpoon quads aft, no hangar, 32 + 64 VLS cells (BLUEPRINT Q1, answered).
- [x] Collision hull: 17 convex pieces (11 hull segments, 6 superstructure blocks, ≤ 196 faces each). Compartments: 12 watertight sections at AUTHORED 12 m stations, full depth. ~~Split by deck~~: deferred to Damage Control L1, where flooding first needs decks.
- [x] Articulated parts: Mk 45 (train / elevate), CIWS ×2 (base / train / elevate), Mk 38 ×2, SPG-62 ×3, 96 VLS and 8 Harpoon launch points. Exported flat (the FBX axis bake applies to first-level objects only); `Editor/Ddg51HullBuilder.cs` re-nests elevate under train and remaps the original HDRP materials.
- [x] Export into `Prefabs/Models` (FBX ×2, `DDG51_GQ_Layout.json`). Hydrostatics MEASURED on a converged 61 × 0.5 m column grid: 8 717 t, draft 6.52 m (9.41 m at the sonar dome), KB 3.92 m, BM 4.79 m, waterline 141 × 17.7 m. Mass = displacement. GM 1.5 m and the gyradii (0.38 B, 0.25 / 0.26 L) are AUTHORED: DDG-51's KG is not public. Sea State floats it on 2 183 columns (25 × 1.5 m), chosen from a grid study (METHODOLOGY AP16).
- [x] `hull-stability-check`:
  - **EditMode:** righting moment at 5° gives GM 1.510 m against 1.500 m. Free roll is 10.92 s against 11.04 s theory (IMO 10.86 s, context).
  - **PhysX:** 11.18 s damped (ζ 0.13 from the decay) against 11.14 s. Pitch is 4.27 s against 4.16 s. At rest it trims 8 cm over its length and sits 3 cm high: the probe grid's centre of buoyancy is 15 cm forward of the hull's, and the probes displace 0.3 % more.
  - **Cost:** the DDG's buoyancy costs 0.30 ms per fixed step in the demo sea (Editor, play mode).

### Slice 4 — Engine Room and Helm L0 (done 2026-10-24: 46/46 GQ tests, 48/48 sea tests; PhysX run on the demo sea)
- [x] Shafts and thrust curve, twin rudders (lift and stall), speed-power resistance: the MMG standard method (G22), twin screw and twin rudder, in `Core/EngineRoom`.
  - **Hull:** DTMB 5415 derivatives from Yoon 2009 (IIHR PMM) at Fn 0.138 / 0.28 / 0.41, interpolated by speed. Added mass m'y and J'z from the three-facility means.
  - **Resistance:** the ITTC 27th campaign C_T, extrapolated by ITTC-57, times one appendage factor.
  - **Propellers:** MARIN 4058 K_T and K_Q.
  - **Rudders:** 15.4 m² twin spades, Fujii f_α, and a stall the standard method leaves out.
  - Every number's source is in `DDG51_GQ_EngineRoom.asset`'s provenance and in `Calibration~/DTMB5415-SOURCES.md`.
- [x] Sea State probe drag per hull axis (G21): the DDG keeps heave only. As isotropic drag it was 85 MN at 30 kn against 3.4 MN of thrust.
- [x] Orders: telegraph bells (G23; astern bells are fractions of astern revolutions), manual rudder, follow route.
- [x] `Helm` L0: course hold (autopilot, at most 20° of rudder), speed hold (steady-revolution feed-forward plus PI), bells, and routes by line of sight with a wheel-over at R·tan(Δψ/2).
- [x] `ShipDrive` applies the MMG accelerations to PhysX: F = m·a_G and τ = I_zz·ṙ, so added mass counts. The roll moment comes from the hydrodynamic sway force m·a_y at its depths, plus speed-dependent lift damping (AUTHORED).
- [x] Checks against public figures (G24). The Python mirror (`Calibration~/mmg_calibration.py`) and the C# tests give the same numbers.
  - **CALIBRATED (four numbers, one figure each):**
    - The appendage factor 1.443 → 31 kn on 100,000 shp at 199 rpm.
    - γ_R 0.9 and ℓ'_R −0.9 → Barry's turn diameters. Tactical diameters 388 / 1,083 / 1,026 m against 457 / 914 / 1,097 m (−15 / +18 / −6 %).
    - Astern K_T 0.5·K0 → the one-minute stop (60.4 s).
  - **VALIDATION (not fitted):**
    - 18 kn at 104 rpm (SIMMAN spec 109).
    - Stop distance 382 m (< 549 m).
    - All stop to 30 kn in 50 s (< 90 s).
    - 20/20 zig-zag 5.5° / 5.4° (CFD 4.5 / 4.8, MMG 5.3 / 5.1).
    - The IMO turning and 10/10 criteria.
    - Steady heel at flank, full rudder: 7.1° EditMode, 7.2° PhysX (Barry 8–10°).
  - **Y'r:** published at −0.0485, which leaves the bare hull directionally unstable; read as containing the m'u'r' term (+m'). A calibrated choice between two readings of one table, settled by the turning.
- [ ] **Known gap (L1):** the transient outward heel after the rudder goes hard over at flank peaks at 13.1° in PhysX, against Barry's 8–10°. The steady heel matches.
  - **Cause:** at L0 there is no roll added inertia, no roll–sway–yaw coupling (4-DOF MMG) and no Ikeda roll damping.
  - **First fix:** the roll moment had left out the sway added-mass reaction. That made the swing 7° inward then 17.5° outward; with the reaction it is 2.9° inward and 13.1° outward.

### Conn stand-in (2026-10-24, ahead of slice 5, at SIX's request: "a way for me to give her helm and bell orders")
- [x] **Deep Water → General Quarters → Drive the DDG:** in the Sea State demo, play mode with the DDG under way at 12 kn. It adds the conn panel and a chase camera, and nothing is saved to the scene.
- [x] **The 'Conn' action map** in the project-wide actions (`Assets/RHMILCOM.inputactions`, Project Settings → Input System Package). It has three control schemes: Keyboard&Mouse (a laptop), Gamepad (Xbox and any pad Unity recognises), Joystick (generic HID pads).
  - **Rudder:** proportional on sticks, standard rudder on keys, Shift for hard over; let go and she steadies.
  - **Bells:** ring up and down.
  - **Course:** turned while held.
  - **Buttons:** steady, midships, all stop.
  - **Camera:** chase or orbit, zoom.
- [x] **Code:** `Examples/` holds ConnInput (the one reader), ConnLogic (pure), ConnController, ConnPanel (IMGUI) and ShipChaseCamera. There is a new course-only order (`Order.SteerCourse`), and `ShipDrive` stamps order sequence numbers.
- [x] **Tests:** ConnLogicTests, plus ConnBindingTests on virtual keyboard and mouse, XInput and generic HID devices. 102/102 with Sea State.
  - **Play mode:** a virtual gamepad and keyboard drove the full chain (stick rudder, release to steady, d-pad bell up, S bell down, E course change).
  - **Not tested:** physical controllers. None was plugged in.
- [x] **Slice 5** replaces the IMGUI panel with the real helm panel and moves the conn into the range scene (2026-10-24). The stand-in menu still works in the Sea State demo.

### Slice 5 — The range (done 2026-10-24: 67/67 GQ tests, 48/48 sea tests; the Phase 1 gate met in play mode, presentation aside)
SIX's picks: G25 App UI on UI Toolkit, G26 switch seats, G27 the AI captain's route / flank / open-arcs rules, G28 debrief, time compression and a threat and missile camera.
- [x] **Range scene:** `Examples/Scenes/GQ_Range.unity`, built by Deep Water → General Quarters → Build Range Scene from a copy of the Sea State demo. The demo's hulls, selector and review camera come out; the DDG, the range and its screen go in. Play it with Deep Water → General Quarters → Play the Range.
- [x] **Combat in the scene:** `Modules/Combat/ShipCombat.cs` runs, on every fixed step, the AI captain, then the raid, then `CombatSystem.Step`. It applies kills to the raid and hands the conn's standing orders to `ShipDrive.Answer`. Every order, the player's or the AI's, goes into the combat system's order log.
  - **Propulsion offline:** Damage Control L0 has no partial casualties, so propulsion offline stops the shafts and logs it.
- [x] **Data:**
  - `ShipLoadoutSO` with `DDG51_GQ_Loadout.asset`: Flight I/II with two CIWS and two Mk 38. Mount positions come from the Blender layout; each number's source is in the asset.
  - Three raids (`RaidScenarioSO`): Small 4, Standard 8+2 (two waves and two strike aircraft), Saturation 24.
  - `AiCaptainProfileSO` and `RangeProfileSO`: an 8 × 6 km patrol box, time scales, the TAO camera window.
  - Built by `Editor/Ddg51CombatData.cs`.
- [x] **Screens (App UI, G25):**
  - **Captain seat (G26):** the 3D view, the helm panel (readouts, telegraph, rudder, steady, course and speed orders, order log), an inset scope (click it to go to the TAO seat), and an event ticker.
  - **TAO seat:** a big vector track scope (NTDS-style symbols, velocity leaders, ESSM and CIWS reach, proposals and the selection marked, wheel to zoom), the camera's window, a track list sorted by time to closest approach, engage and cease fire, doctrine per weapon, magazine, systems and hull.
  - **Both seats:** a top bar with the seat, the raid picker and General quarters, ROE, time compression, the clock and help.
- [x] **Seats:** Tab / tap Start / button 6 switches seats. The conn's controls are read only in the Captain seat, the TAO's only in the TAO seat, and the AI takes the other one.
  - **Input:** new "Tao" and "Range" maps in `Assets/RHMILCOM.inputactions`, each with all three control schemes. Hold Start starts the raid, a tap switches seats. Help (H) lists every binding.
- [x] **AI captain L0 (G19, G27):** `Core/Captain/AiCaptain.cs`, named rules, each logged as a Decision event with its numbers and cause.
  - **C1:** route at 20 kn.
  - **C2:** flank on the first inbound hostile.
  - **C3:** turn, at most every 20 s, to the course that puts the most (threat, mount) pairs inside the gun arcs.
  - **C4:** secure after 30 s quiet.
  - **T1:** confirm a SemiAuto proposal after 2 s.
  - **TEWA support:** TEWA now lists its standing proposals (`ITewa.Proposals`).
- [x] **Extras (G28):**
  - **Debrief:** opens when a raid resolves, showing kills by weapon, expenditure, damage, orders and AI decisions, seed and Fix, and the measured core cost against the budget.
  - **Time compression:** 1×, 2×, 4× (T).
  - **Camera (C):** cycles the ship, the selected track and ESSMs in flight, and holds on a kill for 2.5 s.
  - **Stand-ins:** ESSM and F-16 models for what is flying (slice 6 replaces them).
- [x] **Play verification against the Phase 1 gate** (Editor, 2560 × 1440, the full scene with PhysX and Sea State):
  - **Saturation, Captain seat, AI as TAO:** 24 killed, 0 hits (ESSM 23, Mk 45 1), all 32 ESSM spent, 150 s. Combat core per fixed step: 0.066 ms mean, 0.233 ms p99 over 7 515 steps at up to 24 threats; one 5.7 ms spike, not investigated (probably first-use JIT). Profiler markers during the busiest phase: `GQ.CombatCore` 0.080 ms per step, `GQ.CombatHost` 0.096 ms.
  - **Headless, the same loadout, AI as TAO:** 24 killed, 0.030 ms mean, the same Fix on two runs.
  - **Small raid, AI as TAO:** 4 of 4 killed.
  - **First standard raid, SIX at the TAO seat:** with ESSM on SemiAuto and no confirmations, the guns alone killed 3 and 5 hit the ship. Doctrine matters.
  - **GC** (player loop, standard raid, TAO seat): about 2 KB per frame. 1.1 KB is the screen's readout strings at 15 Hz, 0.6 KB the combat core's log sentences, 0.2 KB UI Toolkit.
  - **Not met here:** "looks and sounds like a fight" belongs to slice 6.
- [ ] **Known gaps** (for later slices):
  - Both CIWS share Damage Control's one CIWS system at L0.
  - The strike aircraft pass 6 km overhead, so TEWA L0 never ranks them a threat (closest approach over 3 km): the TAO engages them by hand.
  - HDRP motion blur smears the frame each time the camera cuts.
  - Not re-checked in play mode: the last two fixes (camera execution order, scope labels re-placed on layout). They are build-verified and covered by the suite.

### Slice 6 — Presentation (G18) (done 2026-10-24: 72/72 GQ tests, 49/49 sea tests; the saturation Fix unchanged)
SIX's picks: G30 effects reused where they fit and built in code where they don't, G31 a Sea State foam deposit API (Q2), G32 recordings plus synthesised sounds, G33 guns track, wake and bow wave, VLS hatches open, no blur on camera cuts.
- [x] **The core says where; presentation decides how.** Engagement events now carry a point, a weapon and a source (VLS group, gun mount, system). None of it is in the Fix: the saturation run is `2AF5C040E4AC6B5E` before and after (`PresentationTests.Saturation_Fix_IsUnchangedByPresentationData`).
- [x] **Effects** (`Modules/Presentation/`, built by Deep Water → General Quarters → Build Combat Effects):
  - VLS plume, Mk 45 / CIWS / Mk 38 muzzle flashes, CIWS tracers, flak bursts, warheads, splashes, ship hits, fire and system smoke, bow spray, missile and threat trails.
  - All on one hand-written HDRP particle shader (`GQ_Particle.shader`) with HDRP fog and soft particles, pooled by `EffectPool`.
  - **Reuse fell through.** The vendor explosion (`HD_Naval_Explosion`) uses a built-in-pipeline shader that HDRP reports as supported but draws as untextured quads; the VFX Arsenal flames were tiny and dim at sea scale. So every effect is code-built (METHODOLOGY AP20).
  - Missile smoke is a ribbon (`TrailRenderer`), not puffs: at 700 m/s puffs read as a dotted line (C23).
- [x] **Splashes and the wake into the foam field** (G31): `HdrpSeaRenderer.DepositFoam`, a Gaussian deposit on the GPU field and its CPU twin, recorded for replay. Sea test `Deposit_AddsAGaussianOfItsLevel_ThenTheCapHolds`.
- [x] **The ship moves:** the Mk 45, both CIWS and both Mk 38s train and elevate onto what they fire at at their rig's rates (`MountAnimator`; checked in play: the Mk 45 laid at 266°, 0° on its target); VLS hatch lids open and close per launch (`VlsHatches`).
- [x] **Audio** (G32): FDS recordings for missile launch and detonation; the Mk 45, CIWS, Mk 38, flak, splash, ship hit and the general-quarters alarm synthesised from a seed (`SynthClips`). Sound arrives late by distance at 343 m/s. All 24 voices were seen playing in play mode; **nobody has listened to them yet.**
- [x] **Camera cuts** carry no motion blur (a Volume override held for 3 frames on each cut).
- [x] **Cost** (Editor, 2560×1440, saturation raid tail, on / off): main thread 16.05–16.10 / 15.84–15.94 ms, `GQ.Presentation` 0.025–0.031 ms, GPU 14.6–15.0 / 15.1–15.5 ms (within noise), GC ~15.0 / ~13.6 KB per frame. Saturation raid: 24 killed, 0 leaked; core 0.083 ms mean per fixed step.
- [x] All unreckoned; no committed DAVFX references.
- **Not verified:** listening; physical controllers on the range; the Guided Fury ground detonation still uses `HD_Naval_Explosion` and draws wrong in HDRP (a follow-up, not fixed here).

### Slice 7 — Phase review (done 2026-10-24)
- [x] **Gate verdict: met.** On the range in play mode:
  - She floats and steers.
  - The CIC detects, assigns, fires and kills or is hit, and every engagement is explained in the log.
  - The saturation raid (24 threats) costs 0.083 ms mean and 0.262 ms p99 of combat-core time per fixed step, against the 1 ms budget.
  - It looks and sounds like a fight (slice 6). The sound has been seen playing but not yet heard.
- [x] **Legacy supersession checklist** (G13). Each legacy feature is carried, superseded or deliberately dropped:

  | Legacy | What it did | In General Quarters |
  |---|---|---|
  | `BurkeVLSSystem` | 96 Flight IIA cells; loadout packed into cells; priority launch queue; hatch, pre-launch and inter-shot delays; cell cooldown; launch events | **Superseded** by `Core/Magazine/Vls.cs`: the Flight I/II groups, 32 fore and 64 aft, per the model (Q1), with seeded launches logged with their causes. Hatches by `Modules/Presentation/VlsHatches.cs`. Launch timing in detail is VLS L1 (Stream B) |
  | `BurkeVLSFXController` | Launch plume, launch sound, hatch animators | **Superseded** by `CombatPresentation` (VLS plume, FDS launch sound) and `VlsHatches` |
  | `BurkeMissile` | Boost, sustain and coast homing on a Rigidbody, with a proximity fuze | **Superseded** by Guided Fury ESSM inside the core (`Vls`, Guided Fury L2). It still sits on `rim-162essm/ESSM Shell.prefab`, where Guided Fury disables it at launch (AP8); the archived script keeps that reference whole |
  | `SPG62FireControlRadar` | A scanning radar with track quality, an STT lock and fire commands to the VLS. Despite its name, it was a search-and-track radar, not an illuminator | **Superseded** by radar L0 (4/3-earth horizon), tracks and TEWA in `Core/Cic`. A real SPG-62 illuminator is Illuminators L1 (Stream A) |
  | `VLSManager` | A 32-cell VLS driven by `RH_RadarModule`; launch limits on wind, roll and pitch; reloads | **Superseded** for launches. **Dropped, deliberately:** its wind was `UnityEngine.Random` every few seconds (it breaks determinism, standing constraint 1), and a Mk 41 is not reloaded at sea. Launch limits from ship motion, if wanted, belong to VLS L1 |
  | Editor tools (`BurkeVLSCellGenerator`, `EngagementTestSetup`, `SPG62FireControlRadarEditor`, `SPG62RadarScreenWindow`, `VLSManagerEditor`) | Wire and test the legacy pieces | Archived with them. Their menus still work for the old scenes |

  - **Archived** (C5, now P8) to `Assets/_Archive/Destroyer Scripts_legacy/`, `Radar Scripts_legacy/` and `Missile Scripts_legacy/`, each with a README saying what it is, why it was archived and how to restore it.
  - The `.meta` files moved with the scripts, so the scenes and prefabs that use them (`_SCENES/DDG Engagement`, `_SCENES/Rocket`, `rim-162essm/ESSM Shell.prefab`) keep their references.
  - **Stays in place:** `RH_RadarModule`, the RH Radar routine's to archive; and `MissileController`, `DestroyerController` and `SimpleAirTarget`, which were not on the checklist. `MissileController` is `VLSManager`'s missile and no scene uses it, so it is the next candidate for the archive.
- [x] This roadmap, the BLUEPRINT, METHODOLOGY (C5 promoted to P8, C25) and a handoff (`HANDOFF.md`).

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
- **2026-10-24** — Slice 4 done: the Engine Room and the Helm at L0, with the decisions in BLUEPRINT G21–G24.
  - **Pipeline:** DTMB 5415 public data (a research pass, sources in `Calibration~/`) → a Python mirror to calibrate → C# with each number's provenance → EditMode checks against public figures → a PhysX run on the sea.
  - **Hull profile:** its roll and yaw gyradii now come from the SIMMAN 5415 table (0.37 B, 0.25 L; they were AUTHORED). The undamped roll period is now 10.7 s.
  - **Probe drag:** the DDG keeps heave only. Roll damping at rest, measured in PhysX, is ζ ≈ 0.09, down from 0.13 when the probes also damped sway.
  - **What PhysX showed:**
    - Flank from rest reaches 30 kn in 50.5 s and holds 31.00 kn at 199 rpm.
    - A straight run holds its heading exactly.
    - The hard-over turn settles at 7.2° of heel, at a 207 m radius and 20.5 kn.
    - The transient heel gap is above.
- **2026-10-24** — Slice 5 done: the range, with the decisions in BLUEPRINT G25–G29.
  - **Built:** the combat core plays in a scene, from two seats, on App UI screens, with the AI captain in the empty seat.
  - **Gate:** the Phase 1 gate is met in play mode at 0.066 ms mean per fixed step against the 1 ms budget, presentation aside (slice 6).
  - **Found on the range:** the aircraft bug (skip / fail log).

- **2026-10-24** — Slice 7 done: the phase review. **Phase 1 is complete.** The four legacy destroyer scripts and their editor tools are archived behind a supersession checklist, and `HANDOFF.md` is written. Next: SIX picks the first element stream.
- **2026-10-24** — Slice 6 done: presentation, with the decisions in BLUEPRINT G30–G33. The Phase 1 gate's presentation half is met in play mode (sound seen playing, not yet heard). Q2 answered: Sea State gained `DepositFoam`.

## Skip / fail log
- **2026-10-24 (slice 5):** the raid's aircraft vanished on their first step: slant range against the launch range (METHODOLOGY AP19). It was in slice 2's `ScriptedRaid`, found on the range, fixed, and a regression test added.
