# Gunnery

All gunnery for Deep Water, every platform: how rounds fly (drag, air density, gravity), firing tables, shot
groups and hit tests, and gun profiles. Decisions: [BLUEPRINT.md](BLUEPRINT.md).

| Folder | What |
|---|---|
| `Core/Ballistics` | `Atmosphere` (ballistic air), `DragCurve`, `FiringTable` (+ `ProjectileData`), `ShotGroup` (the statistical hit model). Moved from General Quarters on 2026-10-27 (K1); namespace `DeepWater.Gunnery.Ballistics` |
| `Core/Guns` | `GunProfile` (round, rate, rounds, spread), `RoundFlight` (3-D: the firing table's physics), `RoundsInFlight` (volleys flown as shot groups, the swept hit test, hits drawn from the caller's seeded source, `HitReport`s out), `LeadSight` (where a round fired now will be at a range; the closest approach to a moving target) |
| `Tests/Editor` | The banned-API scan, the dependency-direction test, and `GunTests` (vacuum drop, agreement with the firing table to 0.1 m at 2 s, hits and misses, determinism, the pipper, the miss distance) |

**Used by:** General Quarters (Guns L1); Air Wing (the jets' gun, in the flight core's tick) and RH Aviator (the AI's trigger and gun tracking).

**Deliberately not here:** missiles (Guided Fury), mounts and fire control (the vehicle modules), hit points
(the vehicle modules, through the RH Shared hit report).

**Honest gaps:** General Quarters' guns still use the statistical shot-group model, not flown rounds; the round's form factor is AUTHORED, not calibrated to a published range; no friendly-fire check in the AI's trigger.
