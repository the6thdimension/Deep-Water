# Deep Water

High-fidelity, physics-based military simulation in Unity (6000.4, HDRP): ships, aircraft, weapons, sensors and the
sea they work in, built to run in real time. Deep Water is also the proving ground for the McMahon Simulation
Methodology ([METHODOLOGY.md](METHODOLOGY.md)): every system is built to the same virtues, and the lessons are written
down as they are learned.

It is a game simulation. Data is public, lethality is abstract, opponents are generic, and nothing here describes a
real system's performance (the safe limits in [CLAUDE.md](CLAUDE.md) and General Quarters' BLUEPRINT).

*Rewritten 2026-10-27: the earlier README described folders the project no longer has.*

## Start here

1. [CLAUDE.md](CLAUDE.md): the goal, the virtues, how things are built here, the branch rules.
2. The module you are working on: its BLUEPRINT (decisions), ROADMAP (what is done and next) and README (how to use it).
3. [METHODOLOGY.md](METHODOLOGY.md): laws, principles, patterns and anti-patterns, each with where it was learned.

## First-party modules

Status as of 2026-10-27; each module's ROADMAP is the live record.

| Module | What it is | Where it stands | Docs |
|---|---|---|---|
| **General Quarters** | The destroyer: a living DDG and its modular combat system, each element on a fidelity ladder | Phase 1 complete; Guns L1 and Damage Control L1 (flooding) built; Radar L1 questions prepared | [BLUEPRINT](Assets/General%20Quarters/BLUEPRINT.md), [ROADMAP](Assets/General%20Quarters/ROADMAP.md), [HANDOFF](Assets/General%20Quarters/HANDOFF.md) |
| **Air Wing** | Flight physics: the F/A-18E from point mass to a 6-DOF airframe with fly-by-wire, engines and landing gear; seats for player and AI | Phases 1–3 complete | [BLUEPRINT](Assets/Air%20Wing/BLUEPRINT.md), [ROADMAP](Assets/Air%20Wing/ROADMAP.md), [README](Assets/Air%20Wing/README.md) |
| **RH Aviator** | The AI pilots: routes, formation, the overhead break and landing, and the dogfight at three skill levels | Phase 3 complete (up to eight aircraft); the fight in the vertical proposed | [BLUEPRINT](Assets/RH%20Aviator/BLUEPRINT.md), [ROADMAP](Assets/RH%20Aviator/ROADMAP.md), [README](Assets/RH%20Aviator/README.md) |
| **Sea State** | The ocean as one source of truth for rendering and physics: waves, buoyancy, foam | Built and tuned | [README](Assets/Sea%20State/README.md) |
| **RH Radar Suite** | Radars on any GameObject (LOD modules, profiles, gizmos), plus an engine-free physics core: the radar equation, detection, clutter, measurement errors, tracking | Kit built; the pure core built and tested, not yet used by the modules; General Quarters' Radar L1 is its first consumer (G61–G68) | [README](Assets/RH%20Radar%20Suite/README.md), [ROADMAP](Assets/RH%20Radar%20Suite/ROADMAP.md) |
| **Gunnery** | All gunnery for every platform: round flight, drag, ballistic air, firing tables, shot groups, hit tests, gun profiles | New 2026-10-27: General Quarters' ballistics moved in; the aircraft gun next | [BLUEPRINT](Assets/Gunnery/BLUEPRINT.md), [README](Assets/Gunnery/README.md) |
| **Guided Fury** | All missile behaviour, for every platform (guidance, seekers, aero, autopilot, thrust, fuzing) | Its own branch rules (protected) | [README](Assets/Guided%20Fury/README.md), [ROADMAP](Assets/Guided%20Fury/ROADMAP.md) |
| **Theatre** | Theatre, METOC and gravity maps | Blueprint locked; nothing built | [BLUEPRINT](Assets/Theatre/BLUEPRINT.md), [ROADMAP](Assets/Theatre/ROADMAP.md) |
| **Reckoning** | A deterministic simulation core (concept) and its spike | Concept written; spike done; no build | [RECKONING-CONCEPT.md](RECKONING-CONCEPT.md), [SPIKE-REPORT](Assets/Reckoning%20Spike/SPIKE-REPORT.md) |
| Carrier Ops | Carrier deck operations | Working but not hooked to Air Wing (parked until after the AI pilots) | [README](Assets/Carrier%20Ops/README.md) |
| RH Shared | Cross-module contracts only | | [README](Assets/RH%20Shared/README.md) |
| RH Testing Suite, RH Utilities | Test framework; scene management and validation | | [Testing](Assets/RH%20Testing%20Suite/README.md), [Utilities](Assets/RH%20Utilities/README.md) |
| JMAC | Older AI, sensor and decision systems (ATLAS, SAURON, Agents) | Legacy: a starting point, not a template | |
| ~~RH Radar~~ | ~~A greenfield radar plan for the autonomous routines~~ | **Struck 2026-10-27 (SIX, GQ G61):** never built; RH Radar Suite is the radar home | [README](Assets/RH%20Radar/README.md) |

Older controllers live under `Assets/Scripts/`; unused ones are being archived to `_Archive/` as their replacements land.

## Scenes

| Scene | What it is for |
|---|---|
| `Assets/General Quarters/Examples/Scenes/GQ_Range.unity` | The destroyer's range: the combat system, guns, flooding |
| `Assets/_SCENES/Dogfight.unity` | Air Wing's test bed: runway, gates, the sea; the AI and the 4 v 4 are added from the RH Aviator menu |
| `Assets/Air Wing/Examples/Scenes/AirWing_FlightTest.unity` | A bare flight test for a pad or stick |
| `Assets/_SCENES/` (others) | CVN-78 Ford, the DDG engagement, the helicopter, rocketry and more: see [its README](Assets/_SCENES/README.md) |

## Tests

EditMode tests sit beside each module (`Tests/Editor`) and run in Unity's Test Runner. Counts on 2026-10-27:
Air Wing and RH Aviator 135, RH Radar Suite 42; General Quarters and Sea State keep theirs in their ROADMAPs.

## Vendor code

Third-party packages (RealisticCarControllerV3, NWH, Obi, RootMotion, SensorToolkit, HurricaneVR, Cesium,
ML-Agents, FlightSimLite, VFX Arsenal and others) are read-only: first-party code wraps them rather than editing them.

## What is deliberately not here

- **Real-system performance.** No real targeting, no real-hardware code, no tuning to defeat real systems.
- **Missile work outside Guided Fury.** Missiles change only through the protected `Guided-Fury` branch.
- **Large vendor content in git.** DAVFX (2.3 GB) and the local licence file stay out of this public repository.

## Honest gaps

Nothing in the aircraft has yet been flown by a human at full fidelity; the radar's new core is not wired into its
modules; several older scenes still use legacy controllers.
