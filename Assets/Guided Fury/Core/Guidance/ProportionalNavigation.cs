using UnityEngine;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Guidance
{
    /// <summary>
    /// True Proportional Navigation (TPN) — the workhorse intercept law for guided missiles.
    ///
    /// **The idea in one sentence:** if the line-of-sight to the target is rotating, command
    /// a lateral acceleration proportional to that rotation rate, multiplied by closing
    /// velocity. When the LOS stops rotating, the missile is on a collision course.
    ///
    /// **The formula (vector form):**
    /// - r       = T - M                  (relative position; missile→target)
    /// - vRel    = vT - vM                (relative velocity)
    /// - rHat    = r / |r|                (LOS unit vector)
    /// - Vc      = -dot(vRel, rHat)       (closing speed, positive when approaching)
    /// - omega   = cross(r, vRel) / |r|²  (LOS rotation rate vector, perpendicular to LOS)
    /// - a_M     = N · Vc · cross(omega, rHat)
    ///
    /// Where N is the navigation gain (typically 3 to 5). The cross(omega, rHat) gives an
    /// acceleration vector perpendicular to the LOS, in the plane of the engagement.
    ///
    /// **Why it works:** ProNav's command is zero whenever the LOS rate is zero — which is
    /// the geometric condition for collision. So the law literally tries to drive the LOS
    /// rate to zero, which is interception. Against constant-velocity targets, ProNav with
    /// N=3 is optimal in the sense that it minimizes the integrated lateral-accel "effort"
    /// required to intercept.
    ///
    /// **Limitations of TPN as implemented here:**
    /// - Performance against highly maneuvering targets degrades; APN (Augmented ProNav, with
    ///   a target-accel term) fixes that. Phase 3+ when we need it.
    /// - No g-limit applied here — that belongs to the L2 integrator (rate-limited 3DOF).
    ///   ProNav can ask for arbitrarily large accel; physics decides if it can deliver.
    /// - Closing-speed sign: if Vc &lt; 0 (missile is opening), command is reversed direction.
    ///   That's correct behavior — the law tells you to turn around. Whether that makes sense
    ///   for your missile is a guidance-doctrine question, not a math question.
    ///
    /// **Stateless singleton** — pure function of context.
    /// </summary>
    public sealed class ProportionalNavigation : IGuidanceLaw
    {
        public static readonly ProportionalNavigation Instance = new ProportionalNavigation();
        private ProportionalNavigation() { }

        public GuidanceLawKind Kind => GuidanceLawKind.ProportionalNavigation;

        public MissileCommand ComputeCommand(in GuidanceContext context, in MissileProfileData profile)
        {
            if (!context.Target.HasTrack)
                return default;

            // Relative kinematics.
            Vector3 r = context.Target.Position - context.State.Position;
            float rangeSq = r.sqrMagnitude;
            if (rangeSq < 1e-4f)
                return default; // co-located; no meaningful guidance

            Vector3 vRel = context.Target.Velocity - context.State.Velocity;
            Vector3 rHat = r * (1f / Mathf.Sqrt(rangeSq));

            // Closing speed: positive when the missile is approaching the target.
            float closingSpeed = -Vector3.Dot(vRel, rHat);

            // LOS rate (omega): perpendicular to LOS, magnitude is the angular rate in rad/s.
            // Derivation: d/dt (r̂) = (omega × r̂); omega = (r × v_rel) / r².
            Vector3 omega = Vector3.Cross(r, vRel) / rangeSq;

            // Acceleration command: a = N · Vc · (omega × r̂)
            // This is perpendicular to the LOS, in the plane of the engagement.
            float gain = Mathf.Max(profile.NavigationGain, 0f);
            Vector3 accel = gain * closingSpeed * Vector3.Cross(omega, rHat);

            return new MissileCommand
            {
                CommandedAccelerationWorld = accel,
                ThrustThrottle             = 1f,
                ArmCommand                 = true,
                DetonateCommand            = false,
            };
        }
    }
}
