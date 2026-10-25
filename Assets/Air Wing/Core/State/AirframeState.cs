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
        /// <summary>Angular velocity in body axes, radians per second (x pitch, y yaw, z roll).</summary>
        public Vector3 BodyRates;
        /// <summary>Load factor along the body's +Y axis, in g (1 in level flight).</summary>
        public float LoadFactor;
        /// <summary>Current mass, kilograms (changes with fuel at L3).</summary>
        public float MassKg;
        /// <summary>Fuel aboard, kilograms (burned from L3).</summary>
        public float FuelKg;
        /// <summary>Engine spool, 0 (idle) … 1 (full), afterburner excluded (spool dynamics from Powerplant L1).</summary>
        public float EngineSpool;
        public FlightStatus Status;
        /// <summary>Touching a surface through PhysX this tick (L1+). Such ticks are flagged in the Fix (W7).</summary>
        public bool InContact;
    }
}
