# The gun profile, field by field

`GunProfile` (`Assets/Gunnery/Core/Guns/GunProfile.cs`) is the gun as Gunnery sees it: its round, how fast it fires,
how many rounds it carries and how widely they spread. SI units. Where it sits and where it points are the vehicle's.

**MEASURED** numbers below come from an offline harness, run 2026-10-27. It compiled `Assets/Gunnery/Core` and RH
Shared's `HitReport` on .NET 8 against Unity 6000.4.11f1's `UnityEngine.CoreModule`, outside the Editor, using the
F/A-18E's gun as `FA18E_AirWing.asset` carries it: level fire from 3,048 m with the shooter at rest, unless stated.
Gunnery's own `GunTests` pass in the same harness (7/7). In the Editor (Mono) the last digit can differ.

## Fields

| Field | Unit | What it changes in play | Values in the repo (label) |
|---|---|---|---|
| `Name` | text | Nothing physical. It becomes `Volley.Weapon` (`RoundsInFlight.cs:71`) and `HitReport.Weapon`, so it names the gun in hit and kill logs. Air Wing's SO treats an empty name as unarmed (`AircraftProfileSO.cs:166`, `:314`) | "M61A2 20 mm" |
| `Round.MassKg` | kg | Acts only through the drag constant k = form factor × area / (2 m) (`RoundFlight.cs:13`): a heavier round slows less. It plays no part in lethality, which is hit points or a kill chance | F/A-18E 0.1 (AUTHORED); Mk 45 31.1, Mk 149 0.10, Mk 38 0.184 (VERIFIED, `GUNS-SOURCES.md`) |
| `Round.DiameterM` | m, the diameter that flies | Acts through k (area). For a sub-calibre round, use the penetrator's diameter (`FiringTable.cs:11`) | 0.02 (AUTHORED); 0.127, 0.01275, 0.025 (VERIFIED) |
| `Round.MuzzleVelocityMps` | m/s, along the bore; the shooter's velocity is added (`RoundsInFlight.cs:73`) | Sets time of flight, drop and lead, so it moves the pipper and the reach. At −10 %, time of flight to 1,000 m goes from 1.142 to 1.285 s and drop from 5.68 to 7.14 m (MEASURED) | 3,450 ft/s, SECONDARY (`GunData.cs:27`); 808, 1,113, 1,100 (VERIFIED) |
| `Round.FormFactor` | × the G7 curve; 0 flies in a vacuum | Sets the drag. At 1,000 m: FF 0 gives 0.951 s and 4.43 m of drop at 1,052 m/s; FF 1 gives 1.142 s, 5.68 m and 728 m/s; FF 2 gives 1.431 s, 7.80 m and 465 m/s (MEASURED). CALIBRATE it to one published range with `FiringTable.CalibrateFormFactor` (`FiringTable.cs:221`). A value far from 1 is a finding, not a fit (C26) | 1.0, AUTHORED and not calibrated (Gunnery `ROADMAP.md:14`); 0.9251, 3.2484, 1.2057 (CALIBRATED) |
| `RoundsPerMinute` | per minute | Each tick fires rpm / 60 × dt rounds, and the vehicle carries the fraction over to the next tick (Air Wing's `GunCarry`). It sets hits per second on a target, so how fast hit points fall. At 6,000 a minute and 50 Hz that is 2 a tick | 6,000 (PUBLIC, GD-OTS: `GunData.cs:25`) |
| `RoundsCarried` | rounds | Endurance. 412 rounds at 6,000 a minute is 4.12 s of trigger (DERIVED). The vehicle copies it into its state at the start (`AircraftFactory.cs:159`). `IsSet` needs it above 0 | 412 (PUBLIC: `GunData.cs:26`) |
| `DispersionMils` | milliradians, 1σ about the bore | σ = mils × 10⁻³ × the distance flown (`RoundsInFlight.cs:27`). Each round in a close pass hits with `ShotGroup.RoundHitProbability(radius, σ, miss)`. On a 4 m sphere with the pass centred: 0.865 per round at 500 m and 0.393 at 1,000 m at 4 mils; at 8 mils, 0.393 and 0.118 (MEASURED). 0 makes a hit all or nothing. A volley is tested only within radius + 3σ (`SigmaReach`, `RoundsInFlight.cs:50`) | 4 (AUTHORED: game value) |
| `MaxTimeOfFlightS` | s | Volleys are dropped after it (`RoundsInFlight.cs:120`), and `LeadSight` stops searching at it, so no pipper exists beyond the range reached in that time. At 3 s the F/A-18E's round reaches 2,072 m from rest and 2,405 m with the shooter at 200 m/s (MEASURED). It also bounds the number of live volleys: one per gun per tick, so 151 for one gun firing at 50 Hz (MEASURED). Cost is volleys × targets per tick | 3 (AUTHORED: "past ~2,500 m nothing is aimed") |

`Validate()` (`GunProfile.cs:24`) throws on no rounds, no rate, a non-positive muzzle velocity, mass or diameter, a
negative dispersion or no time of flight. Call it in the profile's `Bake`, as Air Wing does through
`AircraftProfile.Validate`.

## The vehicle's own gun numbers (not in `GunProfile`)

- **Air Wing** (`Core/Profiles/ArmamentProfile.cs`):
  - `GunMount.MuzzleM`, in the body frame about the centre of mass (AUTHORED, off the model);
  - `GunMount.BoreBody`, the unit bore line, 1° up (AUTHORED);
  - `CombatProfile.HitPoints`, 10 (AUTHORED, abstract: one a round);
  - `CombatProfile.HitRadiusM`, 4 m (AUTHORED: about the jet's presented size).
- **General Quarters** predates `GunProfile`: it carries `ProjectileData` inside its own `GunBallisticsData` (`Core/Magazine/GunsL1.cs:23`). That struct holds:
  - elevation limits, train and elevation rates, and the laying tolerance;
  - its own dispersion (`DispersionMrad`) and solution latency;
  - the fuze, the lethal radius and the fuze's reliability;
  - the kill chance per hit, against air and against surface targets;
  - Phalanx's spotting bias and its time constant.

  The arcs, ranges, rate, burst and magazine are in `GunMountProfileData` (`Core/Magazine/Guns.cs:13`). Values are in `Editor/Ddg51CombatData.cs:304`–`325`, with sources in `Calibration~/GUNS-SOURCES.md`.

## Labels

Every number carries one label in the asset's provenance and in the module's sources file:

| Label | Means |
|---|---|
| VERIFIED | Read on the page of a cited public source (Air Wing's `GunData` writes PUBLIC for this) |
| MEASURED | Measured from the running model or the mesh. Say where: an Editor test's tagged log, play mode, or the offline harness |
| DERIVED | Computed from VERIFIED or MEASURED values (412 rounds at 6,000 a minute is 4.12 s) |
| CALIBRATED | Fitted to one named public figure. One number per figure: the form factor to a published range |
| VALIDATION | Compared with a public figure, not fitted to it. Report the gap (the Mk 45's greatest range comes at 48.0° against a published 47°) |
| AUTHORED | A choice with a reason, because nothing public exists: dispersion, hit points, hit radius, kill chances, time of flight |

Air Wing's provenance also writes **SECONDARY**: a figure from a secondary source that is not pinned for this variant
(the 3,450 ft/s muzzle velocity, `GunData.cs:27`). Treat it as VERIFIED with that caveat kept in the text.

**Public figures only.** Cite the page, as `GUNS-SOURCES.md` does. If nothing public exists, the number is AUTHORED;
never leave a guess unlabelled. Lethality stays abstract: hit points, or an AUTHORED kill chance per hit. No penetration,
fragmentation or vulnerability modelling (General Quarters `BLUEPRINT.md:224`).

## The API in one place

| Call | Where | Use |
|---|---|---|
| `FiringTable.For(round, elMin, elMax, step = 0.5)` | `FiringTable.cs:50` | Builds one table per round and elevation limits, cached for the process (`s_cache`, `:46`) |
| `table.TrySolve(x, y, out elevationDeg, out tofS)` | `:164` | Fire control by lookup: x is horizontal distance and y is height relative to the muzzle. Returns the lowest elevation that passes through the point, or false |
| `table.MaxRangeM`, `MaxRangeElevationDeg`, `RangeAt(el)`, `ApexM(el)` | `:41`, `:210`, `:201` | Calibration and VALIDATION checks |
| `FiringTable.CalibrateFormFactor(round, rangeM, elevationDeg or NaN, elMax)` | `:221` | Bisection; NaN fits the greatest range |
| `LeadSight.RoundAt(gun, muzzle, shooterVel, bore, rangeM, out point, out tof)` | `LeadSight.cs:19` | The pipper: where a round fired now is at that range, flown in 0.02 s steps (`StepS`, `:13`) |
| `LeadSight.MissDistance(gun, muzzle, shooterVel, bore, targetPos, targetVel, out targetMinusRound, out tof)` | `:51` | How close a burst fired now passes a target flying straight. The AI's trigger and its gun tracking use it |
| `rounds.Fire(shooterId, tick, gun, muzzle, shooterVel, bore, n)` | `RoundsInFlight.cs:64` | Adds one volley of n rounds |
| `rounds.Step(tick, dt, targets, draws, hits)` | `:86` | Flies every volley dt, runs the swept hit test against each `GunTarget` (From → To over the same dt), and appends `HitReport`s |
| `rounds.Live`, `RoundsFired`, `RoundsHit`, `FloorM`, `Clear()` | `:52`–`58`, `:125` | Tracers, counters, the drop height, reset |
| `ShotGroup.RoundHitProbability(r, σ, d)`, `BurstKillProbability(p, pk, n)`, `PresentedRadiusM(L, D, axis, lineOfFire)`, `SpottingBias(...)` | `ShotGroup.cs:17`, `:26`, `:33`, `:47` | The statistical hit model |
| `RoundFlight.DragConstant(round)`, `RoundFlight.Step(ref p, ref v, k, dt)` | `RoundFlight.cs:13`, `:24` | One round's flight, midpoint (RK2) |
| `new HitReport(tick, shooter, target, weapon, rounds, point, impactSpeed)` | `RH Shared/Contracts/HitReport.cs:25` | What a hit says; the target's module applies it |

## How far to trust the hit model

`ShotGroup.RoundHitProbability` is exact when the pass is centred. Off centre, it multiplies by exp(−d²/2σ²). The
doc comment says this "errs toward hits for a large one" (`ShotGroup.cs:15`). Measured against a Monte Carlo of the
true disc probability (200,000 rounds per cell, offline), it errs toward **misses** once the target is larger than
the spread:

| r/σ | d/σ = 0 | 1 | 2 | 3 |
|---|---|---|---|---|
| 0.25 (small target) | 0.031 / 0.031 | 0.019 / 0.019 | 0.004 / 0.004 | 0.000 / 0.000 |
| 1 | 0.394 / 0.394 | 0.239 / 0.265 | 0.053 / 0.082 | 0.004 / 0.011 |
| 2 | 0.865 / 0.866 | 0.524 / 0.731 | 0.117 / 0.396 | 0.010 / 0.114 |
| 4 (large target) | 1.000 / 1.000 | 0.606 / 0.997 | 0.135 / 0.966 | 0.011 / 0.802 |

Each cell gives the model, then the true value. Where it bites:

- **The jets:** a 4 m sphere at 4 mils is r/σ = 4 at 250 m, 2 at 500 m and 1 at 1,000 m. So close-in passes that go through the jet off centre score far fewer hits than the geometry gives.
- **The Mk 45's proximity fuze:** its 8 m lethal radius is larger than σ inside about 4 km (2 mrad).
- **Small presented targets** are accurate. A 4.6 × 0.34 m sea-skimmer presents 0.17 to 0.71 m (DERIVED with `PresentedRadiusM`), against CIWS's σ of 4 m at 2 km.

Not fixed. The AI's trigger numbers and the measured fights were taken with the model as it is, so changing it is a
Gunnery-wide decision for SIX.
