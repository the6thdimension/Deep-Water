# METOC — Roadmap

**This document's one job:** what to do next, and what is done. Why each piece exists is in `BLUEPRINT.md` (E26–E47).

**Status: BLUEPRINT LOCKED (2026-10-30). Slice 1 built and seen in play (2026-10-30); E46 waits on SIX (M2). Slice 2 next.**

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

## Slice 2 — Cloud cover, ceiling, visibility (E30, E31)
- [ ] `MetocReport` gains cover (oktas), cloud base and thickness, and visibility (meteorological optical range).
- [ ] Clouds drawn by HDRP's Custom volumetric clouds; the 2D layer for cirrus. Fog from visibility by Koschmieder (HDRP's extinction is 1/mean free path; density falls as e^(−Δh/H), H = 0.1448 × layer depth, verified in `Fog.cs`).
- [ ] Clouds dim the reckoned light.
- [ ] E31's A/B: 229/13 against 128/8 and 64/6 steps, GPU ms and screenshots; SIX picks.
- [ ] Weather blends by order (E33).

## Slice 3 — Rain (E32)
- [ ] Rate in mm/h with METAR classes; VFX Graph rain in a camera-following box; visibility and cloud darkening from the rate.

## Slice 4 — The wind grows the sea (E36)
- [ ] Wind speed and direction in the report; Sea State's Douglas sea and wind direction driven through its existing setters, with growth over duration, labelled open-ocean; the Douglas dial as the god's labelled override.

## Later
- [ ] The Dogfight scenes adopt METOC (on SIX's say, E34).
- [ ] Sensors read the report (their own streams, E36).
- [ ] METOC L1–L3.
