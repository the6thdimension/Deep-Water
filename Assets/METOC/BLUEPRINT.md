# METOC — blueprint (the atmosphere: time, sun, cloud, rain, wind)

> **Status: LOCKED BLUEPRINT (SIX, 2026-10-30). Slice 1 built and seen in play on `feat/metoc` (2026-10-30); E46 waits on SIX (M2).** SIX answered sixteen questions on 2026-10-30 in four rounds (E26–E41). Fourteen picks are Claude's leaning; two are not (E35, E38). E42–E47 are Claude's technical proposals, **Proposed** until SIX locks them. The parent decisions are in `Assets/Theatre/BLUEPRINT.md` (E4, E13, E18–E20).
> **This document's one job:** say what METOC is and why, and record every decision behind it. Work order and progress live in `ROADMAP.md`, not here.

## 1. In plain language

**The ask.** SIX, 2026-10-30, relayed by the session that started this one: work on weather, clouds, rain and time of day, "using the current sky". Build on the sky the project has; don't replace it.

**What METOC is.** METOC (meteorological and oceanographic) is the atmosphere module. It decides the weather at a place and time and publishes it as a **METOC report**: the date and time, where the sun and moon are, how much light reaches the ground, and (in later slices) cloud, ceiling, visibility, rain and wind. HDRP's sky, clouds, fog and rain only draw what the report says.

**Why it is reckoned.** Darkness, cloud and rain will decide outcomes: a lookout who cannot see, a radar in rain, a helicopter that cannot land in a rising sea. So METOC decides everything in plain C# on the fixed tick, seeded, the same on every run (Reckoning standing constraints 1–3). Nothing that changes an outcome comes back from the GPU.

**What is already there.** One shared sky: the `_ENVIRONMENT/Sky and Fog Volume` prefab and `Settings/SkyandFogSettingsProfile.asset`, used by Dogfight, Dogfight Training, DDG Engagement, CVN-78, SENTINAL, the Sea State demo and the Air Wing flight test:
- Physically Based Sky (Earth Simple model, aerosols 0.042);
- volumetric clouds (Cloudy preset, base 1 200 m, 2 000 m thick, 229 primary and 13 light steps, shadows);
- a 2D cloud layer at 2 000 m (from the default profile);
- volumetric fog (mean free path 1 665 m below −192 m, fading out by 1 433 m);
- histogram auto-exposure clamped to EV 10–14;
- a sun of 190 000 lux at 5 485 K at a fixed rotation.

METOC keeps all of it and drives only the parts it decides.

**The test.**
- A ship's watch passes from noon to night on a chosen date and place. The sun sets where and when NOAA says it does, and the moon rises with the right phase.
- The light at the ground falls from about 100 000 lux to a fraction of a lux, and the picture adapts without going black.
- Two runs from the same scenario give bit-identical reports.
- Later slices: cloud cover and ceiling, then rain, cut visibility the way the report says, and the wind grows the sea.

**Honest limits.**
- The sun is good to about 0.02° and the moon to about 0.3° (low-precision public algorithms, E28). Time is Universal Time; the ~69 s between UT and Terrestrial Time and leap seconds are ignored (E44).
- Ground illuminance is a clear-sky model built from published formulas and a twilight table (E45). Clouds dim it from slice 2. It is not a radiative-transfer model.
- Fictional theatres have an authored latitude and longitude.
- Until Theatre's exposure map exists, the wind can only grow an open-ocean sea (E36).

## 2. Vocabulary

Names follow E19's military-planning family. Code names are proposals until their slice builds them.

| Name | What it is | Code |
|---|---|---|
| **METOC** | The module | `DeepWater.Metoc`, `Assets/METOC/` |
| **METOC report** | The weather at a place and time: time, sun, moon, light; later cloud, ceiling, visibility, rain, wind | `MetocReport` |
| **METOC clock** | Reckoned time: Universal Time in seconds from J2000, advanced on the fixed tick × day speed | `MetocClock` |
| **Day speed** | The labelled factor between simulation time and the day's time (E29). 1× is real time | `daySpeed` |
| **Almanac** | The sun and moon positions and the moon's phase, from published algorithms (E28). Named after the Nautical Almanac | `Almanac` |
| **Place** | The latitude and longitude the sky is computed for: the theatre's anchor, authored for fictional theatres | `MetocPlace` |
| **Illumination** | Reckoned ground illuminance on a horizontal surface, in lux (E41, E45) | `Illumination` |
| **METOC order** | A logged change from the scenario or the god role: jump the time, set the day speed, move the place; later, a weather blend (E33) | `MetocOrder` |
| **Overlay volume** | The runtime-only HDRP volume through which METOC draws (E42) | `HdrpMetocRenderer` |

## 3. Architecture

