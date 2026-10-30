# Air Wing — blueprint

> **Status: LOCKED BLUEPRINT (W1–W51); Phases 1 and 2 closed (2026-10-26): the F/A-18E flies at L1 from every device class, takes off and lands, and meets its budget. Phase 3 (AI pilots): interview complete, W36–W51 locked; the AI side is carried in `Assets/RH Aviator/BLUEPRINT.md` (W48), with A1–A6 proposed there.** SIX answered all 24 questions on 2026-10-24 (W1–W24) and locked W25–W31 on 2026-10-25. Progress is in `ROADMAP.md`.
> **This document's one job:** say what Air Wing is and why, record every decision behind it, and define the contract each part of an aircraft follows. Work order and progress live in `ROADMAP.md`, not here. The stock-take that preceded it is summarised in §1.

## 1. In plain language

**The ask.** SIX, 2026-10-24: *"I want to take a turn and deal with my planes."*

**What exists today (stock-take, 2026-10-24, read in source and scene files).** No aircraft in Deep Water meets the project standard.
- The F/A-18E that the scenes use (`Assets/FDS Assets/Aircraft/F-18E.prefab`) runs on AerialArcade course code. It weighs 544 kg (1,200 lb) and pulls its velocity toward the nose on a "rail".
- The `F-18 (1) Variant` in Dogfight runs `FighterJetController`, which never flies: nothing calls its flight code.
- The F-16s run the vendor FlightSimLite model in `Update`, so they fly at the frame rate.
- The UH-60M controller cancels its own weight, so it hovers at zero collective, and tilting never moves it forward.
- No scene or prefab uses any script in `Assets/Scripts/FighterJet Scripts/` or `Assets/Scripts/Aircraft_Handlers/`.
- Carrier Ops is healthy (pure core, profile, tests), but no aircraft is hooked to it.
- RH Aviator is a README and a roadmap, with no code.

**Air Wing** is the Navy's name for every aircraft a carrier embarks. Here it is the module that gives Deep Water real aircraft:
- an airframe with a flight-model ladder;
- a powerplant;
- flight controls;
- landing gear;
- a cockpit (camera and HUD).

The F/A-18E Super Hornet comes first (W2); the helicopter follows as its own stream (W20).

**Two rules make it fit the rest of Deep Water.**
1. **An aircraft changes only through Pilot Inputs** (stick, throttle, pedals, switches) issued by a seat. The player's devices, the mouse-aim instructor and the RH Aviator AI pilot all issue the same Pilot Inputs on the fixed tick (W6, C19). A seat can change hands mid-flight, and the record keeps the same shape (W18).
2. **The flight core is pure C# with its own integrator** (W4). It is deterministic on one machine from day one, with a fingerprint every tick (W7). PhysX is used only for contacts: gear, deck and collisions. This is Reckoning's variant B (`RECKONING-CONCEPT.md`, R35).
   - *Landing gear (slice 2d, SIX 2026-10-26: "as W4 says but with spring damper struts"):* PhysX raycasts find each wheel's contact, its distance and the ground's normal, through the core's `IContactProbe`. Spring-damper struts in the core turn that into force. No Rigidbody, no WheelCollider.

## 2. Vocabulary

Parts are named after the real aircraft systems that do the job (W9).

