namespace CarrierOps.Core.State
{
    /// <summary>
    /// Runtime, unmanaged-friendly snapshot of a carrier profile. Baked from
    /// <see cref="CarrierOps.ScriptableObjects.Profiles.CarrierProfileSO"/> at construction;
    /// the entity owns the copy.
    ///
    /// Field-growth policy is the same as Guided Fury's MissileProfileData: add fields per
    /// subsystem as they come online. Adding is non-breaking; removing is.
    /// </summary>
    public struct CarrierProfileData
    {
        // -- Hull / movement ----------------------------------------------
        public float LengthM;                   // overall length (Ford-class ~333 m)
        public float BeamM;                     // beam at waterline (~78 m)
        public float DisplacementTonnes;        // full-load (~100 000 t)
        public float MaxSpeedKnots;             // max speed (~33 kt for Ford)
        public float AccelKnotsPerSec;          // how quickly speed changes toward commanded
        public float MaxTurnRateDegPerSec;      // sustained max turn rate (~0.5°/s)
        public float TurnRateAccelDegPerSec2;   // how quickly turn rate changes toward commanded

        // -- Motion (sea state) -------------------------------------------
        public SeaStateData SeaState;

        // -- Catapult layout ----------------------------------------------
        public int    CatapultCount;            // typically 4 on a Ford
        public float  CatapultStrokeM;          // length of the catapult track (~94 m / 310 ft)
        public float  CatapultEndSpeedMps;      // commanded end speed (~77 m/s ≈ 150 kt for clean F-18E)
        public float  CatapultPeakG;            // peak G during firing (~3 g)

        // Stage timings — seconds spent in each non-physical stage. Firing duration is
        // determined by the acceleration profile + stroke length.
        public float CatapultSpottedDurationS;   // taxi-on, line up
        public float CatapultTensionedDurationS; // run-up, JBD raise, tension to launch bar
        public float CatapultReadyDurationS;     // final checks / "shooter check"
        public float CatapultRetractDurationS;   // shuttle returns to start, JBD lowers

        // -- Elevator layout ----------------------------------------------
        public int    ElevatorCount;            // 3 on a Ford
        public float  ElevatorTravelM;          // travel distance (hangar deck → flight deck, ~8 m)
        public float  ElevatorSpeedMps;         // travel speed (~0.5 m/s typical for fleet)

        // -- FLOLS (optical landing system) -------------------------------
        public float FlolsGlideslopeDeg;        // commanded glideslope angle (3.5° fleet standard)
        public float FlolsWindowHalfAngleDeg;   // visible-window half-angle of the lens; ball saturates at ±this
        public float FlolsWaveOffThresholdDeg;  // deviation past which the LSO calls wave-off

        // -- Arresting gear -----------------------------------------------
        public int   WireCount;                 // typically 4 on a Ford
        public float WireSpacingM;              // spacing between adjacent wires (~12 m fleet)
        public float WireStrokeM;               // arresting stroke length (~95 m for AAG)
        public float WireDecelerationG;         // sustained deceleration during stop (~1.5 g)
        public float WireRetractDurationS;      // time for wire to reset after a trap

        /// <summary>
        /// Sensible Ford-class default profile. Useful for tests and as the bake target when
        /// an authored SO isn't available.
        /// </summary>
        public static CarrierProfileData FordClass()
        {
            return new CarrierProfileData
            {
                LengthM = 333f,
                BeamM   = 78f,
                DisplacementTonnes = 100000f,
                MaxSpeedKnots          = 33f,
                AccelKnotsPerSec       = 0.05f,   // takes ~10 minutes to reach top speed
                MaxTurnRateDegPerSec   = 0.5f,
                TurnRateAccelDegPerSec2 = 0.1f,

                SeaState = SeaStateData.FreshBreeze,

                CatapultCount             = 4,
                CatapultStrokeM           = 94f,   // ~310 ft (EMALS stroke)
                CatapultEndSpeedMps       = 77f,   // ~150 kt
                CatapultPeakG             = 3.0f,
                CatapultSpottedDurationS  = 4f,
                CatapultTensionedDurationS = 6f,
                CatapultReadyDurationS    = 2f,
                CatapultRetractDurationS  = 8f,

                ElevatorCount   = 3,
                ElevatorTravelM = 8f,
                ElevatorSpeedMps = 0.5f,

                FlolsGlideslopeDeg       = 3.5f,
                FlolsWindowHalfAngleDeg  = 0.7f,   // ±0.7° = full ball deflection (real lens window is roughly that)
                FlolsWaveOffThresholdDeg = 1.5f,

                WireCount             = 4,
                WireSpacingM          = 12f,
                WireStrokeM           = 95f,
                WireDecelerationG     = 1.5f,
                WireRetractDurationS  = 6f,
            };
        }
    }
}
