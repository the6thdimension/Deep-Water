namespace CarrierOps.Core.State
{
    /// <summary>
    /// FLOLS (Fresnel Lens Optical Landing System, aka "the meatball" / "the ball") output
    /// state. Updated each FixedUpdate against an approaching aircraft. Drives the visible
    /// ball offset on the lens unit on the ship's port side and the cut-light wave-off flag.
    ///
    /// **Conventions:**
    /// - <see cref="BallOffsetNormalized"/> is in [-1..+1]:
    ///     +1 = max high (ball at top of lens window)
    ///      0 = on glideslope (ball centered, datum lights bracket it)
    ///     -1 = max low (ball at bottom)
    /// - <see cref="GlideslopeDeviationDeg"/> is the *signed* deviation from commanded glideslope.
    ///   Positive = high, negative = low. The normalized offset is just this clipped to the lens
    ///   visible window and divided by the half-angle.
    /// - <see cref="IsWaveOff"/> is true when the deviation magnitude exceeds the LSO's
    ///   wave-off threshold (or when the aircraft has crossed below the deck line entirely).
    ///
    /// All values are computed by <see cref="CarrierOps.Core.Recovery.FlolsModel"/>; this struct
    /// is the report.
    /// </summary>
    public struct FlolsState
    {
        public bool  HasTrack;                 // false when no approaching aircraft is registered
        public float BallOffsetNormalized;     // [-1..+1]
        public float GlideslopeDeviationDeg;   // signed; positive = high
        public bool  IsWaveOff;

        public static FlolsState NoTrack => default;
    }
}