```
MetocScenarioSO (start UT, place, day speed)      IlluminationProfileSO (published constants, with provenance)
            │                                                 │
            ▼                                                 ▼
MetocSimulation (pure C#, fixed tick, E29) ── MetocClock ── Almanac (sun: NOAA/Meeus; moon: Astronomical Almanac low precision)
   │   orders in: MetocOrder log (tick, issuer, kind, value)        └── Illumination (sun + moon + night floor)
   ▼
MetocReport (struct, every tick) ──► readers: the sea (slice 4, E36), sensors (later, own streams), captains
   │
MetocDirector (MonoBehaviour, FixedUpdate, before the sea)                                                      reckoned
───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
HdrpMetocRenderer (MonoBehaviour, LateUpdate) + HdrpMetocLookSO                                                 unreckoned
   ├─ the Sun light: direction, intensity (≈128 000 lux above the air, E39); HDRP's sky attenuates it
   ├─ a Moon light: direction, phase, intensity (E39); only the brighter body casts shadows (E43)
   └─ overlay volume (runtime profile, higher priority, E42): exposure floor from Illumination (E40); later clouds, fog, rain
MetocPanel (App UI, E35) + keys: readout of the report; sliders and keys issue METOC orders
```

- **Frame.** Theatre E2: Unity X is east, Y is up, Z is north. Azimuth is measured from north, clockwise.
- **The pure core** has no scene or engine dependencies (P6) and is guarded by a banned-API test (Reckoning R36, as in General Quarters, Gunnery and Air Wing).
- **Orders.** Every change to METOC's state is a `MetocOrder`, due at a tick and applied at the start of it, kept in a log with its issuer. A replay of the scenario plus the log reproduces every report.
- **Drawing never writes assets** (E42). The shared profile, the default profile and the scenes are left as they are on disk.

## 4. The fidelity ladder

The METOC row of Theatre §4 holds the levels. E27 splits L0 into slices.

| Level | What | Slices |
|---|---|---|
| **L0** | Uniform conditions + METOC report; the sea grown from wind (open ocean until exposure exists) | 1 time, sun, moon, light (E27) · 2 cloud cover, ceiling, visibility (E30, E31) · 3 rain (E32) · 4 wind → sea (E36) · weather blends by order throughout (E33) |
| L1 | Fronts on a seeded timeline, every change logged | — |
| L2 | Coarse wind field (km cells, ~1 Hz) + seeded Dryden / von Kármán gusts | — |
| L3 | Temperature and humidity effects (e.g. the evaporation duct on radar) | — |

Each level ships with EditMode tests. A request above the highest built level falls back out loud (C14).

## 5. Decision register

