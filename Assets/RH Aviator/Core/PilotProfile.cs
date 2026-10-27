using System;
using DeepWater.AirWing.Core.Seats;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// How one pilot flies (W45), baked from <c>PilotProfileSO</c> (P1). One brain, different numbers: rookie, line
    /// and ace are three of these. Slice 3a carries the hands and the route-flying numbers, slice 3b the formation's; the fight's (field of view,
    /// reaction delay, energy sense) arrive with Pilot L1 (slice 3d).
    /// </summary>
    public struct PilotProfile
    {
        public string Name;
        /// <summary>The shared flight-path layer's gains and limits (W37, A2).</summary>
        public HandsSettings Hands;
        /// <summary>Going around, the pilot comes back onto the line this far before the gate, metres.</summary>
        public float LeadInM;
        /// <summary>On the line, the pilot aims at least this far ahead of itself along it, metres, and at least this many turn radii.</summary>
        public float CarrotM, CarrotTurnRadii;
        /// <summary>The aim point never runs more than this far past the gate, metres.</summary>
        public float ThroughM;
        /// <summary>Closer to the gate than this, along its line, a pilot not yet lined up goes around, metres.</summary>
        public float MinRunM;
        /// <summary>Lined up: heading within this angle of the line, degrees, and crossing inside this fraction of the ring.</summary>
        public float CaptureDeg, CaptureFraction;
        /// <summary>Speed flown where a gate does not ask for one, and the margin kept inside a gate's speed band, knots.</summary>
        public float CruiseKt, BandMarginKt;
        /// <summary>After the course: hold this altitude (m) and speed (kt) on the present heading.</summary>
        public float HoldAltitudeM, HoldKt;

        /// <summary>
        /// Fingertip (W40): the slot off the leader, metres, in the leader's level frame: to the right (negative: the
        /// left wing), back and up.
        /// </summary>
        public float SlotRightM, SlotBackM, SlotUpM;
        /// <summary>The wingman aims this far ahead of the slot along its track, metres.</summary>
        public float SlotLookAheadM;
        /// <summary>Overtake per metre of along-track gap, (m/s)/m; and the deceleration the pilot counts on to stop it, m/s².</summary>
        public float SlotGainPerS, JoinDecelMps2;
        /// <summary>The most overtake while joining, and while in the slot, knots; and the slowest it flies joining from ahead, knots.</summary>
        public float JoinOvertakeKt, SlotOvertakeKt, JoinMinKt;
        /// <summary>In the slot: inside this many metres of it on every axis, closing slower than this many knots.</summary>
        public float InSlotM, InSlotClosureKt;
        /// <summary>Farther than this from the slot, metres, the wingman has lost it and joins again.</summary>
        public float LostSlotM;
        /// <summary>
        /// Speed lead: the formation asks the hands for its target speed plus this many times the gap between that
        /// target and the speed it has, so a slow speed loop still makes the closure it needs.
        /// </summary>
        public float SlotSpeedLead;
        /// <summary>Speed brake out in formation when this many knots faster than asked.</summary>
        public float SlotSpeedBrakeOverKt;
        /// <summary>
        /// The formation hands' gains (a wingman holds height tighter than a route): flight-path angle per metre off the
        /// slot's height, degrees, and G per degree of flight-path error. Everything else is the route hands'.
        /// </summary>
        public float SlotClimbDegPerM, SlotGPerPathDeg;
        /// <summary>
        /// How much of the leader's turn and climb the wingman reads from its rotation rather than its G, 0 … 1: the
        /// rotation leads the G at the start of a pull, the G is steadier (slice 3b).
        /// </summary>
        public float SlotRotationShare;

        /// <summary>The hands a wingman flies with: the route hands, with the formation's height and path gains.</summary>
        public HandsSettings FormationHands
        {
            get
            {
                HandsSettings h = Hands;
                h.ClimbDegPerM = SlotClimbDegPerM;
                h.GPerPathDeg = SlotGPerPathDeg;
                return h;
            }
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(Name)) throw new ArgumentException("a pilot profile needs a name");
            Positive(Hands.MaxBankDeg, nameof(Hands.MaxBankDeg));
            Positive(Hands.MaxG, nameof(Hands.MaxG));
            Positive(Hands.GuidanceGain, nameof(Hands.GuidanceGain));
            Positive(Hands.RollRatePerBankDeg, nameof(Hands.RollRatePerBankDeg));
            Positive(Hands.ClimbDegPerM, nameof(Hands.ClimbDegPerM));
            Positive(Hands.FloorAglM, nameof(Hands.FloorAglM));
            Positive(LeadInM, nameof(LeadInM));
            Positive(CarrotM, nameof(CarrotM));
            Positive(MinRunM, nameof(MinRunM));
            Positive(CaptureFraction, nameof(CaptureFraction));
            Positive(CruiseKt, nameof(CruiseKt));
            Positive(HoldKt, nameof(HoldKt));
            if (SlotRightM == 0f) throw new ArgumentOutOfRangeException(nameof(SlotRightM), 0f, "a slot needs a side (positive right, negative left)");
            Positive(SlotLookAheadM, nameof(SlotLookAheadM));
            Positive(SlotGainPerS, nameof(SlotGainPerS));
            Positive(JoinDecelMps2, nameof(JoinDecelMps2));
            Positive(JoinOvertakeKt, nameof(JoinOvertakeKt));
            Positive(SlotOvertakeKt, nameof(SlotOvertakeKt));
            Positive(JoinMinKt, nameof(JoinMinKt));
            Positive(InSlotM, nameof(InSlotM));
            Positive(InSlotClosureKt, nameof(InSlotClosureKt));
            if (SlotSpeedLead < 0f) throw new ArgumentOutOfRangeException(nameof(SlotSpeedLead), SlotSpeedLead, "must not be negative");
            Positive(SlotSpeedBrakeOverKt, nameof(SlotSpeedBrakeOverKt));
            Positive(SlotClimbDegPerM, nameof(SlotClimbDegPerM));
            Positive(SlotGPerPathDeg, nameof(SlotGPerPathDeg));
            if (!(SlotRotationShare >= 0f && SlotRotationShare <= 1f)) throw new ArgumentOutOfRangeException(nameof(SlotRotationShare), SlotRotationShare, "must be in [0, 1]");
            if (!(LostSlotM > InSlotM)) throw new ArgumentOutOfRangeException(nameof(LostSlotM), LostSlotM, "losing the slot must lie outside being in it");
        }

        static void Positive(float v, string name)
        {
            if (!(v > 0f)) throw new ArgumentOutOfRangeException(name, v, "must be positive");
        }
    }
}
