# The two consumers, as built

Paths are under `Assets/`. Line numbers were read on 2026-10-27 at `5139dbbd`.

## Air Wing's jets, with RH Aviator's trigger (flown rounds)

Built in `3e34e232`, as Air Wing W55–W57 and RH Aviator A12, on Gunnery K4–K6. This is the template for any gun whose
rounds must fly and hit moving things.

| Step | What | Where |
|---|---|---|
| 1. Assemblies | `DeepWater.AirWing` references `DeepWater.Gunnery` and `RH.Shared`; so do its tests. `DeepWater.Aviator` references `DeepWater.Gunnery` | `Air Wing/DeepWater.AirWing.asmdef`, `Air Wing/Tests/Editor/DeepWater.AirWing.Tests.Editor.asmdef`, `RH Aviator/RH Aviator.asmdef` |
| 2. The vehicle's gun types | `GunMount`: a `GunProfile`, the muzzle and the bore line in the body frame. `CombatProfile`: hit points and the hit radius. Both sit on `AircraftProfile.Gun` and `.Combat`, and are validated with the profile | `Air Wing/Core/Profiles/ArmamentProfile.cs:11`, `:29` |
| 3. The profile asset | SO fields for the gun and combat (`AircraftProfileSO.cs:166`–`190`), baked into `GunProfile` with feet per second converted (`:314`–`335`), then `profile.Validate()` (`:336`). The numbers are written by **Deep Water → Air Wing → Apply Gun Data to F-A-18E** (`Editor/GunData.cs:17`), which labels each value and replaces only its own provenance paragraph | `Air Wing/ScriptableObjects/FA18E_AirWing.asset:206`–`218` |
| 4. State | `AirframeState.Rounds`, `GunCarry` and `HitPoints` (`State/AirframeState.cs:71`–`75`), all in the Fix (`Simulation/FlightFix.cs:84`–`86`). `FlightStatus.Killed` (`:14`) means no longer flown: it falls, then is Crashed. Jets start with a full gun and whole (`AircraftFactory.cs:159`–`160`) | |
| 5. The fire command | `PilotInputs.Trigger` (`Core/Inputs/PilotInputs.cs:44`), in the input log. The player fires on left mouse, left Ctrl, pad A or stick button 5 (`Modules/Input/FlightDeviceReader.cs:57`, `AirWingFlight.inputactions`). The AI fires on `AviatorSeat.WillHit` (`RH Aviator/Core/AviatorSeat.cs:282`–`299`), judged on what the pilot sees, through `LeadSight.MissDistance`, within the profile's `GunRangeM` and `TriggerMils` (`PilotProfile.cs:131`) | |
| 6. The stage in the tick | `FlightCore.Step` (`Core/FlightCore.cs:122`): seats issue inputs, aircraft step, then `Guns(tick)` (`:150`–`193`). That builds a `GunTarget` per flying jet from the picture to now (`:158`), runs `Rounds.Step` with the seeded draws (`:161`), takes hit points per `HitReport` and kills at zero (`:162`–`174`), then fires each held trigger with the carry (`:176`–`192`) | |
| 7. Seed | `FlightCore(…, gunnerySeed)` makes a `DeterministicRng` (`:86`–`91`), which implements `IUniformSource` (`Core/Simulation/DeterministicRng.cs:29`). The scene passes the fidelity sheet's seed (`Modules/Aircraft/AirWingRunner.cs:66`) | |
| 8. The adapter | `AirWingRunner.FixedUpdate` calls `_core.Step()` once per fixed step (`AirWingRunner.cs:109`–`117`), at execution order −50 (`:23`) | |
| 9. HUD and tracers | `FlightHud.Pipper` (`Modules/Cockpit/FlightHud.cs:104`–`121`): `LeadSight.RoundAt` at the range of the jet nearest the bore (inside 25° and 2,500 m), or 700 m. GUN and HP lines (`:83`–`85`), SHOT DOWN (`:59`). `TracerView.LateUpdate` draws `Flight.Rounds.Live` (`Modules/World/TracerView.cs:40`–`43`). The runner adds it, so no scene changes (`AirWingRunner.cs:67`) | |
| 10. Tests | `Air Wing/Tests/Editor/GunTests.cs`: the profile's PUBLIC numbers (`:56`), the rate and the carry (`:69`), shot down then falls and crashes (`:81`), two runs the same Fix (`:106`), a miss takes nothing (`:120`). `RH Aviator/Tests/Editor/GunFightTests.cs`: each skill kills a straight target (`:40`), the ace beats the rookie guns hot (`:110`), a 4 v 4 replays kill for kill (`:123`) | |

**Measured** (commit body):

- 256/256 EditMode across Gunnery, Air Wing, RH Aviator, General Quarters and Fast Play.
- Rounds each skill needed on a straight target: rookie 264, line 144, ace 116.
- The ace against the rookie: ahead in 20 of 20 merges, 20 kills to 0.
- Dogfight 4 v 4, 120 s in play mode: 307 rounds, 11 hits, one kill, up to 85 tracers, no exceptions.
- **Not verified:** a human firing.

## General Quarters' mounts (firing table and shot group)

