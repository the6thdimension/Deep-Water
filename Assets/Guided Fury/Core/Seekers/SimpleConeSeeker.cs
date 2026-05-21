using UnityEngine;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Seekers
{
    /// <summary>
    /// Geometric cone seeker. Acquires a target when it's inside the FOV cone *and* within
    /// max range, after the acquisition dwell time has elapsed. Holds lock as long as both
    /// conditions remain true; drops lock immediately when either fails.
    ///
    /// **What this models:**
    /// - The geometric constraint every real seeker has: it can only see what's in its
    ///   forward cone. (Body-fixed seekers in Phase 3; gimbaled steerable seekers later.)
    /// - The dwell-time delay required for any real tracker to discriminate a target from
    ///   background and commit to a track.
    ///
    /// **What this does NOT model (Phase 4+):**
    /// - Signal type — no heat signature, no RCS. Any target counts.
    /// - Lock loss with hysteresis (real seekers don't drop lock instantly when target
    ///   briefly leaves the cone — they coast and try to reacquire).
    /// - Gimbal limits — this seeker's cone is rigidly bolted to the missile's nose. A real
    ///   IR seeker can slew the gimbal independently of body attitude.
    /// - Noise, jitter, glint — that's L5.
    /// - Countermeasures — Phase 5+.
    ///
    /// **State:** dwell timer accumulates while geometric conditions hold; cleared when
    /// they fail. Lock is "achieved" when dwell ≥ AcquisitionTimeS, "released" when geometric
    /// conditions fail.
    /// </summary>
    public sealed class SimpleConeSeeker : ISeeker
    {
        private readonly SeekerProfile profile;
        private float dwellTimeS;
        private TargetTrack observation;
        private bool locked;

        public SimpleConeSeeker(SeekerProfile profile)
        {
            this.profile = profile;
        }

        public SeekerKind Kind => SeekerKind.ConeSeeker;
        public bool HasLock => locked;

        public void Update(in MissileState missileState, in TargetTrack truthTarget, float dt)
        {
            // No truth target → nothing to acquire / hold lock on.
            if (!truthTarget.HasTrack)
            {
                ResetLock();
                return;
            }

            // Check geometric conditions: in-FOV and in-range.
            Vector3 toTarget = truthTarget.Position - missileState.Position;
            float distance = toTarget.magnitude;

            bool inRange = distance <= profile.MaxRangeM && distance > 1e-3f;

            // Compare LOS vs missile forward axis. Use dot product against the half-angle
            // cosine — cheaper than computing the actual angle and equivalent.
            // FovDeg is the FULL cone angle, so half-angle is FovDeg/2.
            bool inFov = false;
            if (inRange)
            {
                Vector3 los = toTarget / distance;
                Vector3 forward = missileState.Orientation * Vector3.forward;
                float dot = Vector3.Dot(los, forward);

                // Half-angle in radians, then its cosine. Cache-locality: this is per-step
                // arithmetic per missile; not worth precomputing on the profile.
                float halfAngleRad = profile.FovDeg * 0.5f * Mathf.Deg2Rad;
                float cosHalfAngle = Mathf.Cos(halfAngleRad);

                inFov = dot >= cosHalfAngle;
            }

            if (inRange && inFov)
            {
                // Geometric conditions met — dwell, or report locked.
                dwellTimeS += dt;
                if (dwellTimeS >= profile.AcquisitionTimeS)
                {
                    locked = true;
                    // Pass the truth observation through verbatim — Phase 3 has no noise model.
                    observation = truthTarget;
                }
            }
            else
            {
                // Conditions failed — reset dwell and drop lock immediately. Phase 4+ adds
                // hysteresis / coast for realistic break-lock behaviour.
                ResetLock();
            }
        }

        public TargetTrack GetObservation()
        {
            return locked ? observation : TargetTrack.None;
        }

        private void ResetLock()
        {
            dwellTimeS = 0f;
            locked = false;
            observation = TargetTrack.None;
        }
    }
}
