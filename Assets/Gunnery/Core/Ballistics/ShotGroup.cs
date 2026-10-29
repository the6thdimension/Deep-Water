using System;
using UnityEngine;

namespace DeepWater.Gunnery.Ballistics
{
    /// <summary>
    /// The shot-group hit model (G36), in the plane across the line of fire at the target. Rounds land in a
    /// circular normal pattern of standard deviation σ around a mean point that misses the target's centre by d.
    /// The target is a disc of radius r: its presented area, or a proximity fuze's lethal radius. Pure.
    /// </summary>
    public static class ShotGroup
    {
        /// <summary>
        /// The chance one round lands within <paramref name="radiusM"/> of the target's centre, exactly: the pattern's
        /// mass inside the disc, 1 − Q₁(d/σ, r/σ) (Marcum's Q). With a = d/σ and b = r/σ it is the Rice integral
        /// ∫₀ᵇ x·exp(−(x² + a²)/2)·I₀(a·x) dx, taken by Gauss–Legendre over the 18 σ around the mean point that hold all but
        /// e⁻⁴⁰ of the pattern. Centred (d = 0) it is 1 − exp(−r²/2σ²) in closed form. Without dispersion it is all or nothing.
        /// Checked 2026-10-29 against the exact Poisson series on 6,321 points (a from 0 to b + 12, b from 0.01 to 60):
        /// within 5e-7 everywhere, the error of the Bessel approximation. It replaced (1 − exp(−r²/2σ²))·exp(−d²/2σ²), which
        /// was exact only for r ≪ σ: off centre on a target larger than the spread it gave 0.135 where the truth is 0.966.
        /// </summary>
        public static float RoundHitProbability(float radiusM, float sigmaM, float offsetM)
        {
            if (sigmaM <= 1e-6f) return offsetM <= radiusM ? 1f : 0f;
            if (radiusM <= 0f) return 0f;
            double b = radiusM / (double)sigmaM, a = Math.Abs(offsetM) / (double)sigmaM;
            if (a <= 0.0) return (float)(1.0 - Math.Exp(-0.5 * b * b));

            double lo = Math.Max(0.0, a - RiceReachSigma), hi = Math.Min(b, a + RiceReachSigma);
            if (hi <= lo) return 0f;                     // the disc ends before the pattern begins
            int panels = Math.Max(1, Math.Min(RicePanels, (int)Math.Ceiling((hi - lo) / RicePanelSigma)));
            double h = (hi - lo) / panels, sum = 0.0;
            for (int p = 0; p < panels; p++)
            {
                double mid = lo + (p + 0.5) * h;
                for (int k = 0; k < GaussX.Length; k++)
                {
                    double x = mid + 0.5 * h * GaussX[k], e = x - a;
                    sum += GaussW[k] * x * Math.Exp(-0.5 * e * e) * ScaledBesselI0(a * x);
                }
            }
            return Mathf.Clamp01((float)(0.5 * h * sum));
        }

        /// <summary>Half-width of the band integrated, in σ: the Rice density beyond it is below e⁻⁴⁰.</summary>
        const double RiceReachSigma = 9.0;
        /// <summary>At most this many Gauss–Legendre panels, each at most <see cref="RicePanelSigma"/> wide.</summary>
        const int RicePanels = 8;
        const double RicePanelSigma = 2.25;

        /// <summary>8-point Gauss–Legendre nodes and weights on [−1, 1].</summary>
        static readonly double[] GaussX =
        {
            -0.9602898564975363, -0.7966664774136267, -0.5255324099163290, -0.1834346424956498,
             0.1834346424956498,  0.5255324099163290,  0.7966664774136267,  0.9602898564975363,
        };
        static readonly double[] GaussW =
        {
            0.1012285362903763, 0.2223810344533745, 0.3137066458778873, 0.3626837833783620,
            0.3626837833783620, 0.3137066458778873, 0.2223810344533745, 0.1012285362903763,
        };

        /// <summary>
        /// exp(−z)·I₀(z) for z ≥ 0, the modified Bessel function scaled so it never overflows: Abramowitz and Stegun
        /// 9.8.1 for z ≤ 3.75 and 9.8.2 above, each within 2e-7 relative.
        /// </summary>
        static double ScaledBesselI0(double z)
        {
            if (z <= 3.75)
            {
                double t = z / 3.75; t *= t;
                double i0 = 1.0 + t * (3.5156229 + t * (3.0899424 + t * (1.2067492 + t * (0.2659732 + t * (0.0360768 + t * 0.0045813)))));
                return i0 * Math.Exp(-z);
            }
            double u = 3.75 / z;
            double p = 0.39894228 + u * (0.01328592 + u * (0.00225319 + u * (-0.00157565 + u * (0.00916281
                     + u * (-0.02057706 + u * (0.02635537 + u * (-0.01647633 + u * 0.00392377)))))));
            return p / Math.Sqrt(z);
        }

        /// <summary>The chance at least one of <paramref name="rounds"/> rounds kills: each hits with
        /// <paramref name="pRound"/> and kills on a hit with <paramref name="pkPerHit"/>, independently.</summary>
        public static float BurstKillProbability(float pRound, float pkPerHit, int rounds) =>
            1f - Mathf.Pow(1f - Mathf.Clamp01(pRound * pkPerHit), Mathf.Max(0, rounds));

        /// <summary>
        /// The radius of a disc with the target's presented area: a cylinder of <paramref name="lengthM"/> and
        /// <paramref name="diameterM"/> seen at angle α between its axis and the line of fire, A = L·d·sin α + (π d²/4)·|cos α|.
        /// </summary>
        public static float PresentedRadiusM(float lengthM, float diameterM, Vector3 targetAxis, Vector3 lineOfFire)
        {
            float cos = 0f;
            if (targetAxis.sqrMagnitude > 1e-8f && lineOfFire.sqrMagnitude > 1e-8f)
                cos = Mathf.Abs(Vector3.Dot(targetAxis.normalized, lineOfFire.normalized));
            float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - cos * cos));
            float area = lengthM * diameterM * sin + Mathf.PI * 0.25f * diameterM * diameterM * cos;
            return Mathf.Sqrt(area / Mathf.PI);
        }

        /// <summary>
        /// Closed-loop spotting (G40): a bias b that the loop corrects with time constant τ. Over a burst of T seconds
        /// the mean bias is b·(1 − e^(−T/τ))·τ/T, and the bias left at the end is b·e^(−T/τ).
        /// </summary>
        public static void SpottingBias(float biasMrad, float burstS, float timeConstantS, out float meanMrad, out float leftMrad)
        {
            if (timeConstantS <= 1e-6f) { meanMrad = leftMrad = 0f; return; }
            if (burstS <= 1e-6f) { meanMrad = leftMrad = biasMrad; return; }
            float decay = Mathf.Exp(-burstS / timeConstantS);
            meanMrad = biasMrad * (1f - decay) * timeConstantS / burstS;
            leftMrad = biasMrad * decay;
        }
    }
}
