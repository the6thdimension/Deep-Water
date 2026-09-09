using UnityEngine;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Guidance
{
    /// <summary>
    /// Pure pursuit guidance: aim the velocity vector directly at the current target position.
    ///
    /// **What it does mathematically:**
    /// - LOS  = (target.position − missile.position).normalized
    /// - vHat = current velocity direction
    /// - The error is the angle between vHat and LOS. To close it, we apply a lateral
    ///   acceleration in the plane spanned by (vHat, LOS), pointing from vHat toward LOS.
    /// - Magnitude: K_p × |speed|², which keeps the turn rate roughly proportional to speed
    ///   for a given gain. (K_p borrowed from the profile's NavigationGain for now — pursuit
    ///   is a single-gain law.)
    ///
    /// **Why it's mostly a baseline, not a real-world doctrine:**
    /// Pursuit tail-chases — it does NOT lead a moving target. Against a crossing target,
    /// pursuit produces ever-tightening turns and typically misses. It's here as the simplest
    /// possible guidance law, a sanity reference for tests, and a starting point for
    /// understanding the seam. Real intercepts use Proportional Navigation.
    ///
    /// **Stateless singleton** — no per-missile data.
    /// </summary>
    public sealed class PursuitGuidance : IGuidanceLaw
    {
        public static readonly PursuitGuidance Instance = new PursuitGuidance();
        private PursuitGuidance() { }

        public GuidanceLawKind Kind => GuidanceLawKind.Pursuit;

        public MissileCommand ComputeCommand(in GuidanceContext context, in MissileProfileData profile)
        {
            if (!context.Target.HasTrack)
                return default;

            Vector3 toTarget = context.Target.Position - context.State.Position;
            float distance = toTarget.magnitude;
            if (distance < 1e-3f)
                return default; // already on top of the target — nothing useful to command

            Vector3 los = toTarget / distance;

            // Heading reference: prefer current velocity direction, fall back to orientation
            // forward when stationary (e.g. very first tick before any integration).
            Vector3 speedHat;
            float speed = context.State.Velocity.magnitude;
            if (speed > 1e-3f)
                speedHat = context.State.Velocity / speed;
            else
                speedHat = context.State.Orientation * Vector3.forward;

            // The lateral accel command is the component of LOS perpendicular to current
            // heading, scaled by gain × speed². This bends the velocity vector toward LOS.
            // Vector3.ProjectOnPlane(los, speedHat) gives the LOS component perpendicular to
            // the heading — exactly the direction we want to push.
            Vector3 lateral = Vector3.ProjectOnPlane(los, speedHat);

            float gain = Mathf.Max(profile.NavigationGain, 0f);
            float speedSquared = Mathf.Max(speed * speed, 1f); // floor to avoid zero accel at standstill

            return new MissileCommand
            {
                CommandedAccelerationWorld = lateral * (gain * speedSquared),
                ThrustThrottle             = 1f,
                ArmCommand                 = true,
                DetonateCommand            = false,
            };
        }
    }
}