E26–E41 are SIX's picks of 2026-10-30, in round order. "Leaning" is Claude's recommendation before the pick. E42–E47 are Claude's proposals.

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| E26 | METOC against E20's "Theatre first" | **METOC's sky slices come now; the coastal-ship-ops slices keep their order after them.** *A reversal of E20* (logged in the Theatre register). Sea growth runs as open ocean until Theatre's exposure exists | Same | Locked (reversal of E20) |
| E27 | Slice 1's scope | **Time of day and the sun:** module skeleton, reckoned clock, sun and moon, `MetocReport`'s sun fields, the HDRP light and night exposure. Clouds and visibility in slice 2, rain in slice 3 | Same | Locked |
| E28 | How the sun is placed | **Date + latitude/longitude from public algorithms:** NOAA's solar-position equations (from Meeus), about 0.02°; the moon from the Astronomical Almanac's low-precision formulas, about 0.3°, with its phase. Fictional theatres carry an authored latitude/longitude | Same | Locked |
| E29 | How time runs | **Reckoned:** the scenario sets the start date and time; the clock advances on the fixed tick × a labelled day-speed factor (default 1×); god jumps are logged orders | Same | Locked |
| E30 | Clouds | **Volumetric clouds driven by cover:** METOC decides oktas (0–8), cloud base and thickness; the adapter drives HDRP's Custom density and shape, `bottomAltitude` and `altitudeRange`. The 2D layer stays for cirrus | Same | Locked |
| E31 | Cloud cost against E4 | **Quality is an SO setting, chosen by measurement:** the cloud slice A/Bs 229/13 against 128/8 and 64/6 steps with screenshots; SIX picks from the numbers. 229/13 stays until then | Same | Locked |
| E32 | Rain | **Rate in mm/h with METAR intensity classes;** VFX Graph rain in a camera-following box; rain cuts visibility and darkens the clouds. No wet surfaces in this stream | Same | Locked |
| E33 | Weather changes at L0 | **Logged blends:** a starting weather, then scenario or god orders that blend to a new state over a set time, deterministic and logged. Seeded fronts stay L1 | Same | Locked |
| E34 | First scene | **A new METOC demo scene, built by a menu builder from the Sea State demo** (same sky prefab, sun and sea). No existing scene is touched; the Dogfight scenes adopt METOC later, on SIX's say | Same | Locked |
| E35 | Controls | **SO presets, keys, an on-screen report and an App UI runtime panel with sliders** (date, time, latitude, longitude, day speed; weather sliders with their slices). Every slider and key issues a METOC order | Presets + keys + readout | Locked (not the leaning) |
| E36 | Coupling | **The sea in this stream, the sensors later.** A METOC slice drives Sea State's existing setters (wind → Douglas sea with growth over time; wind direction), labelled open-ocean; sensors read `MetocReport` in their own streams | Same | Locked |
| E37 | Where decisions live | **This blueprint and `ROADMAP.md`, numbered E26 onward;** the Theatre blueprint logs the E20 reversal with a pointer | Same | Locked |
| E38 | The current sky files | **SIX's uncommitted sky and settings files committed as the baseline** on `feat/metoc` (`08580940`): the HDRP default volume profile and global settings, `Dogfight.unity`, Graphics/Quality/Project settings | Leave them uncommitted; draw through a runtime overlay | Locked (not the leaning) |
| E39 | The sun's brightness | **About 128 000 lux above the air** (solar constant ≈ 1 361 W/m² × ≈ 93 lm/W, from memory; cited in the SO); HDRP's Physically Based Sky dims and reddens it per sun height (verified: `EvaluateSunColorAttenuation` in `LightEvaluation.hlsl`). The moon from the public full-moon figure × phase. SIX's 190 000 lux kept as a labelled look multiplier (≈1.48×) | Same | Locked |
| E40 | Exposure at dusk and night | **Auto-exposure's floor follows the reckoned light level;** SIX's EV 14 ceiling stays | Same | Locked |
| E41 | The light level | **Reckoned:** `MetocReport` carries ground illuminance (sun + moon, clear sky; clouds from slice 2) from public formulas; exposure is driven from it; HDRP's rendered brightness never feeds back | Same | Locked |
| E42 | How METOC draws | **A runtime overlay volume:** its own global volume, priority above the shared sky, with a profile created at run time holding only what METOC drives. No sky asset is written at run time (in play mode, edits to a profile asset persist to disk) | — | Proposed |
| E43 | Sun and moon lights | **Two directional lights:** the scene's Sun and a Moon light METOC adds. The brighter body casts shadows; the other does not | — | Proposed |
| E44 | Time standard | **Universal Time in double seconds from J2000.0** (2000-01-01 12:00 UT). TT − UT (≈69 s) and leap seconds ignored: at most ≈0.003° of sun position. Labelled | — | Proposed |
| E45 | The illuminance model | **Published pieces, constants in `IlluminationProfileSO` with provenance:** IES clear-sky daylight (direct 127.5 klux · e^(−c·m), c = 0.21, plus diffuse 0.8 + 15.5·√sin h klux) above the horizon; a twilight table interpolated in log lux below it; Krisciunas & Schaefer (1991) moonlight with V-band extinction; a starlight-and-airglow floor. Air mass from Kasten & Young (1989). The from-memory values are labelled until cited | — | Proposed |
| E46 | How often the drawn sun moves | **Open, measured in slice 1:** HDRP re-renders the sky to a cubemap when a body moves. At 60× day speed, moving on every visible change (`redrawQuantumDeg` 0, about every 0.02°) cost 0.43–0.44 ms GPU per frame; a 0.25° quantum cost 0.16–0.17 ms (ROADMAP, slice 1). At 1× both are negligible: about once every 5 s against once a minute | **0** (smooth: a 0.25° quantum makes the sun and its shadows jump half the sun's width); 0.25° for fast-time demos | Open (M2) |
| E47 | METOC's Watch | **Every fixed tick (50 Hz)** while its cost is measured negligible against E4's 0.5 ms CPU; a slower Watch only with data (no abstraction without data) | — | Proposed |

Inherited: Theatre E2 (frame), E4 (budget: terrain + sky ≤ 2 ms GPU, ≤ 0.5 ms CPU), E13 (the full atmosphere pack), E18 (METOC is its own module), E19 (names), E24 (sensors bend their own rays); Reckoning R15 (Watches), R20 (labelled assists), R22 (fog of war).

## 6. Standing constraints

1. **Everything METOC decides is reckoned:** plain C# on the fixed tick, seeded, deterministic. The core never uses `UnityEngine.Time`, `UnityEngine.Random`, `DateTime.Now`, `Object.Find*` or an unseeded `System.Random` (Reckoning constraint 1). *Why:* darkness and weather decide outcomes. A banned-API test enforces it.
2. **HDRP only draws.** Nothing reads back from HDRP's sky, clouds, fog, water or exposure to decide anything (Reckoning constraints 2–3; Sea State AP11). *Why:* GPU results are not bit-identical.
3. **No sky asset is written at run time** (E42). *Why:* a play session must never change SIX's sky on disk.
4. **Tuning lives in ScriptableObjects with provenance;** published figures are cited, from-memory figures are labelled until cited (P1, C9). Published algorithm coefficients live in code, next to their citation.
5. **E4's budget is measured, never assumed:** `unity-perf-ab`, with the resolution and the instrument stated.
6. **Safe limits** (General Quarters BLUEPRINT §6, constraint 8) apply: a game simulation from public data.
7. **Vendor code is read-only** (P5): HDRP, VFX Graph, App UI.
8. **Every change to METOC's state is a logged order.** *Why:* replays and the Board of Inquiry need the cause (Reckoning constraint 4).

## 7. What is deliberately not here

| Not doing | Why, and the revisit trigger |
|---|---|
| Wet surfaces | E32. Needs a wetness input on ship, deck and aircraft materials, some vendor. Revisit after rain ships |
| Sensors reading METOC | E36. Each sensor's own stream wires it in |
| Fronts, wind fields, gusts | L1–L2 |
| Lightning, snow, hail, sand | Not in L0. Revisit with a scenario that needs them |
| High-precision ephemerides (VSOP87, full ELP) | E28's accuracy is far below what a game sky can show |
| Stars and planets as reckoned objects | HDRP's night sky draws them as a look only |
| Delta-T and leap seconds | E44 |

## 8. Risks

| Risk | Mitigation |
|---|---|
| **The current sky already uses ≈1.75 ms of E4's 2 ms** (edit-mode baseline, 2560×1440, before volumetric fog), and terrain has not been added. **In the METOC demo's low view over the sea it is ≈3.2 ms in play** (volumetric clouds 2.2–2.4 ms), over the budget before METOC adds anything | E31: the cloud slice A/Bs the step counts; SIX picks from the numbers |
| **A moving sun may re-render the sky every frame** | Measured in slice 1 (E46): ≤0.44 ms GPU per frame at 60×, negligible at 1× |
| **HDRP's exposure limits are tuned for daylight** | E40: the floor follows the reckoned light. Seen at noon, low sun, dusk and night (2026-10-30): the picture never goes black, but the night reads bright (M3) |
| **One Unity Editor, several sessions** | Check the Editor's state before play, compile or reload; ask SIX when another session is using it |
| **From-memory constants** (the twilight table, the solar luminous efficacy, the starlight floor) | Labelled in the SO; cited before any sensor relies on them |

## 9. Open questions for SIX

| # | Question | Cost |
|---|---|---|
| M1 | Lock E42–E45 and E47 (Claude's proposals)? | 2 min |
| M2 | E46: how often the drawn sun moves. Leaning 0 (smooth), from slice 1's numbers | 2 min |
| M3 | The night's look: keep it as bright as now (`adaptationBiasEv` 2: the moonlit sea reads like a hazy day), or darker (bias 4–5, as a night photo is underexposed)? A look value in `HdrpMetocLook.asset`; changing it is a re-tune, not a rebuild | 5 min, with screenshots |

## 10. Glossary

- **Okta:** an eighth of the sky covered by cloud; 0 is clear, 8 overcast (METAR).
- **Ceiling:** the height of the lowest cloud layer covering at least five oktas (broken or overcast).
- **UT, TT:** Universal Time follows the Earth's rotation; Terrestrial Time is the uniform time of ephemerides. They differ by about 69 s today.
- **J2000.0:** the reference instant 2000-01-01 12:00 TT, Julian Date 2 451 545.0.
- **Air mass:** how much atmosphere light crosses, relative to straight up.
- **EV100:** exposure value at ISO 100. One step halves or doubles the light.
- **Lux:** light falling on a surface. Noon sun ≈ 100 000 lux, full moon ≈ 0.25 lux, a clear moonless night ≈ 0.002 lux.

## Change log
- **2026-10-30** — The interview: E26–E41 picked by SIX in four rounds (E35 and E38 differ from Claude's leaning). E26 reverses Theatre E20's order. E42–E47 proposed. Edit-mode baseline of the current sky measured. Blueprint written; SIX's baseline files committed (`08580940`).
- **2026-10-30** — Slice 1 built: the clock, almanac, illumination, simulation, director, HDRP renderer, demo and App UI panel; 37/37 EditMode tests. Seen in play at noon, low sun, dusk and night, and the screenshots were sent to SIX. E46 measured and left Open for SIX (M2), leaning 0. The existing sky measured ≈3.2 ms GPU in the demo's sea-level view (risk updated). M3 added: the night's look.
