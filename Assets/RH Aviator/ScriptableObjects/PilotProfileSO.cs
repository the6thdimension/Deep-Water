using UnityEngine;
using DeepWater.AirWing.Core.Seats;
using DeepWater.Aviator.Core;

namespace DeepWater.Aviator.ScriptableObjects
{
    /// <summary>
    /// One pilot's numbers (W45, P1): the hands' gains and limits, and how it flies a route. Rookie, line and ace are
    /// three assets of this type. Every number is AUTHORED unless <see cref="provenance"/> says otherwise, and the
    /// provenance names each field (the profile test checks it).
    /// </summary>
    [CreateAssetMenu(fileName = "NewPilotProfile", menuName = "Deep Water/RH Aviator/Pilot Profile", order = 10)]
    public class PilotProfileSO : ScriptableObject
    {
        public string displayName = "";

        [Header("Hands (W37): the aim → bank (L1 law), altitude → path, path → G")]
        [Tooltip("Path-following gain k in a = k·V²·sin η / L (2 = classic).")]
        public float guidanceGain;
        public float maxBankDeg;
        public float rollRatePerBankDeg;
        public float climbDegPerM;
        public float maxClimbDeg;
        public float maxDiveDeg;
        public float gPerPathDeg;
        [Tooltip("The most G this pilot will pull.")]
        public float maxG;
        public float throttlePerKt;
        public float throttleTrim;
        [Tooltip("Throttle per knot-second of speed error (the integral that removes the steady error).")]
        public float throttleIntegralPerKtS;
        public bool useAfterburner = true;
        public float speedBrakeOverKt;

        [Header("Ground guard (Air Wing AP25)")]
        public float floorAglM;
        public float guardLookAheadS;
        public float guardClimbDeg;
        public float guardMaxBankDeg;

        [Header("Route flying (Navigate)")]
        public float leadInM;
        public float carrotM;
        public float carrotTurnRadii;
        public float throughM;
        public float minRunM;
        public float captureDeg;
        [Tooltip("Lined up when the present track crosses the gate inside this fraction of its ring.")]
        public float captureFraction;
        public float cruiseKt;
        public float bandMarginKt;

        [Header("After the course (HoldCourse)")]
        public float holdAltitudeM;
        public float holdKt;

        [Header("Provenance")]
        [TextArea(4, 20)] public string provenance = "";

        public PilotProfile Bake()
        {
            var p = new PilotProfile
            {
                Name = displayName,
                Hands = new HandsSettings
                {
                    GuidanceGain = guidanceGain, MaxBankDeg = maxBankDeg, RollRatePerBankDeg = rollRatePerBankDeg,
                    ClimbDegPerM = climbDegPerM, MaxClimbDeg = maxClimbDeg, MaxDiveDeg = maxDiveDeg, GPerPathDeg = gPerPathDeg,
                    MaxG = maxG, ThrottlePerKt = throttlePerKt, ThrottleTrim = throttleTrim, ThrottleIntegralPerKtS = throttleIntegralPerKtS, UseAfterburner = useAfterburner,
                    SpeedBrakeOverKt = speedBrakeOverKt, FloorAglM = floorAglM, GuardLookAheadS = guardLookAheadS,
                    GuardClimbDeg = guardClimbDeg, GuardMaxBankDeg = guardMaxBankDeg,
                },
                LeadInM = leadInM, CarrotM = carrotM, CarrotTurnRadii = carrotTurnRadii, ThroughM = throughM, MinRunM = minRunM, CaptureDeg = captureDeg, CaptureFraction = captureFraction,
                CruiseKt = cruiseKt, BandMarginKt = bandMarginKt, HoldAltitudeM = holdAltitudeM, HoldKt = holdKt,
            };
            p.Validate();
            return p;
        }
    }
}
