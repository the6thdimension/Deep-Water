using System;
using System.Reflection;
using System.Text;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using DeepWater.Sea.Core;
using DeepWater.Sea.Modules.Hdrp;

namespace DeepWater.Sea.Examples.Editor
{
    /// <summary>
    /// Play-mode checks of the foam:
    /// <list type="bullet">
    /// <item><b>Measure Foam Streaks</b> — the residual field read back from the GPU: coverage,
    /// streak direction against the wind, coherence and along/across correlation lengths; the
    /// active whitecap coverage of the same wave frame against Monahan W; HDRP's own foam buffer
    /// (what is drawn) against the residual field; GPU time of HDRP's water passes.</item>
    /// <item><b>Verify Foam vs CPU Twin</b> — record N GPU steps from a cleared field, replay
    /// them with <see cref="ResidualFoamStepJob"/> and compare the two fields texel by texel
    /// (render agrees with the reference, as the wave agreement check does for heights).</item>
    /// <item><b>Benchmark Foam Kernel</b> — GPU time of the residual-foam kernel on the live wave
    /// frame. The renderer dispatches it from LateUpdate, outside HDRP's render context, where no
    /// GPU timestamps are taken; the benchmark runs it inside the context instead.</item>
    /// </list>
    /// </summary>
    public static class SeaFoamTools
    {
        static readonly string[] GpuSamples = { "WaterSurfaceUpdate", "WaterDecalFoam", "WaterDecalDeformation" };
        const string BenchSample = "DeepWater.Sea.ResidualFoam.Benchmark";

        static EditorApplication.CallbackFunction s_Running;
        public static bool IsRunning => s_Running != null;

        [MenuItem("Deep Water/Sea State/Measure Foam Streaks (play mode)")]
        static void MeasureMenu() => MeasureStreaks(120, report => Debug.Log(report));

        [MenuItem("Deep Water/Sea State/Verify Foam vs CPU Twin (play mode)")]
        static void VerifyMenu() => VerifyTwin(180, report => Debug.Log(report));

        [MenuItem("Deep Water/Sea State/Benchmark Foam Kernel (play mode)")]
        static void BenchmarkMenu() => BenchmarkKernel(120, 20, report => Debug.Log(report));

