namespace CarrierOps.Core.State
{
    /// <summary>
    /// Catapult cycle stages. Each maps to a real-world step in fleet operations, compressed
    /// to what matters for visual + physical sim. Each stage has a profile-driven duration
    /// (except Firing, whose duration is determined by the acceleration profile + stroke length).
    ///
    /// Transitions:
    /// Idle → Spotted → Tensioned → Ready → Firing → Retracting → Idle
    ///
    /// The state machine fires Animator triggers and modulates physics as it advances.
    /// </summary>
    public enum CatapultStage : byte
    {
        /// <summary>No aircraft on the cat; shuttle at the aft (retracted) position. Idle ready to accept.</summary>
        Idle       = 0,

        /// <summary>Aircraft taxied onto the cat track and stopped over the shuttle.</summary>
        Spotted    = 1,

        /// <summary>Shuttle hooked to aircraft launch bar; holdback bar tensioned. Engines spooling up. JBD up.</summary>
        Tensioned  = 2,

        /// <summary>Final checks complete; "salute and shoot" position. Last-call holding.</summary>
        Ready      = 3,

        /// <summary>Steam catapult firing — shuttle accelerating down the track with the aircraft attached.</summary>
        Firing     = 4,

        /// <summary>Aircraft released at end of stroke; shuttle returning to start position. JBD lowering.</summary>
        Retracting = 5,
    }
}
