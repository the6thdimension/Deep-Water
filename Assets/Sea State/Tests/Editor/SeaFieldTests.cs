using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using DeepWater.Sea.Core;

namespace DeepWater.Sea.Tests
{
    /// <summary>
    /// The wave field and the simulation: determinism, frame-rate independence, statistics,
    /// the Lagrangian inverse, calm seas and cross-fades. Pure core — no scene, no renderer.
    /// </summary>
    public class SeaFieldTests
    {
        static readonly float3[] Probes =
        {
            new float3(0f, 0f, 0f), new float3(37.5f, 0f, -12f), new float3(-150f, 0f, 220f),
            new float3(1234f, 0f, -987f), new float3(5f, 0f, 5f),
        };

        [Test]
        public void CalmSea_IsFlat()
        {
            using (var sim = new SeaSimulation(2f, 4))
            {
                var calm = SeaTestFixtures.Douglas5();
                calm.WindSea.SignificantHeightM = 0f;
                sim.Apply(calm, default, 0f);
                for (int i = 0; i < 10; i++) sim.Step(0.02);

                var s = sim.Sample(new float3(10f, 0f, -3f));
                Assert.AreEqual(2f, s.SurfaceHeight);
                Assert.AreEqual(new float3(0f, 1f, 0f), s.Normal);
                Assert.AreEqual(float3.zero, s.Velocity);
                Assert.AreEqual(1f, s.Jacobian);
            }
        }

