namespace CarrierOps.Core.State
{
    /// <summary>Elevator stages — small, intentionally minimal vs. catapult.</summary>
    public enum ElevatorStage : byte
    {
        Stowed    = 0,   // at one limit (hangar level)
        Moving    = 1,   // traveling to the other limit
        Deployed  = 2,   // at the other limit (flight deck level)
    }

    /// <summary>
    /// Per-elevator runtime state. CVN-78 has 3 aircraft elevators. `Travel` is normalized
    /// [0..1] — 0 = stowed (hangar), 1 = deployed (flight deck). The transform offset to
    /// apply is `Travel * profile.TravelMeters` along the elevator's local axis.
    /// </summary>
    public struct ElevatorState
    {
        public ElevatorStage Stage;
        public float         Travel;          // [0..1] normalized position
        public bool          CommandUp;       // most recent command: true = deploy, false = stow

        public static ElevatorState Stowed => new ElevatorState
        {
            Stage     = ElevatorStage.Stowed,
            Travel    = 0f,
            CommandUp = false,
        };
    }
}
