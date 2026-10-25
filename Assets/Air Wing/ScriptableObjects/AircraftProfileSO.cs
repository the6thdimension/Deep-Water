using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Profiles;

namespace DeepWater.AirWing.ScriptableObjects
{
    /// <summary>
    /// One aircraft type's numbers (P1, W8). Public figures are entered in the units their source quotes (pounds,
    /// feet, pounds-force) and converted to SI once, in <see cref="Bake"/> (W22). <see cref="provenance"/> names the
    /// source of every number and says which are AUTHORED (estimated), CALIBRATED (fitted to one public figure, C16)
    /// or public. An unsourced number stays out: empty beats fake.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAircraftProfile", menuName = "Deep Water/Air Wing/Aircraft Profile", order = 20)]
    public class AircraftProfileSO : ScriptableObject
    {
        public string displayName = "";

        [Header("Airframe")]
        public float wingAreaFt2;
        public float spanFt;
        public float emptyWeightLb;
        public float internalFuelLb;
        [Tooltip("Zero-lift drag coefficient.")]
        public float cd0;
        [Tooltip("Span efficiency e in K = 1 / (π e AR).")]
        public float oswaldEfficiency;
        public float clMax;
        [Tooltip("Largest negative lift coefficient, as a positive number.")]
        public float clMaxNegative;
        [Tooltip("Lift-curve slope, per radian.")]
        public float clAlphaPerRad;
        public float flapsDeltaClMax;
        public float flapsDeltaCd;
        public float speedBrakeDeltaCd;

        [Header("Powerplant")]
        public int engineCount;
        [Tooltip("Static sea-level thrust per engine at military power, pounds-force.")]
        public float militaryThrustPerEngineLbf;
        [Tooltip("Static sea-level thrust per engine at maximum afterburner, pounds-force.")]
        public float maxThrustPerEngineLbf;
        [Tooltip("Idle thrust as a fraction of military thrust.")]
        public float idleThrustFraction;
        [Tooltip("Thrust ∝ σ^x with σ = ρ/ρ₀.")]
        public float thrustLapseExponent;

        [Header("Flight controls")]
        public float loadFactorMax;
        [Tooltip("Negative limit, as a negative number of g.")]
        public float loadFactorMin;
        public float rollRateMaxDegS;
        public float loadFactorTimeConstantS;
        public float rollTimeConstantS;

        [Header("Provenance (W8)")]
        [TextArea(8, 40)]
        public string provenance = "";

        public AircraftProfile Bake()
        {
            var profile = new AircraftProfile
            {
                Name = displayName,
                Airframe = new AirframeProfile
                {
                    WingAreaM2 = (float)(wingAreaFt2 * Units.SquareFootToSquareMetre),
                    SpanM = (float)(spanFt * Units.FootToMetre),
                    EmptyMassKg = (float)(emptyWeightLb * Units.PoundToKilogram),
                    InternalFuelKg = (float)(internalFuelLb * Units.PoundToKilogram),
                    CD0 = cd0,
                    OswaldEfficiency = oswaldEfficiency,
                    CLmax = clMax,
                    CLmaxNegative = clMaxNegative,
                    CLalphaPerRad = clAlphaPerRad,
                    FlapsDeltaCLmax = flapsDeltaClMax,
                    FlapsDeltaCD = flapsDeltaCd,
                    SpeedBrakeDeltaCD = speedBrakeDeltaCd,
                },
                Powerplant = new PowerplantProfile
                {
                    EngineCount = engineCount,
                    MilitaryThrustPerEngineN = (float)(militaryThrustPerEngineLbf * Units.PoundForceToNewton),
                    MaxThrustPerEngineN = (float)(maxThrustPerEngineLbf * Units.PoundForceToNewton),
                    IdleThrustFraction = idleThrustFraction,
                    ThrustLapseExponent = thrustLapseExponent,
                },
                Controls = new ControlsProfile
                {
                    LoadFactorMax = loadFactorMax,
                    LoadFactorMin = loadFactorMin,
                    RollRateMaxRadS = rollRateMaxDegS * Units.DegToRad,
                    LoadFactorTimeConstantS = loadFactorTimeConstantS,
                    RollTimeConstantS = rollTimeConstantS,
                },
            };
            profile.Validate();
            return profile;
        }
    }
}
