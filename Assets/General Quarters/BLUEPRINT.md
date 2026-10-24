# General Quarters — blueprint

> **Status: LOCKED BLUEPRINT, build starting.** The 20 questions were answered by SIX on 2026-10-23; Phase 1 began the same day.
> **This document's one job:** say what General Quarters is and why, record every decision behind it, and define the contract each combat element follows. Work order and progress live in `ROADMAP.md`, not here.

## 1. In plain language

**The ask.** SIX, 2026-10-23:

> "Make the destroyer a living breathing ship with a respectable and modular combat system where combat elements can be developed to levels of fidelity individually. I want colliders, weapon systems, buoyancy, navigation, and what you feel like is good."

**General Quarters** is the call that sends a crew to battle stations. Here it is the module that brings the DDG-51 to life:
- a hull that floats and moves on the Sea State sea;
- an engine room and a helm;
- a combat information center (CIC) that sees, decides and fires;
- a magazine;
- damage control that keeps her afloat.

**Every combat element sits behind its own contract and owns its own fidelity ladder** (§4). A per-ship loadout picks the level of each element independently. So the radar can be at L2 while the 5-inch gun is still at L0, and each element is deepened and tested on its own.

**One rule makes it fit the rest of Deep Water: the ship is commanded only through Orders.** The captain, the TAO, the AI captain and the scenario all issue the same Orders on the fixed tick. That keeps the combat core deterministic and ready for Reckoning (`RECKONING-CONCEPT.md`, branch `feat/reckoning-physics-spike`) from day one. It holds even while the hull still moves with PhysX.

## 2. Vocabulary

Parts are named after the real shipboard spaces that do the job.

