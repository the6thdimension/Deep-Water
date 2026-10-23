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
SeaStateLibrarySO ───────────────────┤
                                     ▼
SeaStateDirector (MB, FixedUpdate, order −200)
   └─ SeaSimulation (pure C#)  ── SeaBaker ── SpectrumSampleJob   JONSWAP/PM × cos-2s → N components (Burst, Deterministic)
        │                                  └─ FoamCalibrationJob  Jacobian threshold so whitecap area = Monahan W(U)
        ├─ SeaClock (steps × Δt, double)
        ├─ SeaFrameBuildJob   phases wrapped in double, √w cross-fade
        └─ SeaQueryJob        height / normal / orbital velocity / Jacobian at any point (fixed-iteration Lagrangian inverse)
                │
   BuoyantBody (MB, FixedUpdate, order −100) ── BuoyancyModel (pure): Archimedes per probe column + drag vs water velocity
                │
   HdrpSeaRenderer (MB, LateUpdate, order 500)
        ├─ same components at the interpolated render time → GraphicsBuffer
        ├─ WaterDecal + SeaWaveDecal.shadergraph → SeaWaves.hlsl (GPU twin of SeaFieldMath): deformation, horizontal deformation, foam
        ├─ water mask: HDRP swell bands fade in where the near field fades out
        └─ HDRP ripples everywhere (waves shorter than the resolved cut-off)
```

- **Pure core.** `Core/` has no scene dependencies (P6). The MonoBehaviours only pump it and surface it.
- **Timing.** At FixedUpdate n, bodies hold their state from time (n−1)·Δt, so the director advances its clock at the start of every FixedUpdate except the first. Waves are drawn at the interpolated render time (Pat5), matching interpolated rigidbodies.
- **Same water twice.** `Core/Waves/SeaFieldMath.cs` (CPU) and `Modules/Hdrp/Shaders/SeaWaves.hlsl` (GPU) describe one function. Change both or neither.

## Verified (2026-10-22, Unity 6000.4.11f1, HDRP 17.4)

| Check | Result |
|---|---|
| EditMode tests (`Tests/Editor`) | 25 / 25 pass: spectra, sampler determinism, 50 Hz vs 100 Hz agreement, Hs from 4σ of a 24 000 s record within 5 % (linear) / 8 % (choppy), inverse residual < 1 cm at D6, foam coverage, buoyancy |
| Rendered vs physics surface, D4 | 150 random points within 250 m of camera: corr **0.995**, mean \|Δ\| **4 cm**, max 25 cm (σ 0.45 m) |
| Rendered vs physics surface, D7 + swell 4 | 200 points: corr **0.994**, mean \|Δ\| **0.20 m**, max 1.15 m (σ 1.95 m). Residual is HDRP's 3×3 deformation filter on 2 m texels softening 10–20 m waves |
| Orientation | Mirrored / swapped axes give correlation ≈ 0, so the mapping is not flipped |
| Floating at design draft (D4) | 154 m box 52 % submerged (design 52.5 %); 40 m box 47 % (45 %) |

**Baseline cost**, Editor play mode at ~97 fps, D7 + swell 4, 64 components, 142 probes. This is a baseline, not an optimisation claim; measure again in a player build:

| Marker | Time |
|---|---|
| Sea step | 0.008 ms/frame |
| Buoyancy queries | 0.21 ms/frame |
| Render upload | 0.047 ms/frame |
| GPU decal deformation + foam | 0.027 + 0.014 ms |
| HDRP water update | 0.11 ms |
| HDRP water GBuffer | 0.28 ms |

## Deliberately not here (yet)

- **GPU-only physics.** Physics stays on the CPU on purpose. A synchronous GPU readback stalls the frame; an async one lands frames late and on render time. Either breaks FixedUpdate determinism and EditMode testability. The GPU computes only what is *seen*; anything that changes the simulation's outcome is computed on the CPU.
- Edit-mode preview of the sea (play mode only).
- Far-field amplitude calibration of HDRP's Phillips bands against profile Hs.
- Carrier Ops `ISeaStateMotion` backed by this sea; replacing the DDG's `Mathf.Sin(Time.time)` sway (Q3 C, later).
- Water decal atlas 2048 / 2 km near field (Q4 B, later).
- Foam streaks along the wind: whitecaps are placed correctly but read as speckle.

## Sources

The Douglas wind sea and WMO code 3700 heights come from MIT OCW 13.42 L6. Winds come from the NATO STANAG 4194 / ANEP-11 table via USNA EN400 Table 8.1. The period uses Pierson–Moskowitz, γ uses DNV-RP-C205 §3.5.5.5, and whitecaps use Monahan & O'Muircheartaigh (1980). Spreading follows Goda (2000, from memory). Every number is cited in the `DouglasProfileBuilder` header and in each profile's `provenance` field.