| Name | What it is | Code |
|---|---|---|
| **Air Wing** | The module | `DeepWater.AirWing`, `Assets/Air Wing/` |
| **Airframe** | Mass, inertia, aerodynamics; the flight model and its ladder | `Airframe` |
| **Powerplant** | Engines: throttle to thrust, afterburner | `Powerplant` |
| **Flight Controls** | The control law: Pilot Inputs to surface commands | `FlightControls` |
| **Landing Gear** | Struts, wheels, brakes, nosewheel steering (L1+) | `LandingGear` |
| **Cockpit** | Cameras and HUD | `Cockpit` |
| **Atmosphere** | ISA standard atmosphere: density, temperature, speed of sound by altitude | `Atmosphere` |
| **Pilot Inputs** | The only way anything changes an aircraft | `PilotInputs` |
| **Seat** | Whoever issues an aircraft's Pilot Inputs: the player, or an RH Aviator AI pilot | `ISeat` |
| **Instructor** | Mouse aim: you point, and it works the stick for you (W15) | `MouseInstructor` |
| **Fidelity sheet** | Per-aircraft asset that picks each part's ladder level and profile | `FidelitySheetSO` |
| **Input log** | Every tick's Pilot Inputs, with the seat that issued them | `InputLog` |
| **Fix** | The per-tick fingerprint of the flight state (Reckoning's word, as in General Quarters) | `FlightFix` |

**Language audit.** The per-aircraft level asset is a *fidelity sheet*, not a "loadout": in aviation a loadout means weapons and stores (W25).

## 3. Architecture

### 3.1 Two halves

| | The flight core (Airframe, Powerplant, Flight Controls, Atmosphere, seats' decisions) | The Unity side (adapters, contacts, cameras, HUD, device readers) |
|---|---|---|
| Code | Pure C# (P6). No `MonoBehaviour`, no `Time`, no `UnityEngine.Random`, no `Physics`, no scene queries | `MonoBehaviour` adapters; PhysX for gear and collision contacts (L1+); `DeepWater.Input` for devices |
| Determinism | **Bit-identical on one machine from day one** (W7): seeded RNG (P7), fixed tick, fixed substeps, fixed order, per-tick Fix. Identical across machines later, through Reckoning | PhysX contacts are repeatable on one machine only, so ground phases are flagged in the Fix (W7) |
| Talks to the world through | A **truth feed** each tick: the sea height under each aircraft from Sea State's `ISeaSurface` (never HDRP water, AP11), runway and deck heights, and contact forces from the previous step (L1+) | Unity, via adapters |

### 3.2 One tick, a written order

An adapter calls `AirWing.Step(tick)` from `FixedUpdate` (P2) at the project's 50 Hz (`Fixed Timestep: 0.02`, never changed by this module, W24). Inside each tick:

1. **Seats:** each seat issues this tick's Pilot Inputs.
   - The player's devices are sampled once, at the tick boundary.
   - The instructor turns the mouse aim into stick inputs.
   - The AI decides on its own Watch (coarse decisions at 2–5 Hz) and works the stick every tick.
2. **Input log:** the Pilot Inputs go into the log with their seat and tick. A replay from the log must reproduce the Fix bit for bit (W28).
3. **Flight Controls:** the control law turns Pilot Inputs into commands for the airframe.
4. **Flight model:** N fixed substeps (for example 4 × 200 Hz, W24) of Airframe, Powerplant and Atmosphere.
5. **Contacts (L1+):** gear and collision forces from PhysX's last step enter as external forces (variant B).
6. **Fix:** fingerprint each aircraft's state: position, velocity, attitude, rates, engine state.

The adapter then writes the transform, interpolated for rendering (Pat5).

### 3.3 Pilot Inputs

| Field | Range | Meaning at every level (W26) |
|---|---|---|
| Stick pitch | −1 … +1 | A commanded load factor (G) between the profile's negative and positive limits |
| Stick roll | −1 … +1 | A commanded roll rate up to the profile's maximum |
| Pedals | −1 … +1 | A commanded sideslip / yaw rate |
| Throttle | 0 … 1, plus an afterburner detent | Engine power |
| Switches | — | Flaps, speed brake, gear (L1+), autopilot modes (altitude hold, approach speed hold), hook (carrier stream) |

The stick means the same thing at L0 and at L1, so the AI and the instructor don't need retuning when an aircraft moves up a level. The Hornet's fly-by-wire already makes its stick a G and roll-rate command (W23).

**Device arbitration (W27).** The last device moved past its deadzone owns the player's seat. The instructor gives way the instant a stick axis or flight key moves.

### 3.4 Seats and RH Aviator (W5, W18)

- Air Wing defines `ISeat` and `PilotInputs`. RH Aviator implements AI seats against them and references Air Wing; Air Wing never references RH Aviator.
- The carrier hookup stays in Carrier Ops, through its existing `IRecoveringAircraft` seam (W5). It is parked until the AI stream is done (W16).
- **Switchable seats:** the player can take or hand back any aircraft mid-flight, and an AI seat flies every empty one.
- **The seat roster** (`Core/Seats/SeatRoster`, W42, RH Aviator A8): one player seat; each aircraft's own seat, handed over by RH Aviator as a plain `ISeat`, or a `HoldSeat` keeping what the player left. The cycle key (Tab, the Xbox D-pad right) steps the player through the aircraft that have not crashed, in id order, between ticks; the jet left goes to its own seat at once. The player takes over at the jet's throttle and switches, and an AI pilot takes back at the player's throttle, so nothing jumps. The camera and the HUD follow the player's aircraft, and the HUD's pick list shows who flies what.

### 3.5 Data (W8)

- Every number comes from public sources, cited in each profile asset's provenance: the U.S. Navy fact file and Boeing's published figures for weights, thrust, wing area and limits, plus NASA's published F/A-18 reports for aerodynamics and the control law.
- **Calibration (C16):** one public figure sets one number. The remaining figures are VALIDATION.
- **AUTHORED** marks anything estimated. NASA's public aerodynamics (the High Alpha Research Vehicle programme) is for the F/A-18A/B, not the E, so the E's aerodynamics are Hornet data scaled to the E's wing area and weight, and the scaling is marked AUTHORED.
- **Units (W22):** SI in the core. Knots, feet, pounds, G, Mach and degrees on the HUD and in quoted public figures, converted at the edge. Every displayed number carries its unit.
- **Empty beats fake:** a value with no source and no defensible estimate stays empty, and the level that needs it is not built.

## 4. The fidelity ladder

**The contract (C14, as in General Quarters).**
- Each part is an interface plus one implementation per level, built by a factory from `(level, profile)`.
- A fidelity sheet requests a level per part. If that level isn't built yet, the factory falls back to the highest built level **and says so**, in the log and on the HUD.
- Every built level ships with EditMode tests, and each part's tests run at every level it has.
- One state struct (`AirframeState`, C4) carries across all levels.

| Part | L0 (honest, simple) | L1 | L2 | L3 |
|---|---|---|---|---|
| **Airframe** (W3) | Point mass with an energy model. Thrust minus drag sets speed along the path. Lift limited by Cl_max and the G limit sets the load factor; turn rate is g·√(n²−1)/V. First-order response to the commanded G and roll rate. Airborne only | 6-DOF rigid body with mass, inertia and aerodynamic coefficients (Hornet data, scaled to the E) | Tabulated aerodynamics against Mach, with ground effect | Fuel mass and centre-of-gravity shift |
| **Powerplant** | Throttle and afterburner to thrust, with a density lapse with altitude (AUTHORED shape) | Spool-up time constant | Thrust tables against Mach and altitude | Fuel flow and burn (with Airframe L3) |
| **Flight Controls** (W23) | Commanded G and roll rate tracked by a first-order lag, clipped to the profile limits | Textbook fly-by-wire in the Hornet's style: G-command pitch, roll-rate roll, AoA limiter, G limiter, yaw damper. Structure after NASA's published HARV control-law papers; gains AUTHORED | Gain schedules against dynamic pressure and Mach; automatic flaps | — |
| **Landing Gear** | None (L0 is airborne only, W29) | Struts, wheels, brakes and nosewheel steering through PhysX contacts | — | — |
| **Atmosphere** | ISA standard atmosphere, still air | Steady wind | Turbulence (seeded, P7) | — |
| **Cockpit** (W11) | Chase camera; HUD with speed, altitude, AoA, G, Mach and throttle, each with its unit | Cockpit view | — | — |
| **AI pilot** (RH Aviator, W17) | Waypoints, wingman formation, return to base with approach and landing on the runway | Fighting for position against another jet (pursuit geometry, energy), scored by time spent behind it. No weapons | Weapons: **parked**, only if SIX asks (§6, constraint 8) | — |
| **Helicopter** (W20) | Its own stream after jet L1, on the same contracts: thrust along the rotor axis, attitude hold, ground effect | — | — | — |

**Devices (W10).** Keyboard and mouse (with the instructor), an Xbox pad and the VelocityOne stick, all in slice 1, through `DeepWater.Input`'s one-reader pattern. Each device class gets a virtual-device test (C18).

**Budget (W21).** At most 0.01 ms per aircraft per fixed step at L0 and 0.05 ms at L1, so twenty jets at L1 fit within 1 ms. Measured with `unity-perf-ab` in a player build. A budget is a target until it has been measured.

## 5. Decision register

All entries W1–W24 are SIX's picks of 2026-10-24, in round order. "Leaning" is Claude's recommendation before the pick. W25–W31 are Claude's proposals to close gaps the picks left open; SIX locked them as written on 2026-10-25.

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| W1 | What "deal with my planes" means first | **Own the flight physics:** a first-party flight-model ladder; each airframe is data | Same | Locked |
| W2 | First airframe | **F/A-18E Super Hornet** | Same | Locked |
| W3 | Ladder shape | **L0 point-mass energy model → L1 6-DOF with control laws → L2 tabulated aero with Mach and ground effect → L3 engines, fuel and CG** | Same | Locked |
| W4 | Who moves the aircraft | **Own pure-C# integrator on the fixed tick; PhysX for contacts only** (Reckoning variant B) | Same | Locked |
| W5 | Module split | **Two modules.** Air Wing owns airframes, controls and ground handling. RH Aviator is AI-only and flies through Air Wing's Pilot Inputs. The carrier hookup stays in Carrier Ops through `IRecoveringAircraft` | Same | Locked |
| W6 | What the flight model takes in | **Pilot Inputs only** (stick, throttle, pedals, switches). The AI works the same stick. Autopilot modes are switches either seat can flip | Same | Locked |
| W7 | Determinism | **Bit-identical on one machine from day one**, seeded, with a per-tick Fix. Cross-machine later through Reckoning. Ground-contact phases are same-machine only | Same | Locked |
| W8 | Data | **Public cited sources** (Navy fact file, NASA F/A-18 reports). One calibrated number per public figure (C16), gaps AUTHORED. The E's aerodynamics are scaled from the A/B and flagged | Same | Locked |
| W9 | Name | **Air Wing**; parts named after real aircraft systems | Same | Locked |
| W10 | Controls in the first slice | **All three: VelocityOne stick, Xbox pad and mouse aim** (plus keyboard). *SIX's pick; Claude leaned to the stick first, Xbox next.* Cost: a bigger slice 1 and three device-class tests | Stick + keyboard first | Locked |
| W11 | Camera and HUD | **Chase camera plus a HUD** (speed, altitude, AoA, G, Mach, throttle, with units); cockpit view later | Same | Locked |
| W12 | First test bed | **Dogfight, rebuilt.** *SIX's pick; Claude leaned to a new Examples range scene* | New range scene | Locked |
| W13 | How Dogfight is rebuilt | **An editor builder** (Deep Water → Air Wing → Build Dogfight) overwrites the scene in place, so its GUID survives (C24). The old scene stays in git history and is copied to `_Archive` (P8) | Same | Locked |
| W14 | What goes into Dogfight | **Sea State replaces the HDRP water;** a land runway, speed and altitude gates, an airborne start with a runway start as an option (see W29), and the DDG as a still landmark. The dead jet, the two switched-off F-18Es and the radar module come out (checked 2026-10-24: Radar Suite does not use the module in Dogfight) | Same | Locked |
| W15 | Mouse aim | **An instructor** that works the stick toward where you point, through the same Pilot Inputs. Any real stick or flight-key input overrides it at once. Our own code; MouseFlight (MIT) credited where its idea is borrowed | Same | Locked |
| W16 | When carrier ops come | **Last, after the AI pilots.** *SIX's pick; Claude leaned to right after L1 gear.* The carrier's detail questions are parked (§7) | After L1 | Locked |
| W17 | First AI stream | **L0:** waypoints, formation and RTB with an approach and landing on the runway. **L1:** fighting for position, scored by time behind. No weapons | Same | Locked |
| W18 | Player seats | **Switchable:** take or hand back any aircraft mid-flight; the AI flies every empty seat (as General Quarters G3) | Same | Locked |
| W19 | Legacy code | **Archive every unused aircraft script** to `_Archive/` with a supersession checklist (C25): `Scripts/FighterJet Scripts/`, `Scripts/Aircraft_Handlers/` (including the MouseFlight copy), `FighterJetController` and the `F-18 (1) Variant` prefab. `HelicopterController` follows once its replacement exists. The vendor kits stay where they are. The AerialArcade F-18E stays in CVN-78 and OutdoorsScene until Air Wing's jet replaces it | Same | Locked |
| W20 | Helicopter | **Its own Air Wing stream after jet L1**, on the same contracts; the current controller stays until then | Same | Locked |
| W21 | Performance budget | **≤ 0.01 ms per aircraft per step at L0, ≤ 0.05 ms at L1,** measured with `unity-perf-ab` in a player build | Same | Locked |
| W22 | Units | **SI in the core; aviation units at the edge** (HUD, quoted public figures) | Same | Locked |
| W23 | L1 control law | **Textbook Hornet-style fly-by-wire:** G-command pitch, roll-rate roll, AoA and G limiters, yaw damper; structure after NASA's HARV papers, gains AUTHORED | Same | Locked |
| W24 | Integration rate | **Keep the 50 Hz project tick; fixed substeps inside the flight core** (for example 4 × 200 Hz) | Same | Locked |
| W25 | The per-aircraft level asset's name | **Fidelity sheet** (`FidelitySheetSO`), not "loadout", which in aviation means weapons and stores | — | Locked |
| W26 | What the stick means at each level | **G and roll-rate commands at every level**, so Pilot Inputs keep one meaning up the ladder. *Mitigation:* the feel changes between L0 and L1 as the airframe's response gets richer; the HUD names the level | — | Locked |
| W27 | Two devices at once | **The last device moved past its deadzone owns the player's seat;** the instructor yields at once (W15). *Mitigation:* the HUD shows the active device | — | Locked |
| W28 | Replay | **Inputs are sampled at the tick boundary and logged per tick; a replay from the input log reproduces the Fix bit for bit** on the same machine | — | Locked |
| W29 | Runway start at L0 | **Waits for L1.** L0 is a point mass with no gear, so Dogfight starts airborne until Landing Gear L1 exists. The runway is built in slice 1 as scenery and as the AI's landing target | — | Locked |
| W30 | The jet's art | **Built from a copy of the F-18E model** (`_AIRCRAFT/F-18E/Blender/F-18E.blend`'s import), with no vendor scripts on it (AP8). The FDS F-18E prefab, AerialArcade and the original `.blend` are never modified | — | Locked |
| W31 | Shared helpers (RNG, fingerprint) | **Air Wing copies General Quarters' pattern, not its code** (no reference to `DeepWater.GeneralQuarters`). Extract a shared core only when a third module needs it (tenets 11 and 14) | — | Locked |
| W32 | L1 aerodynamics | **The published F/A-18 HARV fits** (Chakraborty, Seiler and Balas, *J. Guidance* 34(1), 2011, Appendix Tables A1–A2): polynomials in angle of attack fitted to NASA HARV flight data, α 0–60°, checked against the authors' public `F18AeroData.mat`; geometry and inertia from NASA TM-107601. Scaled to the E and flagged AUTHORED; Mach tables wait for L2 (W3). *Re-decided 2026-10-26:* first locked as "NASA TM-107601 tables", but that report gives the model's structure and inertia, not its aero numbers (they sit in a McDonnell Douglas report that is not public). *Mitigation:* sideslip enters linearly and there are no flap terms; both are named AUTHORED | Same | Locked (SIX, 2026-10-26; source re-decided by SIX the same day) |
| W33 | Departure at L1 | **Physics decides.** The fly-by-wire limits angle of attack and G as the Hornet's does, but the airframe obeys its tables: abused at its corners (low speed, heavy roll at high angle of attack, full pedal) it can stall, spin or depart. Bad piloting crashes (Q5) | Same | Locked (SIX, 2026-10-26) |
| W34 | Phase 2 order | **Flying first, gear last:** 2a 6-DOF airframe and aero, headless; 2b fly-by-wire; 2c spool time, L1 in the scenes, the budget; 2d landing gear with runway take-off and landing; 2e phase review | Same | Locked (SIX, 2026-10-26) |
| W35 | Default level in the scenes | **Switch to L1 once its tests pass; L0 stays selectable** on the fidelity sheet | Same | Locked (SIX, 2026-10-26) |
| W36 | The AI pilot's brain (Phase 3) | **Modes and named rules:** a small state machine of modes (cruise, join, land, fight) whose switches are named rules, each logged when it fires (C19) | Same | Locked (SIX, 2026-10-26) |
| W37 | How the AI works the stick | **A shared flight-path layer:** every behaviour asks for a flight path (an aim point, or heading, altitude and speed); one layer flies it with stick and throttle, like the mouse instructor. Tuned once; an autopilot for the player can reuse it | Same | Locked (SIX, 2026-10-26) |
| W38 | What the AI knows | **Truth at L0; limits for the fight:** at L0 it reads other aircraft's true state. At L1 it sees only inside a field of view, with a reaction delay. Sensors later | Same | Locked (SIX, 2026-10-26) |
| W39 | Where the first AI slice flies | **Dogfight's gate course:** the AI flies the four gates, which already score speed and altitude; then it joins on the player's wing | Same | Locked (SIX, 2026-10-26) |
| W40 | Formation | **Fingertip first:** one shape, off the leader's wing about three wingspans out and slightly behind; trail and line abreast once that holds steady | Same | Locked (SIX, 2026-10-26) |
| W41 | How the AI returns and lands | **The overhead break:** run in down the runway at 800 ft, break into a tight level turn, fly downwind with the gear down, turn final and land. *SIX's pick; Claude leaned to a straight-in 3° approach first (the profile the landing tests already fly).* *Mitigation:* the break is built from flight-path segments on the shared layer (W37), with the straight-in final as its last segment, so it can be tested a segment at a time | Straight-in first | Locked (SIX, 2026-10-26) |
| W42 | Taking and handing back aircraft | **A cycle key and a pick list:** one key (and a pad button) steps the player through the aircraft in the air; the one left goes to its AI pilot at once; a small HUD list shows who flies what (W18) | Same | Locked (SIX, 2026-10-26) |
| W43 | How many AI aircraft | ~~**Up to four in all:** the player and up to three AI (a wingman, and a pair to fight); about 70 µs of flight core per step at L1~~ **Re-decided by SIX 2026-10-27: up to eight in all** (the player, three wingmen, four opponents); the budget re-measured at eight | Same | Locked (SIX, 2026-10-26; reversed 2026-10-27) |
| W44 | How often the AI decides | **A 4 Hz watch; the stick every tick:** modes and rules are checked four times a second, timed by the tick count so a replay repeats them; the flight-path layer (W37) works the stick on every 50 Hz tick | Same | Locked (SIX, 2026-10-26) |
| W45 | The opponent's skill | **Pilot profiles in a ScriptableObject** (rookie, line, ace): reaction delay, field of view, the G it will pull, how well it manages energy. One brain, different numbers | Same | Locked (SIX, 2026-10-26) |
| W46 | Scoring the fight | **Time in the rear cone:** seconds each jet spends within about 30° of the other's tail and inside about 1 km (W17). On the HUD and logged per fight. No weapons; no gun solution | Same | Locked (SIX, 2026-10-26) |
| W47 | What sends the AI home | **A command or a fuel line:** the player orders it home (a key, or the pick list, W42), or it goes on its own at its profile's bingo fuel. Fuel burn comes at L3, so until then a flight-time limit stands in, labelled as a stand-in wherever it shows | Same | Locked (SIX, 2026-10-26) |
| W48 | Where the AI work is tracked | **RH Aviator's own blueprint and roadmap.** Air Wing's Phase 3 is a pointer plus the Air Wing-side items (seat cycling, the HUD pick list), as W5 splits the modules | Same | Locked (SIX, 2026-10-26) |
| W49 | The AI's levels | **Pilot L0 and Pilot L1**, the jet's ladder words: L0 waypoints, formation, the break and the landing; L1 the fight. RH Aviator's old L0–L5 table is rewritten to match | Same | Locked (SIX, 2026-10-26) |
| W50 | How the AI is tested | **Headless flights, then a scene smoke:** EditMode tests fly the AI seat in the flight core with no scene, each against limits fixed before tuning (it flies the gates, holds formation within a set error, lands from the break); then one play-mode smoke in Dogfight per slice | Same | Locked (SIX, 2026-10-26) |
| W51 | The first Phase 3 slice | **The AI seat, the hands and the gates:** RH Aviator's AI seat, the shared flight-path layer (W37), the mode machine with its logged rules (W36), and the AI flying Dogfight's four gates at L1. Formation, the break and seat cycling come later | Same | Locked (SIX, 2026-10-26) |
| W52 | Who the opponents fight | **Red hunts the player** (SIX, 2026-10-27): every red jet goes for the jet the player flies; blue's wingmen defend. Scored as W46: seconds with an enemy in the rear cone. No weapons | Team rear-cone scoring, both sides | Locked (SIX, 2026-10-27) |
| W53 | Blue's wingmen in a fight | **They fight on their own** (Pilot L1): each picks a red jet and fights it, and comes back to fingertip when nothing is near | Same | Locked (SIX, 2026-10-27) |
| W54 | Mouse and keyboard | **A way to hold level flight**: the jet is hard to fly on mouse and keyboard because, hands off, it drifts, climbs or sinks (SIX, 2026-10-27). The other three candidates (twitchy mouse aim, all-or-nothing keys, mouse and keys fighting) were not chosen | — | Locked (SIX, 2026-10-27) |
| W55 | Arming the jets (SIX, 2026-10-27: "arm the guns and hard points for the planes") | **The gun now; live air-to-air missiles on the hardpoints next.** Constraint 8 allowed weapons "only if SIX asks, and then abstract"; SIX asked. Round flight is Gunnery's (K1), missiles Guided Fury's, the airframe's part is Air Wing's: the trigger (`PilotInputs.Trigger`), rounds and hit points in the state, the gun's mount in the profile (`GunMount`, `CombatProfile`) | Stores first, missiles later | Locked |
| W56 | The gun | **Simulated rounds as shot groups plus a lead-computing HUD pipper** (Gunnery K4). The M61A2's public numbers (412 rounds, 6,000 a minute, GD-OTS sheet); the rest AUTHORED in the provenance. Fire: left mouse or left Ctrl, the Xbox A button, stick button 5 (the stick's trigger stays the brakes) | Rounds with a fixed gun cross | Locked |
| W57 | A hit | **Abstract hit points (10, one per round); at zero the jet is Killed:** out of the fight, no longer flown, it falls until it meets the surface and is Crashed, the kill logged with its shooter (`FlightCore.Kills`) | Same | Locked |
| W58 | The first missile, at game level (SIX's directive, 2026-10-27: "Just simplify the plans and generalize them to make them safer") | **The SR-IR missile archetype** (Guided Fury `ROADMAP.md` A3) on the hardpoints W55 named. A seeker cone with an AUTHORED range and lock chance; proportional navigation with an AUTHORED gain; Guided Fury L2, built from Guided Fury's pure parts and stepped inside Air Wing's tick. A hit is abstract: inside an AUTHORED lethal radius, one seeded draw against an AUTHORED kill chance kills the jet or takes AUTHORED hit points, applied through RH Shared's `HitReport` as the gun's rounds are (W57). No real designator, no seeker or fuze detail, no countermeasures this push. *Done when* the player sees LOCK, fires, and the missile resolves the engagement within its authored flight time, the same on every run with the same seed at any frame rate. Checked against constraint 8 | — | Locked (SIX's directive; written by Claude from W55) |
| W59 | A fight that never stops (SIX, 2026-10-29: "give them 2 respawn points opposite of each other on the map so that once they are destroyed they can load back in and continue training") | **The arena** (`Core/Arena.cs`, run inside the tick by `FlightCore.UseArena`): a jet with a spawn slot that goes down (shot down, crashed, collided, out of the box) comes back at its slot **3 s** later, its height ±300 m and heading ±15° drawn from the arena's own seed, with full rounds and hit points, and is **protected for 5 s** (can't be hit, can't fire). Like a hit, it changes the jet outside the Pilot Inputs (W6's exception for game events), on the tick count, so a run repeats; the input log keeps one entry per jet per tick. Slots come from scene `SpawnPoint`s (line abreast, 300 m); the numbers are `ArenaSO` (AUTHORED, provenance). With no arena, or an arena of zeros, a core flies exactly as before | Same | Locked (SIX's picks, TRAINING-CONCEPT M25, M26) |
| W60 | Jets that meet | **Both go down when their paths over a tick pass within 15 m** (the arena's collision rule; Air Wing had none, and learners would learn to fly through each other). Not a kill: no shooter, no `KillRecord`; logged as an arena event | Same | Locked (SIX's pick, M24) |
| W61 | The edge of the arena | **A 40 × 40 km box** about the merge, (0, 12,000); a jet flying out goes down and respawns. A column: no ceiling, the sea is the floor | Same | Locked (SIX's pick, M25) |
| W62 | Watching a fight nobody flies (SIX, 2026-10-29: "disable the player seat and just give the player a camera rig to fly around the scene to witness engagement … track/follow entities with a quick push of a button or go free roam") | **A spectator camera** (`Modules/Cockpit/SpectatorCamera.cs`): follow any jet (Tab / ] next, Shift+Tab / [ previous, 1–9 by id; right mouse orbits, scroll zooms) or fly free (F; WASD, Q/E, right mouse looks, Shift fast); a tag over every jet in its spawn point's colour. Scenes with no player seat use it; the runner then needs none | — | Built (SIX's direction) |
| W63 | Explosions (SIX, 2026-10-29: "collisions and kills should render explosions") | **An explosion where a jet is shot down, where two collide, and where one meets the sea or the ground** (`Modules/World/ExplosionView.cs`), from the aircraft's own visuals (`AircraftVisualsSO.explosion`). Presentation only; the runner adds the view when a jet has one, so no scene change. *Fixed the same day (SIX: "mutated squares and no fire"):* the first effect, `Assets/_VFX`'s `HD_Naval_Explosion`, uses the built-in pipeline's particle shader, which HDRP draws as bare squares; now General Quarters' `GQ_FX_Warhead` (its HDRP particle shader), scale 1.5 in the air and 2 at the surface, and the view starts every layer (a pooled effect does not play on awake) | — | Built (SIX's direction) |
| W64 | Who flies a learned jet | **A seat kind of its own, `SeatKind.Learned`** (RH Aviator A13), so the input log says which ticks a policy flew (TRAINING-CONCEPT M3: such fights need not re-run; their inputs still replay) | `Ai` | Proposed by Claude |
| W65 | Hard points and a missile (SIX, 2026-10-29: "give the planes hard points and a missile armament, dont use guided fury just make game missile sufficient for video game air to air"; four picks, all recommended) | **A game missile in Air Wing** (`Core/Missiles`, the flight core's missile stage after the guns): four on pylons (two under each wing, drawn, reloaded by a respawn); a lock is a target inside 30° of the nose between 500 m and 5 km held 1 s (the seeker holds the jet it is on while that one stays inside; only then does it take the one nearest the nose); the pickle (`PilotInputs.Pickle`) launches one a second while locked; proportional navigation (N 4) under 30 G, a 3 s motor to about 2.5 times the jet's speed, then a coast (k·v²); a target that leaves its 45° seeker cone, goes down or respawns is lost; a 15 m proximity burst kills 7 times in 10 (a seeded draw) and otherwise takes 6 hit points; self-destruct at 20 s. Everyone fires (W65 with RH Aviator A16): the player on the right mouse button or M, the Xbox right shoulder (B is the gear) or stick button 6, with a lock box on the HUD. A generic archetype with AUTHORED arcade numbers (`GameMissileData`), not any real missile; checked against constraint 8. **Supersedes W58's route** (built from Guided Fury's parts) on SIX's word; and SIX's own 2026-10-27 routing (missiles to Codex or SIX by hand) is set aside for this build because SIX asked Claude directly | W58 (Guided Fury's parts) | Built (SIX's picks) |
| W66 | A radar in every jet's nose (SIX, 2026-10-29: "I want them to have search modes … the two radar tracking modes"; SENSORS-CONCEPT J1, J2, J6, J16, slice 1) | **`Core/Radar`, a radar stage in the flight core after the arena:** range-while-search (±60°, 40 km, 4 s frame), track-while-scan (±30°, 40 km, 2 s), dogfight (±10° by −10…+50°, 10 km, 0.5 s; locks the nearest contact at once) and single-target track (a look every 0.1 s with 6 dB more gain inside a 60° gimbal; lost after 10 misses, back to the search mode it came from). Detection is the radar suite's textbook maths (`RHRadarSuite.Core`: radar equation, Swerling 1, Barton's errors, the `TrackFile`) with the core's own seeded draws; sea clutter competes only when the radar looks down at a jet low enough for the beam to reach the sea. The pilot commands it through `PilotInputs.Radar` and `RadarTrack`, so the input log replays it; a respawn clears it. The F/A-18E's radar and signature (nose 3, beam 12, tail 7 dBsm) are a generic game set (`RadarData`), every number AUTHORED, the power solved so a 5 dBsm jet at 40 km is seen on half the looks; checked against constraint 8 | No radar (sight only) | Built (SIX's J-picks) |
| W67 | Who a contact is, and who is looking at me (SENSORS-CONCEPT J3, J5, slice 2) | **IFF on demand:** each aircraft carries a transponder code (`Aircraft.IffCode`, set per fight by `FlightCore.SetIffCode`; RH Aviator: one code per side), so the core knows codes, never sides. The `Interrogate` switch (honoured once a second while held) asks every live track inside the radar's beam; a flying jet in the asker's code answers at its transponder's reply chance (0.95, a seeded draw) and the track reads friend; no answer leaves it unknown: IFF never says hostile. **A radar warning receiver:** every radar beam crossing a jet this tick (a search sweep, detected or not, above the horizon; a single-target look; a missile's guidance, slice 3) is a paint; the receiver shows each radar's bearing from the nose and kind, strongest first, for 5 s after a search paint and 0.5 s after a track. No range, no identity. All AUTHORED in `RadarData` | Sight only | Built (SIX's J-picks) |
| W68 | The radar missile (SENSORS-CONCEPT J7, J15, J18, slice 3) | A generic medium-range active radar game missile, two on the inboard pylons (the heat-seekers keep the outboard two: J18 2 + 2). The radar pickle (`PilotInputs.RadarPickle`) launches one only on the radar's single-target track, inside 1.5–25 km and 45° of the nose. Until it is 8 km from where the track says the target is, it flies on the shooter's track (`JetRadar.TryLockEstimate`, proportional navigation on the estimate); a lock dropped, or the shooter down or respawned, loses it. Then it goes active: its own seeker takes over, the shooter may let go, and the target's warning receiver hears "missile" (a `PaintKind.Missile` paint from the missile's position). All AUTHORED in `RadarMissileData` | Sight only | Built (SIX's J-picks) |

## 6. Standing constraints

1. **The flight core never touches Unity runtime state.** No `UnityEngine.Time`, `UnityEngine.Random`, `Physics`, `Object.Find*`, `GameObject`, `DateTime.Now` or `Environment.TickCount`, and no unseeded `System.Random`. Value types (`Vector3`, `Quaternion`, `Mathf`, `Unity.Mathematics`) are fine.
   - *Why:* determinism (W7).
   - *Enforced by:* a banned-API source scan of `Core/`, as General Quarters' `CoreBannedApiTests`.
2. **Randomness is a seeded RNG injected from the fidelity sheet;** every draw that decides an outcome is logged. *Why:* P7.
3. **An aircraft changes only through Pilot Inputs from a seat** (W6). *Why:* switchable seats, replay, the Reckoning seam.
4. **Tuning lives in ScriptableObjects** (P1), with cited provenance (W8); AUTHORED values are flagged, and an unsourced value stays empty.
5. **Vendor code is read-only** (P5). AerialArcade, FlightSimLite, NWH and the MouseFlight copy are never edited. **Never modify the original F-18E `.blend` or the FDS prefab**; work on copies (W30).
6. **Never ask the renderer for physical truth** (AP11). Sea height comes from Sea State's `ISeaSurface`, not HDRP water.
7. **The 50 Hz project tick is not changed by this module** (W24). Substeps live inside the flight core.
8. **Safe limits (SIX, 2026-10-24; General Quarters BLUEPRINT §6.8 applies here in full).** Air Wing is a game simulation at the level of commercial and hobby flight sims, and it stays there. In particular:
   - public, cited data only; a figure that is not public stays AUTHORED;
   - **no flight-control, autopilot or guidance code meant to run on real hardware:** no drones, model aircraft or real actuators. The control law and the AI pilot are game models;
   - **no aircraft weapons unless SIX asks**, and then only abstract, as in General Quarters (*SIX asked, 2026-10-27: W55–W57; the gun's sight and the AI's trigger are game models, not fire-control code*);
   - no real-world targeting, no tuning to beat a specific real air defence, and opponents stay generic.
   - *How to apply:* check every slice against this list before it is built. A request that would cross a line is said so plainly and reshaped toward the game side.
9. **The budget is measured, never assumed** (W21).

## 7. What is deliberately not here

| Not in the first pass | Why / revisit when |
|---|---|
| **Carrier ops:** launch, trap, the moving deck, wind over the deck, Sea State feeding Carrier Ops' `ISeaStateMotion`, catapults 2–4, a tailhook on the Air Wing jet | W16. **Parked until the AI stream is done.** Questions to ask then: how the deck carries parked aircraft (kinematic Rigidbody or Reckoning), catapult and wire fidelity (constant g or profiled), wind over the deck and the air wake, and Sea State behind `ISeaStateMotion` |
| ~~Aircraft weapons~~, countermeasures, sensors (RH Aviator Phases 3–4) | Constraint 8; only if SIX asks. *Weapons asked for 2026-10-27 (W55–W57): the gun built, the IR missile next; no countermeasures this push. The missile is the SR-IR archetype at game level (W58)* |
| Multi-ship tactics and mission-level AI (RH Aviator Phases 5–6) | After AI L1 |
| G-induced loss of consciousness (greyout, blackout) | SIX's idea, 2026-10-26: the fly-by-wire's accepted G overshoot could feed it. When the pilot or the cockpit view is modelled |
| Cockpit view | W11. After the chase camera and HUD prove the numbers |
| Damage, failures, ejection | Not asked for |
| VR (HurricaneVR is in the vendor folder) | Not asked for |
| Multiplayer | The input log and the Fix keep the door open (W28) |

## 8. Risks

| Risk | Mitigation |
|---|---|
| **No public Super Hornet aerodynamic tables** | Hornet (A/B) data scaled, flagged AUTHORED; the fact-file figures are the calibration (C16); the residuals are named, not tuned away |
| **Slice 1 is large** (W10: three device classes plus the instructor; W12: a scene rebuild) | Slice 1 is split into 1a–1e, each with its own tests and gate |
| **Ground contact breaks bit-identity** (PhysX) | Ground phases are flagged in the Fix; airborne determinism tests come first; Reckoning later |
| **One Unity Editor and one MCP bridge shared with other sessions** | Build slices only when no other session is driving the Editor; commits go in through `commit-to-branch` |
| **Dogfight holds legacy and vendor content** | The builder overwrites it in place (W13); the old scene stays in git and in `_Archive` |
| **The feel changes when an aircraft moves up a level** (W26) | One stick meaning across levels; the HUD names the level; L1 is tuned against the same manoeuvres as L0 |

## 9. Open questions for SIX

| # | Question | Cost |
|---|---|---|
| ~~Q1~~ | ~~W25–W31 are Proposed. Lock them as written?~~ **Answered 2026-10-25 by SIX: locked as written.** | — |
| ~~Q2~~ | ~~`RH Aviator`'s asmdef is `RH.Aviator` (namespace `RH.`), while new modules use `DeepWater.*`. Rename it to `DeepWater.Aviator` when the AI stream starts (Consistent, Lamport)?~~ **Answered 2026-10-25 by SIX: `DeepWater.Aviator`.** Done in slice 0 (the asmdef's name and root namespace; it has no scripts yet) | — |
| ~~Q3~~ | ~~When the blueprint should reach `main`: now (docs only), or with slice 1?~~ **Answered 2026-10-25 by SIX: now.** Brought up to `main` with slice 0 | — |
| ~~Q4~~ | **Answered 2026-10-25 by SIX: fix all eight**, the AerialArcade file included, plus a local-only guard on `UnityMcpUnlock.cs` that is never committed. Done (ROADMAP slice 0, last item): player scripts compile with 0 errors. *The question was:* ~~Eight files outside Air Wing still stop a player build, so W21's player-build measurement (slice 1e) can't run yet: `AerialArcade/.../F18ControllerEditor.cs` (vendor), `RH Utilities/` ×3, `RH Testing Suite/Core/TestRunner.cs`, `Scripts/Destroyer Scripts/DestroyerEditor.cs`, `_Archive/Carrier_legacy/.../CarrierEditor.cs`, and the local-only `UnityMcpUnlock.cs`. May they be fixed, and how? (Each moves into an `Editor/` folder or gains `#if UNITY_EDITOR`; the vendor file and the unlock file need your word.)~~ | — |
| ~~Q5~~ | ~~The mouse instructor can fly the jet into the sea. Add a floor?~~ **Answered 2026-10-26 by SIX: no floor. "Bad piloting should result in crashes."** The instructor stays as it is; README warns mouse pilots (METHODOLOGY AP25) | — |
| ~~Q6~~ | ~~A crash ends the flight until Stop and Play. Add a reset?~~ **Answered 2026-10-26 by SIX: add it.** Done: keyboard R or the Xbox View button puts the jet back at its start; it travels in the Pilot Inputs (`PilotSwitches.Reset`, momentary), so the log records it and a replay repeats it | — |

