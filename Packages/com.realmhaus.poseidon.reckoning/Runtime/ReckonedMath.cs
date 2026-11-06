using System;

namespace POSEIDON.Reckoning
{
    /// <summary>
    /// Math with a repeatable result. <c>System.Math.Log</c>, <c>Sin</c> and friends call the C
    /// runtime, and IEEE 754 does not require their results to be correctly rounded, so two runtimes
    /// may differ in the last bit. +, -, *, / and Sqrt are required to be exact, so anything built
    /// from only those (as this file is) gives one answer wherever those operations are honest.
    /// Only what phase 0 needs is here; trig arrives with the first rule pack that needs it.
    /// </summary>
    public static class ReckonedMath
    {
        private const double Ln2 = 0.693147180559945309417232121458;
        private const double Sqrt2 = 1.41421356237309504880168872421;
        private const double TwoTo54 = 18014398509481984.0;

        /// <summary>
        /// Natural log for finite x above 0. Writes x = m * 2^e with m near 1, then uses
        /// ln m = 2 * atanh(s) = 2 * (s + s^3/3 + s^5/5 + ...), where s = (m - 1) / (m + 1).
        /// With m in [sqrt(1/2), sqrt(2)], |s| is at most 0.172, so a fixed eleven terms reach
        /// double precision. A fixed count also means a fixed cost.
        /// </summary>
        public static double Log(double x)
        {
            if (!(x > 0.0) || double.IsInfinity(x)) throw new ArgumentOutOfRangeException(nameof(x), x, "Log needs a finite value above 0.");

            int exponentAdjust = 0;
            long bits = BitConverter.DoubleToInt64Bits(x);
            if (((bits >> 52) & 0x7FF) == 0)
            {
                // Subnormal: scale into the normal range so the exponent field means something.
                bits = BitConverter.DoubleToInt64Bits(x * TwoTo54);
                exponentAdjust = -54;
            }

            int e = (int)((bits >> 52) & 0x7FF) - 1023 + exponentAdjust;
            double m = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L); // [1, 2)
            if (m > Sqrt2)
            {
                m *= 0.5;
                e++;
            }

            double s = (m - 1.0) / (m + 1.0);
            double z = s * s;
            double series = 1.0 / 21.0;
            series = 1.0 / 19.0 + z * series;
            series = 1.0 / 17.0 + z * series;
            series = 1.0 / 15.0 + z * series;
            series = 1.0 / 13.0 + z * series;
            series = 1.0 / 11.0 + z * series;
            series = 1.0 / 9.0 + z * series;
            series = 1.0 / 7.0 + z * series;
            series = 1.0 / 5.0 + z * series;
            series = 1.0 / 3.0 + z * series;
            series = 1.0 + z * series;
            return e * Ln2 + 2.0 * s * series;
        }

        // Peter Acklam's rational approximation to the inverse normal CDF. Published relative
        // error is about 1.15e-9 across the full range. These are the formula's own coefficients,
        // not tuning values.
        private static readonly double[] A = { -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00 };
        private static readonly double[] B = { -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01 };
        private static readonly double[] C = { -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00 };
        private static readonly double[] D = { 7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00 };
        private const double LowerTail = 0.02425;

        /// <summary>The z where the standard normal CDF equals p, for p strictly between 0 and 1.</summary>
        public static double InverseNormalCdf(double p)
        {
            if (!(p > 0.0 && p < 1.0)) throw new ArgumentOutOfRangeException(nameof(p), p, "p must lie strictly between 0 and 1.");

            if (p < LowerTail) return Tail(Math.Sqrt(-2.0 * Log(p)));
            if (p > 1.0 - LowerTail) return -Tail(Math.Sqrt(-2.0 * Log(1.0 - p)));

            double q = p - 0.5;
            double r = q * q;
            return (((((A[0] * r + A[1]) * r + A[2]) * r + A[3]) * r + A[4]) * r + A[5]) * q /
                   (((((B[0] * r + B[1]) * r + B[2]) * r + B[3]) * r + B[4]) * r + 1.0);
        }

        private static double Tail(double q)
        {
            return (((((C[0] * q + C[1]) * q + C[2]) * q + C[3]) * q + C[4]) * q + C[5]) /
                   ((((D[0] * q + D[1]) * q + D[2]) * q + D[3]) * q + 1.0);
        }
    }
}
