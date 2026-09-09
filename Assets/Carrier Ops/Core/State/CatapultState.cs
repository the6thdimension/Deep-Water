using UnityEngine;

namespace CarrierOps.Core.State
{
    /// <summary>
    /// Per-catapult runtime state. Lives in <see cref="CarrierState"/> as a fixed-size array
    /// (4 catapults on a Ford-class). Carrier has 4; nothing in the code assumes that count —
    /// the array size lives in the profile.
    /// </summary>
    public struct CatapultState
    {
        public CatapultStage Stage;
        public float         StageTimer;         // seconds elapsed in current stage
        public float         ShuttleDistanceM;   // distance traveled by shuttle along the track (0 at aft, == StrokeLengthM at fwd)
        public float         ShuttleVelocityMps; // current speed of the shuttle
        public bool          JbdRaised;          // visual / animator-tracked: is the Jet Blast Deflector behind this cat raised?

        /// <summary>A fresh idle state.</summary>
        public static CatapultState Idle => new CatapultState
        {
            Stage             = CatapultStage.Idle,
            StageTimer        = 0f,
            ShuttleDistanceM  = 0f,
            ShuttleVelocityMps = 0f,
            JbdRaised         = false,
        };
    }
}
