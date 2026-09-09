namespace CarrierOps.Core.State
{
    /// <summary>
    /// Sea-state authoring data — small, unmanaged-friendly. Carried in the carrier profile
    /// and consumed by the motion model to drive deterministic sway (heave/roll/pitch).
    ///
    /// **Phase 1 surface — sum-of-sines model.** Three sinusoidal components per axis are
    /// blended; the motion model uses these amplitude / period values as the dominant band.
    /// Real ocean spectra (Pierson–Moskowitz, JONSWAP) are future work; this is good enough
    /// for Beaufort 0–6 visual fidelity.
    ///
    /// **Beaufort reference (approximate dominant period in seconds, significant wave height m):**
    /// | Beaufort | Wave height | Period | Notes |
    /// |---|---|---|---|
    /// | 0 (calm) | 0 m | — | Glassy. All amplitudes ~0. |
    /// | 3 (gentle breeze) | 0.6 m | 4 s | Mild rolling. |
    /// | 5 (fresh breeze) | 2 m | 6 s | Moderate. Carrier feels it. |
    /// | 6 (strong breeze) | 3 m | 7 s | Visible deck motion. |
    /// | 8 (gale) | 5.5 m | 9 s | Heavy. Flight ops suspended in reality. |
    ///
    /// Units: amplitudes in meters (heave) or degrees (roll, pitch); periods in seconds.
    /// </summary>
    public struct SeaStateData
    {
        // -- Heave (vertical translation) ---------------------------------
        public float HeaveAmplitudeM;   // peak vertical displacement at the ship's center, meters
        public float HeavePeriodS;      // dominant period, seconds

        // -- Roll (rotation about the longitudinal axis) ------------------
        public float RollAmplitudeDeg;  // peak roll, degrees
        public float RollPeriodS;       // dominant period, seconds

        // -- Pitch (rotation about the transverse axis) -------------------
        public float PitchAmplitudeDeg; // peak pitch, degrees
        public float PitchPeriodS;      // dominant period, seconds

        // -- Determinism --------------------------------------------------
        public int Seed;                // RNG seed for harmonic phase offsets — per P7

        /// <summary>Calm sea — all amplitudes zero. Useful default and test fixture.</summary>
        public static SeaStateData Calm => new SeaStateData
        {
            HeaveAmplitudeM   = 0f, HeavePeriodS   = 4f,
            RollAmplitudeDeg  = 0f, RollPeriodS    = 6f,
            PitchAmplitudeDeg = 0f, PitchPeriodS   = 5f,
            Seed = 12345,
        };

        /// <summary>Beaufort ~5 — fresh breeze. Reasonable "weather" default for ops.</summary>
        public static SeaStateData FreshBreeze => new SeaStateData
        {
            HeaveAmplitudeM   = 0.30f, HeavePeriodS  = 6f,
            RollAmplitudeDeg  = 1.5f,  RollPeriodS   = 8f,
            PitchAmplitudeDeg = 0.6f,  PitchPeriodS  = 6f,
            Seed = 12345,
        };
    }
}
