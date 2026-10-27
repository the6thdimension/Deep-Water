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
        /// The chance one round lands within <paramref name="radiusM"/> of the target's centre.
        /// Centred (d = 0) this is exact, 1 − exp(−r²/2σ²); off centre it is that times exp(−d²/2σ²), which is exact
        /// for a small target (r ≪ σ) and errs toward hits for a large one. Without dispersion it is all or nothing.
        /// </summary>
        public static float RoundHitProbability(float radiusM, float sigmaM, float offsetM)
        {
            if (sigmaM <= 1e-6f) return offsetM <= radiusM ? 1f : 0f;
            float s2 = 2f * sigmaM * sigmaM;
            return (1f - Mathf.Exp(-radiusM * radiusM / s2)) * Mathf.Exp(-offsetM * offsetM / s2);
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
