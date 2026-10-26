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

        [Header("Airframe L1 (W32): polynomials in α (rad), term i times α^i; empty = no L1 data")]
        public float[] cma = new float[0];
        public float[] cmds = new float[0];
        public float[] cmq = new float[0];
        public float[] cLift = new float[0];
        public float[] cLiftDs = new float[0];
        public float[] cDrag = new float[0];
        public float cDrag0;
        public float[] cDragDs = new float[0];
        public float[] cyb = new float[0];
        public float[] cyda = new float[0];
        public float[] cydr = new float[0];
        public float[] clb = new float[0];
        public float[] clda = new float[0];
        public float[] cldr = new float[0];
        public float[] clp = new float[0];
        public float[] clr = new float[0];
        public float[] cnb = new float[0];
        public float[] cnda = new float[0];
        public float[] cndr = new float[0];
        public float[] cnp = new float[0];
        public float[] cnr = new float[0];
        [Tooltip("Mean aerodynamic chord for the L1 moments, feet.")]
        public float meanChordFt;
        [Tooltip("Moments of inertia, slug·ft² (the units NASA quotes).")]
        public float ixxSlugFt2;
        public float iyySlugFt2;
        public float izzSlugFt2;
        [Tooltip("Product of inertia, slug·ft², signed as NASA TM-107601 signs it.")]
        public float ixzSlugFt2;
        [Tooltip("The polynomials are evaluated with α clamped to this range, degrees.")]
        public float alphaMinDeg = -10f;
        public float alphaMaxDeg = 60f;
        [Tooltip("Drag floor C_D0 + K·C_L² under the fits (AUTHORED); 0 turns it off.")]
        public float dragFloorCd0;
        public float dragFloorK;

        [Header("Flight controls L1 (W23): empty (alpha limit 0) = no L1 law")]
        public float alphaLimitDeg;
        [Tooltip("The G command's prefilter, seconds.")]
        public float loadFactorCommandTauS;
        [Tooltip("Pitch rate commanded per unit of alpha error, 1/s.")]
        public float alphaGainPerS;
        [Tooltip("Alpha command added per g·s of G error, degrees.")]
        public float loadFactorIntegralDegPerGs;
        [Tooltip("Bandwidths of the inversion, 1/s.")]
        public float pitchRateBandwidth;
        public float rollRateBandwidth;
        public float yawRateBandwidth;
        [Tooltip("Yaw rate commanded per unit of sideslip error, 1/s.")]
        public float sideslipGainPerS;
        [Tooltip("Sideslip commanded at full pedal, degrees.")]
        public float sideslipPerPedalDeg;
        [Tooltip("Actuators: lag bandwidth rad/s, rate deg/s, travel deg (min, max).")]
        public float stabilatorBandwidth;
        public float stabilatorRateDegS;
        public float stabilatorMinDeg;
        public float stabilatorMaxDeg;
        public float aileronBandwidth;
        public float aileronRateDegS;
        public float aileronMinDeg;
        public float aileronMaxDeg;
        public float rudderBandwidth;
        public float rudderRateDegS;
        public float rudderMinDeg;
        public float rudderMaxDeg;

        [Header("Provenance (W8)")]
        [TextArea(8, 40)]
        public string provenance = "";

        static ActuatorProfile Actuator(float bandwidth, float rateDegS, float minDeg, float maxDeg) => new ActuatorProfile
        {
            BandwidthRadS = bandwidth, RateRadS = rateDegS * Units.DegToRad, MinRad = minDeg * Units.DegToRad, MaxRad = maxDeg * Units.DegToRad,
        };

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
            if (cma != null && cma.Length > 0)
            {
                profile.AirframeL1 = new AirframeL1Profile
                {
                    Aero = new AeroPolynomialsL1
                    {
                        Cma = cma, Cmds = cmds, Cmq = cmq, CL = cLift, CLds = cLiftDs, CD = cDrag, CD0 = cDrag0, Cdds = cDragDs,
                        Cyb = cyb, Cyda = cyda, Cydr = cydr, Clb = clb, Clda = clda, Cldr = cldr, Clp = clp, Clr = clr,
                        Cnb = cnb, Cnda = cnda, Cndr = cndr, Cnp = cnp, Cnr = cnr,
                    },
                    ReferenceAreaM2 = profile.Airframe.WingAreaM2,
                    SpanM = profile.Airframe.SpanM,
                    ChordM = (float)(meanChordFt * Units.FootToMetre),
                    IxxKgM2 = (float)(ixxSlugFt2 * Units.SlugFootSquaredToKilogramMetreSquared),
                    IyyKgM2 = (float)(iyySlugFt2 * Units.SlugFootSquaredToKilogramMetreSquared),
                    IzzKgM2 = (float)(izzSlugFt2 * Units.SlugFootSquaredToKilogramMetreSquared),
                    IxzKgM2 = (float)(ixzSlugFt2 * Units.SlugFootSquaredToKilogramMetreSquared),
                    AlphaMinRad = alphaMinDeg * Units.DegToRad,
                    AlphaMaxRad = alphaMaxDeg * Units.DegToRad,
                    DragFloorCD0 = dragFloorCd0,
                    DragFloorK = dragFloorK,
                };
            }
            if (alphaLimitDeg > 0f)
            {
                profile.ControlsL1 = new ControlsL1Profile
                {
                    AlphaLimitRad = alphaLimitDeg * Units.DegToRad,
                    LoadFactorCommandTauS = loadFactorCommandTauS,
                    AlphaGain = alphaGainPerS,
                    LoadFactorIntegralGain = loadFactorIntegralDegPerGs * Units.DegToRad,
                    PitchRateBandwidth = pitchRateBandwidth,
                    RollRateBandwidth = rollRateBandwidth,
                    YawRateBandwidth = yawRateBandwidth,
                    SideslipGain = sideslipGainPerS,
                    SideslipPerPedalRad = sideslipPerPedalDeg * Units.DegToRad,
                    Stabilator = Actuator(stabilatorBandwidth, stabilatorRateDegS, stabilatorMinDeg, stabilatorMaxDeg),
                    Aileron = Actuator(aileronBandwidth, aileronRateDegS, aileronMinDeg, aileronMaxDeg),
                    Rudder = Actuator(rudderBandwidth, rudderRateDegS, rudderMinDeg, rudderMaxDeg),
                };
            }
            profile.Validate();
            return profile;
        }
    }
}
