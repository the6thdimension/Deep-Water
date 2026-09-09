namespace GuidedFury.Core.Seekers
{
    /// <summary>
    /// Unmanaged seeker configuration. Carved out of the missile profile so the seeker
    /// doesn't need to know about the rest of the profile fields.
    ///
    /// **Phase 3 fields (intentionally minimal):**
    /// - FovDeg: full cone angle of the seeker FOV, centered on the missile's forward axis.
    /// - MaxRangeM: maximum acquisition range (and lock-retention range).
    /// - AcquisitionTimeS: dwell time required for the seeker to declare lock once the
    ///   geometric conditions (in-FOV + in-range) are met. Models the time the seeker's
    ///   tracker needs to discriminate signal from clutter.
    /// </summary>
    public struct SeekerProfile
    {
        public float FovDeg;
        public float MaxRangeM;
        public float AcquisitionTimeS;

        /// <summary>
        /// Track-memory ("coast") duration after geometric break-lock, seconds. While
        /// coasting, the seeker keeps reporting a dead-reckoned track and reacquisition is
        /// seamless (no dwell penalty). 0 = legacy behavior: drop lock instantly.
        /// Models a real tracker's memory mode; prevents guidance blackouts during brief
        /// off-boresight excursions and the terminal flyby LOS swing.
        /// </summary>
        public float CoastTimeS;
    }
}
