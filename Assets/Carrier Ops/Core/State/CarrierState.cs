using UnityEngine;

namespace CarrierOps.Core.State
{
    /// <summary>
    /// Universal carrier state. Lives in the entity, mutated by the various subsystems
    /// (motion, movement, catapults, elevators). The MonoBehaviour adapter mirrors the
    /// relevant fields to the Unity transform / animators each FixedUpdate.
    ///
    /// **Coordinate conventions:**
    /// - Position / Heading: world-frame. Heading in degrees clockwise from north (0 = +Z by Unity convention).
    /// - SwayOffset: world-frame translation from the calm-water "settled" pose. Driven by the motion model.
    /// - SwayPitch / SwayRoll: ship-frame rotations from settled. Degrees.
    /// - SpeedKnots: ship's translational speed over ground.
    /// - TurnRateDegPerSec: instantaneous rate of heading change (positive = starboard).
    /// - TimeOfSim: seconds since the entity was constructed. Used for the deterministic
    ///   motion model so we don't read Time.time.
    /// - Catapults / Elevators: fixed-size runtime state arrays.
    ///
    /// **Why a class container with array fields, not a single struct:** the catapult and
    /// elevator arrays are mutable subsystems. A struct-of-arrays would force allocation per
    /// step. The state object is allocated once at entity construction.
    /// </summary>
    public sealed class CarrierState
    {
        public Vector3 Position;
        public float   HeadingDeg;
        public float   SpeedKnots;
        public float   TurnRateDegPerSec;

        // -- Sea-state-driven sway (delta from settled pose, mutated by IShipMotion) -----
        public Vector3 SwayOffset;    // meters, world-frame heave (Y) + small surge/sway noise
        public float   SwayPitchDeg;
        public float   SwayRollDeg;

        // -- Subsystem state -------------------------------------------------------------
        public CatapultState[] Catapults;
        public ElevatorState[] Elevators;
        public WireState[]     Wires;
        public FlolsState      Flols;        // updated against the nearest registered IRecoveringAircraft each step

        // -- Time bookkeeping ------------------------------------------------------------
        public double TimeOfSim;      // seconds since entity construction; double to avoid drift over long runs

        public CarrierState(int catapultCount, int elevatorCount, int wireCount)
        {
            Position = Vector3.zero;
            HeadingDeg = 0f;
            SpeedKnots = 0f;
            TurnRateDegPerSec = 0f;
            SwayOffset = Vector3.zero;
            SwayPitchDeg = 0f;
            SwayRollDeg = 0f;
            Catapults = new CatapultState[catapultCount];
            Elevators = new ElevatorState[elevatorCount];
            Wires     = new WireState[wireCount];
            Flols     = FlolsState.NoTrack;
            for (int i = 0; i < catapultCount; i++) Catapults[i] = CatapultState.Idle;
            for (int i = 0; i < elevatorCount; i++) Elevators[i] = ElevatorState.Stowed;
            for (int i = 0; i < wireCount; i++)     Wires[i]     = WireState.Idle;
            TimeOfSim = 0.0;
        }
    }
}
