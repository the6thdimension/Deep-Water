# Worked example: the F/A-18E and its AI pilots

Air Wing Phases 1–2, RH Aviator Phase 3 and the gun (2026-10-25 to 2026-10-27). Every commit is in each module's ROADMAP, except the gun's: Gunnery's `ROADMAP.md`, the blueprints (W55–W57, A12) and commit 3e34e232.

## Files

| Layer | Files |
|---|---|
| Flight core | `Air Wing/Core/FlightCore.cs` (tick, picture A1, surface), `Aircraft.cs`, `AircraftFactory.cs` (`LevelStart`, `GroundStart`, `Build`), `Simulation/` (clock, Fix, RNG, probes), `State/AirframeState.cs` |
| Parts | `Airframe/` (L0 point mass, L1 HARV 6-DOF), `FlightControls/` (L0 lag, L1 NDI α command), `Powerplant/` (L0, L1 spool), `LandingGear/` (L1 spring-damper struts), `Atmosphere/` (ISA) |
| Data | `ScriptableObjects/FA18E_AirWing.asset` ← `Editor/HarvL1Data.cs`; `FA18E_FidelitySheet.asset`; `PlayerSeatSettings.asset` |
| Seats | `Core/Seats/PlayerSeat.cs` (owner by last source moved; the instructor; the level hold), `MouseInstructor.cs`, `FlightPathHands.cs`, `SeatRoster.cs`, `Seats.cs` (Hold, Replay); `Core/Inputs/Seat.cs` (`ISeat`, `ISeatStatus`) |
| Scene | `Modules/Aircraft/AirWingRunner.cs` (−50), `AircraftView.cs`, `GearRig.cs`; `Modules/Input/` (reader, `PlayerSeatInput` −60); `Modules/Cockpit/` (HUD, pick list, chase camera) |
| AI core | `RH Aviator/Core/AviatorSeat.cs`, `Brain.cs`, `Navigate.cs`, `Formation.cs`, `Pattern.cs`, `Perception.cs`, `Fight.cs`, `PilotProfile.cs` |
| AI scene | `RH Aviator/Modules/AviatorPilot.cs`, `AviatorOrders.cs` (A3), `AviatorFight.cs` (0), `AviatorBenchmarkRunner.cs`; `Editor/AviatorTools.cs` (profiles, menus), `AviatorBenchmark.cs` |
| The gun | `Gunnery/Core/Guns/` (`GunProfile`, `RoundFlight`, `RoundsInFlight`, `LeadSight`); `RH Shared/Contracts/HitReport.cs`; Air Wing `Core/Profiles/ArmamentProfile.cs` (`GunMount`, `CombatProfile`), `Core/FlightCore.cs` (the guns stage, `Kills`), `Editor/GunData.cs`, `Modules/Cockpit/FlightHud.cs` (pipper, GUN, HP, SHOT DOWN), `Modules/World/TracerView.cs`; RH Aviator `Core/AviatorSeat.cs` (`WillHit`, gun tracking) |
| Tests | `Air Wing/Tests/Editor/*` (79+, with `GunTests`), `RH Aviator/Tests/Editor/*` (`AviatorSeatTests`, `FormationTests`, `HomeTests`, `FightTests`, `GunFightTests`), `Gunnery/Tests/Editor/*` (`GunTests`, `GunneryCoreBannedApiTests`) |

## The line pilot (AUTHORED; `AviatorTools.ApplyLinePilot`)

| Group | Numbers | Why |
|---|---|---|
| Hands | L1 gain 2, 70° bank, roll 2/°, 0.06°/m, ±20°, 0.25 G/°, 6 G, throttle 0.02/kt, trim 0.75, integral 0.004 | slice 3a, gates within 4 m (C41) |
| Ground guard | 100 m, 6 s, climb 8°, bank 30° | Air Wing AP25 |
| Formation | slot 40 m right, 10 m back; look-ahead 800 m; 0.25 /s; decel 1.5 m/s²; overtake 150/30 kt; speed lead 2; brake past 8 kt; formation hands 0.03°/m, 0.5 G/°; rotation share 0.75; join avoid 25 m (no fight) | slice 3b, a 100-run sweep; 1.6× gain margin (C42); join avoid 2026-10-27 |
| Pattern | initial 3 nm at 800 ft, 350 kt; break 600 m past the threshold, slowing to 240 kt; downwind 600 ft, 1,500 m abeam; the 180 at 1,500 m before the threshold; 145 kt; gear below 250 kt; pattern hands 40°, 0.1 G/°, 6° dive, 30 m floor; 3° to 300 m in; flare from 30 ft to 0.8°; final carrot 300 m | slice 3c, 20 of 20 landings (C43) |
| Fight | sees 120° round the nose, 0.7 s late, remembers 8 s, 8 km; 330 kt; lead and lag 1 s inside 800 m when closing over 60 kt; threat 1.5 km in 60° pointing at it; floor 300 m; clear 8 km; energy sense to 2 G at 220 kt; turn matching 0.7; trigger inside 900 m and 12 mils | slice 3d; A12 |
| Rookie | 4.5 G, 60°, 90°, 1.2 s, pure pursuit only, 380 kt, no energy sense, no turn matching; trigger 1,400 m and 30 mils (sprays) | W45; A12 |
| Ace | 7 G, 75°, 150°, 0.35 s, lead and lag 1.2 s, 320 kt, energy to 240 kt, turn matching 1; trigger 800 m and 6 mils (waits for the shot) | W45; A12 |

## The gun (`GunData.Apply`, W56–W57)

The M61A2's 412 rounds and 6,000 a minute are PUBLIC (W56); the rest is AUTHORED in the asset's provenance. Ten hit points, one per round that hits; at zero the jet is Killed, falls and is Crashed (W57).

## Measured

- Gates: 0–4 m from centre, 335 s; Dogfight 239 s.
- Formation: join from 2 nm in 76.7 s; hold 6.8 / 2.3 / 4.2 m through a 30° turn and a 2 G pull; four-ship joins from four starts pass no closer than 23 m.
- Landing: 20 of 20 at 10.2 ft/s, 0.0 m off, stopped ~1,219 m; Dogfight 8.4 ft/s, 1,225 m.
- Fight, guns safe, scored by position (A9 declined): the ace ahead of the rookie in 19 of 20 merges (1,278 s to 60 s); line v line and ace v line 14–6 each, almost all of it head-on passes (the survey numbers: [fight-survey.md](fight-survey.md)); 4 v 4 no crash; the AI at eight 3 µs per pilot per tick (player build).
- Gun (3e34e232): a straight target shot down by each skill, the rookie on 264 rounds, the line 144, the ace 116; ace v rookie guns hot, ahead in 20 of 20 with 20 kills to 0. Since Gunnery's exact hit model (7f13bd97, 2026-10-29): the rookie on 274, the line 142, the ace 116; ace v rookie still 20 kills to 0, on 2,172 rounds (was 2,234). Dogfight 4 v 4 smoke, 120 s: one kill. The headless 4 v 4 scores no kills in 240 s (equal fights stalemate: slice 3f). Not fired by a human yet.
- Level hold: released at 121° bank, level in 15 s, then within 10 ft and 0.5° for 60 s.
