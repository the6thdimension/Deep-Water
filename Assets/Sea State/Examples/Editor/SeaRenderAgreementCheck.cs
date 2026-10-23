using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using DeepWater.Sea.Core;

namespace DeepWater.Sea.Examples.Editor
{
    /// <summary>
    /// Verifies that what HDRP draws is the water physics floats on (methodology C8): reads the
    /// rendered deformation back through HDRP's own water search and compares it with the
    /// physics field at the same instant and points.
    ///
    /// **Know the instrument.** HDRP's CPU readback (HDRP 17.4, SimulationCPU.Deformation.cs)
    /// samples the decal deformation as one vertical channel at the query point; it ignores the
    /// horizontal deformation the GPU applies. So the like-for-like comparison is against the
    /// physics vertical displacement at the query point (no Lagrangian inverse). The comparison
    /// against the true (inverted) surface is reported too, and differs by exactly what HDRP's
    /// readback leaves out.
    ///
    /// Use in play mode: <see cref="Prepare"/> freezes time and turns on readback; call
    /// <see cref="Run"/> a few frames later.
    /// </summary>
    public static class SeaRenderAgreementCheck
    {
        [MenuItem("Deep Water/Sea State/Verify Render vs Physics (play mode)")]
        static void Menu()
        {
            if (!Application.isPlaying) { Debug.LogError("[SeaRenderAgreementCheck] Enter play mode first."); return; }
            Prepare();
            int frames = 0;
            EditorApplication.CallbackFunction wait = null;
            wait = () =>
            {
                if (++frames < 10) return;
                EditorApplication.update -= wait;
                Debug.Log(Run(200, 250f, 11u));
                Time.timeScale = 1f;
            };
            EditorApplication.update += wait;
        }

        /// <summary>Freeze time and turn on HDRP readback for the water surface.</summary>
        public static void Prepare()
        {
            var surface = Object.FindFirstObjectByType<WaterSurface>();
            if (surface != null) surface.scriptInteractions = true;
            Time.timeScale = 0f;
        }

        /// <summary>
        /// Compare rendered vs physics heights at <paramref name="points"/> random points within
        /// <paramref name="radiusM"/> of the near-field centre (the snapped camera position).
        /// </summary>
        public static string Run(int points, float radiusM, uint seed)
        {
            var surface = Object.FindFirstObjectByType<WaterSurface>();
            var director = Object.FindFirstObjectByType<SeaStateDirector>();
            if (surface == null || director == null || !director.IsReady || Camera.main == null)
                return "[SeaRenderAgreementCheck] Needs a WaterSurface, a ready SeaStateDirector and a main camera.";

            var renderer = Object.FindFirstObjectByType<Modules.Hdrp.HdrpSeaRenderer>();
            float texel = renderer != null ? renderer.NearFieldTexelM : 2f;
            Vector3 cam = Camera.main.transform.position;
            float2 centre = math.round(new float2(cam.x, cam.z) / texel) * texel;
            float seaLevel = director.Simulation.SeaLevel;
            int iterations = director.Simulation.InverseIterations;

            var frame = new NativeArray<SeaFrameComponent>(2 * SeaSimulation.MaxComponentsPerState, Allocator.TempJob);
            try
            {
                int n = director.Simulation.BuildFrame(director.RenderTimeS, frame);
                var rng = new Unity.Mathematics.Random(seed);
                var vertical = new Stats();
                var surfaceTrue = new Stats();
                for (int i = 0; i < points; i++)
                {
                    float2 p = centre + rng.NextFloat2(-radiusM, radiusM);
                    var wsp = new WaterSearchParameters
                    {
                        startPositionWS = new float3(p.x, 0f, p.y), targetPositionWS = new float3(p.x, 0f, p.y),
                        error = 0.005f, maxIterations = 16, includeDeformation = true, excludeSimulation = false,
                    };
                    if (!surface.ProjectPointOnWaterSurface(wsp, out WaterSearchResult wsr)) continue;
                    double rendered = wsr.projectedPositionWS.y;

                    SeaFieldMath.Evaluate(frame, n, p, out float atQueryPoint, out _, out _, out _, out _);
                    float2 x0 = SeaFieldMath.InverseHorizontal(frame, n, p, iterations);
                    SeaFieldMath.Evaluate(frame, n, x0, out float onSurface, out _, out _, out _, out _);

                    vertical.Add(rendered, seaLevel + atQueryPoint);
                    surfaceTrue.Add(rendered, seaLevel + onSurface);
                }
                if (vertical.Count < 2) return "[SeaRenderAgreementCheck] HDRP readback returned no points yet — wait a few frames.";

                var c = director.Conditions;
                return $"[SeaRenderAgreementCheck] Douglas {c.DouglasSea} + swell {c.DouglasSwell}, {n} components, {vertical.Count} points within {radiusM:0} m.\n" +
                       $"  like-for-like (vertical channel at query point): {vertical}\n" +
                       $"  vs true choppy surface (HDRP readback omits horizontal deformation): {surfaceTrue}";
            }
            finally
            {
                frame.Dispose();
            }
        }

        struct Stats
        {
            double _sumAbs, _maxAbs, _sR, _sP, _sRR, _sPP, _sRP;
            public int Count;

            public void Add(double rendered, double physics)
            {
                double e = System.Math.Abs(rendered - physics);
                _sumAbs += e; _maxAbs = System.Math.Max(_maxAbs, e);
                _sR += rendered; _sP += physics; _sRR += rendered * rendered; _sPP += physics * physics; _sRP += rendered * physics;
                Count++;
            }

            public override string ToString()
            {
                double n = Count;
                double cov = _sRP / n - (_sR / n) * (_sP / n);
                double vr = _sRR / n - (_sR / n) * (_sR / n);
                double vp = _sPP / n - (_sP / n) * (_sP / n);
                return $"corr {cov / System.Math.Sqrt(vr * vp):F5}, mean |Δ| {_sumAbs / n:F3} m, max |Δ| {_maxAbs:F3} m, " +
                       $"σ rendered {System.Math.Sqrt(vr):F3} m / physics {System.Math.Sqrt(vp):F3} m";
            }
        }
    }
}
