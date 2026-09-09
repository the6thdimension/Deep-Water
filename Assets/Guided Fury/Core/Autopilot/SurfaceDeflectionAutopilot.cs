using UnityEngine;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Autopilot
{
    /// <summary>
    /// L4 two-loop autopilot. Outer loop: lateral-accel command → commanded body rate.
    /// Inner loop: body-rate error → commanded surface deflection.
    ///
    /// **Why two loops:** real autopilots can't apply arbitrary torque to the airframe —
    /// they deflect control surfaces, which produce aero moments via Cm_δ × q × A × arm.
    /// The inner loop converts "I want this body rate" into "deflect the fins this much"
    /// via a proportional gain. Saturated deflection clips at the profile's max deflection;
    /// the inner-loop integration handles the rate limit downstream in the integrator.
    ///
    /// **Tunable gains** come from the profile:
    /// - `AutopilotGain` reused as the inner-loop rate→deflection gain.
    /// - Outer loop is the same kinematic identity as <see cref="SimpleRateAutopilot"/>.
    ///
    /// **Sign convention** matches the aero model:
    /// - Positive pitch deflection commands a NOSE-UP body rotation if Cm_δ is negative.
    /// - Caller flips the sign as appropriate; the autopilot doesn't try to be clever about
    ///   sign conventions — it produces "deflection in the direction that would correct the
    ///   error if the airframe is stable and Cm_δ has the conventional negative sign."
    ///
    /// **Stateless singleton.**
    /// </summary>
    public sealed class SurfaceDeflectionAutopilot : IAutopilot
    {
        public static readonly SurfaceDeflectionAutopilot Instance = new SurfaceDeflectionAutopilot();
        private SurfaceDeflectionAutopilot() { }

        public AutopilotKind Kind => AutopilotKind.SurfaceDeflection;

        public AutopilotOutput Compute(in MissileState state, in MissileCommand command, in MissileProfileData profile)
        {
            Vector3 v = state.Velocity;
            float speedSq = v.sqrMagnitude;

            // Outer loop: accel command → commanded body rate.
            Vector3 commandedBodyRate = Vector3.zero;
            Vector3 aCmd = command.CommandedAccelerationWorld;
            if (speedSq > 1f && aCmd.sqrMagnitude > 1e-6f)
                commandedBodyRate = Vector3.Cross(v, aCmd) / speedSq;

            // Inner loop: rate error in BODY frame → commanded deflection.
            // We need rates in body frame because deflections are body-axis (pitch=body X, yaw=body Y).
            Vector3 currentBodyRateWorld = state.Orientation * state.AngularVelocity;
            Vector3 rateErrorWorld = commandedBodyRate - currentBodyRateWorld;
            Vector3 rateErrorBody = Quaternion.Inverse(state.Orientation) * rateErrorWorld;

            // Cm_delta is negative: a corrective positive body torque requires
            // negative deflection. Opposing the rate error would create positive feedback.
            float gain = profile.AutopilotGain;
            float maxDeflRad = profile.MaxControlDeflectionDeg * Mathf.Deg2Rad;

            float pitch = Mathf.Clamp(-gain * rateErrorBody.x, -maxDeflRad, maxDeflRad);
            float yaw   = Mathf.Clamp(-gain * rateErrorBody.y, -maxDeflRad, maxDeflRad);

            return new AutopilotOutput
            {
                ControlTorqueWorld = Vector3.zero, // L4 uses deflections, not torque
                PitchDeflectionRad = pitch,
                YawDeflectionRad   = yaw,
            };
        }
    }
}

