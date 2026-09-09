namespace CarrierOps.Core.State
{
    /// <summary>
    /// Arresting wire stages — symmetric in shape to <see cref="CatapultStage"/> but
    /// inverted in purpose. The wire sits idle, engages an aircraft, decelerates it, then
    /// retracts to reset.
    ///
    /// Transitions: Idle → Engaged → Decelerating → Retracting → Idle
    ///
    /// **Engaged vs. Decelerating:** in fleet operations the very-first instant of
    /// engagement is a brief impulse (tailhook catches, wire snags, "thunk"). In sim terms
    /// it's a single-step kinematic snap — the aircraft binds to the wire centerline. The
    /// Decelerating stage handles the bulk of the work: applying the deceleration profile
    /// along the wire's stroke until the aircraft is stopped (or has been pulled the full
    /// stroke length).
    /// </summary>
    public enum WireStage : byte
    {
        Idle         = 0,
        Engaged      = 1,
        Decelerating = 2,
        Retracting   = 3,
    }
}
