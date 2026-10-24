using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using DeepWater.Sea.Core;

namespace DeepWater.Sea.Tests
{
    /// <summary>
    /// Residual foam (CPU reference of SeaFoam.compute): advection without smearing, decay,
    /// exact region shifts, production only where the surface breaks, and windrows that
    /// gather foam into streaks running with the wind. Pure core — no GPU, no scene.
    /// </summary>
    public class ResidualFoamTests
    {
        const int N = 128;
        const float SizeM = 256f;            // 2 m texels, as the near field at 1024 m / 512
        const float TexelM = SizeM / N;

        static ResidualFoamParams Still(float lifetime = 1e9f) => new ResidualFoamParams
        {
            Downwind = new float2(1f, 0f),
            DriftMs = 0f,
            ConvergenceMs = 0f,
            SpacingM = 16f,
            MeanderM = 0f,
            MeanderWavelengthM = 90f,
            LifetimeS = lifetime,
            ProductionPerS = 0f,
            Cap = 1e6f,
            JacobianThreshold = FoamCalibration.NoFoamThreshold,
            BreakingGain = 1f,
        };

        sealed class Field : System.IDisposable
        {
            public NativeArray<float> Current, Next;
            public NativeArray<SeaFrameComponent> Components;
            public int ComponentCount;
            public ResidualFoamGrid Grid = ResidualFoamGrid.At(float2.zero, N, SizeM);

            public Field()
            {
                Current = new NativeArray<float>(N * N, Allocator.TempJob);
                Next = new NativeArray<float>(N * N, Allocator.TempJob);
                Components = new NativeArray<SeaFrameComponent>(1, Allocator.TempJob);
            }

            /// <summary>One step as the renderer takes it: the grid drifts with the water, re-centres on <paramref name="follow"/>.</summary>
            public void Step(in ResidualFoamParams p, float dt, float2 follow)
            {
                int2 shift = Grid.Advance(p.DriftMs * dt * p.Downwind, follow);
                new ResidualFoamStepJob
                {
                    Previous = Current, Next = Next, Components = Components, ComponentCount = ComponentCount,
                    Resolution = N, SizeM = SizeM, Centre = Grid.CentreWorld, Shift = shift, DtS = dt, Params = p,
                }.Schedule(N * N, 256).Complete();
                (Current, Next) = (Next, Current);
            }

            public void Step(in ResidualFoamParams p, float dt) => Step(p, dt, float2.zero);

            public float2 World(int i, int j) => ResidualFoamMath.TexelWorld(i, j, N, Grid.CentreWorld, SizeM);

            public float[] ToArray() => Current.ToArray();

            public void Dispose() { Current.Dispose(); Next.Dispose(); Components.Dispose(); }
        }

