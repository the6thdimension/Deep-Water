# Worked example: the F/A-18E and its AI pilots

Air Wing Phases 1–2 and RH Aviator Phase 3 (2026-10-25 to 2026-10-27). Every commit is in each module's ROADMAP.

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
| Tests | `Air Wing/Tests/Editor/*` (79+), `RH Aviator/Tests/Editor/*` (`AviatorSeatTests`, `FormationTests`, `HomeTests`, `FightTests`) |

## The line pilot (AUTHORED; `AviatorTools.ApplyLinePilot`)

| Group | Numbers | Why |
|---|---|---|
| Hands | L1 gain 2, 70° bank, roll 2/°, 0.06°/m, ±20°, 0.25 G/°, 6 G, throttle 0.02/kt, trim 0.75, integral 0.004 | slice 3a, gates within 4 m (C41) |
| Ground guard | 100 m, 6 s, climb 8°, bank 30° | Air Wing AP25 |
| Formation | slot 40 m right, 10 m back; look-ahead 800 m; 0.25 /s; decel 1.5 m/s²; overtake 150/30 kt; speed lead 2; brake past 8 kt; formation hands 0.03°/m, 0.5 G/°; rotation share 0.75 | slice 3b, a 100-run sweep; 1.6× gain margin (C42) |
| Pattern | initial 3 nm at 800 ft, 350 kt; break 600 m past the threshold, slowing to 240 kt; downwind 600 ft, 1,500 m abeam; the 180 at 1,500 m before the threshold; 145 kt; gear below 250 kt; pattern hands 40°, 0.1 G/°, 6° dive, 30 m floor; 3° to 300 m in; flare from 30 ft to 0.8°; final carrot 300 m | slice 3c, 20 of 20 landings (C43) |
| Fight | sees 120° round the nose, 0.7 s late, remembers 8 s, 8 km; 330 kt; lead and lag 1 s inside 800 m when closing over 60 kt; threat 1.5 km in 60° pointing at it; floor 300 m; clear 8 km; energy sense to 2 G at 220 kt; turn matching 0.7 | slice 3d |
| Rookie | 4.5 G, 60°, 90°, 1.2 s, pure pursuit only, 380 kt, no energy sense, no turn matching | W45 |
| Ace | 7 G, 75°, 150°, 0.35 s, lead and lag 1.2 s, 320 kt, energy to 240 kt, turn matching 1 | W45 |

## Measured

- Gates: 0–4 m from centre, 335 s; Dogfight 239 s.
- Formation: join from 2 nm in 76.7 s; hold 6.8 / 2.3 / 4.2 m through a 30° turn and a 2 G pull.
- Landing: 20 of 20 at 10.2 ft/s, 0.0 m off, stopped ~1,219 m; Dogfight 8.4 ft/s, 1,225 m.
- Fight: ace ahead in 20 of 20 merges (34 s to 0); 4 v 4 no crash; the AI at eight 3 µs per pilot per tick (player build).
- Level hold: released at 121° bank, level in 15 s, then within 10 ft and 0.5° for 60 s.
