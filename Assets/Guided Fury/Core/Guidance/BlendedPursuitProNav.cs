using UnityEngine;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Guidance
{
    /// <summary>
    /// Pursuit→ProNav blend for engagements that start far off a collision course —
    /// above all vertical (VLS) launches, where the target sits ~90° off the velocity
    /// vector and pure TPN is degenerate (closing speed ≈ 0 → commanded accel ≈ 0 →
    /// the missile climbs ballistically forever; observed 2026-09-08 on the range's
    /// VLS pad: apogee 10.3 km, zero tip-over).
    ///
    /// **The blend:** weight pursuit by how far the geometry is from a collision course,
    /// measured by the closing ratio Vc / |v|:
    /// - Vc/|v| ≤ 0 (beam / opening geometry): pure pursuit — bend the velocity vector at
    ///   the target. This IS the pitch-over maneuver, emerging from geometry instead of a
    ///   scripted flight program.
    /// - Vc/|v| ≥ ~0.9 (closing nearly head-on along the LOS): pure ProNav — optimal
    ///   terminal homing, exactly the law that passed the air-envelope battery.
    /// - In between: linear blend. Continuous, stateless, deterministic — no mode flags,
    ///   no timers, so there is no discrete "handover" event to mis-order.
    ///
    /// Real doctrine analog: vertical-launch pitch-over + midcourse shaping followed by
    /// terminal PN. Selecting this law replaces neither Pursuit nor ProNav — both remain
    /// available standalone.
    /// </summary>
    public sealed class BlendedPursuitProNav : IGuidanceLaw
    {
        public static readonly BlendedPursuitProNav Instance = new BlendedPursuitProNav();
        private BlendedPursuitProNav() { }

        public GuidanceLawKind Kind => GuidanceLawKind.PursuitThenProNav;

        // Closing-ratio band over which pursuit fades out and ProNav takes over.
        private const float PursuitFullBelow = 0.0f;  // Vc/|v| at or below → pure pursuit
        private const float ProNavFullAbove  = 0.9f;  // Vc/|v| at or above → pure ProNav

        public MissileCommand ComputeCommand(in GuidanceContext context, in MissileProfileData profile)
        {
            if (!context.Target.HasTrack)
                return default;

            // Closing ratio: how much of our speed is actually spent closing the LOS.
            Vector3 r = context.Target.Position - context.State.Position;
            float range = r.magnitude;
            float speed = context.State.Velocity.magnitude;
            float closingRatio = 0f;
            if (range > 1e-3f && speed > 1e-3f)
            {
                Vector3 rHat = r / range;
                Vector3 vRel = context.Target.Velocity - context.State.Velocity;
                float vc = -Vector3.Dot(vRel, rHat);
                closingRatio = vc / speed;
            }

            float proNavWeight = Mathf.InverseLerp(PursuitFullBelow, ProNavFullAbove, closingRatio);

            MissileCommand pursuit = PursuitGuidance.Instance.ComputeCommand(in context, in profile);
            MissileCommand proNav  = ProportionalNavigation.Instance.ComputeCommand(in context, in profile);

            return new MissileCommand
            {
                CommandedAccelerationWorld = Vector3.Lerp(
                    pursuit.CommandedAccelerationWorld, proNav.CommandedAccelerationWorld, proNavWeight),
                ThrustThrottle = 1f,
                ArmCommand     = true,
                DetonateCommand = false,
            };
        }
    }
}