        static void Moments(float[] g, Field f, out double mass, out float2 centroid, out float2 variance)
        {
            mass = 0; double mx = 0, mz = 0, vx = 0, vz = 0;
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                double w = g[j * N + i];
                float2 x = f.World(i, j);
                mass += w; mx += w * x.x; mz += w * x.y;
            }
            centroid = new float2((float)(mx / mass), (float)(mz / mass));
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                double w = g[j * N + i];
                float2 d = f.World(i, j) - centroid;
                vx += w * d.x * d.x; vz += w * d.y * d.y;
            }
            variance = new float2((float)(vx / mass), (float)(vz / mass));
        }

        [Test]
        public void Drift_CarriesAPatchDownwind_WithoutSmearingIt()
        {
            // The grid carries the drift (sub-texel fraction + whole-texel moves), so a patch
            // travels without being resampled: no smearing, no loss. A bilinear semi-Lagrangian
            // step would add ~X·texel of variance (X = distance travelled) — 12 m² on 36 m² here.
            using (var f = new Field())
            {
                const float Sigma = 6f;
                for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    float2 x = f.World(i, j);
                    f.Current[j * N + i] = math.exp(-math.lengthsq(x) / (2f * Sigma * Sigma));
                }
                Moments(f.ToArray(), f, out double mass0, out float2 c0, out float2 v0);

                var p = Still();
                p.Downwind = math.normalize(new float2(0.8f, 0.6f));
                p.DriftMs = 0.6f;
                for (int s = 0; s < 600; s++) f.Step(p, 1f / 60f);   // 10 s → 6 m

                Moments(f.ToArray(), f, out double mass1, out float2 c1, out float2 v1);
                float2 moved = c1 - c0;
                Assert.AreEqual(6f, math.dot(moved, p.Downwind), 0.05f, $"moved {moved}");
                Assert.AreEqual(0f, math.dot(moved, new float2(-p.Downwind.y, p.Downwind.x)), 0.05f);
                Assert.AreEqual(1.0, mass1 / mass0, 1e-4, "drift neither makes nor loses foam");
                Assert.AreEqual(1f, (v1.x + v1.y) / (v0.x + v0.y), 1e-3f, $"variance {v0} → {v1}");
            }
        }

        [Test]
        public void Decay_FollowsTheLifetime()
        {
            using (var f = new Field())
            {
                for (int k = 0; k < N * N; k++) f.Current[k] = 0.5f;
                var p = Still(lifetime: 10f);
                for (int s = 0; s < 100; s++) f.Step(p, 0.1f);   // 10 s = one lifetime
                float centre = f.Current[(N / 2) * N + N / 2];
                Assert.AreEqual(0.5f * math.exp(-1f), centre, 1e-4f);
            }
        }

        [Test]
        public void RegionShift_ByWholeTexels_MovesTheFieldExactly()
        {
            using (var f = new Field())
            {
                var rng = new Random(9u);
                for (int k = 0; k < N * N; k++) f.Current[k] = rng.NextFloat();
                float[] before = f.ToArray();
                int dx = 3, dz = -2;
                f.Step(Still(), 0f, f.Grid.CentreWorld + new float2(dx, dz) * TexelM);
                float[] after = f.ToArray();
                float worst = 0f;
                for (int j = 4; j < N - 4; j++)
                for (int i = 4; i < N - 4; i++)
                    worst = math.max(worst, math.abs(after[j * N + i] - before[(j + dz) * N + i + dx]));
                Assert.Less(worst, 1e-6f);
            }
        }

        [Test]
        public void Production_ComesOnlyFromBreaking()
        {
            // One feeble wave: the Jacobian is ~1 everywhere. A threshold above it breaks
            // everywhere (b = 1 with gain 4); a threshold below it nowhere.
            using (var f = new Field())
            {
                f.Components[0] = new SeaFrameComponent
                {
                    A = new float4(1f, 0f, 0.1f, 0.001f),
                    B = new float4(0.001f, 0f, 1f, 1f),
                };
                f.ComponentCount = 1;
                var p = Still();
                p.ProductionPerS = 2f;
                p.BreakingGain = 4f;

                p.JacobianThreshold = 0.5f;
                f.Step(p, 0.1f);
                Assert.AreEqual(0f, f.Current[(N / 2) * N + N / 2], 1e-7f, "unbroken water makes no foam");

                p.JacobianThreshold = 1.5f;
                f.Step(p, 0.1f);
                Assert.AreEqual(0.2f, f.Current[(N / 2) * N + N / 2], 1e-5f, "full breaking makes P·dt");
            }
        }

        [Test]
        public void CpuJacobian_MatchesSeaFieldMath()
        {
            using (var sim = new SeaSimulation(0f, 4))
            {
                sim.Apply(SeaTestFixtures.Douglas6WithSwell(), default, 0f);
                for (int i = 0; i < 100; i++) sim.Step(0.02);
                var frame = new NativeArray<SeaFrameComponent>(2 * SeaSimulation.MaxComponentsPerState, Allocator.TempJob);
                try
                {
                    int count = sim.BuildFrame(sim.SimulationTimeS, frame);
                    var rng = new Random(3u);
                    float worst = 0f;
                    for (int k = 0; k < 200; k++)
                    {
                        float2 x0 = rng.NextFloat2(-1500f, 1500f);
                        SeaFieldMath.Evaluate(frame, count, x0, out _, out _, out _, out _, out float reference);
                        worst = math.max(worst, math.abs(ResidualFoamMath.Jacobian(frame, count, x0) - reference));
                    }
                    Assert.Less(worst, 1e-5f);
                }
                finally { frame.Dispose(); }
            }
        }

        [Test]
        public void WindrowCoordinate_NeverFolds_AtTheLookLimits()
        {
            // With the spacing warp and meander at their cap, s must still grow across the wind
            // everywhere, or some foam would flow away from every line.
            var p = Still();
            p.Downwind = math.normalize(new float2(0.3f, -0.95f));
            p.SpacingM = 18f;
            p.SpacingVariation = 0.6f;
            p.MeanderM = (0.9f - p.SpacingVariation) * p.SpacingM / 3.55f;
            float2 n = new float2(-p.Downwind.y, p.Downwind.x);
            var rng = new Random(23u);
            float least = float.MaxValue;
            for (int k = 0; k < 20000; k++)
            {
                float2 x = rng.NextFloat2(-2000f, 2000f);
                p.AlongOffsetM = rng.NextFloat(0f, 2f * p.MeanderWavelengthM);
                float ds = ResidualFoamMath.WindrowCoordinate(p, x + 0.05f * n) - ResidualFoamMath.WindrowCoordinate(p, x - 0.05f * n);
                least = math.min(least, ds / 0.1f);
            }
            Assert.Greater(least, 0f, $"∂s/∂(cross-wind) reached {least}");
        }

        [TestCase(0f, 0f)]
        [TestCase(37f, 0f)]
        [TestCase(90f, 0f)]
        [TestCase(145f, 0f)]
        [TestCase(37f, 1f)]
        public void Windrows_GatherFoamIntoStreaksAlongTheWind(float windTravelDeg, float irregular)
        {
            // Scattered foam patches (what breakers leave), no further production: after 40 s
            // of drift and convergence the field must run with the wind — streaks, not blobs.
            using (var f = new Field())
            {
                var rng = new Random(17u);
                var spots = new float3[220];
                for (int k = 0; k < spots.Length; k++)
                    spots[k] = new float3(rng.NextFloat2(-0.5f * SizeM, 0.5f * SizeM), rng.NextFloat(2f, 5f));
                for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    float2 x = f.World(i, j);
                    float v = 0f;
                    foreach (float3 s in spots) v += 0.6f * math.exp(-math.lengthsq(x - s.xy) / (2f * s.z * s.z));
                    f.Current[j * N + i] = v;
                }
                float rad = math.radians(windTravelDeg);
                var p = Still(lifetime: 60f);
                p.Downwind = new float2(math.cos(rad), math.sin(rad));
                p.DriftMs = 0.6f;
                p.ConvergenceMs = 0.25f;
                p.SpacingM = 16f;
                p.MeanderM = irregular > 0f ? 2.2f : 3f;
                p.SpacingVariation = 0.35f * irregular;     // the shipped look's irregularity
                p.StrengthVariation = 0.5f * irregular;
                ResidualFoamStats before = ResidualFoamStatistics.Measure(f.ToArray(), N, TexelM, p.Downwind);

                for (int s = 0; s < 800; s++) f.Step(p, 0.05f);   // 40 s
                ResidualFoamStats after = ResidualFoamStatistics.Measure(f.ToArray(), N, TexelM, p.Downwind);

                Assert.Less(math.degrees(after.AngleToReferenceRad), 10f, $"streaks run at {math.degrees(after.StreakDirectionRad):0.0}°");
                Assert.Greater(after.Coherence, 0.6f, $"coherence {before.Coherence:0.00} → {after.Coherence:0.00}");
                Assert.Greater(after.AlongCorrelationM, 3f * after.AcrossCorrelationM,
                    $"along {after.AlongCorrelationM:0.0} m vs across {after.AcrossCorrelationM:0.0} m (before {before.AlongCorrelationM:0.0} / {before.AcrossCorrelationM:0.0})");
            }
        }

        [Test]
        public void Windrows_PileFoamOntoTheConvergenceLines_ConservingIt()
        {
            // Uniform foam, no drift, no decay: convergence moves foam onto s = k·spacing
            // without creating any (interior mass conserved; edges exchange with clear water).
            using (var f = new Field())
            {
                for (int k = 0; k < N * N; k++) f.Current[k] = 0.2f;
                var p = Still();
                p.Downwind = new float2(0f, 1f);
                p.ConvergenceMs = 0.2f;
                p.SpacingM = 16f;
                for (int s = 0; s < 600; s++) f.Step(p, 0.05f);   // 30 s

                double onLines = 0, total = 0;
                for (int j = N / 4; j < 3 * N / 4; j++)
                for (int i = N / 4; i < 3 * N / 4; i++)
                {
                    float2 x = f.World(i, j);
                    float sCoord = ResidualFoamMath.WindrowCoordinate(p, x);
                    float distance = math.abs(sCoord - p.SpacingM * math.round(sCoord / p.SpacingM));
                    double v = f.Current[j * N + i];
                    total += v;
                    if (distance < p.SpacingM / 8f) onLines += v;
                }
                double interiorInitial = 0.2 * (N / 2) * (N / 2);
                Assert.AreEqual(1.0, total / interiorInitial, 0.02, "interior foam conserved");
                Assert.Greater(onLines / total, 0.6, "foam gathered within ±spacing/8 of the lines (uniform would be 25 %)");
            }
        }

        [Test]
        public void Deposit_AddsAGaussianOfItsLevel_ThenTheCapHolds()
        {
            var field = new NativeArray<float>(N * N, Allocator.Temp);
            try
            {
                float2 centre = float2.zero;
                // A 10 m deposit of level 0.4 at a texel centre: peak 0.4, mass Level x 2 pi sigma^2 (sigma = 5 m), less the cut tail.
                var d = new FoamDeposit { Centre = ResidualFoamMath.TexelWorld(N / 2, N / 2, N, centre, SizeM), RadiusM = 10f, Level = 0.4f };
                ResidualFoamMath.ApplyDeposits(field, N, centre, SizeM, 0.8f, new[] { d }, 1);
                double mass = 0; float peak = 0f;
                for (int k = 0; k < field.Length; k++) { mass += field[k] * TexelM * TexelM; peak = math.max(peak, field[k]); }
                double expected = 0.4 * 2.0 * math.PI * 25.0 * (1.0 - math.exp(-0.5 * 9.0));   // Gaussian mass inside 3 sigma
                Assert.AreEqual(0.4f, peak, 1e-5f, "the peak is the level");
                Assert.AreEqual(expected, mass, expected * 0.02, "mass is the Gaussian's (texel sum, within 2 %)");
                Assert.AreEqual(0f, field[(N / 2) * N + N / 2 + 8], "nothing beyond 1.5 x the radius (16 m out)");

                ResidualFoamMath.ApplyDeposits(field, N, centre, SizeM, 0.8f, new[] { d, d, d }, 3);
                Assert.AreEqual(0.8f, field[(N / 2) * N + N / 2], 1e-6f, "deposits stack, and the cap still holds");

                var outside = new FoamDeposit { Centre = new float2(10 * SizeM, 0f), RadiusM = 10f, Level = 0.4f };
                var before = field.ToArray();
                ResidualFoamMath.ApplyDeposits(field, N, centre, SizeM, 0.8f, new[] { outside }, 1);
                CollectionAssert.AreEqual(before, field.ToArray(), "a deposit outside the grid changes nothing");
            }
            finally { field.Dispose(); }
        }
    }
}
