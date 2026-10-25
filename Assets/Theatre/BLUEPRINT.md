# Theatre — blueprint (with METOC and Gravity)

> **Status: LOCKED BLUEPRINT (SIX, 2026-10-25). NOTHING BUILT YET.** SIX answered all 20 questions on 2026-10-24 (E1–E20) and locked them on 2026-10-25, accepting Claude's proposals E21–E25. The first theatre is a strait; slice 1 is a spike, run in the project on its own branch. The only measured facts so far are from the read-only stock-take (`environment-maps-stocktake.md` in the side-session notes) and the HDRP 17.4 source.
> **This document's one job:** say what the environment modules are and why, and record every decision behind them. Work order and progress live in `ROADMAP.md`, not here.

## 1. In plain language

**The ask.** SIX, 2026-10-24:

> "I want to work on environmental stuff, like map generation, in the Deep Water Unity project."

"Map generation" usually means making content. What Deep Water needs is a **Theatre**: one bounded patch of world (land, seabed, coast and sky) that the simulation queries and HDRP draws, from one source. The Reckoning rules force this. Line of sight, grounding and waterline must ask the simulation's own geometry, never Unity raycasts, so the terrain has to be data the core owns, not just something drawn.

Three new modules and one changed one:
- **Theatre:** a curved 64 km box of land and seabed. Fictional theatres are baked from a theatre type plus a seed; real landforms come second, from open data you download. It carries depth, ground classes, the open-water map and the coast's effect on the waves.
- **METOC:** the atmosphere. A wind field, fronts, gusts, visibility, cloud and rain. Wind over open water grows the sea.
- **Gravity:** a tiny shared core that says which way is down and how far the surface drops with distance. Every module that floats or flies uses it.
- **Sea State** (existing) gains a depth input, the open-water map, the coast wave maps and a curved mean sea level, in its CPU kernel and its GPU twin together.

**The test.** A destroyer runs along a fictional coast in a rising wind:
- the radar loses its contact behind a headland, computed on the same heights the player sees;
- the waves are smaller in the island's lee, bend round the point and break on the shoal;
- she squats and turns wider as the seabed rises under her;
- if she touches rock, the hull is breached; on mud she sticks.

Nobody authors any of it, and Inquiry can say why, with numbers.

**Honest limits.**
- The world is curved but HDRP's water is one flat plane. Drawing a curved sea is unproven until the slice-1 spike (E9).
- Beyond about 500 m from the camera, HDRP draws its own waves: they can be damped near the coast, but they will not bend or follow fetch.
- Free real seabed data outside the US is about 450 m per sample (GEBCO, from memory), so real seabeds are smooth.

## 2. Vocabulary

Words from the military-planning family (E19). Code names are proposals until the first slice builds them.

| Name | What it is | Code (proposed) |
|---|---|---|
| **Theatre** | The module, and one baked 64 km box of land and seabed | `DeepWater.Theatre`, `Assets/Theatre/`, `TheatreSO` |
| **Theatre type** | A template: strait, archipelago, fjord, bay, open coast, with a few dials and a seed | `TheatreTypeSO` |
| **Overlay** | A hand-edit layer (height strokes, paint, placed features), replayed after every regeneration | `TheatreOverlay` |
| **Height field** | One signed grid: positive is land, negative is seabed depth below mean sea level | `HeightField` |
| **Ground** | Material class per cell, on land and seabed: mud, sand, gravel, rock (E23). The root of "aground" | `GroundClass` |
| **Going** | Reserved: the derived movement rating for land vehicles (ground + slope + weather), when ground units come (E23) | — |
| **Exposure** | Per wind direction, how much open water lies upwind of each point. Feeds lee shelter (E6) and fetch (E13) | `ExposureMap` |
| **Coast wave maps** | Per-wave amplitude and phase over the coastal band (coast rungs 3–4) | `CoastWaveMaps` |
| **Far ring** | Look-only land and sea out to about 200 km around the box (E22) | — |
| **Gravity** | The shared core: down at a point, the surface's drop with distance | `DeepWater.Gravity` |
| **Flat-earth** | The declared level of a module that has not yet adopted Gravity (E10) | — |
| **METOC** | The atmosphere module (meteorological and oceanographic) | `DeepWater.Metoc`, `Assets/METOC/` |
| **METOC report** | The weather at a place and time: wind, visibility, cloud, ceiling, rain, sun | `MetocReport` |

