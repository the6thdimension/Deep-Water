using UnityEngine;

namespace DeepWater.GeneralQuarters.Core.Magazine
{
    /// <summary>
    /// Closest approach between a weapon and its target during one tick, both moving in straight lines.
    /// A per-tick distance check misses at missile speeds: at 1 300 m/s closing, a 20 ms tick is 26 m,
    /// wider than a 10 m fuze radius. Solving over the whole step catches every pass.
    /// </summary>
    public static class Intercept
    {
        /// <summary>
        /// Minimum distance between a weapon moving w0 → w1 and a target moving t0 → t1 over the same step.
        /// <paramref name="atFraction"/> is where in the step it happens (0 = start, 1 = end).
        /// </summary>
        public static float MinDistance(Vector3 w0, Vector3 w1, Vector3 t0, Vector3 t1, out float atFraction)
        {
            Vector3 d0 = w0 - t0;
            Vector3 dv = (w1 - w0) - (t1 - t0);
            float vv = dv.sqrMagnitude;
            atFraction = vv > 1e-12f ? Mathf.Clamp01(-Vector3.Dot(d0, dv) / vv) : 0f;
            return (d0 + dv * atFraction).magnitude;
        }
    }
}
