# METOC — Roadmap

**This document's one job:** what to do next, and what is done. Why each piece exists is in `BLUEPRINT.md` (E26–E57).

**Status: BLUEPRINT LOCKED (2026-10-30). Slices 1, 2 and 3 built and seen in play (2026-10-30); slices 1 and 2 on `main` (`368504b6`), slice 3 on `feat/metoc`. Slice 4 (the wind grows the sea) next. M4, M5 and M6 wait on SIX.**

**Rules (same as Theatre and General Quarters):**
- Slices MAY be reordered for dependency.
- Unchecked items MUST NOT be deleted; mark them `~~skipped~~` with the reason.
- Completed items get a date and their commit.
- Every slice ships with EditMode tests, including the core's banned-API scan.
- Every performance claim carries its A/B number (`unity-perf-ab`), with the resolution and the instrument stated.
- Nothing at run time writes a sky asset (E42). The Editor is shared: check its state before play, compile or reload.

Branch: `feat/metoc`, cut from `main` (2026-10-30). Commits go in through `commit-to-branch`. Never pushed: the archive branch, DAVFX, the MCP unlock script, `*.csproj`, `*.slnx`.

---

## Baseline (E4) — the current sky, before METOC

**Measured 2026-10-30.** Editor, edit mode, the Game view repainted 300 times after 30 warm-up frames. Game view 2560×1440, quality High Fidelity, RTX 3070 Ti Laptop GPU. Scene: Dogfight Training's spectator camera at 1 837 m, looking north, inside the cloud deck. GPU times come from `ProfilerRecorder` with `GpuRecorder` on HDRP's own samplers.