## 3. Architecture

### 3.1 One source, many readers

```
TheatreTypeSO + seed + Overlays
        │  bake in the Editor (CPU, deterministic, E11)
        ▼
TheatreSO: height field · ground · exposure · coast wave maps · far ring · provenance · hash
        ├─► HDRP Terrain (draw: 4–6 rule layers + sparse vegetation, E14)              unreckoned
        ├─► PhysX TerrainCollider (aircraft and later vehicle contacts)
        └─► TheatreQuery (pure C#): height, depth, ground, line of sight with curvature, grounding

Gravity (pure C#): Down(p), Drop(p) ◄── Theatre, Sea State, General Quarters, Guided Fury, aircraft (E10)

METOC (pure C#, on its Watch) ──► Sea State: the sea grows from wind × exposure × duration (E13)
                               ├─► HDRP sky, clouds, fog (draw)                         unreckoned
                               └─► sensors: visibility, ceiling

Sea State + depth (coast rung 1) + exposure (rung 2, fetch) + coast wave maps (rungs 3–4) + curved mean level
          CPU kernel ⇄ GPU twin, changed together (Sea State README; METHODOLOGY C8)
```

### 3.2 Frame and precision (E2, E7)
- **One fixed origin at the box centre.** Unity X is east, Y is up *at the centre*, Z is north. The box is ±32 km. There is no origin rebasing.
- **Precision.** Reckoned positions are double. Float appears only at the presentation boundary, where its step is at most 3.9 mm anywhere in the box (2¹⁵ · 2⁻²³ m).
- **Curvature is real geometry.** The mean sea surface is a sphere of radius R about a point below the centre. Land heights are baked on the curve.

  | From the centre | 1 km | 5 km | 10 km | 20 km | 32 km |
  |---|---|---|---|---|---|
  | Drop d²/2R (R = 6 371 km) | 0.08 m | 2.0 m | 7.8 m | 31 m | 80 m |
  | Tilt of "down" | 0.009° | 0.045° | 0.09° | 0.18° | 0.29° |

- **R per theatre** (E25): fictional theatres use the mean Earth radius, 6 371 km (AUTHORED simplification). Real landforms use Cartographer's exact WGS84 ⇄ ECEF ⇄ ENU converter, which is curved by construction.
- **Sensors bend their own rays** (E24). Geometry is curved at the true radius. Each sensor's profile carries its own effective-radius factor: radar 4/3, as General Quarters' radar already uses analytically; optical about 7/6 (from memory; cite before use).

### 3.3 Determinism (E11)
- The bake runs on the CPU, seeded, in double precision or Burst `FloatMode.Deterministic`. The baked asset is the truth.
- The asset's FNV-1a hash goes into the Fix, so a run in a different theatre can never be mistaken for the same run.
- Queries are pure functions of the asset.
- Nothing that changes an outcome runs on the GPU (Reckoning standing constraint 2). Line of sight, grounding and depth never use Unity raycasts or HDRP's water queries (Reckoning standing constraint 3; Sea State AP11).

