using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeepWater.Sea.Core
{
    /// <summary>Authoring-independent description of one wave system (the wind sea or a swell).</summary>
    public struct WaveSystemSpec
    {
        /// <summary>Significant wave height Hs, m. 0 = this system is absent.</summary>
        public float SignificantHeightM;
        /// <summary>Spectral peak period Tp, s.</summary>
        public float PeakPeriodS;
        /// <summary>JONSWAP peak-enhancement factor γ (1 = Pierson–Moskowitz).</summary>
        public float Gamma;
        /// <summary>cos-2s spreading exponent s (larger = more long-crested).</summary>
        public float SpreadingS;
        /// <summary>Mean travel direction, radians from +X toward +Z.</summary>
        public float DirectionRad;
        /// <summary>Lagrangian choppiness Q applied to every component (1 = choppy-wave model).</summary>
        public float Choppiness;
        /// <summary>How many discrete components to draw from the spectrum.</summary>
        public int ComponentCount;
        /// <summary>RNG seed (P7). 0 is remapped to 1 because the xorshift RNG rejects 0.</summary>
        public uint Seed;
    }

    /// <summary>
    /// Draws a deterministic set of <see cref="WaveComponent"/>s from a directional JONSWAP
    /// spectrum. Runs Burst-compiled with <c>FloatMode.Deterministic</c> so the same spec and
    /// seed give bitwise-identical waves on every 64-bit platform.
    ///
    /// **Method (single-summation, equal-energy bins):**
    /// 1. Integrate S(ω) on a fine grid over [0.5 ωp, 6 ωp] and normalise so the total
    ///    variance is exactly Hs²/16 (this also absorbs the approximation in A_γ).
    /// 2. Keep only frequencies the renderer can draw (ω ≤ ω for <c>MinWavelengthM</c>); the
    ///    rest of the variance is reported, not hidden — it is carried by HDRP's ripple band.
    /// 3. Split the kept variance into N equal-energy bins; each component gets one random
    ///    frequency inside its bin (no common period, so the sea never visibly repeats), one
    ///    direction drawn from the cos-2s distribution (single summation: no standing-wave
    ///    artefacts), a random phase, and amplitude a = √(2 · bin variance).
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
    public struct SpectrumSampleJob : IJob
    {
        public WaveSystemSpec Spec;
        public float MinWavelengthM;
        public float Gravity;
        public int OutputOffset;

        public NativeArray<WaveComponent> Output;

        /// <summary>[0] target Hs, [1] captured Hs, [2] components written.</summary>
        public NativeArray<float> Report;

        const int FrequencySamples = 2048;
        const int DirectionSamples = 512;
        const double LowPeakRatio = 0.5;
        const double HighPeakRatio = 6.0;

        public void Execute()
        {
            Report[0] = math.max(0f, Spec.SignificantHeightM);
            Report[1] = 0f;
            Report[2] = 0f;

            int n = Spec.ComponentCount;
            if (Spec.SignificantHeightM <= 0f || Spec.PeakPeriodS <= 0f || n <= 0) return;

            double hs = Spec.SignificantHeightM;
            double tp = Spec.PeakPeriodS;
            double g = Gravity;
            double wp = 2.0 * math.PI_DBL / tp;
            double wLo = LowPeakRatio * wp;
            double wHi = HighPeakRatio * wp;
            double dw = (wHi - wLo) / FrequencySamples;

            // Cumulative variance table (trapezoid rule).
            var cumulative = new NativeArray<double>(FrequencySamples + 1, Allocator.Temp);
            double prev = WaveSpectrum.Jonswap(wLo, hs, tp, Spec.Gamma);
            cumulative[0] = 0.0;
            for (int i = 1; i <= FrequencySamples; i++)
            {
                double s = WaveSpectrum.Jonswap(wLo + i * dw, hs, tp, Spec.Gamma);
                cumulative[i] = cumulative[i - 1] + 0.5 * (prev + s) * dw;
                prev = s;
            }

            double total = cumulative[FrequencySamples];
            if (total <= 0.0) { cumulative.Dispose(); return; }
            double normalise = hs * hs / 16.0 / total;

            double wTop = math.min(WaveSpectrum.DeepWaterOmega(MinWavelengthM, g), wHi);
            if (wTop <= wLo) { cumulative.Dispose(); return; }
            double captured = Interpolate(cumulative, (wTop - wLo) / dw);
            double binVariance = captured / n;

            // Inverse CDF of the cos-2s distribution over Δθ ∈ [−π, π].
            var dirCdf = new NativeArray<double>(DirectionSamples + 1, Allocator.Temp);
            double dTheta = 2.0 * math.PI_DBL / DirectionSamples;
            double prevD = WaveSpectrum.Cos2s(-math.PI_DBL, Spec.SpreadingS);
            dirCdf[0] = 0.0;
            for (int i = 1; i <= DirectionSamples; i++)
            {
                double d = WaveSpectrum.Cos2s(-math.PI_DBL + i * dTheta, Spec.SpreadingS);
                dirCdf[i] = dirCdf[i - 1] + 0.5 * (prevD + d) * dTheta;
                prevD = d;
            }
            double dirTotal = dirCdf[DirectionSamples];

            var rng = new Random(Spec.Seed == 0u ? 1u : Spec.Seed);
            float amplitude = (float)math.sqrt(2.0 * binVariance * normalise);

            for (int j = 0; j < n; j++)
            {
                double wA = wLo + dw * InverseLookup(cumulative, j * binVariance);
                double wB = wLo + dw * InverseLookup(cumulative, (j + 1) * binVariance);
                double omega = wA + rng.NextDouble() * (wB - wA);

                double spread = -math.PI_DBL + dTheta * InverseLookup(dirCdf, rng.NextDouble() * dirTotal);
                double theta = Spec.DirectionRad + spread;
                double phase = rng.NextDouble() * 2.0 * math.PI_DBL;

                Output[OutputOffset + j] = new WaveComponent
                {
                    Direction = new float2((float)math.cos(theta), (float)math.sin(theta)),
                    Wavenumber = (float)(omega * omega / g),
                    Omega = (float)omega,
                    Amplitude = amplitude,
                    Phase = (float)phase,
                    Choppiness = Spec.Choppiness,
                };
            }

            Report[1] = (float)(4.0 * math.sqrt(captured * normalise));
            Report[2] = n;

            dirCdf.Dispose();
            cumulative.Dispose();
        }

        // Linear interpolation of a table at a fractional index.
        static double Interpolate(NativeArray<double> table, double index)
        {
            int last = table.Length - 1;
            if (index <= 0.0) return table[0];
            if (index >= last) return table[last];
            int i = (int)index;
            double f = index - i;
            return table[i] + f * (table[i + 1] - table[i]);
        }

        // Fractional index at which a monotonically non-decreasing table reaches value.
        static double InverseLookup(NativeArray<double> table, double value)
        {
            int lo = 0, hi = table.Length - 1;
            if (value <= table[lo]) return lo;
            if (value >= table[hi]) return hi;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (table[mid] <= value) lo = mid; else hi = mid;
            }
            double span = table[hi] - table[lo];
            return span > 0.0 ? lo + (value - table[lo]) / span : lo;
        }
    }
}
