---
name: unity-perf-ab
description: "Measure a Unity performance change in Deep Water as an A/B on the same scenario, and keep it only if the number moves with fidelity pinned: profiler-marker timing over N frames after a warm-up, GC allocation attribution to the allocating call path, GPU timing taken inside the render context, and EditMode micro-benchmarks. Use this whenever optimising, claiming something is faster, chasing frame time, GC allocations or hitches, benchmarking a Burst job, compute shader or kernel, choosing between implementations on speed, or setting a tick or frame budget. Covers the harness templates, the MCP profiler tools, and the traps that fake a result (Game view resolution, safety checks, the Editor vs a player, GPU timestamps outside the render context)."
allowed-tools: Bash, Read, Edit, Write, Grep, Glob
---

# Unity performance A/B

Project rule (CLAUDE.md, methodology C11): profile before claiming a perf win, and an optimisation ships with its A/B number or not at all. "Should be faster" is not a perf claim.

## Choose the instrument

| Question | Instrument |
|---|---|
| Pure CPU kernel or job cost | EditMode micro-benchmark: Stopwatch over many calls after a warm-up, as in `Assets/Sea State/Editor/SeaBenchmark.cs` (menu *Benchmark Sea Kernel*) or the spike's `Criterion3_TickCost` test. Log ms per call or ns per item with the scale (items, components) |
| Per-frame cost of named systems in play | `templates/profile-markers.cs`: `ProfilerRecorder` per marker, 300 frames after 30 warm-up. Add your own `ProfilerMarker` to code you own |
| Who allocates GC each frame | Record with ProfilerDriver, then `templates/gc-attribution.cs`. Or the MCP tools `Unity_Profiler_GetFrameRangeGcAll…` / `Unity_Profiler_GetFrameGcAllocati…` over a frame range |
| GPU time of a compute or render pass | A `ProfilingSampler` **inside `RenderPipelineManager.beginContextRendering`**, read back with a GPU recorder. Reference: `SeaFoamTools.BenchmarkKernel` (menu *Benchmark Foam Kernel*) |
| Where the time goes, broadly | MCP `Unity_Profiler_GetFrameRangeTopTimeSummary` / `GetSampleTimeSummary` on a recorded range |
| The Editor's own loop: compile, domain reload, play-mode entry, player build | `scripts/editor_log_clock.py` in the background stamps Editor.log lines with wall-clock time. A RunCommand logs `MARK <label>`, then writes its one-line edit and calls `AssetDatabase.Refresh()`. `scripts/build_cycle.py <clock> <label> Library/Bee` prints refresh, compile, reload and ready seconds, plus Csc and ILPostProcess per assembly from Bee's trace. Attribute a slow reload with one Editor profiler capture (`ProfilerDriver.profileEditor = true`, `EditorUtility.RequestScriptReload()`, then walk frame 0 with `HierarchyFrameDataView`). Ledger: `BUILD-SPEED-LEDGER.md` |

## Procedure

1. **Fix the scenario and write it down** for both A and B. Examples: Douglas 7 + swell 4, 64 wave components, panel on or off, Game view resolution, Editor or player, Burst safety checks on or off.
2. **Pin fidelity first.** A test proves B computes the same thing as A, e.g. the packed SIMD kernel equals the reference within 1 mm, and batched buoyancy equals per-probe within 1e-4. Without it, a speed-up may just be a different answer.
3. **Run A, then B,** on the same instrument and the same frames count, with the warm-up discarded. For noisy numbers, run each twice.
4. **Report the pair and the ratio,** with the unit and scale. Examples: "991 → 182 ns/probe at 8 192 probes (×5.5)", "≈0.42 → 0.10 ms per fixed step", "45.6 → 18 KB/frame".
5. **Keep or drop.**
   - Keep it only if the number moved and the fidelity test passes.
   - A change that moved nothing gets dropped and noted: batch-size tuning was not pursued.
   - A slower choice kept for a reason gets its cost recorded: `FloatMode.Deterministic` costs 16–21 %, kept for reliability.
6. **Record:** the README's performance table, plus a METHODOLOGY changelog line naming the harness.

## Traps that fake a result

- **Auto-refresh while the Editor is unfocused is erratic** (2026-10-25): one saved `.cs` was picked up in 7 s, the next in 237 s. Time edit cycles from an explicit `AssetDatabase.Refresh()` after a `MARK` line, not from the save.
- **An unfocused Editor runs on the efficiency cores** (2026-10-25, the i9-12900H's 8 E-cores). Windows 11 power throttling (EcoQoS) confines a background Unity and its workers to the E-cores.
  - A domain reload took 16.3 s throttled and 7.4 s with the process tree opted out. Shader compiling used 5.6 cores throttled and 13 cores opted out.
  - Every Editor number taken while another window has focus is a background number, which includes every MCP-driven session. Say which you measured.
  - Check with `Get-Counter '\Processor(*)\% Processor Time'`: logical 12–19 pinned at 100 % while 0–11 idle means throttled.
  - The project's Full Speed tool (`Assets/Full Speed`, round 5) keeps the Editor's tree opted out; Deep Water > Full Speed > Report shows each process's state. Check it's on before timing anything.
  - The prototype, for another project or a headless Unity, is `scripts/ecoqos_optout.ps1 -EditorPid <Unity pid> -Log <file>` (round 4).
- **First play after an Editor start is cold.** In GQ_Range, play entry took 7.4 s the first time and 2.5 s the second (2026-10-25). Discard the first play, as you would a warm-up.
- **A long-running Editor drifts slower.** Domain reloads went from 38 s in the morning to 46–57 s after 5 h, with no code change. Interleave A and B runs (ABAB), and report n and the range.
- **Game view resolution changes everything.** A switch to a fixed 2560×1440 between runs cut fps from ≈97 to 32 with no code change. The marker template records the resolution; compare only like with like.
- **No GPU timestamps outside the render context.** `CommandBuffer.BeginSample` executed from `LateUpdate` via `Graphics.ExecuteCommandBuffer` reports 0; so did a `ProfilingSampler` outside the context. Time GPU work inside `beginContextRendering`. Check `SystemInfo.supportsGpuRecorder`.
- **Editor numbers aren't player numbers.** Say "Editor, safety checks <on/off>" next to every figure, and don't extrapolate to a build without measuring one.
- **Unfocused Editor:** the player loop is throttled. Set `Application.runInBackground = true` for the run, and restore `PlayerSettings.runInBackground` afterwards (the `unity-mcp-workflow` skill's `exit-play.cs`).
- **First calls include Burst compilation and cache warm-up.** Always discard a warm-up.
- **A per-frame number for per-step work:** fixed steps per frame vary. Divide by fixed steps for simulation markers, or time per step directly.
- **Small batches are dispatch-bound.** Report scale with every number: 0.28 ms at 1 024 probes says nothing about 142.
