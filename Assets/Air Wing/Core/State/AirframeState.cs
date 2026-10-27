using UnityEngine;

namespace DeepWater.AirWing.Core.State
{
    public enum FlightStatus : byte
    {
        Flying = 0,
        /// <summary>Met the surface (sea or runway) with no gear to take it. Terminal at L0.</summary>
        Crashed = 1,
    }

    /// <summary>
    /// Flight Controls L1's own state (W23): where the surfaces stand and the law's memory. Zero at L0. Kept in the
    /// aircraft state, not in the part, so the Fix covers it and a reset (Q6) clears it.
    /// </summary>
    public struct ControlState
    {
        /// <summary>Surface positions, radians, in the HARV sign convention (see <c>ControlSurfaces</c>).</summary>
        public float StabilatorRad, AileronRad, RudderRad;
        /// <summary>The G loop's integral, as a pitch-rate command, radians per second.</summary>
        public float PitchIntegralRadS;
        /// <summary>The prefiltered G command, g.</summary>
        public float LoadFactorCommand;
    }

    /// <summary>
    /// One aircraft's state, the lingua franca of every ladder level (C4): each level reads it, steps it and
    /// writes it back, so an aircraft can change level between runs without a conversion. Fields a level does not
    /// model keep their last value (fuel at L0, for example). SI units throughout (W22); Unity's axes and
    /// handedness (+X right, +Y up, +Z forward, left-handed).
    /// </summary>
    public struct AirframeState
    {
        /// <summary>World position, metres.</summary>
        public Vector3 Position;
        /// <summary>World velocity, metres per second.</summary>
        public Vector3 Velocity;
        /// <summary>Body to world rotation; the body's +Z is the nose, +Y the canopy.</summary>
        public Quaternion Attitude;
        /// <summary>
        /// Angular velocity in body axes, radians per second (x pitch, y yaw, z roll), in Unity's sense: positive about
        /// +X pitches the nose down, about +Y yaws right, about +Z rolls the right wing up. At L0, a point mass with no
        /// sideslip, these are the flight path's (wind-axis) rates; the body is drawn the angle of attack above it.
        /// </summary>
        public Vector3 BodyRates;
        /// <summary>Load factor along the lift (the body's +Y at L0), in g: 1 in level flight.</summary>
        public float LoadFactor;
        /// <summary>Angle of attack, radians: how far the nose sits above the flight path.</summary>
        public float AngleOfAttack;
        /// <summary>Current mass, kilograms (changes with fuel at L3).</summary>
        public float MassKg;
        /// <summary>Fuel aboard, kilograms (burned from L3).</summary>
        public float FuelKg;
        /// <summary>Engine spool, 0 (idle) … 1 (full), afterburner excluded (spool dynamics from Powerplant L1).</summary>
        public float EngineSpool;
        /// <summary>The afterburner's share of its extra thrust, 0 (out) … 1 (full); Powerplant L1. Follows the switch at L0.</summary>
        public float AfterburnerSpool;
        public FlightStatus Status;
        /// <summary>Touching a surface through PhysX this tick (L1+). Such ticks are flagged in the Fix (W7).</summary>
        public bool InContact;
        /// <summary>The landing gear, 0 (up) … 1 (down and locked); Landing Gear L1.</summary>
        public float GearDeployed;
        /// <summary>Flight Controls L1's surfaces and memory (zero at L0).</summary>
        public ControlState Controls;
    }
}
