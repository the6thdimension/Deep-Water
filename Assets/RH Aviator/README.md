# RH Aviator

The AI pilot. It sits in an Air Wing aircraft's seat and flies it through the same Pilot Inputs the player uses. Follows McMahon
Pat1: **SO-Profile + Pure-Core + MB-Adapter**. **Read `BLUEPRINT.md` first** (W36–W51, SIX 2026-10-26); progress is in `ROADMAP.md`. Nothing is built yet.

**Assembly:** `DeepWater.Aviator` (namespace `DeepWater.Aviator`). Renamed from `RH.Aviator` on 2026-10-25 by SIX's call (Air Wing BLUEPRINT Q2), so it matches the other `DeepWater.*` modules.

**Since 2026-10-25, Air Wing owns the airframe side** (`Assets/Air Wing/BLUEPRINT.md`, W5, W6, W17). RH Aviator stays AI-only. Its AI seats issue Air Wing's `PilotInputs` through `ISeat`, exactly as the player does. That replaces the `IAircraftActuator` contract below. Aircraft weapons (L2–L3 below) stay out unless SIX asks (Air Wing §6, constraint 8).

## Folder layout

```
RH Aviator/
  Core/              — pure C#: the AI seat, the brain (modes, named rules, the rule log), the picture, formation and pattern geometry, fight scoring
  Modules/           — scene adapters: placing AI seats in Dogfight, the go-home order
  ScriptableObjects/ — PilotProfileSO (rookie, line, ace), routes
  Prefabs/           — (none planned: the jet is Air Wing's)
  Examples/          — Dogfight AI: the gates, formation, the break, 1v1
  Tests/Editor/      — colocated tests
```

## The ladder (W49)

| Level | What the pilot does | Knows |
|---|---|---|
| **Pilot L0** | Navigate a route (Dogfight's gates first), join and hold fingertip, go home, fly the overhead break, land | the truth |
| **Pilot L1** | Fight for position, scored by time in the other jet's rear cone. No weapons | its field of view, after a reaction delay |

The old six-tier table (intercept, ACM with guns, BVR, multi-ship, mission AI) is superseded; its items are struck through with reasons in `ROADMAP.md` (BLUEPRINT A5).

## Contracts

- **Output to the aircraft:** Air Wing's `ISeat` and `PilotInputs` (stick, throttle, pedals, switches; Air Wing W5, W6). The AI never writes an aircraft's state.
- **References:** `DeepWater.AirWing` (from slice 3a) and `RH.Shared`. **Air Wing never references RH Aviator.**
- **Later:** sensor input through `RH.Shared.Contracts.ISensorReport` (W38: after Pilot L1).
- ~~`IAircraftActuator`~~ superseded 2026-10-25 by Air Wing's `ISeat` and `PilotInputs`.

## Why this is decoupled from the actual flight model

The flight model (Air Wing's first-party ladder; formerly the AerialArcade F-18E, Flight Sim Lite) is **not** part of
this module. RH Aviator decides *what* to fly (a gate, a formation slot, the break, a fight);
the hands turn that into stick and throttle, and Air Wing's flight controls move the surfaces. The stick means
the same at every Air Wing level, so an aircraft can move up a level without re-authoring the AI.

## Legacy fragments

- `Assets/_Archive/Aircraft_Handlers_legacy/MouseFlight/Scripts/AI_FlightController.cs` (archived 2026-10-25, Air Wing slice 0)
- `Assets/_Archive/FighterJet Scripts_legacy/FlightControllerManager.cs` (archived 2026-10-25, Air Wing slice 0)
- `Assets/JMAC/Agents/{SimpleHomingMissile, SimpleDamageable, WeaponSystem}.cs`

Slice 3a mines these read-only for pursuit, throttle and turn heuristics (BLUEPRINT A6) and leaves them in place.
