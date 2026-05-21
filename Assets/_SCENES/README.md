# Scenes — Deep Water

One-line description of what each scene exercises. **Keep these accurate** — Claude reads this before touching scene-related code.

| Scene | What it exercises |
|---|---|
| `CVN-78 FORD` | Gerald R. Ford-class supercarrier — HDRP ocean + physically-based sky, AerialArcade-flyable F-18E spotted on the deck (`AirplaneAudio`, `XboxAirplane_Input`, `Waypoint_FixedWing`), `CarrierController` for sway / elevators / catapults (mostly stubbed). Currently no targets, no scenario — pure flight-ops sandbox. Cleanup applied 2026-05-17 (legacy VR + RCC scene manager removed). |
| `DDG Engagement` | Arleigh Burke DDG running a SAM engagement (VLS / ESSM / SPG-62 fire control). Primary scene for testing the missile + radar + VLS stack end-to-end. |
| `Dogfight` | Fixed-wing air-to-air combat. Exercises `FixedWingController`, AI flight, mouse flight, waypoint AI. |
| `Helo` | Helicopter flight + handling (`HelicopterController`). |
| `Rocket` | Rocketry / ballistics sandbox — `Rocket`, `RocketBehavior`, `RocketPhysics`. |
| `Rocket Man` | (TODO — describe: variant of Rocket with a player/controlled element?) |
| `SENTINAL` | (TODO — describe sentinel scenario: what unit, what behavior is being tested?) |
| `Mapping` | Terrain / world mapping setup (likely Cesium-based). |
| `OutdoorsScene` | General outdoor environment / lighting / atmospherics test bed. |

## Conventions

- New scenes for new scenarios go here, not in vendor demo folders.
- Add a row above when adding a scene. One sentence. Name the systems it exercises so Claude knows where to look.
- Mark a scene as `(deprecated)` rather than deleting it if it's still referenced elsewhere.
- Scenes used purely for unit/integration tests should live with the `RH Testing Suite/` examples, not here.
