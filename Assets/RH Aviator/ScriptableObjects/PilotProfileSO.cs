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

        [Header("Fingertip formation (W40)")]
        [Tooltip("The slot off the leader in its level frame, metres: right (negative: left wing), back, up.")]
        public float slotRightM;
        public float slotBackM;
        public float slotUpM;
        public float slotLookAheadM;
        [Tooltip("Overtake per metre of along-track gap, (m/s)/m.")]
        public float slotGainPerS;
        [Tooltip("The deceleration the pilot counts on to take its overtake off, m/s².")]
        public float joinDecelMps2;
        public float joinOvertakeKt;
        public float slotOvertakeKt;
        public float joinMinKt;
        public float inSlotM;
        public float inSlotClosureKt;
        public float lostSlotM;
        [Tooltip("Speed lead: the target speed plus this many times its gap to the speed flown.")]
        public float slotSpeedLead;
        public float slotSpeedBrakeOverKt;
        [Tooltip("The formation hands: flight-path angle per metre off the slot's height, degrees.")]
        public float slotClimbDegPerM;
        [Tooltip("The formation hands: G per degree of flight-path error.")]
        public float slotGPerPathDeg;
        [Tooltip("How much of the leader's turn and climb is read from its rotation rather than its G, 0 … 1.")]
        [Range(0f, 1f)] public float slotRotationShare;

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
                SlotRightM = slotRightM, SlotBackM = slotBackM, SlotUpM = slotUpM, SlotLookAheadM = slotLookAheadM,
                SlotGainPerS = slotGainPerS, JoinDecelMps2 = joinDecelMps2, JoinOvertakeKt = joinOvertakeKt, SlotOvertakeKt = slotOvertakeKt,
                JoinMinKt = joinMinKt, InSlotM = inSlotM, InSlotClosureKt = inSlotClosureKt, LostSlotM = lostSlotM,
                SlotSpeedLead = slotSpeedLead, SlotSpeedBrakeOverKt = slotSpeedBrakeOverKt, SlotClimbDegPerM = slotClimbDegPerM, SlotGPerPathDeg = slotGPerPathDeg, SlotRotationShare = slotRotationShare,
            };
            p.Validate();
            return p;
        }
    }
}
