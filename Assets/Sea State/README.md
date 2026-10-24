# Sea State

A Douglas-scale sea for Deep Water. Pick a Douglas wind sea (0–9) and swell (0–9). The tool sets wave height, period, spectrum, wind and whitecaps from cited tables. Ships float on a deterministic, Burst-evaluated wave field. HDRP Water draws **the same waves**, so the hull you see rides the wave you see.

> **Honest gap line.** Probe buoyancy, not seakeeping: there are no added-mass, radiation or RAO models. Each column's force acts at the centroid of its submerged part, so roll stability (GM) is right for moderate heel; near 90° the probe model breaks down. A hull asset may set its centre of mass (`overrideCenterOfMass`); otherwise Unity derives it from the colliders. The inertia tensor is always Unity's uniform-density one, shifted to that centre of mass. The demo buoy's ballast (centre of mass 0.3 m above the keel) is authored: a uniform 2 m cube that light is unstable upright. The far field beyond the camera's near-field square is HDRP's own FFT. Its Hs is solved from a measured calibration (98–101 % of the target at Douglas 2–8), but it is statistically matched, not phase-locked, and it carries no wind streaks. Windrow spacing, convergence and foam lifetime are authored, not sourced. Wind reference height (10 m vs 19.5 m) is unconfirmed against ANEP-11.

## Try it

1. **Deep Water → Sea State → Build Douglas Profiles.** Writes the 10 wind-sea and 10 swell profiles plus the library, with provenance.
2. **Deep Water → Sea State → Build Demo Scene.** Builds `Examples/Scenes/SeaStateDemo.unity`.
3. Press Play.
   - Keys **0–9** set the sea, **Shift+0–9** the swell, **[ / ]** turn the wind, **Tab** hides the panel.
   - Right-drag orbits, the wheel zooms, **WASD/QE** move and **F1–F4** jump to preset views.
4. Play-mode checks under **Deep Water → Sea State**: **Verify Render vs Physics**, **Measure Hs by Zone**, **Calibrate Far Field** (writes the table into the look; ~9 min), **Measure Foam Streaks**, **Verify Foam vs CPU Twin** and **Benchmark Foam Kernel**. Each logs one report to the Console.

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
        ├─ SeaFoam.compute (GPU twin of ResidualFoamStepJob): residual foam drifts with the water and gathers into windrows
        ├─ water mask: HDRP swell bands fade in as the near field fades out, √(1 − w) against √w (Hs constant)
        ├─ far field: HDRP wind + band multiplier solved from FarFieldCalibration, so its Hs is the sea state's Hs
        └─ HDRP ripples everywhere (waves shorter than the resolved cut-off)