        /// <summary>
        /// Run the residual-foam kernel <paramref name="dispatchesPerFrame"/> times per frame inside
        /// HDRP's render context, on scratch textures, with the renderer's current inputs, and report
        /// the mean GPU time per dispatch over <paramref name="frames"/> frames.
        /// </summary>
        public static void BenchmarkKernel(int frames, int dispatchesPerFrame, Action<string> done)
        {
            if (!Begin(out var director, out var renderer, done)) return;
            var compute = (ComputeShader)new SerializedObject(renderer).FindProperty("foamCompute").objectReferenceValue;
            int kernel = compute.FindKernel("DWSeaFoamIntegrate");
            int n = renderer.ResidualFoamTexture.width;
            float size = renderer.NearFieldTexelM * n;
            var a = new RenderTexture(renderer.ResidualFoamTexture.descriptor);
            var b = new RenderTexture(renderer.ResidualFoamTexture.descriptor);
            a.Create(); b.Create();
            var frame = new NativeArray<SeaFrameComponent>(2 * SeaSimulation.MaxComponentsPerState, Allocator.Persistent);
            var components = new GraphicsBuffer(GraphicsBuffer.Target.Structured, frame.Length, UnsafeSize);
            var sampler = new ProfilingSampler(BenchSample);
            var cmd = new CommandBuffer { name = BenchSample };
            Recorder recorder = Recorder.Get(BenchSample);
            recorder.enabled = true;
            int lastFrame = -1, measured = 0, count = 0;
            double sumNs = 0;
            double start = EditorApplication.timeSinceStartup + 1.0;

            void Dispatch(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
            {
                if (Time.frameCount == lastFrame) return;   // once per frame, whatever the camera count
                lastFrame = Time.frameCount;
                count = director.Simulation.BuildFrame(director.RenderTimeS, frame);
                components.SetData(frame, 0, 0, count);
                ResidualFoamParams p = renderer.LastResidualFoamParams;
                float2 c = renderer.ResidualFoamCentre;
                cmd.Clear();
                sampler.Begin(cmd);
                cmd.SetComputeBufferParam(compute, kernel, "_DWSeaComponents", components);
                cmd.SetComputeIntParam(compute, "_DWSeaComponentCount", count);
                cmd.SetComputeIntParam(compute, "_DWSeaFoamResolution", n);
                cmd.SetComputeVectorParam(compute, "_DWSeaFoamGrid", new Vector4(c.x, c.y, size, 0f));
                cmd.SetComputeIntParams(compute, "_DWSeaFoamShift", 0, 0);
                cmd.SetComputeVectorParam(compute, "_DWSeaFoam", new Vector4(p.JacobianThreshold, p.BreakingGain, 0f, 0f));
                cmd.SetComputeVectorParam(compute, "_DWSeaFoamStep", new Vector4(1f / 60f, p.LifetimeS, p.ProductionPerS, p.Cap));
                cmd.SetComputeVectorParam(compute, "_DWSeaFoamFlow", new Vector4(p.Downwind.x, p.Downwind.y, p.DriftMs, p.ConvergenceMs));
                cmd.SetComputeVectorParam(compute, "_DWSeaFoamWindrow", new Vector4(p.SpacingM, p.MeanderM, p.MeanderWavelengthM, p.AlongOffsetM));
                cmd.SetComputeVectorParam(compute, "_DWSeaFoamWindrow2", new Vector4(p.SpacingVariation, p.StrengthVariation, 0f, 0f));
                for (int i = 0; i < dispatchesPerFrame; i++)
                {
                    cmd.SetComputeTextureParam(compute, kernel, "_DWSeaFoamPrev", (i & 1) == 0 ? a : b);
                    cmd.SetComputeTextureParam(compute, kernel, "_DWSeaFoamNext", (i & 1) == 0 ? b : a);
                    cmd.DispatchCompute(compute, kernel, (n + 7) / 8, (n + 7) / 8, 1);
                }
                sampler.End(cmd);
                context.ExecuteCommandBuffer(cmd);
            }

            RenderPipelineManager.beginContextRendering += Dispatch;
            Run(() =>
            {
                bool finished = false;
                try
                {
                    if (EditorApplication.timeSinceStartup < start) return false;
                    long ns = recorder.gpuElapsedNanoseconds;   // three frames behind; zero until it arrives
                    if (ns > 0) { sumNs += ns; measured++; }
                    if (measured < frames && EditorApplication.timeSinceStartup < start + 60.0) return false;
                    finished = true;
                    double perDispatch = measured > 0 ? sumNs / measured / dispatchesPerFrame / 1e6 : 0.0;
                    done($"[SeaFoamTools] Foam kernel benchmark — {n}², {count} wave components, {dispatchesPerFrame} dispatches/frame over {measured} frames " +
                         $"({SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsDeviceType}): {perDispatch:0.0000} ms per step" +
                         (measured == 0 ? " — NO GPU TIMINGS (device reports none)" : ""));
                    return true;
                }
                finally
                {
                    if (finished)
                    {
                        RenderPipelineManager.beginContextRendering -= Dispatch;
                        recorder.enabled = false;
                        cmd.Release(); components.Release(); frame.Dispose();
                        a.Release(); b.Release();
                        UnityEngine.Object.Destroy(a); UnityEngine.Object.Destroy(b);
                    }
                }
            });
        }

        static readonly int UnsafeSize = Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<SeaFrameComponent>();

        /// <summary>Average GPU time of the foam pass and HDRP's water passes over <paramref name="frames"/>, then measure the field.</summary>
        public static void MeasureStreaks(int frames, Action<string> done)
        {
            if (!Begin(out var director, out var renderer, done)) return;
            var recorders = new Recorder[GpuSamples.Length];
            var sums = new double[GpuSamples.Length];
            for (int i = 0; i < recorders.Length; i++) { recorders[i] = Recorder.Get(GpuSamples[i]); recorders[i].enabled = true; }
            int seen = 0;
            int lastFrame = Time.frameCount;
            AsyncGPUReadbackRequest? request = null, hdrpRequest = null;
            float[] residual = null;

            Run(() =>
            {
                if (request == null)
                {
                    if (Time.frameCount == lastFrame) return false;
                    lastFrame = Time.frameCount;
                    for (int i = 0; i < recorders.Length; i++) sums[i] += recorders[i].gpuElapsedNanoseconds;
                    if (++seen < frames) return false;
                    request = AsyncGPUReadback.Request(renderer.ResidualFoamTexture);
                    return false;
                }
                if (!request.Value.done) return false;
                if (residual == null)
                {
                    for (int i = 0; i < recorders.Length; i++) recorders[i].enabled = false;
                    if (request.Value.hasError) { done("[SeaFoamTools] GPU readback failed."); return true; }
                    residual = request.Value.GetData<float>().ToArray();
                    // What HDRP actually draws: its own foam buffer, read back right after ours.
                    RenderTexture hdrpFoam = HdrpFoamBuffer(UnityEngine.Object.FindAnyObjectByType<WaterSurface>());
                    if (hdrpFoam != null) { hdrpRequest = AsyncGPUReadback.Request(hdrpFoam); return false; }
                }
                if (hdrpRequest != null && !hdrpRequest.Value.done) return false;

                var sb = new StringBuilder();
                sb.Append(Header("Foam streaks", director, renderer));
                AppendStats(sb, residual, renderer);
                AppendWhitecaps(sb, director, renderer);
                AppendHdrpAgreement(sb, residual, hdrpRequest, renderer);
                sb.Append($"\n  GPU time (mean of {frames} frames, ms, {SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsDeviceName}, game view {Screen.width}×{Screen.height}):");
                for (int i = 0; i < recorders.Length; i++)
                    sb.Append($"\n    {GpuSamples[i],-28} {sums[i] / frames / 1e6:0.000}{(recorders[i].isValid ? "" : "  (sampler not found)")}");
                sb.Append("\n    (residual-foam kernel: Benchmark Foam Kernel — it runs outside HDRP's render context, where no GPU timestamps are taken)");
                if (!SystemInfo.supportsGpuRecorder) sb.Append("\n    (this device reports no GPU recorder timings)");
                done(sb.ToString());
                return true;
            });
        }

        /// <summary>Record <paramref name="steps"/> GPU steps from clear water, replay them on the CPU, compare.</summary>
        public static void VerifyTwin(int steps, Action<string> done)
        {
            if (!Begin(out var director, out var renderer, done)) return;
            renderer.BeginResidualFoamRecording(steps);
            AsyncGPUReadbackRequest? request = null;

            Run(() =>
            {
                var record = renderer.ResidualFoamRecord;
                if (record == null) { done("[SeaFoamTools] Recording was cancelled (renderer disabled?)."); return true; }
                if (request == null)
                {
                    if (record.Count < steps) return false;
                    request = AsyncGPUReadback.Request(renderer.ResidualFoamTexture);
                    return false;
                }
                if (!request.Value.done) return false;
                if (request.Value.hasError) { renderer.EndResidualFoamRecording(); done("[SeaFoamTools] GPU readback failed."); return true; }

                float[] gpu = request.Value.GetData<float>().ToArray();
                float size = renderer.NearFieldTexelM * Mathf.Sqrt(gpu.Length);
                int n = (int)Mathf.Sqrt(gpu.Length);
                double t0 = EditorApplication.timeSinceStartup;
                float[] cpu = Replay(record, n, size);
                double replayS = EditorApplication.timeSinceStartup - t0;
                renderer.EndResidualFoamRecording();

                double sumG = 0, sumC = 0, sumGG = 0, sumCC = 0, sumGC = 0, sumD2 = 0;
                float worst = 0f;
                for (int k = 0; k < gpu.Length; k++)
                {
                    double g = gpu[k], c = cpu[k];
                    sumG += g; sumC += c; sumGG += g * g; sumCC += c * c; sumGC += g * c;
                    sumD2 += (g - c) * (g - c);
                    worst = math.max(worst, (float)math.abs(g - c));
                }
                int m = gpu.Length;
                double cov = sumGC / m - (sumG / m) * (sumC / m);
                double varG = sumGG / m - (sumG / m) * (sumG / m), varC = sumCC / m - (sumC / m) * (sumC / m);
                double corr = cov / math.sqrt(math.max(varG * varC, 1e-30));
                double rmsField = math.sqrt(sumCC / m), rmsDiff = math.sqrt(sumD2 / m);

                var sb = new StringBuilder();
                sb.Append(Header("Foam GPU vs CPU twin", director, renderer));
                sb.Append($"\n  {steps} steps replayed ({record[0].Components.Length} wave components/step, {n}² texels) in {replayS:0.0} s.");
                sb.Append($"\n  correlation {corr:0.000000}; RMS difference {rmsDiff:0.00000} ({rmsDiff / math.max(rmsField, 1e-12) * 100.0:0.000} % of RMS foam); worst texel {worst:0.00000}");
                sb.Append($"\n  mean foam GPU {sumG / m:0.00000} vs CPU {sumC / m:0.00000}");
                done(sb.ToString());
                return true;
            });
        }

        static float[] Replay(System.Collections.Generic.IReadOnlyList<ResidualFoamStepRecord> record, int n, float sizeM)
        {
            var a = new NativeArray<float>(n * n, Allocator.TempJob);
            var b = new NativeArray<float>(n * n, Allocator.TempJob);
            var components = new NativeArray<SeaFrameComponent>(2 * SeaSimulation.MaxComponentsPerState, Allocator.TempJob);
            try
            {
                foreach (ResidualFoamStepRecord step in record)
                {
                    int count = step.Components.Length;
                    NativeArray<SeaFrameComponent>.Copy(step.Components, components, count);
                    new ResidualFoamStepJob
                    {
                        Previous = a, Next = b, Components = components, ComponentCount = count,
                        Resolution = n, SizeM = sizeM, Centre = step.Centre, Shift = step.Shift,
                        DtS = step.DtS, Params = step.Params,
                    }.Schedule(n * n, 512).Complete();
                    if (step.Deposits != null && step.Deposits.Length > 0)
                        ResidualFoamMath.ApplyDeposits(b, n, step.Centre, sizeM, step.Params.Cap, step.Deposits, step.Deposits.Length);
                    (a, b) = (b, a);
                }
                return a.ToArray();
            }
            finally { a.Dispose(); b.Dispose(); components.Dispose(); }
        }

        static void AppendStats(StringBuilder sb, float[] field, HdrpSeaRenderer renderer)
        {
            int n = (int)Mathf.Sqrt(field.Length);
            ResidualFoamParams p = renderer.LastResidualFoamParams;
            ResidualFoamStats s = ResidualFoamStatistics.Measure(field, n, renderer.NearFieldTexelM, p.Downwind);
            float windDeg = math.degrees(math.atan2(p.Downwind.y, p.Downwind.x));
            sb.Append($"\n  residual foam (interior {s.Texels} texels of {renderer.NearFieldTexelM:0.##} m): mean {s.Mean:0.000}, max {s.Max:0.000}; " +
                      $"coverage ≥0.1 {s.CoverageAbove01 * 100f:0.0} %, ≥0.3 {s.CoverageAbove03 * 100f:0.0} %");
            sb.Append($"\n  streaks run at {math.degrees(s.StreakDirectionRad):0.0}° vs wind travel {windDeg:0.0}° → off by {math.degrees(s.AngleToReferenceRad):0.0}°; " +
                      $"coherence {s.Coherence:0.00}; correlation length along {s.AlongCorrelationM:0.0} m / across {s.AcrossCorrelationM:0.0} m " +
                      $"(×{s.AlongCorrelationM / math.max(s.AcrossCorrelationM, 1e-3f):0.0})");
            sb.Append($"\n  flow: drift {p.DriftMs:0.00} m/s, windrow convergence {p.ConvergenceMs:0.000} m/s, spacing {p.SpacingM:0} m, lifetime {p.LifetimeS:0} s");
        }

        // Active whitecaps of the drawn frame: area where the Jacobian is below the calibrated
        // threshold, on a 128² lattice over the interior, against Monahan W for the wind.
        static void AppendWhitecaps(StringBuilder sb, SeaStateDirector director, HdrpSeaRenderer renderer)
        {
            // TempJob: BuildFrame schedules a job on it.
            var frame = new NativeArray<SeaFrameComponent>(2 * SeaSimulation.MaxComponentsPerState, Allocator.TempJob);
            try
            {
                int count = director.Simulation.BuildFrame(director.RenderTimeS, frame);
                float thr = director.Simulation.LastBake.FoamJacobianThreshold;
                float2 c = renderer.ResidualFoamCentre;
                // Same interior as the streak statistics: 0.6 of the region half-size (full near-field weight).
                float half = 0.6f * 0.5f * renderer.NearFieldTexelM * renderer.ResidualFoamTexture.width;
                const int L = 128;
                int below = 0;
                for (int j = 0; j < L; j++)
                for (int i = 0; i < L; i++)
                {
                    float2 x = c + ((new float2(i, j) + 0.5f) / L * 2f - 1f) * half;
                    if (ResidualFoamMath.Jacobian(frame, count, x) < thr) below++;
                }
                var cond = director.Conditions;
                sb.Append($"\n  active whitecaps (J < {thr:0.000}): {below * 100f / (L * L):0.00} % of the interior vs Monahan W {cond.WhitecapFraction * 100f:0.00} % " +
                          $"at {cond.WindSpeedMs:0.0} m/s (one instant, {L * L} points)");
            }
            finally { frame.Dispose(); }
        }

        // HDRP 17.4 keeps its foam buffer internal (WaterSurface.Foam.cs: FoamBuffer()). Read here for
        // verification only; null if a future HDRP renames it, and the report says so.
        static RenderTexture HdrpFoamBuffer(WaterSurface surface)
        {
            if (surface == null) return null;
            MethodInfo m = typeof(WaterSurface).GetMethod("FoamBuffer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return (m?.Invoke(surface, null) as RTHandle)?.rt;
        }

        // Render agrees with the reference: HDRP's surface-foam channel against our residual field at
        // the same world points, over the interior. HDRP holds residual + recent whitecaps, so its mean
        // sits above ours; the correlation says whether the streaks HDRP draws are the ones we computed.
        static void AppendHdrpAgreement(StringBuilder sb, float[] residual, AsyncGPUReadbackRequest? hdrp, HdrpSeaRenderer renderer)
        {
            if (hdrp == null || hdrp.Value.hasError)
            {
                sb.Append("\n  HDRP foam buffer: not readable in this HDRP version — drawn foam not verified.");
                return;
            }
            int n = hdrp.Value.width;
            if (n != (int)Mathf.Sqrt(residual.Length) || hdrp.Value.layerDataSize != n * n * 4)
            {
                sb.Append($"\n  HDRP foam buffer: {n}² × {hdrp.Value.layerDataSize / (n * n)} B/texel, expected R16G16 at the residual's size — not compared.");
                return;
            }
            var data = hdrp.Value.GetData<ushort>();
            float size = renderer.NearFieldTexelM * n;
            float2 offset = (renderer.NearFieldCentre - renderer.ResidualFoamCentre) / size;   // HDRP UV → residual UV
            int lo = (int)(0.2f * n), hi = n - lo;
            double sh = 0, sr = 0, shh = 0, srr = 0, shr = 0;
            int m = 0, above = 0;
            for (int j = lo; j < hi; j++)
            for (int i = lo; i < hi; i++)
            {
                double h = Mathf.HalfToFloat(data[(j * n + i) * 2]);
                float2 q = (new float2(i, j) + 0.5f) + offset * n - 0.5f;   // residual texel coordinates
                double r = Bilinear(residual, n, q);
                sh += h; sr += r; shh += h * h; srr += r * r; shr += h * r; m++;
                if (h >= 0.1) above++;
            }
            double cov = shr / m - (sh / m) * (sr / m);
            double vh = shh / m - (sh / m) * (sh / m), vr = srr / m - (sr / m) * (sr / m);
            double corr = cov / math.sqrt(math.max(vh * vr, 1e-30));
            sb.Append($"\n  HDRP foam buffer (drawn): mean {sh / m:0.000} vs residual {sr / m:0.000}; ≥0.1 {above * 100.0 / m:0.0} %; " +
                      $"correlation with the residual field {corr:0.000} ({m} interior texels)");
        }

        static float Bilinear(float[] g, int n, float2 q)
        {
            q = math.clamp(q, 0f, n - 1.001f);
            int x0 = (int)q.x, z0 = (int)q.y;
            float fx = q.x - x0, fz = q.y - z0;
            float a = math.lerp(g[z0 * n + x0], g[z0 * n + x0 + 1], fx);
            float b = math.lerp(g[(z0 + 1) * n + x0], g[(z0 + 1) * n + x0 + 1], fx);
            return math.lerp(a, b, fz);
        }

        static string Header(string what, SeaStateDirector director, HdrpSeaRenderer renderer)
        {
            var c = director.Conditions;
            return $"[SeaFoamTools] {what} — Douglas {c.DouglasSea} + swell {c.DouglasSwell}, wind {c.WindSpeedMs:0.0} m/s from {c.WindFromDeg:0}°, " +
                   $"Hs {c.SignificantWaveHeightM:0.00} m; residual foam {(renderer.ResidualFoamActive ? "on" : "OFF")}.";
        }

        static bool Begin(out SeaStateDirector director, out HdrpSeaRenderer renderer, Action<string> done)
        {
            director = UnityEngine.Object.FindAnyObjectByType<SeaStateDirector>();
            renderer = UnityEngine.Object.FindAnyObjectByType<HdrpSeaRenderer>();
            if (!Application.isPlaying || director == null || !director.IsReady || renderer == null || !renderer.ResidualFoamActive)
            {
                done("[SeaFoamTools] Needs play mode with a ready SeaStateDirector and an HdrpSeaRenderer running residual foam.");
                return false;
            }
            if (IsRunning) { done("[SeaFoamTools] A foam check is already running."); return false; }
            return true;
        }

        static void Run(Func<bool> step)
        {
            s_Running = () =>
            {
                bool finished;
                try { finished = step(); }
                catch (Exception e) { Debug.LogException(e); finished = true; }
                if (finished) { EditorApplication.update -= s_Running; s_Running = null; }
            };
            EditorApplication.update += s_Running;
        }
    }
}
