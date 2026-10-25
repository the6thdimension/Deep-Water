# Theatre — Roadmap

**This document's one job:** what to do next, and what is done. Why each piece exists is in `BLUEPRINT.md` (decisions E1–E25).

**Status: PLANNED, NOTHING BUILT (2026-10-24).** Building starts only when SIX says go *and* says the Unity Editor is free; other sessions share it.

**Rules (same as General Quarters):**
- Phases MAY be reordered for dependency.
- Unchecked items MUST NOT be deleted; mark them `~~skipped~~` with the reason.
- Completed items get a date and their commit.
- Every level ships with EditMode tests.
- Every performance claim carries its A/B number (`unity-perf-ab`), with the Editor resolution stated.
- A Sea State change touches the CPU kernel and the GPU twin together, and re-runs **Verify Render vs Physics** (`render-physics-agreement`).
- A hull or buoyancy change passes `hull-stability-check`.

Branch: `feat/environment-maps`, cut from `main` and kept in step with it (CLAUDE.md, Branches). HEAD stays wherever the shared checkout is; commits go in through `commit-to-branch`. Never pushed: the archive branch, DAVFX, the MCP unlock script; no force pushes.

---

## Phase 1 — Coastal ship operations (E3, E20)

**The gate (the blueprint's test, first half):** General Quarters' destroyer steams through a baked fictional strait on the curved sea.
- The radar loses a contact behind the headland, on the theatre's own heights.
- Waves shrink and break over the shoal, the same on screen as in the buoyancy.
- Grounding on rock breaches the hull; on mud she sticks.
- Two seeded runs give the same Fix, and it includes the theatre's hash.
- Terrain and sky stay within E4's budget.

### Slice 1 — Spike: a curved sea and coast wave maps (E9)

Plan only; the criteria are written before any code, per `spike-with-pass-criteria`. Reference spike: `Assets/Reckoning Spike/SPIKE-REPORT.md`.

**The tensions:**
- **T1:** E7 needs a curved sea on screen; HDRP 17.4 draws its ocean as one flat plane.
- **T2:** E6 needs per-wave coast maps. E4 caps memory at 512 MB, and Reckoning forbids outcome-changing GPU work. So the maps must be baked on the CPU, bit-identical every time, read identically by both evaluators, and fit the budget.

**Variants:**

| Part | Variant | What it is |
|---|---|---|
| A (T1) | **A1 curved mesh** | HDRP's `WaterGeometryType.Custom` (verified in the 17.4 source: it takes user mesh renderers, placed in world space). The mesh is camera-following and radial, dense near the camera, with each vertex at −d²/2R from the box centre. Sea State's near-field decal sits on top |
| A (T1) | **A2 level plane** (the E7 fallback) | HDRP's `Infinite` plane, moved each frame to the curved mean sea level under the camera |
| B (T2) | **B1 per wave** | Amplitude and phase maps for each of the sea's 64 waves, re-baked whenever the sea is re-baked |
| B (T2) | **B2 per band** | Maps for a fixed grid of frequency × direction bands, baked once with the theatre. Each wave reads its band's maps |

**Test bed:**
- a spike strait at 16 m spacing: two coasts, a 1:50 beach, a straight-contour shelf and a shoal;
- camera stations at 0, 10, 20 and 30 km from the centre, 30 m eye height;
- Douglas 4 and 7 with swell 4;
- Editor at 2560 × 1440.

**Pass criteria:**

| # | Measured | Threshold | Yardstick, and why it matters |
|---|---|---|---|
| CA1 | Drawn vs physics surface, like-for-like, at each camera station | mean \|Δ\| ≤ 2 cm, max ≤ 6 cm | Sea State's flat result today (0.9 cm / 3 cm at Douglas 7), at most doubled. A ship must ride the wave you see |
| CA2 | Hidden height of a 30 m mast at 15, 20, 25 and 30 km | within 2 m of (d − d_h)²/2R with d_h = √(2Rh) | The geometric horizon. A2 is expected to fail (it hides d²/2R, e.g. 49 m at 25 km); the size of the miss is the fallback's labelled gap |
| CA3 | Water GPU time against today's flat plane | ≤ +0.5 ms | E4's 2 ms covers terrain and sky; the sea must not eat it |
| CA4 | HDRP features on the custom mesh: foam (HDRP foam buffer vs residual field), decal, water-mask handover, underwater transition | foam correlation ≥ 0.85; each feature shown in a screenshot | Today's `SeaFoamTools` result is 0.85–0.95 |
| CB1 | Two bakes of the same strait | bit-identical hash | The hash goes into the Fix (E11) |
| CB2 | CPU kernel vs GPU decal with the maps, 10 000 points in the coastal band | mean \|Δ\| ≤ 2 cm, max ≤ 6 cm | C8, same as CA1 |
| CB3 | On the 1:50 beach and the straight-contour shelf | wave height within 5 % of linear shoaling to breaking; refraction angle within 2° of Snell's law; breaking where H/d = 0.78, within one cell | Linear wave theory (textbook). Proves the physics, not just the plumbing |
| CB4 | Map memory, extrapolated from the strait to 200 km of coast | ≤ 192 MB | With the 135 MB theatre, keeps the module inside E4's 512 MB |
| CB5 | Sea change (Douglas 4 → 7, or METOC growth) | no re-bake (B2), or ≤ 0.5 s off the main thread (B1) | E13 grows the sea continuously |
| CB6 | CPU query cost with maps (`SeaBenchmark`, 8 192 probes) | ≤ 3× today (182 → ≤ 550 ns/probe) | The DDG's buoyancy is 0.30 ms per fixed step today |
| CB7 | Where rays cross (behind the shoal) | after smoothing, amplitude factor ≤ 3, no NaN or unbounded spike | Bounded behaviour, not a calibrated value |

**Where it runs (Q5, SIX to decide):** proposed in the project on its own branch, as on 2026-10-23.
- Its own folder, `Assets/Theatre Spike/`, with its own assembly that nothing references.
- Sea State is not edited: the spike carries its own evaluator and decal variant.
- No package changes are expected. If one is needed, its files are backed up first and a footprint note goes in the side-session notes.

**Done when:** `Assets/Theatre Spike/SPIKE-REPORT.md` has every criterion × variant with numbers. E6 and E7's fallbacks are taken or released in the register (as **Proposed**; SIX locks). The spike is committed on its own branch.

### Slice 2 — Module skeleton, Gravity, and the first baked theatre
- [ ] Baseline first (E4): `unity-perf-ab` on the current DDG range and the Sea State demo, resolution stated.
- [ ] Folders per P3 for `Theatre` and `Gravity`; asmdefs (`DeepWater.Theatre`, `DeepWater.Gravity`, `.Tests.Editor`); a banned-API source scan of both cores.
- [ ] `Gravity`: `Down(p)`, `Drop(p)` on one sphere per theatre (E25), pure C#, tests against d²/2R and the tilt table.
- [ ] `HeightField` (signed, 16 m, 4 097²), `GroundClass` map, `TheatreQuery` (height, depth, ground, line of sight with curvature and a per-sensor effective radius, E24).
- [ ] `TheatreTypeSO`: a **strait** (Q3), with dials and a seed. A deterministic CPU bake, and an FNV-1a hash of the result.
- [ ] Overlays (E16): a height stroke and a ground-paint layer, replayed after regeneration; the hash covers them.
- [ ] Draw: HDRP Terrain built from the bake, with the drop baked into the heights; 4–6 rule layers and sparse seeded vegetation (E14); the far ring (E22) as coarse look-only heights.
- [ ] Builder menu: **Deep Water → Theatre → Bake Theatre** and **Build Demo Scene**. The scene GUID survives rebuilds (C24).
- [ ] *Done when:* two bakes hash the same; line-of-sight tests pass against hand-worked cases, including over the horizon; the scene loads and draws within E4 against the baseline, with a screenshot.

### Slice 3 — Coast rung 1 and grounding (Sea State)
- [ ] Sea State's mean level follows Gravity (curved), in the CPU kernel and the GPU twin; Sea State's buoyancy uses Gravity's down. That is Sea State's Gravity rung (E10).
- [ ] Depth texture into `SeaFieldPacked`/`SeaFieldMath` and `SeaWaves.hlsl`: per-wave depth damping and the 0.78·d cap; surf foam through `DepositFoam`.
- [ ] HDRP far field damped near the coast through the water mask (amplitude only, labelled).
- [ ] Depth under the keel; a grounding event with its ground class; tested on Sea State's demo hulls.
- [ ] *Done when:* `render-physics-agreement` holds in the coastal band; `hull-stability-check` unchanged in deep water; grounding tests pass for rock and mud.

### Slice 4 — The destroyer joins the theatre (with the General Quarters session)
- [ ] General Quarters hull and buoyancy adopt Gravity (its rung, E10); golden fingerprints re-pinned at the new level (C29).
- [ ] General Quarters radar masking by terrain through `TheatreQuery` (4/3-earth effective radius).
- [ ] Grounding into Damage Control (rock breaches, mud holds, E15); depth under the keel on the conn.
- [ ] Shallow-water squat and turning: an Engine Room rung, calibrated to public figures (E12, C16).
- [ ] *Done when:* the Phase 1 gate plays end to end.

## Phase 2 — Weather and the full coast

### Slice 5 — METOC L0 (E13)
- [ ] METOC's detailed questions asked and recorded (E26 onward).
- [ ] `MetocReport` (wind, visibility, cloud, ceiling, time and sun, rain) on its Watch; HDRP sky, cloud and fog draw it; sensors read visibility.
- [ ] The sea grown from wind × exposure × duration; the Douglas dial as a labelled god override. Reckoning R21 marked superseded.

### Slice 6+ — Coast rungs 2–4 (E6)
- [ ] Rung 2: lee shelter from exposure.
- [ ] Rung 3: shoaling. Rung 4: refraction, as the spike decided.

## Phase 3 — Real landforms and flight
- [ ] Theatre source L1 (E17): copy Cartographer's coordinate core; GDAL conversion script; importer; licences verified and recorded.
- [ ] Low-level flight (E3), with the aircraft module once it adopts Gravity.