```

- **Pure core.** `Core/` has no scene dependencies (P6). The MonoBehaviours only pump it and surface it.
- **One water, three evaluators.** The packed SIMD kernel (`SeaFieldPacked`) is the physics hot path. The plain kernel (`SeaFieldMath`) is the reference, and its layout is the one the GPU twin uses. `PackedKernel_MatchesReference` keeps them within 1 mm.
- **Timing.** At FixedUpdate n, bodies hold their state from time (n−1)·Δt, so the director advances its clock at the start of every FixedUpdate except the first. Waves are drawn at the interpolated render time (Pat5), matching interpolated rigidbodies.
- **Same water twice.** `Core/Waves/SeaFieldMath.cs` (CPU) and `Modules/Hdrp/Shaders/SeaWaves.hlsl` (GPU) describe one function. Change both or neither. The same goes for `Core/Foam/ResidualFoam.cs` and `Modules/Hdrp/Shaders/SeaFoam.compute`.
- **Far field, counted not declared.** HDRP's Phillips spectrum is driven by wind and patch size, not by Hs. **Calibrate Far Field** measures HDRP's Hs over a wind × patch-size grid (14 × 6 cells, 12 instants each) and stores the table, with its provenance, in the look. The renderer then solves the table for the wind and band multiplier that give the sea state's Hs.
- **Foam.** Whitecaps are where the surface Jacobian drops below a threshold calibrated so their area equals Monahan W. What breaking crests leave behind is a second, long-lived field:
  - it drifts downwind with the water at ~3 % of the wind;
  - Langmuir convergence gathers it into windrows, Beaufort's "streaks along the direction of the wind", from force 7 up;
  - the step is in flux form, so foam is conserved exactly (tested), and the grid itself drifts with the water so drift never smears it.

## Verified (2026-10-22 / 23, Unity 6000.4.11f1, HDRP 17.4)

| Check | Result |
|---|---|
| EditMode tests (`Tests/Editor`) | **46 / 46** pass: spectra, sampler determinism, 50 Hz vs 100 Hz agreement, Hs from 4σ of a 24 000 s record within 5 % (linear) / 8 % (choppy), inverse residual < 1 cm at D6, foam coverage, packed kernel = reference (≤ 1 mm), batched buoyancy = per-probe forces (≤ 1e-4), buoyancy model; heeled 154 m box: righting moment within 2 % of wall-sided theory (41.3 vs 41.6 MN·m; ballasted 66.8 vs 67.1), free roll 8.67 s vs box theory 8.67 s, inverted box loaded like upright; 2 m cube buoy at 5°: uniform tips (−806 N·m), ballasted rights itself with 1157 N·m = m g GM sin φ; a hull asset applies its centre of mass only when it overrides it; residual foam: drift moves a patch 6 m with mass and variance unchanged (≤ 0.01 % / 0.1 %), exact whole-texel re-centring, decay, production only where the surface breaks, foam conserved onto windrows, streaks within 10° of the wind for four directions and the shipped irregular look, windrow coordinate never folds |
| Rendered vs physics, D7 + swell 4, like-for-like | 200 points within 250 m: corr **0.99999**, mean \|Δ\| **0.9 cm**, max **3.0 cm** (σ 2.0 m), blur compensation on. Off: 2.2 cm mean / 6.3 cm max |
| Rendered vs physics, D4 | corr 0.995 (first measurement, against the true surface; see the note below) |
| Orientation | Mirrored / swapped axes give correlation ≈ 0, so the mapping is not flipped |
| Floating at design draft (D4) | 154 m box 52 % submerged (design 52.5 %); 40 m box 47 % (45 %) |
| Roll period, demo hulls in PhysX (calm, released from 5°) | 154 m box **9.42 s** (box theory 9.04 s undamped; probe damping adds ~4 %); 40 m **3.98 s** (4.0 s); 11 m **1.78 s** (1.76 s); ballasted buoy **3.36 s** (3.36 s, with Unity's inertia 3 794 kg·m² about the lowered centre of mass). Before the 2026-10-23 fix, forces at the column centre gave GM = BM: 6.0 s for the 154 m box by the same theory |
| Buoy in Douglas 4 | Upright, 40 % submerged (design 40 %); tilts at most 23° while following the waves |

**Distant sea, Hs by zone** (**Measure Hs by Zone**, play mode: HDRP readback, 12 instants × 300 points per zone). "Before" is a linear handover with HDRP's own wind; "after" is the energy-preserving handover with the calibrated far field.

| Sea | Far field, before → after | Handover band, before → after | Near field: rendered / physics at the same points |
|---|---|---|---|
| Douglas 2 | 121 % → **100 %** | 78 % → 83 %¹ | — |
| Douglas 4 | 100 % → **98 %** | 80 % → **100 %** | — |
| Douglas 6 | 125 % → **99 %** | 96 % → **100 %** | — |
| Douglas 7 + swell 4 | 149 % → **98 %** | 110 % → **103 %** | **100.3 %** |
| Douglas 8 | 134 % → **101 %** | 98 % → **99 %** | — |

¹ 37 % of Douglas 2's Hs is in waves shorter than 10 m, which only HDRP's ripples carry, and the CPU readback does not evaluate ripples. The near-field zone holds one or two wavelengths at Douglas 6–8, so its Hs beats with the wave groups (89–101 % of target); against physics at the same points it reads 100.3 %.

**Foam** (**Measure Foam Streaks** and **Verify Foam vs CPU Twin**, play mode, after 150 s in each sea).

| Check | Douglas 5 (Beaufort 6) | Douglas 6 (Beaufort 8) | Douglas 7 + swell 4 (Beaufort 10) |
|---|---|---|---|
| Active whitecaps vs Monahan W | 2.0 % vs 2.2 % | 9.0 % vs 9.3 % | 27.7 % vs 27.4 % |
| Residual foam ≥ 0.1 | 0.1 % (no streaks) | 19 % | 61 % |
| Streak direction vs wind | 0.1° off, coherence 0.71 (too faint to see) | **0.4°** off, coherence 0.84 | **0.0°** off, coherence 0.90 |
| Correlation length along / across the wind | 13.9 / 2.1 m (×6.7) | 17.8 / 1.6 m (**×10.9**) | 23.7 / 2.2 m (**×10.7**) |
| HDRP's drawn foam vs the residual field | corr 0.85 | corr **0.95** | corr **0.91** |
| GPU field vs CPU twin, 180 steps, 512² | | | corr **1.000000**, worst texel < 1e-5 |

**Know the instrument.**
- HDRP's CPU readback (`ProjectPointOnWaterSurface`) samples the decal as one *vertical* channel at the query point and ignores horizontal deformation. Against the true choppy surface it therefore reads about 0.18 m low at D7. That is the readback's blind spot, not render error. The like-for-like row compares what HDRP stores with the physics vertical displacement at the same point. Run it with **Deep Water → Sea State → Verify Render vs Physics** (play mode).
- The same readback samples decal deformation with **repeat** addressing; the GPU clamps. Outside the decal square it would tile the near field across the horizon (2.41 m instead of 1.89 m at Douglas 4), so the zone tool includes deformation only inside the square.
- HDRP 17.4 water decals write the foam pass as (R = surface, G = deep), but HDRP's foam pass reads (G, B). A water-decal graph's surface foam never arrives, and its deep foam is drawn as surface foam. Until this pass, the near-field whitecaps on screen were the deep-foam output at gain 1. `hdrpFoamChannelWorkaround` (on) sends our surface foam out through the deep channel; HDRP's subsurface foam then gets nothing from the decal. Found by forcing each channel to 1 and looking; the foam statistics had been perfect while nothing reached the screen (AP14).

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
| GPU: residual foam step (512², 64 components) | — | **0.068 ms** (**Benchmark Foam Kernel**, RTX 3070 Ti Laptop, D3D12) |
| GPU: HDRP water passes, residual foam on / off | — | no measurable change: surface update 0.10 / 0.10, decal foam 0.02 / 0.02, decal deformation 0.03 / 0.03 ms |

## Deliberately not here (yet)

- **GPU-only physics.** Physics stays on the CPU on purpose. A synchronous GPU readback stalls the frame; an async one lands frames late and on render time. Either breaks FixedUpdate determinism and EditMode testability. The GPU computes only what is *seen*; anything that changes the simulation's outcome is computed on the CPU.
- Edit-mode preview of the sea (play mode only).
- Wind streaks in the far field. HDRP's FFT foam has none, so at Douglas 6+ the foam texture changes at the near-field edge (~500 m). Far-field foam *coverage* is a curve (`farFoamByWhitecapFraction`), not yet calibrated against W the way Hs is.
- HDRP's deep (subsurface) foam from the decal: lost to the channel workaround until HDRP reads the channels it writes.
- The far field's egg-crate look at `farChaos` 0.85 (waves running both ways make standing patterns). Lowering chaos needs a new **Calibrate Far Field** run.
- Carrier Ops `ISeaStateMotion` backed by this sea; replacing the DDG's `Mathf.Sin(Time.time)` sway (Q3 C, later).
- Water decal atlas 2048 / 2 km near field (Q4 B, later).
- A UI Toolkit selector panel (would remove IMGUI's ~17 KB/frame of text-mesh garbage).
- Player-build performance numbers (all numbers above are Editor numbers with safety checks on).
- Warm-starting the Lagrangian inverse from the previous step. It would cut ~35 % of query cost, but makes results depend on history; not done without a measured need.

## Sources

The Douglas wind sea and WMO code 3700 heights come from MIT OCW 13.42 L6. Winds come from the NATO STANAG 4194 / ANEP-11 table via USNA EN400 Table 8.1. The period uses Pierson–Moskowitz, γ uses DNV-RP-C205 §3.5.5.5, and whitecaps use Monahan & O'Muircheartaigh (1980). Spreading follows Goda (2000, from memory). Every number is cited in the `DouglasProfileBuilder` header and in each profile's `provenance` field.

Foam: the onset of wind streaks follows the Beaufort sea descriptions (force 7 "foam begins to be blown in streaks along the direction of the wind", 8 "well-marked streaks", 9–10 "dense streaks"). Surface drift of ~3 % of the wind is the usual rule of thumb (e.g. Wu 1983, *J. Phys. Oceanogr.* 13). Windrow spacing from a few metres to a few hundred is from Thorpe (2004, *Annu. Rev. Fluid Mech.* 36). These three citations are from memory; verify them before quoting externally. Spacing, convergence speed, lifetime and production are authored, and the look says so. The far-field calibration table carries its own provenance (date, method, spread) in `HdrpSeaLook.asset`.
