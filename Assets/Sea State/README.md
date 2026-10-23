# Sea State

A Douglas-scale sea for Deep Water. Pick a Douglas wind sea (0–9) and swell (0–9). The tool sets wave height, period, spectrum, wind and whitecaps from cited tables. Ships float on a deterministic, Burst-evaluated wave field. HDRP Water draws **the same waves**, so the hull you see rides the wave you see.

> **Honest gap line.** Probe buoyancy, not seakeeping: there are no added-mass, radiation or RAO models. The far field beyond the camera's near-field square is HDRP's own FFT, statistically matched, not phase-locked, and its amplitude is not calibrated to Hs yet. Wind reference height (10 m vs 19.5 m) is unconfirmed against ANEP-11.

## Try it

1. **Deep Water → Sea State → Build Douglas Profiles.** Writes the 10 wind-sea and 10 swell profiles plus the library, with provenance.
2. **Deep Water → Sea State → Build Demo Scene.** Builds `Examples/Scenes/SeaStateDemo.unity`.
3. Press Play.
   - Keys **0–9** set the sea, **Shift+0–9** the swell, **[ / ]** turn the wind, **Tab** hides the panel.
   - Right-drag orbits, the wheel zooms, **WASD/QE** move and **F1–F4** jump to preset views.

Requirements: the HDRP asset must have water and water decals on, and *Horizontal Deformation* on for choppy crests. The near-field decal takes **two** atlas regions (waves and foam): 512² needs a 1024 atlas, 1024² needs a 2048 atlas.

## How it works

```
SeaStateProfileSO / SwellProfileSO ──┐  (Douglas tables, cited)
SeaStateLibrarySO.BuildBakeInput ────┤  (one place a Douglas choice becomes bake input)
                                     ▼
SeaStateDirector (MB, FixedUpdate, order −200)
   └─ SeaSimulation (pure C#)  ── SeaBaker ── SpectrumSampleJob       JONSWAP/PM × cos-2s → N components (Burst, Deterministic)
        │                                  └─ FoamJacobianSampleJob   (parallel) + FoamThresholdJob: whitecap area = Monahan W(U)
        ├─ SeaClock (steps × Δt, double)
        ├─ SeaFrameBuildJob   phases wrapped in double, √w cross-fade; writes the GPU layout AND the packed SIMD layout
        └─ SeaQueryJob        packed kernel (4 components per float4): height / normal / orbital velocity / Jacobian
                │             (SeaQueryReferenceJob = the plain-layout yardstick, used by tests and render verification)
   BuoyancySystem (MB, FixedUpdate, order −100) ── one BuoyancyProbeJob over every probe of every hull
                │                                  + BuoyancySumJob → one AddForce + AddTorque per hull
   BuoyantBody (MB) registers a Rigidbody + BuoyancyHullSO with the system
                │
   HdrpSeaRenderer (MB, LateUpdate, order 500)
        ├─ same components at the interpolated render time → GraphicsBuffer (+ per-wave blur pre-compensation)
        ├─ WaterDecal + SeaWaveDecal.shadergraph → SeaWaves.hlsl (GPU twin of SeaFieldMath): deformation, horizontal deformation, foam
        ├─ water mask: HDRP swell bands fade in where the near field fades out
        └─ HDRP ripples everywhere (waves shorter than the resolved cut-off)
```

- **Pure core.** `Core/` has no scene dependencies (P6). The MonoBehaviours only pump it and surface it.
- **One water, three evaluators.** The packed SIMD kernel (`SeaFieldPacked`) is the physics hot path. The plain kernel (`SeaFieldMath`) is the reference, and its layout is the one the GPU twin uses. `PackedKernel_MatchesReference` keeps them within 1 mm.
- **Timing.** At FixedUpdate n, bodies hold their state from time (n−1)·Δt, so the director advances its clock at the start of every FixedUpdate except the first. Waves are drawn at the interpolated render time (Pat5), matching interpolated rigidbodies.
- **Same water twice.** `Core/Waves/SeaFieldMath.cs` (CPU) and `Modules/Hdrp/Shaders/SeaWaves.hlsl` (GPU) describe one function. Change both or neither.

## Verified (2026-10-22, Unity 6000.4.11f1, HDRP 17.4)

