namespace CarrierOps.Core.State
{
    /// <summary>
    /// Per-wire runtime state. Stored in a fixed-size array on <see cref="CarrierState"/>,
    /// sized from <see cref="CarrierProfileData.WireCount"/> at construction.
    ///
    /// **State machine:** see <see cref="WireStage"/>.
    ///
    /// **Identity of the engaged aircraft:** stored as an integer "registration ID" rather
    /// than a managed reference, so this struct stays unmanaged-friendly. The
    /// <see cref="CarrierEntity"/> manages the int → IRecoveringAircraft mapping via a
    /// registration table.
    /// </summary>
    public struct WireState
    {
        public WireStage Stage;
        public float     StageTimer;
        public int       EngagedAircraftId;       // 0 = none; positive int from the entity's registry
        public float     RunoutMeters;            // distance the engaged aircraft has been pulled along the wire
        public float     AircraftSpeedAtCatch;    // captured at the moment of engagement, for stop-distance accounting

        public static WireState Idle => new WireState
        {
            Stage = WireStage.Idle,
            StageTimer = 0f,
            EngagedAircraftId = 0,
            RunoutMeters = 0f,
            AircraftSpeedAtCatch = 0f,
        };
    }
}
