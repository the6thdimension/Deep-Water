# RH Aviator

Greenfield AI aircraft module. Drives any airframe (vendor F-18, fixed-wing, helo)
through an actuator contract — no tight coupling to any one vehicle. Follows McMahon
Pat1: **SO-Profile + Pure-Core + MB-Adapter**.

## Folder layout

```
RH Aviator/
  Core/              — interfaces, AviatorEntity, decision logic (BT / FSM / utility)
  Modules/           — pluggable: roles (CAP, intercept, strike), tactics, formations
  ScriptableObjects/ — AircraftProfileSO, DoctrineSO, RoleProfileSO
  Prefabs/           — assembled AI aircraft
  Examples/          — demo scenes (1v1, 2v2, BVR)
  Tests/Editor/      — colocated tests
```

## Fidelity / capability ladder

| Tier | Name | What it models |
|---|---|---|
| L0 | Waypoint follower | Fly a path. No combat. Baseline. |
| L1 | Single-target intercept | Acquire a track, run intercept geometry, RTB on bingo. |
| L2 | Air-combat maneuvering (ACM) | Energy management, lag/lead pursuit, gun snapshots. |
| L3 | BVR + countermeasures | Long-range missile shots, notch maneuver, chaff/flare. |
| L4 | Multi-ship tactics | Wingman / element / division roles, mutual support. |
| L5 | Mission-level AI | Role-driven strike packages, SEAD coordination, JFACC tasking. |

## Contracts

- **Consumes:** `RH.Shared.Contracts.ISensorReport` (track input — radar, IRST, datalink).
- **Produces:** Implements `RH.Shared.Contracts.IThreat` (the AI aircraft IS a threat to enemies).
- **Output to airframe:** `IAircraftActuator` (defined in `Core/Contracts/`) — pitch/roll/yaw/throttle
  command surface. Vendor F-18 wrapper, AerialArcade adapter, etc. all implement this.

## Why this is decoupled from the actual flight model

The flight model (PuppetMaster F-18, Flight Sim Lite, custom 6DOF) is **not** part of
this module. RH Aviator decides *what* to fly (target intercept, formation hold);
the actuator implements *how* (control surfaces). Lets us swap airframes without
re-authoring the AI.

## Legacy fragments

- `Assets/Scripts/Aircraft_Handlers/MouseFlight/Scripts/AI_FlightController.cs`
- `Assets/Scripts/FighterJet Scripts/FlightControllerManager.cs`
- `Assets/JMAC/Agents/{SimpleHomingMissile, SimpleDamageable, WeaponSystem}.cs`

The routine mines these for domain knowledge (intercept geometry, throttle curves)
during Phase 0, then leaves them in place — they're consumers of the eventual
`IAircraftActuator`, not part of RH Aviator core.
