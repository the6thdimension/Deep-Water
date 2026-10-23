using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using DeepWater.Sea.Core;
using DeepWater.Sea.Modules.Hdrp;

namespace DeepWater.Sea.Examples.Editor
{
    /// <summary>
    /// Play-mode measurement of the rendered sea's significant wave height (4σ of surface height,
    /// read back through HDRP's own water search) in three zones around the camera:
    /// near (simulated waves), handover (both fields cross-fading) and far (HDRP's FFT only) —
    /// and the calibration that makes HDRP's far field produce a requested Hs.
    ///
    /// Both run frame by frame (EditorApplication.update) so the sea moves between samples.
    ///
    /// Readback caveat (HDRP 17.4, verified in source): the CPU water search samples decal
    /// deformation with REPEAT addressing, the GPU with CLAMP. Outside the decal square the
    /// readback would tile the near field across the horizon although nothing of the kind is
    /// drawn, so deformation is only included for points inside the square.
    /// </summary>
    public static class SeaFarFieldTools
    {
        // Zones in units of the near-field half-size H (Chebyshev for near/handover, radial for far).
        const float NearMax = 0.5f, HandoverMin = 0.65f, HandoverMax = 0.9f, FarMin = 1.4f, FarMax = 2.7f;

        static readonly float[] CalibrationWindsKmh = { 10, 15, 20, 25, 35, 45, 60, 75, 95, 110, 130, 160, 200, 250 };
        static readonly float[] CalibrationRepetitionsM = { 250, 400, 600, 900, 1300, 1900 };

        static EditorApplication.CallbackFunction s_Running;

        public static bool IsRunning => s_Running != null;

        [MenuItem("Deep Water/Sea State/Measure Hs by Zone (play mode)")]
        static void MeasureMenu() => MeasureZones(12, 300, 0.6f, report => Debug.Log(report));

        [MenuItem("Deep Water/Sea State/Calibrate Far Field (play mode)")]
        static void CalibrateMenu() => CalibrateFarField(report => Debug.Log(report));

        /// <summary>
        /// Sample Hs in the near, handover and far zones at <paramref name="instants"/> moments
        /// <paramref name="spacingS"/> apart, <paramref name="pointsPerZone"/> points each.
        /// Reports pooled Hs and the spread of the per-instant values (the far field's own
        /// fluctuation, not measurement noise alone).
        /// </summary>
        public static void MeasureZones(int instants, int pointsPerZone, float spacingS, Action<string> done)
        {
            if (!Begin(out var surface, out var director, out var renderer, done)) return;
            HdrpSeaLookSO look = LookOf(renderer);
            float h = 0.5f * look.nearFieldSizeM;
            var near = new Zone(); var handover = new Zone(); var far = new Zone();
            // Physics at the same near-zone points: the near field is one phase-resolved
            // realization over a few wavelengths, so its local Hs beats with the wave groups.
            // Rendered vs physics there is the like-for-like check; vs target is the realization.
            var nearPhysics = new Zone();
            int taken = 0;
            double next = EditorApplication.timeSinceStartup + 1.0;
            var rng = new Unity.Mathematics.Random(31u);

            Run(() =>
            {
                if (EditorApplication.timeSinceStartup < next) return false;
                next = EditorApplication.timeSinceStartup + spacingS;
                float2 c = Centre(renderer);
                near.Instant(SampleChebyshev(surface, c, 0f, h * NearMax, h, pointsPerZone, ref rng, director.Simulation, out Moments physics));
                nearPhysics.Instant(physics);
                handover.Instant(SampleChebyshev(surface, c, h * HandoverMin, h * HandoverMax, h, pointsPerZone, ref rng, null, out _));
                far.Instant(SampleRadial(surface, c, h * FarMin, h * FarMax, h, pointsPerZone, ref rng));
                if (++taken < instants) return false;

                var cond = director.Conditions;
                float target = cond.SignificantWaveHeightM;
                done($"[SeaFarFieldTools] Hs by zone — Douglas {cond.DouglasSea} + swell {cond.DouglasSwell}; target Hs {target:0.00} m " +
                     $"(resolved near field {cond.ResolvedWaveHeightM:0.00} m); far field {(renderer.FarFieldCalibrated ? "calibrated" : "UNCALIBRATED")} " +
                     $"(wind {renderer.FarFieldWindKmh:0} km/h × {renderer.FarFieldMultiplier:0.000}); handover {(look.energyPreservingHandover ? "energy-preserving" : "linear")}; " +
                     $"{instants} instants × {pointsPerZone} points, {spacingS:0.##} s apart.\n" +
                     near.Line($"near     (≤{NearMax * h:0} m)", target) + "\n" +
                     nearPhysics.Line("  physics, same points", target) +
                     $"  → rendered/physics {near.Pooled.Hs / math.max(nearPhysics.Pooled.Hs, 1e-6f) * 100f:0.0} %\n" +
                     handover.Line($"handover ({HandoverMin * h:0}–{HandoverMax * h:0} m)", target) + "\n" +
                     far.Line($"far      ({FarMin * h:0}–{FarMax * h:0} m)", target));
                return true;
            });
        }

