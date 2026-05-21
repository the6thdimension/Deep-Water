using UnityEngine;
using CarrierOps.Core.State;

namespace CarrierOps.ScriptableObjects.Profiles
{
    /// <summary>
    /// Authoring asset for a carrier profile. Designers tune values in the Inspector; the
    /// runtime entity carries a baked <see cref="CarrierProfileData"/> struct copy.
    ///
    /// Bake-once policy: live edits during play do NOT affect a running carrier — same rule
    /// as MissileProfileSO. This prevents the "I tweaked a slider and the ship in front of
    /// me retroactively changed mass" surprise. A future dev tool can opt in to live tuning
    /// explicitly.
    ///
    /// Defaults are Ford-class realistic.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCarrierProfile", menuName = "Carrier Ops/Carrier Profile", order = 1)]
    public class CarrierProfileSO : ScriptableObject
    {
        [Header("Identification")]
        public string carrierId   = "CVN-78";
        public string displayName = "USS Gerald R. Ford";
        [TextArea(2, 4)]
        public string description = "Ford-class supercarrier. 100k tonnes, 333m LOA, 33 kt, EMALS × 4, AAG arresting gear, 3 aircraft elevators.";

        [Header("Hull & Movement")]
        public float lengthM = 333f;
        public float beamM   = 78f;
        public float displacementTonnes = 100000f;
        public float maxSpeedKnots          = 33f;
        public float accelKnotsPerSec       = 0.05f;
        public float maxTurnRateDegPerSec   = 0.5f;
        public float turnRateAccelDegPerSec2 = 0.1f;

        [Header("Sea State")]
        [Tooltip("Heave amplitude in meters at the ship's center.")]
        public float heaveAmplitudeM   = 0.30f;
        public float heavePeriodS      = 6f;

        [Tooltip("Roll amplitude in degrees.")]
        public float rollAmplitudeDeg  = 1.5f;
        public float rollPeriodS       = 8f;

        [Tooltip("Pitch amplitude in degrees.")]
        public float pitchAmplitudeDeg = 0.6f;
        public float pitchPeriodS      = 6f;

        [Tooltip("RNG seed for sum-of-sines phase offsets. Deterministic: same seed → same motion.")]
        public int seaStateSeed = 12345;

        [Header("Catapults (EMALS × 4)")]
        [Tooltip("Number of catapults available on the deck.")]
        [Range(1, 4)] public int catapultCount = 4;

        [Tooltip("Catapult stroke length in meters. EMALS ≈ 94 m.")]
        public float catapultStrokeM = 94f;

        [Tooltip("Commanded end-of-stroke speed in meters/second. ~77 m/s ≈ 150 kt for a clean F-18E.")]
        public float catapultEndSpeedMps = 77f;

        [Tooltip("Peak G load during the firing stroke. Realistic: ~3 g for a F-18E launch.")]
        public float catapultPeakG = 3f;

        [Header("Catapult Cycle Timing (seconds)")]
        public float catSpottedDurationS    = 4f;
        public float catTensionedDurationS  = 6f;
        public float catReadyDurationS      = 2f;
        public float catRetractDurationS    = 8f;

        [Header("Elevators")]
        [Range(1, 3)] public int elevatorCount = 3;
        public float elevatorTravelM   = 8f;
        public float elevatorSpeedMps  = 0.5f;

        [Header("FLOLS (Optical Landing System)")]
        [Tooltip("Commanded glideslope angle. Fleet standard is 3.5°.")]
        public float flolsGlideslopeDeg = 3.5f;

        [Tooltip("Visible-window half-angle of the Fresnel lens. The ball saturates at ±this deviation.")]
        public float flolsWindowHalfAngleDeg = 0.7f;

        [Tooltip("Deviation past which the LSO calls wave-off. Drives cut lights / wave-off flag.")]
        public float flolsWaveOffThresholdDeg = 1.5f;

        [Header("Arresting Gear")]
        [Range(1, 4)] public int wireCount = 4;
        [Tooltip("Spacing between adjacent wires across the angled deck. ~12 m fleet.")]
        public float wireSpacingM = 12f;
        [Tooltip("Arresting stroke length — how far the aircraft travels while decelerating. AAG ~95 m.")]
        public float wireStrokeM = 95f;
        [Tooltip("Sustained deceleration G-load during a trap. ~1.5 g typical, up to ~3 g for short straps.")]
        public float wireDecelerationG = 1.5f;
        [Tooltip("Seconds for the wire to retract / reset after a successful trap.")]
        public float wireRetractDurationS = 6f;

