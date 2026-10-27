using UnityEngine;

namespace DeepWater.Gunnery.Guns
{
    /// <summary>
    /// The lead-computing sight (K4, SIX's pick): where a round fired now will be when it has flown out to a range, and
    /// when. The HUD draws its pipper at that point; a pilot who puts the pipper on the target where the target will
    /// be hits it. The same model flies the real rounds (<see cref="RoundFlight"/>), so the pipper cannot disagree with
    /// the tracers. A game sight: no radar ranging, no real fire-control law (BLUEPRINT constraint 3).
    /// </summary>
    public static class LeadSight
    {
        public const float StepS = 0.02f;

        /// <summary>
        /// Fly one round from <paramref name="muzzle"/> until it is <paramref name="rangeM"/> from where it started, or the
        /// gun's time of flight runs out. Returns false if it never got that far.
        /// </summary>
        public static bool RoundAt(in GunProfile gun, Vector3 muzzle, Vector3 shooterVelocity, Vector3 boreWorld, float rangeM,
                                   out Vector3 point, out float timeOfFlightS)
        {
            Vector3 p = muzzle, v = shooterVelocity + boreWorld.normalized * gun.Round.MuzzleVelocityMps;
            float k = RoundFlight.DragConstant(gun.Round);
            float r2 = rangeM * rangeM;
            for (float t = 0f; t < gun.MaxTimeOfFlightS; t += StepS)
            {
                Vector3 before = p;
                RoundFlight.Step(ref p, ref v, k, StepS);
                if ((p - muzzle).sqrMagnitude >= r2)
                {
                    // Interpolate the last step to the range itself.
                    float a = (before - muzzle).magnitude, b = (p - muzzle).magnitude;
                    float f = b > a ? (rangeM - a) / (b - a) : 1f;
                    point = Vector3.Lerp(before, p, f);
                    timeOfFlightS = t + StepS * f;
                    return true;
                }
            }
            point = p;
            timeOfFlightS = gun.MaxTimeOfFlightS;
            return false;
        }

        /// <summary>
        /// How close a burst fired now would pass a target flying straight on at <paramref name="targetVelocity"/>:
        /// the round is flown step by step and the closest approach to the moving target found (between steps the
        /// relative motion is taken as straight). <paramref name="targetMinusRound"/> is where the target is relative to
        /// the round at that moment, which says which way to move the nose. The AI's trigger rule (RH Aviator) shoots
        /// when the miss is small enough for its skill, and its gun tracking steers on the offset.
        /// </summary>
        public static float MissDistance(in GunProfile gun, Vector3 muzzle, Vector3 shooterVelocity, Vector3 boreWorld,
                                         Vector3 targetPosition, Vector3 targetVelocity, out Vector3 targetMinusRound, out float timeOfFlightS)
        {
            Vector3 p = muzzle, v = shooterVelocity + boreWorld.normalized * gun.Round.MuzzleVelocityMps;
            float k = RoundFlight.DragConstant(gun.Round);
            float best = float.PositiveInfinity;
            targetMinusRound = targetPosition - muzzle;
            timeOfFlightS = 0f;
            Vector3 rel0 = targetPosition - p;
            for (float t = 0f; t < gun.MaxTimeOfFlightS; t += StepS)
            {
                RoundFlight.Step(ref p, ref v, k, StepS);
                Vector3 rel1 = targetPosition + targetVelocity * (t + StepS) - p;
                Vector3 d = rel1 - rel0;
                float s = d.sqrMagnitude > 1e-9f ? Mathf.Clamp01(-Vector3.Dot(rel0, d) / d.sqrMagnitude) : 0f;
                Vector3 closest = rel0 + d * s;
                float miss = closest.magnitude;
                if (miss < best) { best = miss; targetMinusRound = closest; timeOfFlightS = t + StepS * s; }
                // Past the target and opening: the closest approach is behind us.
                if (s < 1f && Vector3.Dot(rel1, v) < 0f) break;
                rel0 = rel1;
            }
            return best;
        }

        /// <summary>As above, the miss alone.</summary>
        public static float MissDistance(in GunProfile gun, Vector3 muzzle, Vector3 shooterVelocity, Vector3 boreWorld,
                                         Vector3 targetPosition, Vector3 targetVelocity) =>
            MissDistance(gun, muzzle, shooterVelocity, boreWorld, targetPosition, targetVelocity, out _, out _);
    }
}
