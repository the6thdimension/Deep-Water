using Unity.Mathematics;

namespace DeepWater.Sea.Core
{
    /// <summary>What a residual foam field looks like, in numbers.</summary>
    public struct ResidualFoamStats
    {
        public int Texels;
        public float Mean;
        public float Max;
        /// <summary>Area fraction with foam level ≥ 0.1 and ≥ 0.3.</summary>
        public float CoverageAbove01, CoverageAbove03;
        /// <summary>Direction the streaks run, radians from +X toward +Z, folded to (−π/2, π/2].</summary>
        public float StreakDirectionRad;
        /// <summary>Structure-tensor coherence: 0 = no preferred direction, 1 = perfectly parallel lines.</summary>
        public float Coherence;
        /// <summary>Angle between the streaks and the reference (wind) direction, radians, 0–π/2.</summary>
        public float AngleToReferenceRad;
        /// <summary>Lag at which the autocorrelation falls to 1/e along / across the reference direction, m.</summary>
        public float AlongCorrelationM, AcrossCorrelationM;
    }

    /// <summary>
    /// Statistics of a residual foam grid (N×N, row-major, X fastest, texel centres) over its
    /// interior. Orientation from the structure tensor of the field's gradient (streaks run
    /// across the dominant gradient); correlation lengths from the normalised autocorrelation
    /// along and across a reference direction. Shared by the tests and the play-mode tool so
    /// both measure the same way.
    /// </summary>
    public static class ResidualFoamStatistics
    {
        /// <param name="interiorFraction">Chebyshev half-width of the measured interior, 0–1 of the grid half-size.</param>
        /// <param name="maxLagTexels">Longest correlation lag considered; lengths beyond it read as this lag.</param>
        public static ResidualFoamStats Measure(float[] grid, int n, float texelM, float2 reference,
            float interiorFraction = 0.6f, int maxLagTexels = 64)
        {
            int lo = (int)math.floor(0.5f * n * (1f - interiorFraction)), hi = n - lo;
            var s = new ResidualFoamStats();
            double sum = 0, jxx = 0, jzz = 0, jxz = 0;
            int c01 = 0, c03 = 0, count = 0;
            float max = 0f;
            for (int j = math.max(lo, 1); j < math.min(hi, n - 1); j++)
            for (int i = math.max(lo, 1); i < math.min(hi, n - 1); i++)
            {
                float f = grid[j * n + i];
                sum += f; count++;
                max = math.max(max, f);
                if (f >= 0.1f) c01++;
                if (f >= 0.3f) c03++;
                double gx = 0.5 * (grid[j * n + i + 1] - grid[j * n + i - 1]);
                double gz = 0.5 * (grid[(j + 1) * n + i] - grid[(j - 1) * n + i]);
                jxx += gx * gx; jzz += gz * gz; jxz += gx * gz;
            }
            if (count == 0) return s;

            s.Texels = count;
            s.Mean = (float)(sum / count);
            s.Max = max;
            s.CoverageAbove01 = (float)c01 / count;
            s.CoverageAbove03 = (float)c03 / count;

            double trace = jxx + jzz;
            double gradientDir = 0.5 * math.atan2(2.0 * jxz, jxx - jzz);
            s.StreakDirectionRad = Fold((float)(gradientDir + 0.5 * math.PI_DBL));
            s.Coherence = trace > 0 ? (float)(math.sqrt((jxx - jzz) * (jxx - jzz) + 4.0 * jxz * jxz) / trace) : 0f;
            float refDir = math.atan2(reference.y, reference.x);
            s.AngleToReferenceRad = math.abs(Fold(s.StreakDirectionRad - refDir));

            float2 along = math.normalizesafe(reference, new float2(1f, 0f));
            float2 across = new float2(-along.y, along.x);
            s.AlongCorrelationM = CorrelationLength(grid, n, lo, hi, s.Mean, along, maxLagTexels) * texelM;
            s.AcrossCorrelationM = CorrelationLength(grid, n, lo, hi, s.Mean, across, maxLagTexels) * texelM;
            return s;
        }

        /// <summary>Fold an orientation (mod π) into (−π/2, π/2].</summary>
        static float Fold(float a)
        {
            a = a - math.PI * math.floor(a / math.PI);   // [0, π)
            return a > 0.5f * math.PI ? a - math.PI : a;
        }

        // 1/e lag (texels, linearly interpolated) of the autocorrelation along unit direction u,
        // over every second interior texel, bilinear taps.
        static float CorrelationLength(float[] grid, int n, int lo, int hi, float mean, float2 u, int maxLag)
        {
            double variance = 0;
            int samples = 0;
            for (int j = lo; j < hi; j += 2)
            for (int i = lo; i < hi; i += 2)
            {
                double d = grid[j * n + i] - mean;
                variance += d * d; samples++;
            }
            if (samples == 0 || variance <= 0) return 0f;
            variance /= samples;

            const float Threshold = 0.36787944f;   // 1/e
            float previous = 1f;
            for (int lag = 1; lag <= maxLag; lag++)
            {
                double cov = 0;
                int m = 0;
                for (int j = lo; j < hi; j += 2)
                for (int i = lo; i < hi; i += 2)
                {
                    float2 q = new float2(i, j) + lag * u;
                    if (q.x < 0f || q.y < 0f || q.x > n - 1 || q.y > n - 1) continue;
                    cov += (grid[j * n + i] - mean) * (Bilinear(grid, n, q) - mean);
                    m++;
                }
                float r = m > 0 ? (float)(cov / m / variance) : 0f;
                if (r <= Threshold)
                    return lag - 1 + (previous - Threshold) / math.max(previous - r, 1e-6f);
                previous = r;
            }
            return maxLag;
        }

        static float Bilinear(float[] grid, int n, float2 q)
        {
            int x0 = math.min((int)q.x, n - 2), z0 = math.min((int)q.y, n - 2);
            float fx = q.x - x0, fz = q.y - z0;
            float a = math.lerp(grid[z0 * n + x0], grid[z0 * n + x0 + 1], fx);
            float b = math.lerp(grid[(z0 + 1) * n + x0], grid[(z0 + 1) * n + x0 + 1], fx);
            return math.lerp(a, b, fz);
        }
    }
}