| Name | What it is | Code |
|---|---|---|
| **General Quarters** | The module | `DeepWater.GeneralQuarters`, `Assets/General Quarters/` |
| **Hull** | Buoyancy, mass properties, colliders, compartments | `Hull` |
| **Engine Room** | Propulsion and the engineering plant | `EngineRoom` |
| **Helm** | Steering, autopilot, routes, navigation sensors | `Helm` |
| **CIC** | Sensors → tracks → threat evaluation and weapon assignment (TEWA) → fire control | `Cic` |
| **Magazine** | Weapons and their stores: VLS cells, gun mounts | `Magazine` |
| **Damage Control** | Damage, flooding, fire, repair | `DamageControl` |
| **Order** | The only way anything changes the ship | `Order` |
| **Doctrine / ROE** | Per-weapon auto / semi-auto / manual; weapons free / tight / hold | `Doctrine` |
| **Loadout** | Per-ship asset choosing each element's fidelity level and profile | `ShipLoadoutSO` |
| **Engagement log** | Cause records for notable combat events (Reckoning's Inquiry, at L0) | `EngagementLog` |

## 3. Architecture

### 3.1 Two halves

| | The combat core (CIC, Magazine, Damage Control, doctrine) | The physical ship (Hull, Engine Room, Helm) |
|---|---|---|
| Code | Pure C# (P6). No `MonoBehaviour`, no `Time`, no `UnityEngine.Random`, no `Physics`, no scene queries | PhysX `Rigidbody` + Sea State `BuoyantBody` + force-based propulsion, behind adapters |
| Determinism | **Deterministic from day one** (G1): seeded RNG (P7), fixed tick, fixed system order, per-tick fingerprint | PhysX motion, not deterministic across machines. Swaps to Reckoning's own ship motion (variant B, R35) behind a seam when that lands |
| Talks to the world through | A **truth feed** each tick: own-ship state; truth targets (position, velocity, kind, signature); environment (sea state) | Unity, via adapters |

### 3.2 One tick, a written order
A `MonoBehaviour` adapter calls `CombatSystem.Step(tick)` from `FixedUpdate` (P2), at 50 Hz. Systems run in this order, each on its Watch (a whole-number tick period; Reckoning R15):

1. **Orders:** apply any queued Orders.
2. **Sensors:** each sensor sweeps on its own Watch.
3. **Tracks:** correlate detections into tracks.
4. **Doctrine / TEWA:** evaluate threats, assign weapons where doctrine allows.
5. **Fire control:** turn assignments into firings: launch missiles, lay guns.
6. **Weapons in flight:** step missiles (Guided Fury entities) and shells; resolve intercepts.
7. **Damage control:** apply hits; degrade systems.
8. **Fix:** fingerprint the combat state.

### 3.3 Orders
Every Order carries the issuer (Captain, TAO, AI captain, Scenario) and the tick, and goes into the order log.

| Order | Fields | Issued by |
|---|---|---|
| `HelmOrder` | course °, speed kn (or a route) | Captain, AI |
| `EngageOrder` | track, weapon class or mount, rounds | TAO, AI, doctrine (auto) |
| `CeaseFireOrder` | track or all | TAO, AI |
| `DoctrineOrder` | weapon class → Auto / SemiAuto / Manual | TAO |
| `RoeOrder` | WeaponsFree / WeaponsTight / WeaponsHold | Captain, scenario |

**Switchable seats (G3):** a player in a seat issues that seat's Orders; an empty seat is filled by the AI captain issuing the same Orders.

### 3.4 Doctrine and ROE (G14)
- **Auto:** TEWA fires on its own.
- **SemiAuto:** TEWA proposes; the TAO confirms with an `EngageOrder`.
- **Manual:** only TAO Orders fire.
- **ROE:**
  - **WeaponsFree:** engage anything not identified friendly.
  - **WeaponsTight:** engage only tracks identified hostile.
  - **WeaponsHold:** fire only in self-defence or on direct order.
- **Defaults:** CIWS on Auto.
- **Every automatic engagement is written to the engagement log with its reason.** The rule, its numbers and the doctrine/ROE that allowed it, e.g.:

  > TEWA: track 12 (hostile missile, CPA in 21.4 s, 9.8 km) → ESSM (band 2–30 km); doctrine Auto, ROE WeaponsFree.

### 3.5 Weapons and Guided Fury
- Missiles are Guided Fury `MissileEntity` instances. The CIC builds them headless from a `MissileProfileSO` and steps them on its own tick.
- **Two levels are chosen separately:** Guided Fury's own ladder (L0 kinematic … L4 full aero) is the *missile's* fidelity, picked in the loadout per weapon; the VLS ladder is the *launcher's* fidelity.
- A track-backed `ITargetSource` feeds guidance from CIC tracks, not from scene transforms.
- **Intercepts are resolved in the core** using closest approach over each step (relative motion within the step), not a per-frame distance check. At 1 km/s closing a missile moves 20 m per 20 ms step, so a per-frame check misses.

### 3.6 Data (G15)
- Ship and weapon numbers come from public, unclassified sources, cited in each profile asset's provenance text.
- Anything estimated is marked **AUTHORED**, as Sea State's profiles and the ESSM profile already do.

## 4. The fidelity ladder

**The contract.**
- Each element is an interface plus one implementation per level, built by a factory from `(level, profile)`.
- A loadout requests a level per element. If that level isn't implemented yet, the factory falls back to the highest implemented level **and says so**: in the log, and on the HUD later.
- Each implemented level ships with EditMode tests, and the element's tests run at every level it has.

**The ladders.** **L0 is Phase 1.** Higher levels are roadmap streams.

| Element | L0 (honest, simple) | L1 | L2 | L3+ |
|---|---|---|---|---|
| **Hull** | Blender-authored hull on Sea State probes; compound colliders; PhysX motion | Compartments feed buoyancy: flooding changes list and trim | Reckoning ship motion (added mass, variant B) | — |
| **Engine Room** | MMG standard method (G22): hull derivatives of DTMB 5415, two shafts with the MARIN thrust curve, twin rudders with lift and stall, resistance from the ITTC campaign; calibrated to USS Barry's handling figures (G24) | Plant: 4 × LM2500, controllable-pitch props, fuel; 4-DOF roll terms | Casualties from damage; plant states | — |
| **Helm** | Manual helm, telegraph bells, course and speed hold, waypoint routes with wheel-over; truth position | GPS/INS drift, fixes | Radar navigation | Collision avoidance, station keeping |
| **Radar (SPY-1D)** | Detects every target within instrumented range and above the radar horizon, on its Watch; no noise | Radar equation with RCS in dBsm, Pd/Pfa, sea clutter from Sea State | Beam and energy scheduling, track quality | Multipath, ducting |
| **Illuminators (SPG-62 ×3)** | Not modelled; missiles home on track truth | Three terminal-illumination channels: engagements wait for a free illuminator | Slew and scheduling | — |
| **Tracks** | Perfect correlation: one track per target, truth state | Noisy measurements, α-β/Kalman filter, track quality | Multi-sensor fusion (ESM later) | — |
| **TEWA** | Rank hostile tracks by time to closest approach; assign by weapon range bands | Pk-weighted pairing, shoot-look-shoot vs salvo | Inventory-aware optimisation | — |
| **VLS (Mk 41)** | Cells, loadout counts (ESSM quad-packed), launch interval; Guided Fury missile at the loadout's missile level; seeded Pk draw on intercept | Hatch and launch sequence timing; midcourse uplink | — | — |
| **Guns (Mk 45 / CIWS / Mk 38)** | Burst model: time of flight from range, seeded Pk(range) per burst, rate of fire, magazine, firing arcs | Point-mass ballistics with drag and dispersion; mount slew; firing on the roll | Fire-control solution with ship-motion prediction and track noise | — |
| **Harpoon / Tomahawk** | Guided Fury profiles at kinematic levels (profiles to author) | Sea-skimming / waypoint profiles | — | — |
| **Damage Control** | Hit points per system and hull; systems at zero go offline | Compartments + flooding into buoyancy probes | Fire spread + damage-control parties | — |
| **AI captain** | Named, recorded rules | ML-Agents trained captain (G19) | — | — |
| **Presentation** | Full pass in Phase 1 (G18): weapon effects, splashes into the foam field, fire and smoke, audio. Unreckoned | — | — | — |

## 5. Decision register

All entries are SIX's picks of 2026-10-23, in round order. Leaning is Claude's recommendation before the pick.

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| G1 | Relation to Reckoning | **Hybrid:** pure, deterministic, Order-driven combat core; PhysX + Sea State hull until variant-B motion lands | Hybrid | Locked |
| G2 | First combat loop | **All elements thin first (L0),** then deepen one at a time | Same | Locked |
| G3 | Player | **Switchable** captain / TAO; AI fills the empty seat | Same | Locked |
| G4 | Hull geometry source | **Authored in Blender** from a copy of the DDG `.blend`: collision hull, compartments, articulated Mk 45 / CIWS / VLS hatches | Generate from mesh | Locked |
| G5 | Damage depth (first full pass) | **Flooding + fire + damage control** (ladder L0 → L2) | Compartments + flooding | Locked |
| G6 | Opponents | **Raids, then a peer** ship on the same combat system | Same | Locked |
| G7 | Sensors | **Radars first** (SPY-1D, SPG-62 ×3); ESM and EO/IR next; sonar later | Same | Locked |
| G8 | Weapons in the first pass | **VLS SAMs (ESSM, SM-2); guns (Mk 45, CIWS, Mk 38); Harpoon and Tomahawk.** Not decoys or torpedoes | VLS + guns | Locked |
| G9 | Propulsion | **Force-based + plant** (ladder L0 → L1) | Force-based | Locked |
| G10 | Navigation | **Helm, autopilot, routes + nav sensor errors** (ladder L0 → L1) | Helm/autopilot/routes | Locked |
| G11 | Interface | **Tactical display + panels,** orbit/chase camera | Same | Locked |
| G12 | Home | **New module + new range scene;** DDG Engagement untouched until migrated | Same | Locked |
| G13 | Legacy destroyer code | **Supersede, then archive** (C5) once the range scene proves it | Same | Locked |
| G14 | Autonomy | **Doctrine modes + ROE,** CIWS Auto by default, reasons logged | Same | Locked |
| G15 | Data | **Public sources, flagged** | Same | Locked |
| G16 | Scale | **1 DDG, raids up to ~24, 1 peer;** ~1 ms combat-core time per fixed step | Same | Locked |
| G17 | Name | **General Quarters**; parts named after real spaces | Same | Locked |
| G18 | Presentation | **Full presentation now** (Phase 1) | Existing packs, honest | Locked |
| G19 | AI captain | **Named rules now, ML-Agents later** | Named rules | Locked |
| G20 | Phase 1 | **L0 engagement on the range** | Same | Locked |
| G21 | Sea State probe drag on a propelled ship (2026-10-24, slice 4) | **Per-axis scale in the hull's frame** (x sway, y heave, z surge); the DDG keeps heave only | Vertical-only switch | Locked |
| G22 | Engine Room L0 hull forces | **MMG standard method** (Yasukawa & Yoshimura 2015), twin screw and twin rudder | Linear derivatives (Clarke) | Locked |
| G23 | Speed orders | **Bells and knots:** telegraph bells for the conn; the autopilot holds knots | Knots only | Locked |
| G24 | Calibration data | **DTMB 5415 public data**, calibrated to the USNI 1994 USS Barry figures; every number's source in the profile's provenance | Same | Locked |
| G25 | Range UI technology (2026-10-24, slice 5) | **App UI on UI Toolkit** (`com.unity.dt.app-ui` 2.2.0-pre.8, a pre-release) | Plain UI Toolkit | Locked |
| G26 | Seats on screen | **Switch seats:** one key or button; the screen re-lays out per seat; the AI captain takes the seat left | Same | Locked |
| G27 | The AI captain at the conn | **Route, flank, open arcs** (rules C1–C4) | Same | Locked |
| G28 | Range extras | **Debrief, time compression, threat and missile camera** (not clickable cause chains) | — | Locked |
| G29 | The range's starting doctrine | **ESSM SemiAuto (TEWA proposes, the TAO confirms), guns and CIWS Auto, ROE weapons tight.** Proposed by Claude so the TAO seat has a job; confirmed by SIX 2026-10-24 (Q5); one line in the loadout asset | ESSM Auto (the TAO seat only steers doctrine) | Locked |
| G30 | Effects (2026-10-24, slice 6) | **Mix: reuse where it fits, code-built where it doesn't.** In the event nothing vendor fitted HDRP at sea scale, so all of it is code-built on one HDRP particle shader | Same | Locked |
| G31 | Splashes into the foam field (Q2) | **Yes:** Sea State gains `HdrpSeaRenderer.DepositFoam`, a Gaussian deposit on the residual foam field, GPU and CPU twin, recorded for replay | Same | Locked |
| G32 | Audio | **Recordings where we have them (FDS missile launch and detonation), synthesised from a seed for the rest**, delayed by distance at the speed of sound | Same | Locked |
| G33 | Presentation extras | **Guns track their targets, wake and bow wave, VLS hatches open, no blur on camera cuts** | — | Locked |

Inherited from Reckoning: R2 (everything deterministic eventually), R15 (Watches), R35 (variant B, Proposed), R36 (banned-API guard, Proposed). General Quarters applies R36 to its core now, as a source-scan test.

## 6. Standing constraints

1. **The combat core never touches Unity runtime state.** No `UnityEngine.Time`, `UnityEngine.Random`, `Physics`, `Object.Find*`, `GameObject`, `DateTime.Now` or `Environment.TickCount`; no unseeded `System.Random`. Value types (`Vector3`, `Quaternion`, `Mathf`) are fine.
   - *Why:* determinism (G1).
   - *Enforced by:* `CoreBannedApiTests`.
2. **Randomness is a seeded `DeterministicRng` injected from the loadout.** Every draw that decides an outcome is logged as a chance root (seed, draw index). *Why:* P7, and Inquiry names its luck.
3. **The ship changes only through Orders.** *Why:* switchable seats, replay, the Reckoning seam.
4. **Tuning lives in ScriptableObjects** (P1), with cited provenance (G15).
5. **Vendor code is read-only** (P5). DAVFX is never referenced by a committed asset, because the public repo can't carry it.
6. **Never modify the original DDG `.blend` or prefab.** Work on copies (G4, G12).
7. **The ~1 ms combat-core budget per fixed step** (G16) is measured with the `unity-perf-ab` harness, never assumed.

## 7. What is deliberately not here

| Not in the first pass | Why / revisit when |
|---|---|
| Decoys (SRBOC, Nulka), torpedoes, sonar | G7, G8. After the peer fight works |
| ESM, EO/IR | G7. The next sensor stream after radar L1 |
| Cooperative engagement across ships | G16. One DDG first |
| Diegetic bridge/CIC consoles | G11. Screen UI first |
| Multiplayer | Reckoning R3: Orders and Fix keep the door open |

## 8. Risks

| Risk | Mitigation |
|---|---|
| **Scope:** five richest-option picks (G5, G9, G10, G18 and the ladders) | Ladders, and phases that each end playable; L0 end-to-end first |
| **Blender dependency for the hull** (G4) | Slice ordering: core and combat first; the Blender MCP must be running for the hull slice |
| **Hull motion is not cross-machine deterministic** under PhysX | Explicit seam; Reckoning variant B later (R35) |
| **Radar Suite is MonoBehaviour-bound** (`Time.time`, GameObject contacts) | General Quarters owns a pure sensor contract; Radar Suite physics ported at radar L1+ |
| **Guided Fury fuzing lives in its MonoBehaviour** (`Physics.OverlapSphere`) | The CIC resolves intercepts in the core with closest approach |

## 9. Open questions for SIX

| # | Question | Cost |
|---|---|---|
| ~~Q1~~ | ~~Which DDG-51 flight is the model?~~ **Answered 2026-10-23 by the geometry: Flight I/II.** Two CIWS, Mk 141 Harpoon quads aft, Mk 32 torpedo tubes, no hangar; VLS 32 fore and 64 aft. Its textures are named DDG112 (a Flight IIA hull number); the mesh wins. The loadout follows Flight I/II | — |
| ~~Q2~~ | ~~May Sea State gain a small API to deposit foam and splashes?~~ **Answered 2026-10-24: yes** (G31). | — |
| Q3 | The peer ship: a mirror DDG, or a different class? | 2 min |
| ~~Q5~~ | ~~G29: ESSM on SemiAuto or Auto at the start of a run?~~ **Answered 2026-10-24: SemiAuto** (G29 Locked). | — |
| ~~Q4~~ | ~~Merge path to `main`?~~ **Answered 2026-10-24:** SIX had everything brought up to `main` (4f8149b). `feat/general-quarters` stays the working branch, kept in step with `main`. | — |

## Change log
- **2026-10-23** — Blueprint locked from the 20-question interview (G1–G20). Phase 1 started.
- **2026-10-24** — Slice 5 decisions G25–G28 (SIX) and G29 (Proposed); Q5 added.
- **2026-10-24** — Slice 6 decisions G30–G33 (SIX); Q2 answered.
- **2026-10-24** — Q5 answered by SIX: ESSM starts a run on SemiAuto; G29 Locked.