        /// <summary>
        /// Measure HDRP's far-field Hs (band multipliers 1, the look's chaos) over a wind × patch
        /// size grid and store it in the look's <see cref="FarFieldCalibration"/>. Each cell is the
        /// pooled Hs of <c>Instants</c> instants spread over several seconds: a small patch holds
        /// few dominant modes, so its instantaneous Hs beats by ±10 %.
        /// </summary>
        public static void CalibrateFarField(Action<string> done)
        {
            if (!Begin(out var surface, out var director, out var renderer, done)) return;
            HdrpSeaLookSO look = LookOf(renderer);
            float h = 0.5f * look.nearFieldSizeM;
            int nw = CalibrationWindsKmh.Length, nr = CalibrationRepetitionsM.Length;
            const int Instants = 12, Points = 300;
            const double SettleS = 0.4, SpacingS = 0.5;
            var hs = new float[nw * nr];
            var spread = new float[nw * nr];
            // Cell −1 measures ripples alone (band multipliers 0): the floor under every cell.
            int cell = -2, instant = Instants;
            double next = 0;
            float ripplesHs = 0f;
            var zone = new Zone();
            var rng = new Unity.Mathematics.Random(77u);
            float savedWind = surface.largeWindSpeed, savedRep = surface.repetitionSize;
            float savedB0 = surface.largeBand0Multiplier, savedB1 = surface.largeBand1Multiplier;
            double t0 = EditorApplication.timeSinceStartup;

            Run(() =>
            {
                double now = EditorApplication.timeSinceStartup;
                if (instant >= Instants)
                {
                    if (cell == -1) ripplesHs = zone.Pooled.Hs;
                    else if (cell >= 0) { hs[cell] = zone.Pooled.Hs; spread[cell] = zone.RelativeSpread; }
                    cell++;
                    if (cell >= nw * nr)
                    {
                        look.farFieldCalibration = new FarFieldCalibration
                        {
                            windKmh = (float[])CalibrationWindsKmh.Clone(),
                            repetitionM = (float[])CalibrationRepetitionsM.Clone(),
                            hsM = hs,
                            chaos = look.farChaos,
                            provenance = $"Measured {DateTime.Now:yyyy-MM-dd HH:mm} by SeaFarFieldTools.CalibrateFarField: HDRP 17.4 CPU water search " +
                                         $"(deformation excluded), {Instants} instants {SpacingS:0.#} s apart × {Points} points per cell, " +
                                         $"radial {FarMin * h:0}–{FarMax * h:0} m; band multipliers 1, chaos {look.farChaos:0.##}; " +
                                         $"ripples alone read {ripplesHs:0.000} m (the CPU search does not evaluate ripples). " +
                                         $"Per-instant spread median {Median(spread) * 100f:0} % of Hs.",
                        };
                        EditorUtility.SetDirty(look);
                        AssetDatabase.SaveAssets();
                        surface.largeWindSpeed = savedWind; surface.repetitionSize = savedRep;
                        surface.largeBand0Multiplier = savedB0; surface.largeBand1Multiplier = savedB1;
                        done($"[SeaFarFieldTools] Far-field calibration stored: {nw}×{nr} cells in {now - t0:0} s, Hs {Min(hs):0.00}–{Max(hs):0.00} m; " +
                             $"ripples alone {ripplesHs:0.000} m; per-instant spread median {Median(spread) * 100f:0} %, max {Max(spread) * 100f:0} %.\n" + Table(hs));
                        return true;
                    }
                    bool ripplesOnly = cell < 0;
                    surface.largeWindSpeed = ripplesOnly ? savedWind : CalibrationWindsKmh[cell / nr];
                    surface.repetitionSize = ripplesOnly ? savedRep : CalibrationRepetitionsM[cell % nr];
                    surface.largeBand0Multiplier = ripplesOnly ? 0f : 1f;
                    surface.largeBand1Multiplier = ripplesOnly ? 0f : 1f;
                    surface.largeChaos = look.farChaos;
                    zone = new Zone();
                    instant = 0;
                    next = now + SettleS;
                    return false;
                }
                if (now < next) return false;
                next = now + SpacingS;
                zone.Instant(SampleRadial(surface, Centre(renderer), h * FarMin, h * FarMax, h, Points, ref rng));
                instant++;
                return false;
            });
        }