### 3.4 The coast and the sea (E6, E12)
The coast ladder is in §4. What it means for Sea State:
- **Rung 1:** a depth texture read by both `SeaFieldPacked`/`SeaFieldMath` and `SeaWaves.hlsl`. Each wave is damped by its wavelength against the depth, and capped at H ≤ 0.78·d. Surf foam is deposited through the existing `DepositFoam`.
- **Rung 2:** each wave is damped by the exposure along its own direction (the lee).
- **Rungs 3–4:** coast wave maps carry each wave's amplitude (shoaling) and phase (refraction) over the coastal band. They are baked on the CPU and sampled by both evaluators.
- **The far field.** HDRP's own waves beyond the near field are damped near coasts through HDRP's water mask (amplitude only). They do not refract; that gap is labelled.
- **Ships and the seabed (E12).** Depth under the keel reaches the helm and the AI. Grounding is a logged event that General Quarters' damage control consumes; its ground class decides the outcome (rock breaches; sand and mud hold, E15). Shallow-water squat and turning are a General Quarters engine-room rung, calibrated to public figures (C16). Theatre supplies the depth query.

### 3.5 The atmosphere (E13)
**Reckoning R21 is superseded:** the atmosphere comes now, not later.
- The sea becomes an output: wind over fetch (from exposure) and duration grows it, and what is left becomes swell.
- The Douglas dial becomes the god role's labelled override.
- Everything METOC decides is reckoned (CPU, on its Watch, seeded gusts). HDRP's sky, clouds and fog only draw it.
- Its ladder is in §4. It must not block coastal ship operations.
- METOC's own detailed questions (front shapes, gust model parameters, the visibility-to-fog mapping checked in HDRP's source) are asked when its first slice starts, and recorded here as E-numbers.

### 3.6 Real landforms (E1, E17)
- **Second source, after fictional theatres.**
- **Route:** copy Cartographer's coordinate core (converter, region box, tile keys, its 52 verified tests) into `Theatre/Core`, re-laid to the P3 layout. A small offline script (GDAL, open source) converts files you download into plain height files, and Unity imports those. There is no network code.
- **Sources (from memory; verify each licence before the slice):** Copernicus GLO-30 for land, GEBCO for the seabed, NOAA coastal DEMs where available, OSM land polygons for the coastline (ODbL).
- Every source's licence and attribution is recorded in the asset and shown.
- **Limits** (standing constraint 1): real elevation and seabed only.

### 3.7 Gravity across modules (E10, E21)
- Gravity arrives with this work, as a pure-C# assembly with no dependency on Theatre.
- Each other module adopts it as a rung on its own ladder, when it is next worked on.
- Until a module adopts it, the module declares itself **flat-earth** out loud, and its golden fingerprints stay pinned at the flat level (C29).
- **Why a theatre can't mix the two (E21):** a flat-earth body on the curved sea is pushed sideways. At 10 km the mean sea tilts 0.09°, which pushes a 9 000 t destroyer with about 140 kN; beyond about 3 km it also floats visibly off the drawn sea.
- **Adoption order:**
  1. Sea State (this work);
  2. General Quarters hull and buoyancy (needed for coastal ship ops);
  3. General Quarters gun firing tables, which are flat-earth today: at 20 km the target sits about 31 m below the gun's horizontal;
  4. Guided Fury;
  5. the aircraft module.

## 4. The fidelity ladders

| Element | L0 | L1 | L2 | L3 |
|---|---|---|---|---|
| **Theatre source** | Theatre types + seed (E8) | Real landforms from downloaded files (E17) | A hand-painted land/sea sketch as generator input | — |
| **Look** | 4–6 rule layers (height, slope, shore distance, wetness) + sparse seeded trees and scrub (E14) | — | — | — |
| **Coast (Sea State)** | Depth damping + breaking cap + surf foam | Lee shelter (exposure) | Shoaling (amplitude maps) | Refraction (phase maps) |
| **Seabed and ships** | Depth under the keel; grounding decided by ground class | Squat and shallow-water turning (General Quarters engine room) | — | — |
| **METOC** | Uniform wind + METOC report (visibility, cloud, ceiling, time and sun, rain); the sea grown from wind × exposure × duration | Fronts on a seeded timeline, every change logged | Coarse wind field (km cells, ~1 Hz) + seeded Dryden / von Kármán gusts | Temperature and humidity effects (e.g. the evaporation duct on radar) |
| **Gravity adoption** | Theatre, Sea State | General Quarters hull and buoyancy | General Quarters guns; Guided Fury | Aircraft |
| **Far ring** | Look-only coarse heights to ~200 km (E22) | — | — | — |