| Check | Result |
|---|---|
| EditMode tests (`Tests/Editor`) | **27 / 27** pass: spectra, sampler determinism, 50 Hz vs 100 Hz agreement, Hs from 4σ of a 24 000 s record within 5 % (linear) / 8 % (choppy), inverse residual < 1 cm at D6, foam coverage, packed kernel = reference (≤ 1 mm), batched buoyancy = per-probe forces (≤ 1e-4), buoyancy model |
| Rendered vs physics, D7 + swell 4, like-for-like | 200 points within 250 m: corr **0.99999**, mean \|Δ\| **0.9 cm**, max **3.0 cm** (σ 2.0 m), blur compensation on. Off: 2.2 cm mean / 6.3 cm max |
| Rendered vs physics, D4 | corr 0.995 (first measurement, against the true surface; see the note below) |
| Orientation | Mirrored / swapped axes give correlation ≈ 0, so the mapping is not flipped |
| Floating at design draft (D4) | 154 m box 52 % submerged (design 52.5 %); 40 m box 47 % (45 %) |

**Know the instrument.** HDRP's CPU readback (`ProjectPointOnWaterSurface`) samples the decal as one *vertical* channel at the query point and ignores horizontal deformation. Against the true choppy surface it therefore reads about 0.18 m low at D7. That is the readback's blind spot, not render error. The like-for-like row compares what HDRP stores with the physics vertical displacement at the same point. Run it with **Deep Water → Sea State → Verify Render vs Physics** (play mode).

**Cost.** CPU numbers come from `Deep Water → Sea State → Benchmark Sea Kernel` and play-mode markers (Editor, Burst safety checks on, D7 + swell 4, 64 components). Re-measure in a player build before quoting them externally.

| Path | v1 | Now |
|---|---|---|
| Bake (sea-state change) | 40.2 ms | **2.6 ms** (whitecap calibration parallel + SIMD) |
| Query, 8 192 probes | 991 ns/probe | **182 ns/probe** (×5.5) |
| Query, 1 024 / 142 probes | 1.03 / 0.27 ms | **0.28 / 0.12 ms** (×3.8 / ×2.3; small batches are dispatch-bound) |
| Demo buoyancy (4 hulls, 142 probes) | ≈ 0.42 ms per fixed step | **0.10 ms per fixed step** (one job, one AddForce + AddTorque per hull) |
| Frame build (step) | 3.0 µs | 3.9 µs (now also packs the SIMD layout) |
| Determinism (`FloatMode.Deterministic` vs Fast) | — | costs 16–21 % single-threaded, 0.01 mm difference — kept |
| GC per frame, sea runtime | — | **0 B** (attributed with the Profiler: remaining player-loop allocations are HDRP's own, ~90 B) |
| GC per frame, demo panel | 45.6 KB | 18 KB (readout cached at 4 Hz; the rest is IMGUI's internal text meshes) |
| GPU: wave decal deformation + foam | 0.04 ms | 0.04–0.09 ms (resolution-independent 512² atlas; GPU clock varies) |

## Deliberately not here (yet)

- **GPU-only physics.** Physics stays on the CPU on purpose. A synchronous GPU readback stalls the frame; an async one lands frames late and on render time. Either breaks FixedUpdate determinism and EditMode testability. The GPU computes only what is *seen*; anything that changes the simulation's outcome is computed on the CPU.
- Edit-mode preview of the sea (play mode only).
- Far-field amplitude calibration of HDRP's Phillips bands against profile Hs.
- Carrier Ops `ISeaStateMotion` backed by this sea; replacing the DDG's `Mathf.Sin(Time.time)` sway (Q3 C, later).
- Water decal atlas 2048 / 2 km near field (Q4 B, later).
- Foam streaks along the wind: whitecaps are placed correctly but read as speckle.
- A UI Toolkit selector panel (would remove IMGUI's ~17 KB/frame of text-mesh garbage).
- Player-build performance numbers (all numbers above are Editor numbers with safety checks on).
- Warm-starting the Lagrangian inverse from the previous step. It would cut ~35 % of query cost, but makes results depend on history; not done without a measured need.

## Sources

The Douglas wind sea and WMO code 3700 heights come from MIT OCW 13.42 L6. Winds come from the NATO STANAG 4194 / ANEP-11 table via USNA EN400 Table 8.1. The period uses Pierson–Moskowitz, γ uses DNV-RP-C205 §3.5.5.5, and whitecaps use Monahan & O'Muircheartaigh (1980). Spreading follows Goda (2000, from memory). Every number is cited in the `DouglasProfileBuilder` header and in each profile's `provenance` field.