        static string Table(float[] hs)
        {
            var sb = new System.Text.StringBuilder("  wind\\rep");
            foreach (float r in CalibrationRepetitionsM) sb.Append($"{r,8:0}");
            for (int w = 0; w < CalibrationWindsKmh.Length; w++)
            {
                sb.Append($"\n  {CalibrationWindsKmh[w],7:0}  ");
                for (int r = 0; r < CalibrationRepetitionsM.Length; r++) sb.Append($"{hs[w * CalibrationRepetitionsM.Length + r],8:0.00}");
            }
            return sb.ToString();
        }

        static bool Begin(out WaterSurface surface, out SeaStateDirector director, out HdrpSeaRenderer renderer, Action<string> done)
        {
            surface = UnityEngine.Object.FindAnyObjectByType<WaterSurface>();
            director = UnityEngine.Object.FindAnyObjectByType<SeaStateDirector>();
            renderer = UnityEngine.Object.FindAnyObjectByType<HdrpSeaRenderer>();
            if (!Application.isPlaying || surface == null || director == null || !director.IsReady || renderer == null)
            {
                done("[SeaFarFieldTools] Needs play mode with a WaterSurface, a ready SeaStateDirector and an HdrpSeaRenderer.");
                return false;
            }
            if (IsRunning) { done("[SeaFarFieldTools] A measurement is already running."); return false; }
            surface.scriptInteractions = true;   // HDRP readback
            Time.timeScale = 1f;
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

        static HdrpSeaLookSO LookOf(HdrpSeaRenderer renderer)
            => (HdrpSeaLookSO)new SerializedObject(renderer).FindProperty("look").objectReferenceValue;

        static float2 Centre(HdrpSeaRenderer renderer)
        {
            float texel = renderer.NearFieldTexelM;
            Vector3 cam = Camera.main.transform.position;
            return math.round(new float2(cam.x, cam.z) / texel) * texel;
        }

        static Moments SampleChebyshev(WaterSurface surface, float2 c, float rMin, float rMax, float halfSize, int n,
            ref Unity.Mathematics.Random rng, SeaSimulation physics, out Moments physicsMoments)
        {
            var m = new Moments();
            physicsMoments = new Moments();
            int guard = 0;
            while (m.Count < n && guard++ < n * 20)
            {
                float2 o = rng.NextFloat2(-rMax, rMax);
                if (math.cmax(math.abs(o)) < rMin) continue;
                if (!Height(surface, c, o, halfSize, out float y)) continue;
                m.Add(y);
                if (physics != null)
                {
                    float2 p = c + o;
                    physicsMoments.Add(physics.Sample(new float3(p.x, 0f, p.y)).SurfaceHeight);
                }
            }
            return m;
        }

        static Moments SampleRadial(WaterSurface surface, float2 c, float rMin, float rMax, float halfSize, int n,
            ref Unity.Mathematics.Random rng)
        {
            var m = new Moments();
            for (int i = 0; i < n; i++)
            {
                float a = rng.NextFloat(0f, 2f * math.PI);
                float r = math.sqrt(rng.NextFloat(rMin * rMin, rMax * rMax));   // uniform over the annulus
                if (Height(surface, c, r * new float2(math.cos(a), math.sin(a)), halfSize, out float y)) m.Add(y);
            }
            return m;
        }

        /// <summary>Surface height at centre + offset; decal deformation only inside the decal square (GPU clamp).</summary>
        static bool Height(WaterSurface surface, float2 centre, float2 offset, float halfSize, out float y)
        {
            float2 p = centre + offset;
            var wsp = new WaterSearchParameters
            {
                startPositionWS = new float3(p.x, 0f, p.y), targetPositionWS = new float3(p.x, 0f, p.y),
                error = 0.01f, maxIterations = 12,
                includeDeformation = math.cmax(math.abs(offset)) < halfSize, excludeSimulation = false,
            };
            bool ok = surface.ProjectPointOnWaterSurface(wsp, out WaterSearchResult r);
            y = r.projectedPositionWS.y;
            return ok;
        }

        struct Moments
        {
            double _sum, _sumSq;
            public int Count;
            public void Add(double v) { _sum += v; _sumSq += v * v; Count++; }
            public void Add(Moments o) { _sum += o._sum; _sumSq += o._sumSq; Count += o.Count; }
            public float Hs => Count < 2 ? 0f : (float)(4.0 * Math.Sqrt(Math.Max(0.0, _sumSq / Count - (_sum / Count) * (_sum / Count))));
        }

        /// <summary>Pooled moments plus the per-instant Hs values, so the report can show their spread.</summary>
        sealed class Zone
        {
            public Moments Pooled;
            readonly List<float> _instantHs = new List<float>();

            public void Instant(Moments m) { Pooled.Add(m); _instantHs.Add(m.Hs); }

            /// <summary>Standard deviation of the per-instant Hs over the pooled Hs.</summary>
            public float RelativeSpread
            {
                get
                {
                    if (_instantHs.Count < 2 || Pooled.Hs <= 0f) return 0f;
                    double mean = 0, sq = 0;
                    foreach (float v in _instantHs) mean += v;
                    mean /= _instantHs.Count;
                    foreach (float v in _instantHs) sq += (v - mean) * (v - mean);
                    return (float)(Math.Sqrt(sq / (_instantHs.Count - 1)) / Pooled.Hs);
                }
            }

            public string Line(string label, float target)
            {
                float hs = Pooled.Hs;
                string vs = target > 0f ? $"{hs / target * 100f:0} % of target" : "—";
                return $"  {label,-24} Hs {hs:0.00} m  ({vs}; per-instant spread ±{RelativeSpread * 100f:0} %, {Pooled.Count} samples)";
            }
        }

        static float Min(float[] a) { float m = float.MaxValue; foreach (var v in a) m = Mathf.Min(m, v); return m; }
        static float Max(float[] a) { float m = float.MinValue; foreach (var v in a) m = Mathf.Max(m, v); return m; }
        static float Median(float[] a) { var s = (float[])a.Clone(); Array.Sort(s); return s.Length == 0 ? 0f : s[s.Length / 2]; }
    }
}
