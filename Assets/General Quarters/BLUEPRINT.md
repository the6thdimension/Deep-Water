# General Quarters — blueprint

> **Status: LOCKED BLUEPRINT; Phase 1 complete (2026-10-24).** The 20 questions were answered by SIX on 2026-10-23; Phase 1 (every element at L0, played end to end on the range) began the same day and closed with slice 7. Picking it up cold: `HANDOFF.md`.
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
| **VLS (Mk 41)** | Cells, loadout counts (ESSM quad-packed), launch interval; Guided Fury missile at the loadout's missile level; seeded Pk draw on intercept | Per-cell launch delay; guidance channels (G69) | — | — |
| **Guns (Mk 45 / CIWS / Mk 38)** | Burst model: time of flight from range, seeded Pk(range) per burst, rate of fire, magazine, firing arcs | Point-mass ballistics with drag and dispersion; mount slew; firing on the roll | Fire-control solution with ship-motion prediction and track noise | — |
| **Anti-ship and land-attack cruise missiles** (archetypes, G69) | Guided Fury archetypes at kinematic levels (to author) | Sea-skimming / waypoint archetypes | — | — |
| **Damage Control** | Hit points per system and hull; systems at zero go offline | Compartments (decks and wing spaces) and orifice flooding; the water's mass and centre of mass ride on the hull (G53–G60) | Fire spread + damage-control parties | — |
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
| G13 | Legacy destroyer code | **Supersede, then archive** (C5) once the range scene proves it. Done 2026-10-24 (slice 7): the checklist is in `ROADMAP.md` | Same | Locked |
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
| G34 | Stream B order (2026-10-24) | **Guns L1 first**, then VLS L1 with SM-2, then Harpoon and Tomahawk | Same | Locked |
| G35 | Gun ballistics at L1 | **3-DOF point mass:** gravity, ISA atmosphere, drag from a standard curve; no wind or Coriolis | Same | Locked |
| G36 | How a burst kills at L1 | **Shot group:** one ballistic solution per burst. The miss distance (aim error, ship motion, bias) and a dispersion σ give a per-round hit probability against the target's presented area (or a proximity fuze's lethal radius); one seeded draw per burst | Same | Locked |
| G37 | Mount slew and ship motion at L1 | **Slew plus roll error:** mounts train and elevate at real rates in the core and fire only once laid. The ship's roll and pitch rates times the solution latency are aim error; L1 fire control does not predict motion (L2) | Same | Locked |
| G38 | Lead | **Ballistic lead:** iterate the time of flight against the track's constant-velocity prediction | Same | Locked |
| G39 | Mk 45 against air | **Proximity fuze against air, point-detonating against surface targets** | Same | Locked |
| G40 | CIWS closed loop | **Modelled:** each burst starts with an aim bias that Phalanx's spotting shrinks within the burst, carried between bursts on the same track | Same | Locked |
| G42 | VLS L1 scope (2026-10-24, slice B2) | **Launch sequence, uplink and ship velocity, without the vertical boost:** per-cell hatch, ignition and clear-the-cell timing; the ship's velocity added at launch; midcourse uplink (G43). *Reversed by SIX on 2026-10-24 to Claude's leaning, from:* ~~a real vertical launch: the missile climbs vertically, then pitches over within a constrained rate before it can turn toward the target (this costs a minimum range)~~ | Same | Locked (reversed); **superseded by G69** (2026-10-27) |
| G43 | Midcourse uplink | **Channels and an update rate:** in midcourse the missile flies to a predicted intercept point that SPY-1 refreshes at an uplink rate. Only a limited number of missiles can be guided at once (AUTHORED) | Same | Locked; **superseded by G69** (2026-10-27) |
| G44 | Illuminators (pulled forward from Stream A) | **In this slice:** terminal homing needs one of three SPG-62s for its last seconds | Same | Locked; **superseded by G69** (2026-10-27) |
| G45 | SPG-62 model | **Real arcs and slew:** one forward and two aft, each with its own arc, slew and settle time. A missile entering terminal claims a free illuminator that can see the target, assigned by time to go | Same | Locked; **superseded by G69** (2026-10-27) |
| G46 | Missile fidelity in the core | **SM-2 at Guided Fury L2, like ESSM.** No L4 bridge. *Reversed by SIX on 2026-10-24 to Claude's leaning, from:* ~~ESSM and SM-2 both at Guided Fury L4 (full aero), with the bridge baking Guided Fury's aero tables for headless use~~ | Same | Locked (reversed); **superseded by G69** (2026-10-27) |
| G47 | Budget with L4 | **Keep 1 ms (G16).** If L4 cannot meet it at saturation, missiles beyond a set number in flight run at L3, and the log and the HUD say so. *Since G46's reversal (2026-10-24) the missiles fly at L2, so this fallback applies only if a missile level is raised later; the 1 ms gate itself stands* | Same | Locked; **superseded by G69** (2026-10-27) |
| G48 | SM-2 variants | **Block IIIA (RIM-66M-5) only.** *Reversed by SIX on 2026-10-24 to Claude's leaning, from:* ~~both Block IIIA and Block IIIB (adds an IR terminal seeker)~~ | Same | Locked (reversed); **superseded by G69** (2026-10-27) |
| G49 | Block IIIB's IR seeker | ~~IR backup now: if no illuminator is free, IIIB homes on IR from an AUTHORED acquisition range. Flagged: it eases the illuminator bottleneck and may flatter saturation results.~~ **Moot since 2026-10-24:** with G48 reversed there is no IIIB, so no IR seeker is built and the flag goes with it. Every SM-2 and ESSM needs a free illuminator to complete an intercept (G44) | IIIB flies as IIIA until EW exists | Moot; **superseded by G69** (2026-10-27) |
| G50 | VLS load | **64 SM-2 Block IIIA and 32 ESSM** (8 cells quad-packed); 24 cells left for Tomahawk and VLA. AUTHORED. *Changed 2026-10-24 with G48's reversal, from:* ~~48 IIIA and 16 IIIB~~ | Same | Locked; **superseded by G69** (2026-10-27) |
| G51 | Salvo doctrine | **SM-2 in pairs (shoot-shoot), ESSM one at a time.** Shoot-look-shoot stays TEWA L1 (Stream A) | Same | Locked; **superseded by G69** (2026-10-27) |
| G52 | Where the missile work lands | **Through Guided-Fury:** a feature branch cut from `Guided-Fury` for the SM-2 Block IIIA profile, a PR into `Guided-Fury`, then everything levels. *The L4 bridge left this item with G46's reversal (2026-10-24)* | Same | Locked; **superseded by G69** (2026-10-27) |
| G41 | Gun data | **The G7 standard drag curve (BRL) times one form factor per projectile, CALIBRATED to its published range;** dispersions and lethality AUTHORED where nothing is public; other published figures are VALIDATION | Same | Locked |
| G53 | Damage Control L1: where compartments come from (2026-10-24, Stream C) | **Hybrid:** Blender's 12 bulkhead stations stay (G4). The hull profile adds decks and side splits as data. Each space's volume is measured from the hull's own probe columns, so it follows the real hull shape. No Blender re-export | Same | Locked |
| G54 | Subdivision | **Decks and wing spaces:** each station splits at the damage-control deck into a hold and an upper space. The six midship holds get port and starboard wing spaces outboard. There is no centreline bulkhead (USN practice: asymmetric flooding capsizes ships). The deck height and the wing breadth are AUTHORED | Same | Locked |
| G55 | How flood water reaches the hull | **The Rigidbody's mass and centre of mass:** the water's mass is added to the Rigidbody, and its centre of mass moves to the combined centroid. The water's own centroid comes from a level that stays horizontal in the world, recomputed every step, so as she heels the water runs to the low side. That is the free-surface effect, with no formula bolted on. The inertia tensor is carried to the new centre by the parallel-axis rule; products of inertia are left out at L1 | Added weight as forces on the probe columns | Locked |
| G56 | Flooding model | **Orifice flow through openings:** Q = Cd·A·√(2g·Δh) on the head at each opening, in both directions. Holes join a space to the sea. Authored openings join spaces: hatches in the damage-control deck, doors in the bulkheads above it, and weather openings at the main deck. Each space holds its volume × permeability (SOLAS values). Flooding progresses when water reaches an opening | Same | Locked |
| G57 | What opens a hole at L1 | **A Breach Order** (a ship-frame point and an area, issued by the scenario, a range control or a test) **and leaker hits.** A hit's hole is sized from its warhead, area ∝ W^(2/3) (cube-root blast scaling), CALIBRATED to one public figure: USS Stark, 1987, a 165 kg Exocet warhead and a 10 × 15 ft hole. **Flagged:** one figure, and Stark's hole was above the waterline | Same | Locked |
| G58 | The crew's response at L1 | **Physics only.** Pumps, shoring, plugging and counter-flooding come with the damage-control parties at L2 | Same | Locked |
| G59 | HUD and debrief | **A damage-control board on both seats:** a side view and plan of the spaces coloured by fill; list, trim, draft, GM (intact → now), reserve buoyancy and inflow. **A flooding timeline in the debrief:** breaches, spaces flooded, peak list and trim, GM lost, saved or lost, each line with its cause | Same | Locked |
| G60 | Losing the ship at L1 | **Counted from the flooding:** SINKING when the water aboard leaves less buoyant volume than her weight (reserve buoyancy ≤ 0); CAPSIZED past an AUTHORED heel held for an AUTHORED time. L0 hull hit points still damage systems but no longer decide whether she is lost | Same | Locked |
| G61 | Radar L1: where the radar physics lives (2026-10-27, Stream A; R1) | **Split the suite's pure core** (`Core/Detection`, `Core/Tracking`) into its own engine-free assembly, `RHRadarSuite.Core`, referenced by both RH Radar Suite and General Quarters. The empty `Assets/RH Radar` plan is struck with a reason | Same | Locked |
| G62 | Which radars at L1 (R2) | **SPY-1's role only:** one search-and-track radar, end to end. The SPG-62 illuminators stay Stream B's (G44) | Same | Locked |
| G63 | Where its numbers come from (R3) | **A generic S-band array in SPY-1's role, AUTHORED, calibrated to one public detection-range figure, flagged** (as G57). No claim about the real system's performance (standing constraint 8) | Same | Locked |
| G64 | Tracks with it (R4) | **Radar L1 and Tracks L1 in one slice:** the suite's track file (α-β, M of N, gating) replaces Tracks L0, so fire control reads smoothed tracks, never truth | Same | Locked |
| G65 | Target radar cross-section (R5) | **One dBsm per `TargetKind`, AUTHORED.** Aspect lobes wait for the suite's Phase 2 | Same | Locked |
| G66 | How a look decides "seen" (R6) | **A Swerling 1 Pd draw per look at a fixed Pfa,** from the run's seeded generator, so the Fix replays it (Reckoning R3) | Same | Locked |
| G67 | Sea clutter (R7) | **Constant-γ, with γ from Sea State's sea state;** finding a public γ-by-sea-state table is the slice's first task. If none holds up, one AUTHORED γ, flagged | Same | Locked |
| G68 | Identification (R8) | **Identity from truth, as L0.** IFF is its own later step | Same | Locked |
| G69 | Slice B2 at game level (2026-10-27; SIX: "Just simplify the plans and generalize them to make them safer"). **Supersedes G42–G52**, whose text stays above as the record | **Game level, generic.** (a) **Missiles:** the area SAM archetype (Guided Fury `ROADMAP.md` A4) and the point-defence SAM already built, both at Guided Fury L2 with AUTHORED numbers. No real-system profile, block or variant, and no flyout validation against real performance. (b) **VLS L1:** an AUTHORED per-cell launch delay and the ship's velocity added at launch; no vertical boost. (c) **Guidance channels:** the ship guides at most an AUTHORED number of missiles at once and refreshes each one's aim point at an AUTHORED interval. (d) **Illuminators:** three channels, one forward and two aft, each with an arc and an AUTHORED slew-and-settle time. A missile's last seconds need a free channel that can see the target, assigned by time to go; with none free it cannot complete its intercept. (e) **Load:** 64 area SAMs and 32 point-defence SAMs (8 cells quad-packed); 24 cells left for other loads. AUTHORED. (f) **Salvo doctrine:** area SAMs in pairs, point-defence SAMs one at a time; shoot-look-shoot stays TEWA L1. (g) **Budget:** 1 ms at saturation (G16); a missile level raised later falls back out loud (G47's rule). (h) **Where it lands:** the area SAM through a branch cut from `Guided-Fury` and a PR into it (G52's route). G8's and G34's missile names read as archetypes from here on, and §4's rows follow. Checked against standing constraint 8 | — | Locked (SIX's directive, 2026-10-27; written by Claude from G42–G52) |

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
8. **Safe limits (SIX, 2026-10-24).** General Quarters is a game simulation, built at the level of commercial and hobby naval sims, and it stays there. The test is whether the work could help someone cause real-world harm.
   - **Stays fine:**
     - public, cited specifications at the level of reference sites, fact files and academic papers (G15);
     - textbook physics and engineering models: ballistics, the radar equation, manoeuvring, buoyancy, flooding;
     - abstract lethality: hit points, AUTHORED kill chances, systems going offline;
     - fictional or generic scenarios, raids and opponents, and AI captains that play the game.
   - **Not done:**
     1. **No non-public data:** nothing classified, leaked, export-controlled (ITAR/EAR technical data) or for official use only, even when it is easy to find. A figure that is not public stays AUTHORED.
     2. **Nothing that runs on real hardware:** no guidance, autopilot, fire-control or seeker code meant to drive physical actuators, drones, model rockets or real sensors. The core stays a game simulation.
     3. **No real-world targeting:** no strike planning against real places, real coordinates, real infrastructure or real people. Land attack (Tomahawk) goes against fictional or generic targets only.
     4. **No engineering-grade lethality:** no warhead or fragmentation design, and no vulnerability models of real ships, systems or people meant to predict or improve real-world damage. Damage stays abstract and coarse; a single public historical figure may calibrate a game mechanic (as G57 does).
     5. **No defeat-optimisation against real systems:** no tuning of tactics, electronic warfare or countermeasures to beat a specific real-world defence or radar. EW and ECCM stay generic.
     6. **No mass-casualty weapons:** nothing chemical, biological, radiological or nuclear, in any form.
     7. **Peer opponents stay generic:** a mirror DDG or a fictional class. "How to defeat a specific real navy's current ships" is not a goal.
   - *How to apply:* before a slice is built, check its items against this list. A request that would cross a line is said so plainly and reshaped toward the game side; it is not quietly built.

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
| ~~Q6~~ | ~~Radar L1 (Stream A): R1–R8 in `RADAR-L1-QUESTIONS.md`.~~ **Answered 2026-10-27 by SIX: every leaning taken** (G61–G68). | — |
| ~~Q5~~ | ~~G29: ESSM on SemiAuto or Auto at the start of a run?~~ **Answered 2026-10-24: SemiAuto** (G29 Locked). | — |
| ~~Q4~~ | ~~Merge path to `main`?~~ **Answered 2026-10-24:** SIX had everything brought up to `main` (4f8149b). `feat/general-quarters` stays the working branch, kept in step with `main`. | — |

## Change log
- **2026-10-23** — Blueprint locked from the 20-question interview (G1–G20). Phase 1 started.
- **2026-10-24** — Slice 5 decisions G25–G28 (SIX) and G29 (Proposed); Q5 added.
- **2026-10-24** — Slice 6 decisions G30–G33 (SIX); Q2 answered.
- **2026-10-24** — Q5 answered by SIX: ESSM starts a run on SemiAuto; G29 Locked.
- **2026-10-24** — Phase 1 complete (slice 7). G13 carried out: legacy destroyer scripts archived.
- **2026-10-24** — Stream B slice B2 decisions G42–G52 (SIX): VLS L1 with a vertical launch, uplink, SPG-62 illuminators pulled forward, ESSM and SM-2 at L4, both SM-2 blocks, IIIB with IR backup (flagged).
- **2026-10-24** — Reversals (SIX, back to Claude's leaning): G42 drops the vertical boost, G46 puts SM-2 at L2 like ESSM, G48 keeps Block IIIA only. Consequences: G49 moot (no IIIB, no IR seeker, the flag goes), G50 becomes 64 IIIA and 32 ESSM, G47's L3 fallback is unused at L2, G52 loses the L4 bridge. Standing constraint 8 added: safe limits.
- **2026-10-24** — Stream B started: decisions G34–G41 (SIX), Guns L1 first. Guns L1 built (slice B1); sources in `Calibration~/GUNS-SOURCES.md`.
- **2026-10-24** — Stream C started with Damage Control L1: decisions G53–G60 (SIX). Seven matched Claude's leaning; G55 did not (SIX picked the Rigidbody's mass and centre of mass over added-weight forces on the probe columns).
- **2026-10-27** — Q6 added: the Radar L1 questions prepared overnight (`RADAR-L1-QUESTIONS.md`); nothing decided.
- **2026-10-27** — Q6 answered by SIX: Radar L1 decisions G61–G68, all eight matching Claude's leaning. `RADAR-L1-QUESTIONS.md` superseded. Nothing built.
- **2026-10-27** — The ballistics (`Core/Ballistics`: Atmosphere, DragCurve, FiringTable, ShotGroup) moved to the new **Gunnery** module (SIX: Gunnery K1); General Quarters references `DeepWater.Gunnery` and keeps its mounts, fire control and orders. Module lines: General Quarters is ships, Air Wing planes, Guided Fury all missile behaviour.
- **2026-10-27** — G69 (SIX's directive: "Just simplify the plans and generalize them to make them safer"): slice B2 rewritten to game level and made generic. The area SAM is an archetype with AUTHORED numbers at Guided Fury L2; VLS L1, guidance channels and the three illuminators are game mechanics with AUTHORED times and counts. G42–G52 are marked superseded; their text stays, so the register remains append-only. §4's VLS and cruise-missile rows follow G69. Checked against standing constraint 8. Real-system detail removed. No code, asset or test changed. Engineering-grade original preserved outside the repo (from 2121da9e).