        public CarrierProfileData Bake()
        {
            return new CarrierProfileData
            {
                LengthM = lengthM,
                BeamM   = beamM,
                DisplacementTonnes = displacementTonnes,
                MaxSpeedKnots          = maxSpeedKnots,
                AccelKnotsPerSec       = accelKnotsPerSec,
                MaxTurnRateDegPerSec   = maxTurnRateDegPerSec,
                TurnRateAccelDegPerSec2 = turnRateAccelDegPerSec2,

                SeaState = new SeaStateData
                {
                    HeaveAmplitudeM   = heaveAmplitudeM,
                    HeavePeriodS      = heavePeriodS,
                    RollAmplitudeDeg  = rollAmplitudeDeg,
                    RollPeriodS       = rollPeriodS,
                    PitchAmplitudeDeg = pitchAmplitudeDeg,
                    PitchPeriodS      = pitchPeriodS,
                    Seed              = seaStateSeed,
                },

                CatapultCount             = catapultCount,
                CatapultStrokeM           = catapultStrokeM,
                CatapultEndSpeedMps       = catapultEndSpeedMps,
                CatapultPeakG             = catapultPeakG,
                CatapultSpottedDurationS  = catSpottedDurationS,
                CatapultTensionedDurationS = catTensionedDurationS,
                CatapultReadyDurationS    = catReadyDurationS,
                CatapultRetractDurationS  = catRetractDurationS,

                ElevatorCount    = elevatorCount,
                ElevatorTravelM  = elevatorTravelM,
                ElevatorSpeedMps = elevatorSpeedMps,

                FlolsGlideslopeDeg       = flolsGlideslopeDeg,
                FlolsWindowHalfAngleDeg  = flolsWindowHalfAngleDeg,
                FlolsWaveOffThresholdDeg = flolsWaveOffThresholdDeg,

                WireCount            = wireCount,
                WireSpacingM         = wireSpacingM,
                WireStrokeM          = wireStrokeM,
                WireDecelerationG    = wireDecelerationG,
                WireRetractDurationS = wireRetractDurationS,
            };
        }

        private void OnValidate()
        {
            if (lengthM < 1f) lengthM = 1f;
            if (beamM < 1f) beamM = 1f;
            if (displacementTonnes < 1f) displacementTonnes = 1f;
            if (maxSpeedKnots < 0f) maxSpeedKnots = 0f;
            if (accelKnotsPerSec < 0f) accelKnotsPerSec = 0f;
            if (maxTurnRateDegPerSec < 0f) maxTurnRateDegPerSec = 0f;
            if (turnRateAccelDegPerSec2 < 0f) turnRateAccelDegPerSec2 = 0f;
            if (heaveAmplitudeM < 0f) heaveAmplitudeM = 0f;
            if (heavePeriodS < 0.1f) heavePeriodS = 0.1f;
            if (rollAmplitudeDeg < 0f) rollAmplitudeDeg = 0f;
            if (rollPeriodS < 0.1f) rollPeriodS = 0.1f;
            if (pitchAmplitudeDeg < 0f) pitchAmplitudeDeg = 0f;
            if (pitchPeriodS < 0.1f) pitchPeriodS = 0.1f;
            if (catapultStrokeM < 1f) catapultStrokeM = 1f;
            if (catapultEndSpeedMps < 0f) catapultEndSpeedMps = 0f;
            if (catapultPeakG < 0f) catapultPeakG = 0f;
            if (catSpottedDurationS < 0f) catSpottedDurationS = 0f;
            if (catTensionedDurationS < 0f) catTensionedDurationS = 0f;
            if (catReadyDurationS < 0f) catReadyDurationS = 0f;
            if (catRetractDurationS < 0f) catRetractDurationS = 0f;
            if (elevatorTravelM < 0f) elevatorTravelM = 0f;
            if (elevatorSpeedMps < 0f) elevatorSpeedMps = 0f;
            if (flolsGlideslopeDeg < 0.5f) flolsGlideslopeDeg = 0.5f;
            if (flolsWindowHalfAngleDeg < 0.1f) flolsWindowHalfAngleDeg = 0.1f;
            if (flolsWaveOffThresholdDeg < flolsWindowHalfAngleDeg) flolsWaveOffThresholdDeg = flolsWindowHalfAngleDeg;
            if (wireSpacingM < 0.1f) wireSpacingM = 0.1f;
            if (wireStrokeM < 1f) wireStrokeM = 1f;
            if (wireDecelerationG < 0.1f) wireDecelerationG = 0.1f;
            if (wireRetractDurationS < 0.1f) wireRetractDurationS = 0.1f;
        }
    }
}