        [Test]
        public void SameSeaAndSteps_GiveBitwiseIdenticalQueries()
        {
            float[] a = RunAndSample(SeaTestFixtures.Douglas6WithSwell(), 0.02, 500);
            float[] b = RunAndSample(SeaTestFixtures.Douglas6WithSwell(), 0.02, 500);
            for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i], b[i], 0f, $"value {i}");
        }

        [Test]
        public void QueryResults_DoNotDependOnBatchCompositionOrOrder()
        {
            using (var sim = new SeaSimulation(0f, 4))
            {
                sim.Apply(SeaTestFixtures.Douglas6WithSwell(), default, 0f);
                for (int i = 0; i < 123; i++) sim.Step(0.02);

                var positions = new NativeArray<float3>(Probes, Allocator.TempJob);
                var results = new NativeArray<SeaSample>(Probes.Length, Allocator.TempJob);
                try
                {
                    sim.Query(positions, results);
                    for (int i = 0; i < Probes.Length; i++)
                    {
                        var single = sim.Sample(Probes[i]);
                        Assert.AreEqual(results[i].SurfaceHeight, single.SurfaceHeight, 0f);
                        Assert.AreEqual(results[i].Normal, single.Normal);
                    }
                }
                finally { results.Dispose(); positions.Dispose(); }
            }
        }

        [Test]
        public void FiftyAndHundredHertzStepping_AgreeAtSharedTimes()
        {
            // The sea is a function of simulation time only — not of how it was stepped.
            using (var slow = new SeaSimulation(0f, 4))
            using (var fast = new SeaSimulation(0f, 4))
            {
                slow.Apply(SeaTestFixtures.Douglas6WithSwell(), default, 0f);
                fast.Apply(SeaTestFixtures.Douglas6WithSwell(), default, 0f);
                for (int second = 1; second <= 30; second++)
                {
                    for (int i = 0; i < 50; i++) slow.Step(0.02);
                    for (int i = 0; i < 100; i++) fast.Step(0.01);
                    Assert.AreEqual(slow.SimulationTimeS, fast.SimulationTimeS, 1e-9);
                    foreach (var p in Probes)
                        Assert.AreEqual(slow.Sample(p).SurfaceHeight, fast.Sample(p).SurfaceHeight, 1e-4f,
                            $"t = {second} s at {p}");
                }
            }
        }

        [Test]
        public void LinearField_TimeSeriesHs_MatchesResolvedHs()
        {
            // Q = 0 (Airy): the Eulerian record at a point is Gaussian with variance Σa²/2.
            var input = SeaTestFixtures.Douglas5();
            input.WindSea.Choppiness = 0f;
            double hs4Sigma, resolved;
            using (var sim = new SeaSimulation(0f, 4))
            {
                resolved = sim.Apply(input, default, 0f).ResolvedHsM;
                hs4Sigma = FourSigmaOfRecord(sim, new float3(17f, 0f, -40f), 0.5, 48000);
            }
            Assert.AreEqual(resolved, hs4Sigma, 0.05 * resolved);
        }

        [Test]
        public void ChoppyField_TimeSeriesHs_StaysCloseToResolvedHs()
        {
            // Lagrangian waves change the Eulerian height distribution at second order only.
            double hs4Sigma, resolved;
            using (var sim = new SeaSimulation(0f, 4))
            {
                resolved = sim.Apply(SeaTestFixtures.Douglas5(), default, 0f).ResolvedHsM;
                hs4Sigma = FourSigmaOfRecord(sim, new float3(17f, 0f, -40f), 0.5, 48000);
            }
            Assert.AreEqual(resolved, hs4Sigma, 0.08 * resolved);
        }

        [Test]
        public void LagrangianInverse_ResidualUnderOneCentimetre_AtDouglas6()
        {
            using (var sim = new SeaSimulation(0f, 4))
            {
                sim.Apply(SeaTestFixtures.Douglas6WithSwell(), default, 0f);
                for (int i = 0; i < 777; i++) sim.Step(0.02);

                var frame = new NativeArray<SeaFrameComponent>(2 * SeaSimulation.MaxComponentsPerState, Allocator.TempJob);
                try
                {
                    int n = sim.BuildFrame(sim.SimulationTimeS, frame);
                    float worst = 0f;
                    var rng = new Random(99u);
                    for (int i = 0; i < 400; i++)
                    {
                        float2 x = rng.NextFloat2(-500f, 500f);
                        float2 x0 = SeaFieldMath.InverseHorizontal(frame, n, x, 4);
                        float2 back = x0 + SeaFieldMath.HorizontalDisplacement(frame, n, x0);
                        worst = math.max(worst, math.length(back - x));
                    }
                    Assert.Less(worst, 0.01f, $"worst residual {worst:0.0000} m");
                }
                finally { frame.Dispose(); }
            }
        }

        [Test]
        public void CrossFade_EndsExactlyOnTheNewSea()
        {
            using (var faded = new SeaSimulation(0f, 4))
            using (var direct = new SeaSimulation(0f, 4))
            {
                faded.Apply(SeaTestFixtures.Douglas5(), default, 0f);
                for (int i = 0; i < 100; i++) faded.Step(0.02);
                faded.Apply(SeaTestFixtures.Douglas6WithSwell(), default, 4f);
                for (int i = 0; i < 50; i++) faded.Step(0.02);    // 1 s into a 4 s fade
                Assert.AreEqual(0.25f, faded.TransitionProgress, 1e-4f);
                Assert.Greater(faded.ActiveComponentCount, faded.LastBake.Count, "both seas are live mid-fade");

                for (int i = 0; i < 250; i++) faded.Step(0.02);   // 6 s total > 4 s fade
                Assert.AreEqual(1f, faded.TransitionProgress);
                Assert.AreEqual(faded.LastBake.Count, faded.ActiveComponentCount);

                direct.Apply(SeaTestFixtures.Douglas6WithSwell(), default, 0f);
                for (int i = 0; i < 400; i++) direct.Step(0.02);

                foreach (var p in Probes)
                    Assert.AreEqual(direct.Sample(p).SurfaceHeight, faded.Sample(p).SurfaceHeight, 1e-5f);
            }
        }

        [Test]
        public void FoamCalibration_HitsTheTargetCoverage()
        {
            var input = SeaTestFixtures.Douglas6WithSwell();
            var comps = new NativeArray<WaveComponent>(SeaSimulation.MaxComponentsPerState, Allocator.TempJob);
            try
            {
                var report = SeaBaker.Bake(input, comps);
                Assert.Greater(report.FoamJacobianThreshold, FoamCalibration.NoFoamThreshold);

                // Independent check on a different grid and time: coverage ≈ target.
                var frame = new NativeArray<SeaFrameComponent>(report.Count, Allocator.TempJob);
                try
                {
                    for (int i = 0; i < report.Count; i++) frame[i] = SeaFieldMath.Prepare(comps[i], 3.7, 1f);
                    int below = 0, total = 0;
                    for (int y = 0; y < 80; y++)
                    for (int x = 0; x < 80; x++)
                    {
                        SeaFieldMath.Evaluate(frame, report.Count, new float2(x * 7.3f + 3f, y * 7.3f - 400f),
                            out _, out _, out _, out _, out float j);
                        if (j < report.FoamJacobianThreshold) below++;
                        total++;
                    }
                    float coverage = (float)below / total;
                    Assert.AreEqual(input.WhitecapFraction, coverage, 0.5f * input.WhitecapFraction,
                        $"coverage {coverage:P2} vs target {input.WhitecapFraction:P2}");
                }
                finally { frame.Dispose(); }
            }
            finally { comps.Dispose(); }
        }

        [Test]
        public void PackedKernel_MatchesReference()
        {
            // The SIMD kernel is an optimisation, not a new model: it must agree with the
            // reference layout (which the GPU decal also uses) to float rounding.
            using (var sim = new SeaSimulation(0f, 4))
            {
                sim.Apply(SeaTestFixtures.Douglas6WithSwell(), default, 0f);
                for (int i = 0; i < 321; i++) sim.Step(0.02);

                const int n = 500;
                var frame = new NativeArray<SeaFrameComponent>(2 * SeaSimulation.MaxComponentsPerState, Allocator.TempJob);
                var positions = new NativeArray<float3>(n, Allocator.TempJob);
                var packed = new NativeArray<SeaSample>(n, Allocator.TempJob);
                var reference = new NativeArray<SeaSample>(n, Allocator.TempJob);
                try
                {
                    int count = sim.BuildFrame(sim.SimulationTimeS, frame);
                    var rng = new Random(5u);
                    for (int i = 0; i < n; i++) positions[i] = new float3(rng.NextFloat(-2000f, 2000f), 0f, rng.NextFloat(-2000f, 2000f));

                    sim.Query(positions, packed);
                    new SeaQueryReferenceJob
                    {
                        Components = frame, ComponentCount = count, SeaLevel = 0f, InverseIterations = 4,
                        Positions = positions, Results = reference,
                    }.Run(n);

                    float worstH = 0f, worstV = 0f, worstJ = 0f, worstCos = 1f;
                    for (int i = 0; i < n; i++)
                    {
                        worstH = math.max(worstH, math.abs(packed[i].SurfaceHeight - reference[i].SurfaceHeight));
                        worstV = math.max(worstV, math.length(packed[i].Velocity - reference[i].Velocity));
                        worstJ = math.max(worstJ, math.abs(packed[i].Jacobian - reference[i].Jacobian));
                        worstCos = math.min(worstCos, math.dot(packed[i].Normal, reference[i].Normal));
                    }
                    Assert.Less(worstH, 1e-3f, $"height {worstH} m");
                    Assert.Less(worstV, 1e-3f, $"velocity {worstV} m/s");
                    Assert.Less(worstJ, 1e-4f, $"Jacobian {worstJ}");
                    Assert.Greater(worstCos, 0.999999f, $"normal cos {worstCos}");
                }
                finally { reference.Dispose(); packed.Dispose(); positions.Dispose(); frame.Dispose(); }
            }
        }

        static float[] RunAndSample(SeaBakeInput input, double dt, int steps)
        {
            using (var sim = new SeaSimulation(0f, 4))
            {
                sim.Apply(input, default, 0f);
                var values = new float[Probes.Length * 5];
                int k = 0;
                for (int i = 0; i < steps; i++) sim.Step(dt);
                foreach (var p in Probes)
                {
                    var s = sim.Sample(p);
                    values[k++] = s.SurfaceHeight;
                    values[k++] = s.Normal.x;
                    values[k++] = s.Normal.z;
                    values[k++] = s.Velocity.y;
                    values[k++] = s.Jacobian;
                }
                return values;
            }
        }

        static double FourSigmaOfRecord(SeaSimulation sim, float3 point, double dt, int samples)
        {
            double sum = 0.0, sumSq = 0.0;
            for (int i = 0; i < samples; i++)
            {
                sim.Step(dt);
                double h = sim.Sample(point).SurfaceHeight;
                sum += h;
                sumSq += h * h;
            }
            double mean = sum / samples;
            return 4.0 * math.sqrt(sumSq / samples - mean * mean);
        }
    }
}
