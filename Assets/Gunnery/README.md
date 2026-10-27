# Gunnery

All gunnery for Deep Water, every platform: how rounds fly (drag, air density, gravity), firing tables, shot
groups and hit tests, and gun profiles. Decisions: [BLUEPRINT.md](BLUEPRINT.md).

| Folder | What |
|---|---|
| `Core/Ballistics` | `Atmosphere` (ballistic air), `DragCurve`, `FiringTable` (+ `ProjectileData`), `ShotGroup` (the statistical hit model). Moved from General Quarters on 2026-10-27 (K1); namespace `DeepWater.Gunnery.Ballistics` |
| `Tests/Editor` | The banned-API scan and the dependency-direction test. General Quarters' gun tests still exercise the ballistics through Guns L1 |

**Used by:** General Quarters (Guns L1). Air Wing's aircraft gun is the next slice.

**Deliberately not here:** missiles (Guided Fury), mounts and fire control (the vehicle modules), hit points
(the vehicle modules, through the RH Shared hit report).

**Honest gap:** no gunnery-only tests yet beyond the guards; the ballistics are covered by General Quarters' tests.