Each implemented level ships with EditMode tests. A request above the highest built level falls back out loud (C14).

## 5. Decision register

E1–E20 are SIX's picks of 2026-10-24, in round order, locked 2026-10-25. Leaning is Claude's recommendation before the pick. **Flagged** picks carry their mitigation. E21–E25 were Claude's proposals; SIX accepted them on 2026-10-25.

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| E1 | Where maps come from | **Both kinds behind one model: fictional first, real landforms second.** Real landforms bring real elevation and seabed only: no real roads, buildings, ports or bases, and weapons never aim at a real named place | Same | Locked |
| E2 | Size and precision | **A 64 km box (±32 km), one fixed origin.** Double in the reckoned core, float only at the presentation boundary (≤ 3.9 mm). A coarse no-physics ring to the horizon. No rebasing | Same | Locked |
| E3 | First purposes | **Ship operations near coasts, then low-level flight** | Same | Locked |
| E4 | Performance budget | **Balanced:** terrain + sky ≤ 2 ms GPU, ≤ 0.5 ms CPU, ≤ 512 MB; theatre queries ≤ 0.05 ms per fixed tick. Editor numbers at a stated resolution. The first build slice opens with a measured baseline (`unity-perf-ab`) | Same | Locked |
| E5 | Height spacing | **16 m everywhere:** one 4 097² grid covers the box, about 135 MB with Unity's, PhysX's and the core's copies (8 m would be about 540 MB) | Same | Locked |
| E6 | How the sea meets the coast | **The whole ladder:** depth damping + breaking cap → lee shelter → shoaling → refraction. **Flagged:** coast wave maps cost memory (about 400 MB for a 2 km band along 200 km of coast at 16 m, illustrative), rays can cross, and HDRP's far field cannot refract. *Mitigation:* built rung by rung; the slice-1 spike (E9) measures the maps; fallback: ship up to shoaling and park refraction with the spike's numbers | Depth damping + breaking first, shelter next | Locked, flagged |
| E7 | Earth curvature | **Truly curved geometry** about the box centre. **Flagged:** HDRP's water is one flat plane; gravity must point at the centre (E10). *Mitigation:* the slice-1 spike tests drawing a curved sea; fallback: the simulation stays fully curved and the drawn sea is a flat plane kept level under the camera. Low coasts beyond ~10 km then sink too early; the drawing gap is labelled | Curvature in reckoned maths only, visuals flat | Locked, flagged |
| E8 | How fictional theatres are made | **Theatre types as ScriptableObjects** (strait, archipelago, fjord, bay, open coast) with a few dials and a seed; the same type and seed give the same theatre, bit for bit | Same | Locked |
| E9 | De-risking E6 and E7 | **Lock now, spike first in slice 1.** The plan bends to the spike's result | Same | Locked |
| E10 | Gravity across modules | **Module by module:** each module adopts the shared Gravity as a rung on its own ladder when next worked on. Until then it declares itself flat-earth, and its golden fingerprints stay at the flat level (C29) | Same | Locked |
| E11 | Bake or generate at load | **Bake in the Editor;** the asset is the truth and its hash enters the Fix | Same (generate-at-load later for the sandbox) | Locked |
| E12 | What the seabed does to ships | **Grounding** (a logged event for General Quarters' damage control), **depth under the keel** for helm and AI, and **shallow-water handling** (squat, wider turning below ~3× draft) as a General Quarters engine-room rung | Grounding + sounder now, handling later | Locked |
| E13 | Sky, weather and fog | **The full atmosphere pack now** (Reckoning §4.11): wind field, fronts, seeded gusts, visibility, cloud, rain; the sea grown by wind over fetch and duration; the Douglas dial becomes a labelled override. **Supersedes Reckoning R21** ("later") | Conditions record as simulation inputs; atmosphere later per R21 | Locked; R21 superseded |
| E14 | The land's look | **Rule-based:** 4–6 layers by height, slope, shore distance and wetness, plus sparse seeded trees and scrub | Same | Locked |
| E15 | Ground material | **Classes (mud, sand, gravel, rock) on seabed and land.** Seabed class decides grounding; land classes wait for ground vehicles | Seabed only, deciding grounding | Locked |
| E16 | Hand edits | **Overlays:** separate layers replayed after every regeneration, included in the bake and its hash | Same | Locked |
| E17 | Real-landform route | **Copy Cartographer's coordinate core and import downloaded files** converted offline with GDAL. No network code | Same | Locked |
| E18 | Module boundaries | **Theatre** (land, seabed, coast coupling), **METOC** (atmosphere), **Gravity** (shared core); Sea State stays its own | Same | Locked |
| E19 | Names | **The military-planning family:** Theatre, Theatre type, Overlay, Exposure, Gravity, METOC, METOC report | Charting family | Locked |
| E20 | Slice order | **Theatre first:** spike → baked fictional theatre → coast rung 1 + grounding on demo hulls → General Quarters adopts Gravity → METOC L0 → coast rungs 2–4 | Same | Locked |
| E21 | Mixing curved and flat modules | **A theatre admits only modules that use Gravity;** flat-earth modules keep their own flat scenes until they adopt | — | Locked (accepted 2026-10-25) |
| E22 | The far ring | **Look only, to ~200 km.** Sensors never need it: a straight line between two points in the box stays in the box. accepted by SIX (2026-10-25) | — | Locked (accepted 2026-10-25) |
| E23 | The class word | **Ground** for the class map (true on land and seabed; "Material" collides with Unity's `Material`). **Going** reserved for the derived land-movement rating. Family 2's "Going" overclaimed for the seabed | — | Locked (accepted 2026-10-25) |
| E24 | Where sensor curvature lives | **In each sensor's profile:** an effective-radius factor (radar 4/3, optical ~7/6), not in Gravity | — | Locked (accepted 2026-10-25) |
| E25 | Earth model | **One sphere per theatre:** R = 6 371 km for fictional theatres (AUTHORED); real landforms use the exact WGS84 converter | — | Locked (accepted 2026-10-25) |

Inherited from Reckoning: R2 (everything deterministic eventually), R5 (a few ships in ~20 km: the play area inside the 64 km box), R14 (Windows, same build), R15 (Watches). **R21 is superseded by E13.**

## 6. Standing constraints

1. **Safe limits** (General Quarters BLUEPRINT §6, constraint 8) apply in full. For theatres:
   - real landforms carry real elevation and seabed only: no real roads, buildings, ports, bases or other infrastructure;
   - no weapon is aimed at a real named place, and no theatre is framed as a real military site (the Dahlgren anchor in `Mapping.unity` is not used).
   - *Why:* no real-world targeting.
2. **Theatre queries are pure functions of the baked asset.** Line of sight, grounding and depth never use Unity raycasts or HDRP water queries. *Why:* determinism (Reckoning standing constraint 3; AP11).
3. **Nothing that changes an outcome runs on the GPU.** GPU twins are verified against the CPU (C8).
4. **Sea State's CPU kernel and GPU twin change together.** *Why:* the ship must ride the wave you see.
5. **Tuning lives in ScriptableObjects with provenance; authored numbers say so** (P1, C9).
6. **Data stays local.**
   - No network code, no paid providers, no API keys.
   - Every source's licence is recorded and shown.
   - No Google, Cesium ion or ArcGIS content in any baked asset; their terms restrict caching and baking (from memory; verify).
7. **Vendor code is read-only** (P5): HDRP, Cesium, ArcGIS.
8. **One "down" per theatre** (E21). Every module in a theatre uses Gravity; flat-earth modules keep their own flat scenes until they adopt it.
9. **Overlays always win.** A regeneration never discards a hand edit.
10. **Budgets are measured, never assumed:** `unity-perf-ab`, Editor resolution stated (E4).

## 7. What is deliberately not here

| Not doing | Why, and the revisit trigger |
|---|---|
| Real infrastructure (OSM roads, buildings, ports, bases) | Safe limits, E1. Not planned |
| Cesium, Google 3D Tiles or ArcGIS as simulation truth | They stream by camera view (not reproducible), need the network and accounts, and their terms limit baking. At most an optional flight backdrop, never truth |
| Origin rebasing, theatres over 64 km | E2. Revisit if a campaign needs more than one box |
| Ground units' 1–2 m terrain, roads, Going | E3. When ground units are scheduled |
| Tides | E12 stopped short of tides. Revisit with a harbour scenario |
| Beach run-up and swash | The coast ladder ends at refraction |
| Generating theatres at load | E11. Revisit for the sandbox's "new theatre" button |
| Downloading data over the network | E17 |
| Cross-platform determinism | R14 |

## 8. Risks

| Risk | Mitigation |
|---|---|
| **HDRP water is one flat plane** (E7) | Slice-1 spike: HDRP's custom-mesh water mode (verified in the 17.4 source: `WaterGeometryType.Custom` takes user mesh renderers in world space) on a camera-following curved mesh. Fallback in E7 |
| **Coast wave maps against the memory budget** (E6) | Spike measures them. Only waves that feel the bottom get maps; long waves use coarser maps. Fallback in E6 |
| **Crossing rays in refraction** | Spike criterion: bounded artefacts after smoothing |
| **HDRP's far field cannot refract or follow fetch** | The near field (~1 km) is phase-locked; the far field is amplitude-damped through HDRP's water mask; labelled gap |
| **General Quarters must adopt Gravity before coastal ship ops** (E10, E21) | Slice 4, planned with the General Quarters session |
| **The atmosphere's scope** (E13) | Its own ladder; L0 first; never blocks coastal ship ops |
| **One Unity Editor, several sessions** | Build slices start only when SIX says the Editor is free |
| **Terrain memory at 4 097² plus the PhysX copy** | Measured in slice 2 against E4 |
| **Licence claims are from memory** | Each verified before the real-landform slice and recorded in the asset |

## 9. Open questions for SIX

| # | Question | Cost |
|---|---|---|
| ~~Q1~~ | ~~Lock the blueprint?~~ **Answered 2026-10-25: locked.** | — |
| ~~Q2~~ | ~~E21–E25: accept?~~ **Answered 2026-10-25: accepted.** | — |
| ~~Q3~~ | ~~The first theatre type?~~ **Answered 2026-10-25: a strait** (two coasts, a shoal, a headland). | — |
| Q4 | Revoke the Cesium ion token and the ArcGIS API key that are public on `origin/main` (found in the stock-take; not part of this module) | 10 min in two dashboards |
| ~~Q5~~ | ~~Where the slice-1 spike runs?~~ **Answered 2026-10-25: in the project, on its own branch** (`feat/theatre-spike`, cut from `feat/environment-maps`). | — |

## Change log
- **2026-10-24** — Stock-take (read-only), then the 20-question interview: E1–E20 picked by SIX in five rounds. E6, E7, E12, E13, E15 and E19 differ from Claude's leaning. E13 supersedes Reckoning R21. E21–E25 proposed. Blueprint written, awaiting lock.
- **2026-10-25** — Locked by SIX: E1–E20 Locked, E21–E25 accepted, the first theatre is a strait (Q3), the spike runs in the project on its own branch (Q5). Q4 (revoking the public Cesium and ArcGIS keys) stays open.