Guns L1 was built in `753dc1e` (G34–G41). The mounts were made to follow the core in `d02d226`, the jitter fixed in
`76c2d6a`, and the ballistics moved to Gunnery in `118e8829` (K1). This is the template for fire control by lookup and
a statistical hit per burst. It does not use `GunProfile` or `RoundsInFlight`: the Gunnery `README.md:17` lists that
as an honest gap.

| Step | What | Where |
|---|---|---|
| 1. Assemblies | The runtime, Editor and test asmdefs each reference `DeepWater.Gunnery` (`118e8829`). There is no `RH.Shared` reference: kills go through the combat system (`ctx.RecordKill`), not a `HitReport` yet | `General Quarters/DeepWater.GeneralQuarters.asmdef`, `Editor/…Editor.asmdef`, `Tests/Editor/…Tests.Editor.asmdef` |
| 2. The vehicle's gun types | `GunBallisticsData` (`Core/Magazine/GunsL1.cs:23`–`42`) inside `GunMountProfileData.Ballistics` (`Core/Magazine/Guns.cs:13`, `:41`) | |
| 3. The profile asset | `Editor/Ddg51CombatData.cs`: `Mk45L1`, `CiwsL1` and `Mk38L1` (`:304`–`325`), used by the mounts (`:112`–`116`). It is written to `ScriptableObjects/DDG51_GQ_Loadout.asset` by **Deep Water → General Quarters → Build Range Assets** (`:46`). Sources and labels are in `Calibration~/GUNS-SOURCES.md` | |
| 4. Build | `FiringTable.For(projectile, elMin, elMax)` once per mount (`GunsL1.cs:82`). The factory refuses L1 without ballistics data (`Guns.cs:260`–`261`) | |
| 5. Lead and lay | `CanEngage` (`GunsL1.cs:157`–`190`) iterates the lead four times with `TrySolve(horizontal, height)` (`:175`). It rounds the time of flight to whole ticks so rounding is not aim error (`:178`), and checks range and arc. `Step` (`:99`) slews toward the solution at the mount's rates (`Slew`, `:151`–`155`), and fires only once laid (`:114`) and ready | |
| 6. The hit | `ResolveArrivals` runs first each tick (`:192`). The miss distance across the line of fire combines aim error, ship motion × latency, and spotting bias. It feeds `PresentedRadiusM` (or the fuze's lethal radius), `RoundHitProbability` and `BurstKillProbability` (`:224`–`229`). One seeded draw per burst decides (`ctx.Rng.Chance`, `:231`), and the log line prints the whole sum (C28) | |
| 7. Fix | `GunMountL1.Fingerprint` (`:240`–`247`): rounds, ready tick, online, train, elevation, bias, bursts in flight | |
| 8. The adapter | `ShipCombat.FixedUpdate` (`Modules/Combat/ShipCombat.cs:152`) calls `Combat.Step` (`:169`), at execution order −100 (`:30`) | |
| 9. Presentation | `IGunMount.TryGetLay` (`Guns.cs:67`) reports the core's train and elevation. `MountAnimator.Update` follows it at twice the rig's rate (`Modules/Presentation/MountAnimator.cs:56`, `:115`–`131`). `CombatPresentation` rebinds it to each new run's mounts (`CombatPresentation.cs:127`). No pipper: ships have no sight to draw | |
| 10. Tests | `Tests/Editor/GunsL1Tests.cs`: ISA and G7 against their sources (`:74`); the vacuum range v²/g (`:92`); calibrated ranges and VALIDATION (`:101`); solutions pass the aim point, with a dense sweep every 25 m to 22 km (`:121`, `:142`–`143`); lead on a crossing aircraft (`:150`); laying before the first shot (`:163`); ship motion as miss (`:179`); spotting (`:194`); the fuzes (`:212`); the shot-group maths (`:221`); refusal without data (`:237`); no jitter (`:276`); guns-only against sea-skimmers (`:326`); the saturation raid repeats within 1 ms per step (`:359`). `PresentationTests` checks that the model shows the core's lay (`:123`) | |

**Measured** (commit bodies):

- Guns only against 8 sea-skimmers: L0 killed 0, L1 killed 6 (CIWS 4, Mk 45 2).
- Play mode: the Mk 45 laid and fired at 12,965 m (10.7°, 25.4 s time of flight). A PhysX roll of 2.0°/s gave 22.8 m of aim error.
- 0.060 ms per fixed step at saturation (L0: 0.066).
- After the jitter fix: 0 drops in 91 s of play.

## What a third consumer (a ground vehicle, say) would add

DERIVED from the code; nothing built.

- **A surface.** `RoundsInFlight` has none: volleys are only dropped below `FloorM` (−100 m by default, `RoundsInFlight.cs:52`). Over terrain, test the round against the ground in the vehicle's stage, or set `FloorM` from the terrain under the fight.
- **Shared ids.** `GunTarget.Id` and the shooter id live in one space (`RoundsInFlight.cs:100` skips only the shooter). Once ships, jets and tanks share a fight, the ids must not collide across modules.
- **The dependency test.** Add the module's asmdef name to the list in `Gunnery_ReferencesNoVehicleOrMissileModule` (`Gunnery/Tests/Editor/GunneryCoreBannedApiTests.cs:59`).
- **Fast Play.** Add the runtime asmdef to Fast Play's `FirstPartyRuntime` (`Fast Play/Tests/Editor/StaticStateTests.cs:21`). `DeepWater.Aviator` is not on that list as of `5139dbbd`.