## Change log
- **2026-10-24** — Stock-take (read-only), then the blueprint locked from the 24-question interview (W1–W24, SIX). Three picks differ from Claude's leaning: W10, W12 and W16. W25–W31 Proposed by Claude. Nothing built.
- **2026-10-25** — Q2 and Q3 answered by SIX. Slice 0 done (ROADMAP). Q4 added: player-build blockers outside Air Wing.
- **2026-10-25** — Q4 answered by SIX (fix all eight); done, and player scripts compile with 0 errors.
- **2026-10-25** — Q1 answered by SIX: W25–W31 locked as written. Slice 1a started.
- **2026-10-25** — Slices 1b–1d done (ROADMAP). Slice 1e: Q5 (the mouse instructor has no floor) and Q6 (a reset after a crash) added.
- **2026-10-26** — Q5 answered by SIX (no instructor floor: bad piloting crashes) and Q6 (a reset: done). Phase 2 begun: W32–W35 locked by SIX, all as Claude leaned.
- **2026-10-26** — W32's source re-decided by SIX: TM-107601 holds no aero numbers, so L1 flies the published HARV flight-data fits. Slice 2a done (ROADMAP).
- **2026-10-26** — Slice 2b: W23's law realised as nonlinear dynamic inversion commanding α (after Snell, Enns and Garrard 1992, on the HARV model) after a fixed-gain PI law failed; the yaw damper is the sideslip and yaw-rate inversion. The AoA limit is 30° (AUTHORED). Two of its criteria are missed and reported (ROADMAP 2b).
- **2026-10-27** — SIX: "finish out phase 3", then test dogfights with many aircraft, fix mouse-and-keyboard flying, and run the player with three wingmen against four opponents. W43 re-decided (eight aircraft, a reversal); W52 red hunts the player; W53 wingmen fight on their own; W54 the controls fix is level-flight holding.
- **2026-10-27** — SIX armed the jets: W55 (the gun now, live missiles next), W56 (rounds as shot groups and a lead-computing pipper), W57 (hit points, then killed and falling). Gun built; round flight lives in the new Gunnery module.
- **2026-10-27** — W58 (SIX's directive: "Just simplify the plans and generalize them to make them safer"): W55's "missiles next" is planned at game level as the SR-IR missile archetype, with a cone, a range and a lock chance, PN with an authored gain, and an abstract hit through `HitReport`. Checked against constraint 8 (General Quarters `BLUEPRINT.md` §6 standing constraint 8). Real-system detail removed. Nothing built. Engineering-grade original preserved outside the repo (from 2121da9e).
- **2026-10-29** — W68 built: the radar missile (SENSORS-CONCEPT slice 3). `RadarMissileTests` 7/7.
- **2026-10-29** — W67 built: IFF and the radar warning receiver (SENSORS-CONCEPT slice 2). `IffAndWarningTests` 8/8.
- **2026-10-29** — SIX asked for two squadrons with a machine-learning brain that fight each other to train, with respawn points opposite each other; then a spectator camera and explosions. W59 (the arena: respawn at a slot after 3 s, 5 s protected, seeded jitter), W60 (collisions inside 15 m), W61 (a 40 km box), W62 (the spectator camera), W63 (explosions) built; W64 (`SeatKind.Learned`) proposed. The learned pilot is RH Aviator's (A13–A15).
- **2026-10-29** — W66 built: the jets' radar (SENSORS-CONCEPT slice 1). `RadarTests` 11/11.
- **2026-10-29** — The explosions fixed (W63: General Quarters' HDRP warhead burst, started by the view; the first effect's built-in particle shader drew squares). W65 built on SIX's word and four picks: a game missile on four pylons, a lock, proportional navigation, a 15 m burst, everyone fires; it supersedes W58's Guided Fury route.
