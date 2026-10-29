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

        [Header("Going home and the break (W41, W47); heights above the runway")]
        public float initialM;
        public float initialCaptureM;
        public float initialKt;
        public float breakAlongM;
        public float breakHeightM;
        public float breakKt;
        public float downwindOffsetM;
        public float downwindHeightM;
        public float finalTurnAlongM;
        public float approachKt;
        public float gearKt;
        public float patternBankDeg;
        public float patternGPerPathDeg;
        public float patternMaxDiveDeg;
        public float patternFloorAglM;
        public float glidePathDeg;
        public float touchdownM;
        public float flareHeightM;
        public float flarePathDeg;
        public float finalCarrotM;
        public float rolloutPedalPerM;
        public float rolloutPedalPerDeg;
        [Tooltip("STAND-IN for bingo fuel until fuel burn exists (W47): seconds of flight before going home on its own. 0: never.")]
        public float bingoFlightTimeS;

        [Header("The fight (Pilot L1: W38, W45)")]
        [Tooltip("Half-angle the pilot can see round its nose, degrees.")]
        public float fieldOfViewDeg;
        [Tooltip("How late what it sees arrives, seconds.")]
        public float reactionDelayS;
        public float memoryS;
        public float visualRangeM;
        public float cornerKt;
        [Tooltip("Seconds of the target's velocity ahead of it (0: no lead pursuit).")]
        public float leadS;
        [Tooltip("Seconds of the target's velocity behind it (0: no lag pursuit).")]
        public float lagS;
        public float lagRangeM;
        public float overshootClosureKt;
        public float lagSlowKt;
        public float threatRangeM;
        public float threatConeDeg;
        public float fightFloorM;
        public float clearRangeM;
        [Tooltip("Energy sense: eases the turn to 2 G at this speed, knots; 0: none.")]
        public float slowKt;
        [Tooltip("How much of the target's turn is matched by feed-forward, 0 … 1.")]
        [Range(0f, 1f)] public float turnMatch;
        [Tooltip("Range control: knots of overtake per metre past the attack range (0: none).")]
        public float rangeKtPerM;
        public float attackRangeM;
        public float chaseOverKt;
        [Tooltip("Collision avoidance: a jet predicted to pass inside this distance (m) within this time (s).")]
        public float avoidM;
        public float avoidS;
        [Tooltip("Collision avoidance on a join with no fight: a jet predicted to pass inside this distance (m), inside the slot spacing (0: none).")]
        public float joinAvoidM;

        [Header("Trigger discipline (Gunnery K6)")]
        [Tooltip("Fires only inside this range, m.")]
        public float gunRangeM;
        [Tooltip("Fires when the burst would pass within this many milliradians of the target (0: never fires).")]
        public float triggerMils;

        [Header("Missile discipline (Air Wing W65)")]
        [Tooltip("Launches with a lock on its target inside this range, m (0: never launches).")]
        public float missileRangeM;

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
                SlotSpeedLead = slotSpeedLead, SlotSpeedBrakeOverKt = slotSpeedBrakeOverKt,
                InitialM = initialM, InitialCaptureM = initialCaptureM, InitialKt = initialKt, BreakAlongM = breakAlongM, BreakHeightM = breakHeightM, BreakKt = breakKt,
                DownwindOffsetM = downwindOffsetM, DownwindHeightM = downwindHeightM, FinalTurnAlongM = finalTurnAlongM,
                ApproachKt = approachKt, GearKt = gearKt, PatternBankDeg = patternBankDeg,
                PatternGPerPathDeg = patternGPerPathDeg, PatternMaxDiveDeg = patternMaxDiveDeg, PatternFloorAglM = patternFloorAglM,
                GlidePathDeg = glidePathDeg, TouchdownM = touchdownM, FlareHeightM = flareHeightM, FlarePathDeg = flarePathDeg, FinalCarrotM = finalCarrotM,
                RolloutPedalPerM = rolloutPedalPerM, RolloutPedalPerDeg = rolloutPedalPerDeg, BingoFlightTimeS = bingoFlightTimeS,
                FieldOfViewDeg = fieldOfViewDeg, ReactionDelayS = reactionDelayS, MemoryS = memoryS, VisualRangeM = visualRangeM,
                CornerKt = cornerKt, LeadS = leadS, LagS = lagS, LagRangeM = lagRangeM, OvershootClosureKt = overshootClosureKt, LagSlowKt = lagSlowKt,
                ThreatRangeM = threatRangeM, ThreatConeDeg = threatConeDeg, FightFloorM = fightFloorM, ClearRangeM = clearRangeM, SlowKt = slowKt, TurnMatch = turnMatch, RangeKtPerM = rangeKtPerM, AttackRangeM = attackRangeM, ChaseOverKt = chaseOverKt, AvoidM = avoidM, AvoidS = avoidS, JoinAvoidM = joinAvoidM, GunRangeM = gunRangeM, TriggerMils = triggerMils, MissileRangeM = missileRangeM, SlotClimbDegPerM = slotClimbDegPerM, SlotGPerPathDeg = slotGPerPathDeg, SlotRotationShare = slotRotationShare,
            };
            p.Validate();
            return p;
        }
    }
}
