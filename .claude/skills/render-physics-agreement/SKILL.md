---
name: render-physics-agreement
description: "Prove that what HDRP draws matches what the CPU simulation computes in Deep Water's Sea State (waves, foam, far-field sea height), and calibrate renderer parameters against measured output. Use this whenever comparing the rendered water to physics, when a visual effect seems missing or wrong on screen even though its own buffers look right, when using HDRP water readback (ProjectPointOnWaterSurface, the CPU simulation), water decals or the foam buffer, when tuning or calibrating HDRP water parameters (far-field Hs, foam), or when a measured disagreement needs a cause. Covers the forced-maximum channel smoke test, reading the instrument's source before trusting it, like-for-like comparison, A/B with a switch, and calibration tables with spread."
allowed-tools: Bash, Read, Edit, Write, Grep, Glob
---

# Render ↔ physics agreement

Sea State's rule (methodology C8) is one physical field with two evaluators and one verified agreement. The CPU evaluates the sea for physics; the GPU draws the same function from the same buffer. Two traps (AP13, AP14) cost the most time on 2026-10-22/23. This skill exists to avoid them.

## The tools (play mode; `Deep Water/Sea State/` menu)

| Menu | Entry point | Measures |
|---|---|---|
| Verify Render vs Physics | `SeaRenderAgreementCheck.Prepare()`, then `.Run(points, radiusM, seed)` | Rendered vs physics height at random points: correlation, mean \|Δ\|, max \|Δ\| |
| Measure Hs by Zone | `SeaFarFieldTools.MeasureZones(instants, points, spacingS, done)` | Hs near field / handover band / far field, from HDRP readback |
| Calibrate Far Field | `SeaFarFieldTools.CalibrateFarField(done)` | Builds the HDRP far-field Hs table (wind × repetition) with spread |
| Measure Foam Streaks | `SeaFoamTools.MeasureStreaks(frames, done)` | Streak direction and anisotropy, plus correlation with HDRP's **own** foam buffer |
| Verify Foam vs CPU Twin | `SeaFoamTools.VerifyTwin(steps, done)` | GPU residual foam vs a CPU replay of the same steps |
| Benchmark Foam Kernel | `SeaFoamTools.BenchmarkKernel(frames, dispatches, done)` | GPU time, timed inside the render context |

From MCP, call the entry points in a RunCommand during play and wait on the report: the `unity-mcp-workflow` skill's `scripts/wait_file.sh`, or the logged result.

## Procedure

1. **Read the instrument's source before trusting a number** (AP13). State what each side actually measures. HDRP 17.4 facts, verified from source:
   - The CPU readback (`ProjectPointOnWaterSurface`) samples water decals as one vertical channel at the query point, so it **ignores horizontal deformation**.
   - It also samples the decal with **repeat** addressing (`SignedMod` in `SimulationCPU.Utilities.cs`), while the GPU clamps. Outside the decal square it tiles the near field across the horizon, so include deformation only inside the square.
   - HDRP water is **render-clocked**. Never query it for physics (AP11).
2. **Compare like-for-like.** Freeze the frame (`Prepare`), sample both sides at the same points and time, and report correlation, mean \|Δ\| and max \|Δ\| against the field's σ.
   - Check orientation once: mirrored or swapped axes must give correlation ≈ 0.
   - Reference at Douglas 7 + swell 4: corr 0.99999, mean 0.9 cm, max 3 cm (σ 2 m).
3. **Forced-maximum smoke test, once per output channel** (AP14). Before trusting a new renderer output, force it to 1 and look at the screen.
   - HDRP 17.4 water-decal graphs write foam as (R surface, G deep), but HDRP reads (G, B). Surface foam never arrives.
   - Every upstream check passed (GPU = CPU twin to 1e-5) while nothing reached the pixels.
   - Then keep a standing check at **the last buffer the renderer reads**: HDRP's foam buffer correlated 0.91–0.95 with the residual field after the fix.
4. **A/B every compensation with a switch.** Measure on and off on the same frozen frame. Keep it only if the number moves: blur compensation took 2.2 cm to 0.9 cm, so it stayed (C11).
5. **Calibrate against what the renderer produces,** not its parameter names (C13).
   - Build a measured table over the controlling inputs, averaging over the instrument's own fluctuation. A 250 m HDRP patch's Hs varies ±7 % instant to instant, so use 12 instants × 0.5 s, not 0.15 s.
   - Store the table, its method, spread and date with the asset (`HdrpSeaLook.asset` provenance). Solve it at runtime.
   - Result: far-field Hs went from 100–149 % to 98–101 % of target.
6. **Judge by eye as well.** Taste is a gate: numbers passed while production tuned to Beaufort's words blanketed Douglas 7, and the first windrows were a regular grid. Take screenshots; HDRP needs in-play `ScreenCapture`, not `Camera.Render`.
7. **Record:** the README's measured tables (before → after), and METHODOLOGY entries for any new instrument lesson.