| Pass (GPU) | ms / frame |
|---|---|
| Volumetric clouds: trace 0.62, upscale 0.19, combine 0.15, reproject 0.10, shadow 0.09, ambient probe 0.02 | ≈1.1 (parent sampler 0.91) |
| Physically Based Sky (`RenderSky`) | 0.33 |
| Cloud layer (`RenderClouds`) | 0.16 |
| Fog on opaque surfaces (`OpaqueAtmosphericScattering`) | 0.13 |
| Sky ambient probe | 0.02 |
| **Sky total, before volumetric fog** | **≈1.75** of E4's 2 ms |
| Volumetric fog (`VolumetricLighting`, `VolumeVoxelization`) | not captured: zero samples. Likely async compute (unverified) |
| Water, for scale (not in E4's sky) | 0.81 |

The sky's own `PreRenderSky`, `RenderSkyToCubemap` and `UpdateSkyEnvironmentConvolution` did not run: the sky was static. A moving sun may start them (E46).

---

## Slice 1 — Time of day, sun, moon, light (E27)

- [x] The interview: E26–E41 picked by SIX, E42–E47 proposed (2026-10-30, `BLUEPRINT.md`).
- [x] SIX's sky and settings files committed as the baseline (E38, `08580940`).
- [x] Edit-mode baseline of the current sky (above).
- [x] Folders per P3; asmdefs `DeepWater.Metoc` (pure core + SOs + director, references nothing), `DeepWater.Metoc.Hdrp`, `DeepWater.Metoc.Examples`, `DeepWater.Metoc.Editor`, `DeepWater.Metoc.Tests.Editor`; the core's banned-API scan, which also bans `UnityEngine` and `Mathf` in `Core/` (2026-10-30).
- [x] `MetocClock`: UT seconds from J2000 (E44), advanced per fixed tick × day speed (E29); calendar conversion both ways. It counts steps from an anchor instead of adding each step: added up at 2026's UT, 500 steps of 1.2 s (20 ms at 60×) lost 24 µs (C62). A game day of 50 Hz steps now ends within 2 ulp of the exact time (2026-10-30).
- [x] `Almanac`: the sun from NOAA's equations (E28), with refraction; the moon from the low-precision formulas, with its phase, distance and disc size. Tests against Meeus 7.a, 7.c, 12.a, 13.b, 25.a, 28.b, 47.a and 48.a, plus noon at both solstices and the equinox, and polar night and midnight sun (2026-10-30).
- [x] `Illumination`: the clear-sky model of E45, constants in `IlluminationProfileSO` with provenance; tests at the zenith, the horizon (continuous at ±1e-10°), through twilight, at full and quarter moon and on a moonless night (2026-10-30).
- [x] `MetocReport` (with a fingerprint), `MetocOrder` and its log, `MetocSimulation`. Two runs of 3 600 ticks are bit-identical; replaying the order log reproduces every report; a bad order is refused with its reason in the log, and a bad scenario is refused (2026-10-30).
- [x] `MetocDirector` (FixedUpdate, marker `METOC.Step`) and `HdrpMetocRenderer` (LateUpdate, marker `METOC.Draw`): the Sun, a Moon light that reflects the sun, the moon's disc size from its distance, shadows from the brighter body only, and the exposure floor through a runtime overlay volume (E40, E42, E43) (2026-10-30).
- [x] Demo: **Deep Water → METOC → Build Demo Scene** and **Play the Demo** copy the Sea State demo (E34). Keys: T / Shift+T ±1 h, = and − double or halve the day speed, P pauses. The App UI panel shows the report and has sliders for date, time, latitude, longitude and day speed (E35); every one issues a logged order (2026-10-30). *Changed:* the [ ] keys of the plan went to the Sea State panel's wind; day speed uses = and −.
- [x] Measured: 2.4–2.5 µs per tick (EditMode, Mono, 100 000 ticks); in play, `METOC.Step` 0.015 ms and `METOC.Draw` 0.018 ms CPU per frame. The E46 A/B is in the table below (2026-10-30).
- [x] *Done when:* tests green (37/37, Test Runner, 2026-10-30); the demo seen at noon, sunset, dusk and night in play mode, with screenshots (sent to SIX 2026-10-30); METHODOLOGY C62–C66; committed and pushed on `feat/metoc`.

### Slice 1 — measured in play (2026-10-30)

Editor, play mode, Game view 2560×1440, quality High Fidelity, RTX 3070 Ti Laptop GPU. The METOC demo at 30° N, 40° W on 2026-10-30. No other Unity Editor or trainer was running. GPU times come from `ProfilerRecorder` with `GpuRecorder` on HDRP's samplers, averaged over 300 frames after 60 warm-up frames.

| Shot | UT | Body | Ground light | Exposure floor | Shadows from |
|---|---|---|---|---|---|
| Noon | 14:23 | sun 46.1° at 179.8° | 83.6 klux | EV 10.02 (SIX's day floor) | sun |
| Low sun | 19:42 | sun 2.1° | 3 830 lux | EV 10.02 | sun |
| Civil dusk | 20:19:30 | sun −6.0° | 3.33 lux | EV 2.41 | none |
| Night | 02:24 (31st) | moon 27.7° at 74.6°, 70 % lit, waning | 0.023 lux | EV −4.74 | moon |

The camera's volume stack held the same `limitMin` as the floor in every shot, so the overlay volume wins over the shared sky.

**E46: how often the drawn sun moves.** Day speed 60×, 300 frames per run, runs in ABAB order.

| Run | Quantum | Sun moves | Sky to cubemap + convolution (GPU, ms per frame) | Frame (ms) |
|---|---|---|---|---|
| A1 | 0 (every visible move) | 57 | 0.212 + 0.229 = 0.44 | 15.30 |
| B1 | 0.25° | 5 | 0.074 + 0.081 = 0.16 | 14.98 |
| A2 | 0 | 56 | 0.204 + 0.221 = 0.43 | 15.17 |
| B2 | 0.25° | 5 | 0.082 + 0.088 = 0.17 | 16.73 |

- The quantum saves ≈0.27 ms GPU per frame at 60×. The frame times don't show it: B2 was slower overall, its volumetric clouds 2.42 ms against 2.21–2.24.
- "Every visible move" is about every 0.02°, not every frame: `Vector3.Angle` reads 0 below that (float `acos` near 1). At 60× the sun moves 0.004° a frame, so it moved about every fifth frame.
- At 1× the sun moves 0.25° a minute: with quantum 0 the sky re-renders about every 5 s, and with 0.25° once a minute. Both are negligible.
- The cubemap passes ran on 25 frames in B with 5 sun moves; the moon's moves account for some of the rest. The other triggers are not traced.

**The existing sky in this view.** Volumetric clouds 2.21–2.42 ms, Physically Based Sky 0.49–0.51, the cloud layer 0.26–0.31, fog on opaque 0.17–0.18 ms. That is ≈3.2 ms GPU from the sky METOC drives, before METOC's own passes. It is over E4's 2 ms in this low view over the sea, against ≈1.75 ms in Dogfight Training's view inside the cloud deck (the baseline above). E31's step-count A/B in slice 2 is where it gets cut.

**Seen by eye.** Noon (looking into the sun) is hazy and bright. The low sun and civil dusk read right. The night is bright: at 0.023 lux the floor lets exposure open to EV −4.7, and the moon's halo reads like a sun through haze. The look's `adaptationBiasEv` (2) sets how dark the night stays. This is SIX's look call (M3).

## Slice 2 — Cloud cover, ceiling, visibility (E30, E31, E48–E52)

Interview done 2026-10-30: E48–E52 picked, E42–E47 locked. Slice 1 on `main` (`c788fda2`).

- [x] `MetocWeather` in the core: main layer cover (oktas), base and thickness; high cloud (oktas); visibility (MOR, m) and obscuration top (m). Validated; the scenario starts with one; `MetocReport` carries it (and its fingerprint mixes it), with the ceiling and METAR and WMO labels (2026-10-30).
- [x] Weather orders that blend (E33): a target weather and a blend time in day seconds. Cover and heights blend linearly, visibility and obscuration top in log space, exact at both ends. A new order mid-blend starts from where the weather is. Logged; replay reproduces it; a bad weather or blend time is refused with its reason (2026-10-30).
- [x] Clouds dim the reckoned light: Kasten & Czeplak (E49), on sun, moon and night sky, constants in `IlluminationProfileSO` (from memory, labelled); `CloudTransmission` in the report. T(4 oktas) = 0.929, T(8) = 0.250 (2026-10-30).
- [x] Weather presets as `MetocWeatherSO` assets (clear, scattered, broken, overcast, haze, fog bank), METAR-labelled, provenance AUTHORED; the scenario SO names its starting preset (Clear) and refuses to build without one (2026-10-30).
- [x] HDRP draws it through the overlay (E30, E48, E51, E52): volumetric clouds' `shapeFactor` from the cover calibration (off at 0 oktas), `bottomAltitude` and `altitudeRange` from base and thickness; fog mean free path visibility / ln 20, uniform to half the obscuration top; the 2D layer's opacity from high cloud; the moon light dimmed by the cloud (HDRP's cloud shadows only cover the sun). The camera's volume stack matched every override in every shot (2026-10-30).
- [x] **Deep Water → METOC → Calibrate Cloud Cover (play mode)** (E48): five cube-face cameras over the upper hemisphere, solid-angle weighted, fixed exposure; the table, its spread and method stored in the look. Uncalibrated draws `shapeFactor` = 1 − cover and says so once (2026-10-30). *Changed:* the clear reference is the same clouds at density 0, not clouds off (see the calibration below), and each point averages twelve fractional noise-tile offsets, not three.
- [x] Night bias 4 (E50) in the look (2026-10-30).
- [x] The panel: weather presets as buttons (blend over 15 min of day time) and sliders for cover, base, high cloud and visibility (set at once); the readout shows the weather, ceiling, labels, cloud transmission, blend progress and the drawn `shapeFactor`, with UNCALIBRATED when it applies (2026-10-30).
- [x] Tests: weather validation, blends, Kasten & Czeplak, ceiling and labels, fog mapping, calibration inversion, determinism and replay with weather orders. 54/54 (17 new: `MetocWeatherTests` 12, `MetocDrawingTests` 5), Test Runner, EditMode, 1.6 s (2026-10-30).
- [x] E31's A/B: 229/13 against 128/8 and 64/6 steps, GPU ms and screenshots (below). **SIX picked 128/8** (2026-10-30); the look and the SO's defaults carry it.
- [x] *Done when:* tests green (54/54); the demo seen clear, scattered, broken, overcast, in haze and in a fog bank, by day and at night under clear and overcast, with screenshots (sent to SIX 2026-10-30); the calibration's table and spread recorded here; METHODOLOGY C67–C70; committed and pushed on `feat/metoc`.

### Slice 2 — measured (2026-10-30)

Editor, play mode, Game view 2560×1440, quality High Fidelity, RTX 3070 Ti Laptop GPU. The METOC demo at 30° N, 40° W, 2026-10-30 14:24 UT (sun 46.1°), eye at 102 m. No other Unity Editor or trainer was running.

**The cover calibration (E48), as stored in `HdrpMetocLook.asset`.** Clouds at base 750 m, 1 500 m thick (the Broken preset); sun 21.9° elevation, 237.9° azimuth (the calibration's own fixed day). Five 90° faces at 256², the upper hemisphere, solid-angle weighted; fixed exposure EV 13; fog, the 2D layer, tonemapping and bloom off; temporal accumulation 0.5; 30 frames to settle. Twelve offsets per point (the R2 sequence over one noise tile); min and max are the spread across them. A pixel is cloud where its luminance differs from the clear reference by more than τ = 0.15; the last two columns show the table's sensitivity to τ.

| shapeFactor | Cover (mean) | Min | Max | τ 0.08 | τ 0.30 |
|---|---|---|---|---|---|
| 1.00 | 0.061 | 0.011 | 0.238 | 0.074 | 0.048 |
| 0.95 | 0.137 | 0.048 | 0.323 | 0.169 | 0.098 |
| 0.90 | 0.271 | 0.104 | 0.555 | 0.318 | 0.200 |
| 0.85 | 0.419 | 0.211 | 0.671 | 0.477 | 0.322 |
| 0.80 | 0.570 | 0.293 | 0.771 | 0.632 | 0.453 |
| 0.75 | 0.676 | 0.429 | 0.860 | 0.740 | 0.546 |
| 0.70 | 0.782 | 0.572 | 0.929 | 0.842 | 0.640 |
| 0.65 | 0.852 | 0.694 | 0.971 | 0.908 | 0.730 |
| 0.60 | 0.888 | 0.793 | 0.983 | 0.938 | 0.767 |
| 0.50 | 0.942 | 0.857 | 0.987 | 0.971 | 0.867 |
| 0.40 | 0.968 | 0.891 | 0.988 | 0.984 | 0.925 |
| 0.30 | 0.987 | 0.976 | 0.988 | 0.994 | 0.962 |
| 0.20 | 0.988 | 0.988 | 0.988 | 0.994 | 0.977 |
| 0.00 | 0.989 | 0.989 | 0.989 | 0.994 | 0.978 |

- **One sky varies a lot.** At `shapeFactor` 0.9 a single view reads 0.10 to 0.56 cover: the up face, a third of the dome, sees about one cloud's width of deck. The mean of twelve is what the table inverts.
- **The table depends on the cloud base.** The same sweep at base 1 500 m, 1 000 m thick (twelve offsets) reads 0.213, 0.481, 0.704 and 0.809 at 0.9, 0.8, 0.7 and 0.6: up to 0.09 cover (about ¾ okta) less than at 750 m. One table serves every base: a labelled limit (M5).
- **Repeatable.** The 1 500 m sweep with five offsets and with twelve agreed within 0.06 at every point.
- **Do not re-learn (runs 1–8):**
  - *A black sky.* In a play session where HDRP's volumetric clouds have never rendered, a camera that renders with them off draws them black once a volume turns them on. Runs 1 and 4 read 1.0 cover everywhere. Cameras made with the clouds already on render them (exp4). The clear reference is now the same clouds at density 0. A table must rise at least 0.3 across the sweep, or it is refused (run 2 was).
  - *Offsets that changed nothing.* `shapeOffset` is in noise-tile units (100 km / `shapeScale`, 20 km here), and the noise repeats. Offsets of 12 000 and −9 000 drew the same clouds three times: the spread read 0.002, while two runs a minute apart differed by 0.08 (the wind had moved the one sample). Offsets are now fractions of a tile.

**The weather set** (screenshots sent to SIX). Scattered, broken and haze were retaken with the final table.

| Shot | Report | T | Ground light | `shapeFactor` | Fog mean free path | 2D layer |
|---|---|---|---|---|---|---|
| Clear | SKC, no ceiling, 30 km | 1.000 | 83 601 lux | clouds off | 10 014 m to 1 000 m | 0 |
| Scattered | SCT 3/8 at 900 m, high 1/8, 25 km | 0.973 | 81 368 lux | 0.865 | 8 345 m | 0.13 |
| Broken | BKN 6/8, ceiling 750 m, high 2/8, 15 km | 0.718 | 60 025 lux | 0.715 | 5 007 m | 0.25 |
| Overcast | OVC 8/8, ceiling 450 m, 8 km | 0.250 | 20 900 lux | 0.000 | 2 670 m | 0 |
| Haze | FEW 1/8, high 3/8, 4 km, "mist or haze" | 0.999 | 83 548 lux | 0.958 | 1 335 m | 0.38 |
| Fog bank | SKC, 300 m, "fog" | 1.000 | 83 601 lux | clouds off | 100.1 m, uniform to 60 m, gone by 120 m | 0 |
| Night, clear (02:24 UT, 31st) | SKC | 1.000 | 0.0234 lux | clouds off | as clear | 0 |
| Night, overcast | OVC 8/8 | 0.250 | 0.0058 lux | 0.000 | as overcast | 0 |

At night the exposure floor read EV −2.74 under a clear sky and −4.74 under overcast (bias 4, E50); the moon light dropped from 0.065 to 0.016 lux with the cloud.

**E31: cloud steps.** The broken sky at noon, 300 frames after 60 warm-up, runs in ABC ABC order, with the earlier cover table (`shapeFactor` 0.794). GPU ms per frame from `ProfilerRecorder` with `GpuRecorder`.

| Steps (primary/light) | Volumetric clouds | Of which the trace | Frame |
|---|---|---|---|
| 229/13, run 1 | 2.59 | 2.23 | 23.44 (warm-up or noise) |
| 229/13, run 2 | 1.78 | 1.53 | 15.46 |
| 128/8 | 0.92 / 0.92 | 0.66 / 0.67 | 15.03 / 14.44 |
| 64/6 | 0.59 / 0.58 | 0.33 / 0.33 | 14.46 / 14.11 |

- The three pictures look the same at this view. Not checked: low sun, thin edges, flying through the deck.
- **The final sky, 128/8 and the final table** (`shapeFactor` 0.715, more cloud): volumetric clouds 1.27 ms (trace 1.00), Physically Based Sky 0.51, the 2D layer 0.23, fog on opaque 0.17. **The sky totals ≈2.18 ms**, against ≈3.2 ms before this slice: still ≈0.2 ms over E4's 2 ms in this view, before terrain (M4).
- Volumetric fog read zero samples again (likely async compute, unverified).

## Slice 3 — Rain (E32, E53–E57)

Interview done 2026-10-30: E53–E56 picked (BLUEPRINT). Slice 2 on `main` (`368504b6`).

- [x] The spike (E53): **Deep Water → METOC → Build Rain VFX** builds `MetocRain.vfx` from code, through the editor-only assembly `Unity.Testing.VisualEffectGraph.Editor`, which VFX Graph grants its internals to. Pass criteria fixed before the build: it compiles, it renders streaks, and it costs ≤ 0.5 ms GPU. **Passed** (table below); the Shuriken fallback was not needed (2026-10-30).
- [x] The rain field in `MetocWeather`: mm/h, validated 0–500, blends linearly; WMO intensity and METAR code; the report's fingerprint mixes it (2026-10-30).
- [x] Rain's extinction (E54): `Visibility` in the core, its constants in `IlluminationProfileSO` (from memory, labelled). The report carries `RainExtinctionPerM` and `EffectiveVisibilityM`. 10 mm/h alone gives 2 801 m; with 20 km of air, 2 463 m (2026-10-30).
- [x] HDRP draws it: the fog from the effective visibility, up to the cloud base under rain; rain darkens the drawn clouds' sun and ambient dimmers (E55), released to the shared sky's when dry (2026-10-30).
- [x] `MetocRain` (LateUpdate, marker `METOC.Rain`): moves the box to the camera's position (not its rotation) and sets the count, fall, size and brightness from the report and the look (E57). Once the last drop has fallen after the rain ends, it switches the effect off. A graph that lacks a property is refused, naming the menu that rebuilds it (2026-10-30).
- [x] Presets Light rain (7 oktas, 1.5 mm/h), Rain (8, 5) and Heavy rain (8, 20), visibility 20 km of air in each. The panel's rain slider runs 0–100 mm/h on a square-root scale. The readout shows the rain, its class, the visibility through it, how much the clouds are darkened and the drops drawn (2026-10-30).
- [x] The thermite: the spike's first build, kept at SIX's request as `Assets/_VFX/Thermite/Thermite.vfx` and `Thermite.prefab`. It is a frozen copy: exposure is skipped, and its box sits on the prefab's transform. It draws in play: 59 792 drops, not culled (2026-10-30).
- [x] Tests:
  - rain validation, classes and codes;
  - extinction and effective visibility, and the report;
  - blends and drying out, and a bad rain order;
  - fog under rain, cloud dimming, drawn drops and drop luminance.

  63/63 (9 new), Test Runner, EditMode (2026-10-30).
- [x] *Done when:* tests green (63/63); the demo seen in light, moderate and heavy rain by day and in rain at night, with screenshots (sent to SIX 2026-10-30); the cost measured against E56; METHODOLOGY C71–C74; committed and pushed on `feat/metoc`.

### Slice 3 — measured (2026-10-30)

Editor, play mode, Game view 2560×1440, quality High Fidelity, RTX 3070 Ti Laptop GPU. The METOC demo at 30° N, 40° W, 2026-10-30 14:24 UT (sun 46.1°), eye at 102 m, looking 4° up towards the north-west. Three `Unity.exe` processes were running: this Editor and two others, likely its asset-import workers (not checked). No trainer was running.

**The spike (E53).**

| Pass criterion | Result |
|---|---|
| Compiles | Yes: 4 contexts, capacity 131 072, 10 exposed properties (11 in the spike, before the box moved onto the object) |
| Renders streaks | Yes, in play (the shots below) |
| ≤ 0.5 ms GPU | 0.11 ms for 60 000 drops driven by hand in the spike; 0.14–0.16 ms through `MetocRain` (below) |

**The look (E57).** With the camera's exposure applied, a drop at 0.6·E/π is as bright as the overcast sky and vanished. k 1, 2 and 3 were shot, and 3 was kept, with opacity 0.4 and 4 mm width. SIX's look call: M6.

**The rain set** (screenshots sent to SIX).

| Shot | Report | Effective visibility | Ground light | Drops drawn | Drop luminance | Cloud dimmers | Fog (mean free path, uniform to, gone by) |
|---|---|---|---|---|---|---|---|
| Light rain | BKN 7/8 at 900 m, −RA 1.5 mm/h | 6 340 m (rain 0.323 per km) | 43 781 lux | 12 815 | 41 808 nits | 0.905 | 2 116 m, 450 m, 900 m |
| Rain | OVC 8/8 at 600 m, RA 5 mm/h | 3 571 m (0.689 per km) | 20 900 lux | 26 390 | 19 958 nits | 0.769 | 1 192 m, 300 m, 600 m |
| Heavy rain | OVC 8/8 at 450 m, +RA 20 mm/h | 1 664 m (1.650 per km) | 20 900 lux | 60 629 | 19 958 nits | 0.571 | 555 m, 225 m, 450 m |
| Rain at night (03:00 UT) | as Rain; moon 48.5°, 80 % lit | 3 571 m | 0.015 lux, floor EV −3.4 | 26 390 | 0.014 nits | 0.769 | as Rain |
| Overcast, dry | OVC 8/8 at 450 m, 8 km | 8 000 m | 20 900 lux | 0, effect off | — | 1.000 (the shared sky's) | 2 670 m, 225 m, 450 m |

- Light rain and rain read as streaks over a sea that fades out before the horizon. In heavy rain the horizon is gone: 1.7 km of visibility, seen from 102 m up.
- The brown cast is the shared overcast sky's: the dry overcast shot has it too.
- At night the drops are faint grey lines on black. They dim with the reckoned light, as real rain does when no lamp lights it.

**E56: the cost of rain.** Heavy rain (20 mm/h) with `MetocRain` on and off in ABAB order, then once at the drop cap (100 mm/h). 300 frames after 300 warm-up frames, so the box is full before the count starts. GPU ms per frame from `ProfilerRecorder` with `GpuRecorder`; `METOC.Rain` is CPU.

| Run | Rain | Drops | Init | Update | Transparent pass | Rain, GPU | Volumetric clouds | `METOC.Rain` (CPU) |
|---|---|---|---|---|---|---|---|---|
| A1 | on, 20 mm/h | 60 629 | 0.0067 | 0.0430 | 0.1082 | **0.158** | 0.578 | 0.0122 |
| B1 | off | 0 | 0 | 0 | 0.0000 | 0 | 0.448 | — |
| A2 | on, 20 mm/h | 60 629 | 0.0049 | 0.0382 | 0.0919 | **0.135** | 0.436 | 0.0119 |
| B2 | off | 0 | 0 | 0 | 0 | 0 | 0.437 | — |
| Cap | on, 100 mm/h | 124 518 | 0.0053 | 0.0467 | 0.1737 | **0.226** | 0.439 | 0.0130 |

- `ForwardTransparent` holds only the rain in this scene: it reads zero with the rain off. The drops' draw, `RenderQuadIndirect` (0.087–0.169 ms), runs inside it.
- E56's 0.5 ms holds with room: about 0.16 ms in heavy rain and 0.23 ms at the cap.
- The clouds' 0.58 ms in A1 against 0.44 ms in the other runs is the first run after a weather change, or noise. Rain changes only the clouds' dimmers, not their trace.
- `METOC.Draw` read 0.022–0.024 ms CPU, against 0.018 ms in slice 1.

**Do not re-learn.**
- *Exposure skipped.* By default HDRP's unlit VFX outputs write their colour without the camera's exposure. A colour of 0.6 then drew white at EV 10: the "thermite". `useExposureWeight` on the output applies the exposure (weight 1), and the colour is then in nits.
- *Local slots.* A world-space particle system still reads its bounds, birth box, Tile/Warp volume and velocity in the effect's own space. A camera position fed into a box centre on an object that also moves counts the move twice, and the effect is culled; the thermite prefab was, until its binder came off. Move the object, and keep the centres at 0.
- *Reset stops a recorder* (C64, re-learned in the spike). Call `ProfilerRecorder.Reset()`, then `Start()`.
- *A screenshot is written late.* `ScreenCapture.CaptureScreenshot` in the Editor writes on a later frame. The dry overcast frame landed 6 s late, after the next step had spawned the thermite, and showed the thermite; it was retaken. Wait for the file before changing the scene (C74).
- *The builder's assembly name.* `Unity.Testing.VisualEffectGraph.Editor` is granted VFX Graph's internals (`Editor/PackageInfo.cs`). If `com.unity.testing.visualeffectgraph` is ever installed, rename the assembly to another granted name.

## Slice 4 — The wind grows the sea (E36)
- [ ] Wind speed and direction in the report; Sea State's Douglas sea and wind direction driven through its existing setters, with growth over duration, labelled open-ocean; the Douglas dial as the god's labelled override.

## Later
- [ ] The Dogfight scenes adopt METOC (on SIX's say, E34).
- [ ] Sensors read the report (their own streams, E36).
- [ ] METOC L1–L3.
