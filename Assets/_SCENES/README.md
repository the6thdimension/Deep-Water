# Scenes — Deep Water

One-line description of what each scene exercises. **Keep these accurate** — Claude reads this before touching scene-related code.

| Scene | What it exercises |
|---|---|
| `CVN-78 FORD` | Gerald R. Ford-class supercarrier — HDRP ocean + physically-based sky, four AerialArcade F-18Es on the deck (two switched off; two flying `AIAirplaneController` waypoints). Carrier Ops `CarrierBehaviour` on the CVN78 prefab (sway, catapults, elevators, FLOLS, wires): only catapult 1 is fully wired, no aircraft is attached to a catapult and no jet carries `TailhookHook`, so nothing launches or traps yet (Air Wing carrier stream, parked). Currently no targets, no scenario — pure flight-ops sandbox. Cleanup applied 2026-05-17 (legacy VR + RCC scene manager removed); rows corrected 2026-10-25. |
| `DDG Engagement` | Arleigh Burke DDG running a SAM engagement (VLS / ESSM / SPG-62 fire control). Primary scene for testing the missile + radar + VLS stack end-to-end. |
| `Dogfight` | **Air Wing's test bed** (W12–W14), built by Deep Water → Air Wing → Build Dogfight (it overwrites this scene in place; rebuild after editing the builder or the gate course). The Air Wing F/A-18E airborne at 5,000 ft and 400 kt; the Sea State sea; land with runway 09/27 (a crash at L0, no gear until L1); four gates from `Air Wing/Examples/Dogfight/Dogfight_Gates.asset`; the DDG-51 model as a landmark. How to fly it: `Assets/Air Wing/README.md`. The scene before 2026-10-25 is in `_Archive/Dogfight_legacy/`. |
| `Helo` | UH-60M with the legacy `HelicopterController`, which cancels its own weight (hovers at zero collective) and never translates from tilt. Air Wing's helicopter stream replaces it after jet L1 (W20). |
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
