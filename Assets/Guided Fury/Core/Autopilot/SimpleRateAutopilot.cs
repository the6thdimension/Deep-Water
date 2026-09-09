using UnityEngine;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Autopilot
{
    /// <summary>
    /// L3-style direct-rate controller. Extracted from the inline logic in
    /// <see cref="GuidedFury.Core.Integrators.PseudoRb6DofL3Integrator"/>.
    ///
    /// **Algorithm:**
    /// 1. Convert commanded lateral accel to a commanded body rate using the identity
    ///    ω = (v × a_cmd) / |v|²  (matches the LOS-rate calc in ProNav, by no coincidence —
    ///    if guidance produces a_lat that would rotate the velocity vector at angular rate ω,
    ///    then to follow it we need to rotate the body at that same ω.)
    /// 2. Compute rate error ω_cmd − ω_current.
    /// 3. Apply a torque proportional to the error, scaled by mass and `AutopilotGain`.
    ///    Scaling by mass is a unit convenience so the gain has reasonable scale across
    ///    missile sizes; a proper PID would scale by inertia instead.
    ///
    /// **Stateless singleton.** L3 keeps using its inline version for now; new code (L4) goes
    /// through this. A future cleanup pass should refactor L3 to call this and delete its
    /// duplicated inline code.
    /// </summary>
    public sealed class SimpleRateAutopilot : IAutopilot
    {
        public static readonly SimpleRateAutopilot Instance = new SimpleRateAutopilot();
        private SimpleRateAutopilot() { }

        public AutopilotKind Kind => AutopilotKind.SimpleRate;

        public AutopilotOutput Compute(in MissileState state, in MissileCommand command, in MissileProfileData profile)
        {
            Vector3 v = state.Velocity;
            float speedSq = v.sqrMagnitude;

            Vector3 commandedBodyRate = Vector3.zero;
            Vector3 aCmd = command.CommandedAccelerationWorld;
            if (speedSq > 1f && aCmd.sqrMagnitude > 1e-6f)
                commandedBodyRate = Vector3.Cross(v, aCmd) / speedSq;

            Vector3 currentBodyRateWorld = state.Orientation * state.AngularVelocity;
            Vector3 rateErrorWorld = commandedBodyRate - currentBodyRateWorld;

            return new AutopilotOutput
            {
                ControlTorqueWorld = rateErrorWorld * (profile.AutopilotGain * state.Mass),
                PitchDeflectionRad = 0f,
                YawDeflectionRad   = 0f,
            };
        }
    }
}
