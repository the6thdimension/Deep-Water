using UnityEngine;

namespace GuidedFury.Core.State
{
    /// <summary>
    /// Universal guidance-to-physics command. Produced by an IGuidanceLaw (later phase),
    /// consumed by an IPhysicsIntegrator. The struct is the contract — neither side reaches
    /// into the other (legacy anti-pattern AP4).
    ///
    /// **Design intent:** keep the command at the highest abstraction the *lowest* integrator
    /// can consume directly. A lateral acceleration in world frame is enough for L0..L2 and
    /// can be mapped to body-rate or surface-deflection commands inside the higher-LOD
    /// integrators. We can add lower-level fields (BodyRateCommand, ControlDeflections) when
    /// L3/L4 actually need to be commanded that way — adding fields is non-breaking; removing
    /// is.
    ///
    /// **Phase 1 surface (intentionally minimal):**
    /// - CommandedAccelerationWorld: the "what guidance wants" — lateral accel in m/s², world
    ///   frame. Magnitude is not pre-limited; integrators apply their own g-limits.
    /// - ThrustThrottle: [0,1] requested propellant flow. Boost-only motors typically ignore
    ///   this and burn fixed; cruise motors honor it.
    /// - ArmCommand: latching arm signal from the launch system. Once true, stays true.
    /// - DetonateCommand: latching command to detonate now (e.g. command-detonate from a fire
    ///   control unit, or self-destruct from the fuze).
    ///
    /// All fields default to zero/false → a default(MissileCommand) is a valid "do nothing"
    /// command, useful for tests and pre-launch ticks.
    /// </summary>
    public struct MissileCommand
    {
        public Vector3 CommandedAccelerationWorld; // m/s², world frame, lateral guidance command
        public float   ThrustThrottle;             // [0,1]
        public bool    ArmCommand;
        public bool    DetonateCommand;
    }
}
