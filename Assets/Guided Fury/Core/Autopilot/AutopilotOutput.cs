using UnityEngine;

namespace GuidedFury.Core.Autopilot
{
    /// <summary>
    /// What an <see cref="IAutopilot"/> produces each step. Two parallel outputs because L3
    /// and L4 expect different things downstream:
    ///
    /// - <see cref="ControlTorqueWorld"/>: a world-frame torque vector. L3's
    ///   `SimpleRateAutopilot` produces this directly; L3 integrator applies it as a torque.
    /// - <see cref="PitchDeflectionRad"/> / <see cref="YawDeflectionRad"/>: commanded control
    ///   surface deflection angles. L4's `SurfaceDeflectionAutopilot` produces these; the L4
    ///   integrator translates them into aero moments via Cm_δ.
    ///
    /// An autopilot fills in the field its kind makes sense for and leaves the other zero.
    /// Integrators read whichever they need.
    /// </summary>
    public struct AutopilotOutput
    {
        public Vector3 ControlTorqueWorld;       // world frame, N·m. Used by L3.
        public float   PitchDeflectionRad;        // commanded pitch (radians). Used by L4.
        public float   YawDeflectionRad;          // commanded yaw   (radians). Used by L4.

        public static AutopilotOutput Zero => default;
    }
}
