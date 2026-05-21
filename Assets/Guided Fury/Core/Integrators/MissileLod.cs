namespace GuidedFury.Core.Integrators
{
    /// <summary>
    /// Level-of-Detail tiers for missile physics simulation.
    ///
    /// Each tier represents a strict increase in fidelity and CPU cost. The same MissileState
    /// flows through every tier — switching LOD only changes which integrator advances the state.
    /// See METHODOLOGY.md C4 for the "universal state struct" candidate principle this enables.
    /// </summary>
    public enum MissileLod
    {
        /// <summary>
        /// Kinematic trajectory. No physics engine, no rotation dynamics. Position evolves
        /// either ballistically under gravity (dumb bomb) or constant-speed toward a guidance
        /// direction (cheap homing). Designed for very large salvos or distant tracks where
        /// the player will never see the difference.
        /// </summary>
        L0_Kinematic = 0,

        /// <summary>
        /// 3DOF point mass. Forces: gravity, drag, thrust along velocity. Orientation follows
        /// velocity vector (no independent rotation dynamics). No turn-rate limit. Acceleration
        /// commands honored instantly. Useful for far engagements where realism of the
        /// trajectory shape matters but airframe dynamics do not.
        /// </summary>
        L1_PointMass3Dof = 1,

        /// <summary>
        /// 3DOF + turn-rate limit. As L1, but lateral acceleration is bounded by a profile-
        /// configured g-limit and turn-rate limit. Most non-hero engagements live here:
        /// realistic intercept geometry without the cost of full rigid-body dynamics.
        /// </summary>
        L2_RateLimited3Dof = 2,

        /// <summary>
        /// Pseudo-6DOF. Full rigid-body orientation with simplified aero: scalar drag, scalar
        /// lift, body-axis thrust, stability "weather-vane" torque. Tracks angle-of-attack
        /// roughly. Suitable for close engagements and hero missiles where 6DOF visual fidelity
        /// matters but full coefficient tables do not.
        /// </summary>
        L3_PseudoRb6Dof = 3,

        /// <summary>
        /// Full 6DOF. Aerodynamic coefficient model (CD, CL, CM, CN as functions of Mach,
        /// angle-of-attack, sideslip, and control deflections), mass properties (Ixx, Iyy,
        /// Izz, Ixz), control surfaces with deflection/rate limits, autopilot loop on top of
        /// guidance, Mach-dependent atmosphere coupling. The "hero missile" tier.
        /// </summary>
        L4_FullAero6Dof = 4,

        /// <summary>
        /// Hardware-in-the-loop fidelity. L4 plus seeker dynamics (gimbal rates, FOV,
        /// boresight noise, glint), INS drift, autopilot bandwidth limits, fuze logic at
        /// detector granularity. Reserved for high-fidelity exercise replay and seeker R&amp;D;
        /// not needed for general gameplay.
        /// </summary>
        L5_HardwareInTheLoop = 5,
    }
}
