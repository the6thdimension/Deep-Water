using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using DeepWater.Sea.Core;

namespace DeepWater.Sea.Tests
{
    /// <summary>Spectrum shapes and the spectrum → components sampler.</summary>
    public class SpectrumTests
    {
        [Test]
        public void Jonswap_GammaApproachingOne_ConvergesToPiersonMoskowitz()
        {
            for (double w = 0.2; w < 3.0; w += 0.1)
            {
                double pm = WaveSpectrum.Jonswap(w, 3.25, 9.0, 1.0);
                double j = WaveSpectrum.Jonswap(w, 3.25, 9.0, 1.0 + 1e-12);
                Assert.AreEqual(pm, j, 1e-9 * math.max(1.0, pm));
            }
        }

        [Test]
        public void Jonswap_IntegratesToHsSquaredOver16()
        {
            const double hs = 3.25, tp = 9.0;
            foreach (double gamma in new[] { 1.0, 3.3 })
            {
                double sum = 0.0, dw = 0.0005;
                for (double w = dw; w < 8.0; w += dw) sum += WaveSpectrum.Jonswap(w, hs, tp, gamma) * dw;
                // A_γ is an approximation for γ > 1 (DNV quotes a few percent); PM is exact.
                double tolerance = gamma == 1.0 ? 0.005 : 0.03;
                Assert.AreEqual(hs * hs / 16.0, sum, tolerance * hs * hs / 16.0, $"γ = {gamma}");
            }
        }

        [Test]
        public void DnvGamma_FollowsThePiecewiseRule()
        {
            Assert.AreEqual(5.0, WaveSpectrum.DnvGamma(4.0, 3.6 * 2.0), 1e-12);
            Assert.AreEqual(1.0, WaveSpectrum.DnvGamma(4.0, 5.0 * 2.0), 1e-12);
            Assert.AreEqual(math.exp(5.75 - 1.15 * 4.3), WaveSpectrum.DnvGamma(4.0, 4.3 * 2.0), 1e-12);
        }

        [Test]
        public void PiersonMoskowitzPeakPeriod_MatchesFivesRootHs()
        {
            // Tp ≈ 4.99 √Hs  (derived from Hs = 0.21 U²/g, ωp = 0.877 g/U)
            Assert.AreEqual(4.99 * math.sqrt(3.25), WaveSpectrum.PiersonMoskowitzPeakPeriod(3.25, 9.81), 0.02);
        }

        [Test]
        public void Sampler_SameSeed_IsBitwiseIdentical_DifferentSeedDiffers()
        {
            var a = Sample(SeaTestFixtures.WindSea(3.25f, seed: 7u), 10f, out _);
            var b = Sample(SeaTestFixtures.WindSea(3.25f, seed: 7u), 10f, out _);
            var c = Sample(SeaTestFixtures.WindSea(3.25f, seed: 8u), 10f, out _);
            try
            {
                bool anyDifferent = false;
                for (int i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i].Omega, b[i].Omega);
                    Assert.AreEqual(a[i].Phase, b[i].Phase);
                    Assert.AreEqual(a[i].Direction, b[i].Direction);
                    Assert.AreEqual(a[i].Amplitude, b[i].Amplitude);
                    anyDifferent |= a[i].Phase != c[i].Phase;
                }
                Assert.IsTrue(anyDifferent, "A different seed must give a different sea.");
            }
            finally { a.Dispose(); b.Dispose(); c.Dispose(); }
        }

        [Test]
        public void Sampler_ComponentVariance_MatchesReportedResolvedHs()
        {
            var comps = Sample(SeaTestFixtures.WindSea(3.25f), 10f, out float resolvedHs);
            try
            {
                double variance = 0.0;
                for (int i = 0; i < comps.Length; i++) variance += 0.5 * comps[i].Amplitude * comps[i].Amplitude;
                Assert.AreEqual(resolvedHs, 4.0 * math.sqrt(variance), 1e-3);
                Assert.LessOrEqual(resolvedHs, 3.25f + 1e-4f);
            }
            finally { comps.Dispose(); }
        }

        [Test]
        public void Sampler_TinyCutoff_ResolvesNearlyAllVariance()
        {
            var comps = Sample(SeaTestFixtures.WindSea(3.25f), 0.05f, out float resolvedHs);
            comps.Dispose();
            // [0.5 ωp, 6 ωp] holds all but ~0.1 % of PM variance.
            Assert.AreEqual(3.25, resolvedHs, 0.01 * 3.25);
        }

        [Test]
        public void Sampler_EnergyIsCentredOnTheMeanDirection()
        {
            var spec = SeaTestFixtures.WindSea(3.25f, count: 256);
            var comps = Sample(spec, 10f, out _);
            try
            {
                float2 mean = float2.zero;
                for (int i = 0; i < comps.Length; i++) mean += comps[i].Amplitude * comps[i].Amplitude * comps[i].Direction;
                float angle = math.atan2(mean.y, mean.x);
                Assert.AreEqual(spec.DirectionRad, angle, math.radians(6f));
            }
            finally { comps.Dispose(); }
        }

        [Test]
        public void Sampler_FrequenciesClusterAtThePeak()
        {
            var spec = SeaTestFixtures.WindSea(3.25f, count: 256);
            var comps = Sample(spec, 0.05f, out _);
            try
            {
                // Equal-energy bins put components where the variance is: the densest
                // frequency neighbourhood must contain ωp.
                float wp = 2f * math.PI / spec.PeakPeriodS;
                int near = 0, far = 0;
                for (int i = 0; i < comps.Length; i++)
                {
                    float r = comps[i].Omega / wp;
                    if (r > 0.9f && r < 1.1f) near++;
                    else if (r > 1.9f && r < 2.1f) far++;
                }
                Assert.Greater(near, 3 * far, $"near-peak {near} vs 2ωp {far}");
            }
            finally { comps.Dispose(); }
        }

        [Test]
        public void Sampler_CalmSea_ProducesNoComponents()
        {
            var comps = Sample(SeaTestFixtures.WindSea(0f), 10f, out float resolvedHs);
            Assert.AreEqual(0, comps.Length);
            Assert.AreEqual(0f, resolvedHs);
            comps.Dispose();
        }

        [Test]
        public void Sampler_SeaShorterThanCutoff_IsReportedNotHidden()
        {
            // Douglas 1: Hs 0.05 m, Tp ≈ 1.1 s → λp ≈ 1.9 m, entirely below a 10 m cut-off.
            var comps = Sample(SeaTestFixtures.WindSea(0.05f), 10f, out float resolvedHs);
            Assert.AreEqual(0, comps.Length);
            Assert.AreEqual(0f, resolvedHs);
            comps.Dispose();
        }

        static NativeArray<WaveComponent> Sample(WaveSystemSpec spec, float minWavelength, out float resolvedHs)
        {
            var all = new NativeArray<WaveComponent>(SeaSimulation.MaxComponentsPerState, Allocator.TempJob);
            var report = new NativeArray<float>(3, Allocator.TempJob);
            new SpectrumSampleJob
            {
                Spec = spec,
                MinWavelengthM = minWavelength,
                Gravity = SeaTestFixtures.Gravity,
                Output = all,
                Report = report,
            }.Run();
            resolvedHs = report[1];
            int n = (int)report[2];
            var result = new NativeArray<WaveComponent>(n, Allocator.TempJob);
            NativeArray<WaveComponent>.Copy(all, result, n);
            report.Dispose();
            all.Dispose();
            return result;
        }
    }
}
