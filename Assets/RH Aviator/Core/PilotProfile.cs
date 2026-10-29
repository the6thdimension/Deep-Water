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

        /// <summary>
        /// Going home and the break (W41, W47), metres and knots, heights above the runway. The initial: this long, at
        /// the break height and speed, captured inside this radius of its line; the break this far past the threshold.
        /// </summary>
        public float InitialM, InitialCaptureM, InitialKt, BreakAlongM, BreakHeightM;
        /// <summary>The speed the break slows to, knots: the rest comes off on downwind (slice 3c: slowing to the approach speed in a 70° turn stalled the jet at 31° of attack).</summary>
        public float BreakKt;
        /// <summary>Downwind: offset to the left of the centreline, its height; the turn to final starts this far before the threshold.</summary>
        public float DownwindOffsetM, DownwindHeightM, FinalTurnAlongM;
        /// <summary>The approach speed, the speed below which gear and flaps come down (knots), and the bank limit in the pattern (degrees).</summary>
        public float ApproachKt, GearKt, PatternBankDeg;
        /// <summary>
        /// The pattern hands' path gain (G per degree), dive limit (degrees) and ground-guard floor (metres): at 145 kt
        /// a G changes the path 2.4 times faster than at 350 kt, so the route's gain overshoots (slice 3c: a −24° dive
        /// in the turn to final), and the route's 100 m floor sits above the glide path.
        /// </summary>
        public float PatternGPerPathDeg, PatternMaxDiveDeg, PatternFloorAglM;
        /// <summary>The glide path's angle (degrees), its aim point past the threshold (metres); the flare's height and its last angle.</summary>
        public float GlidePathDeg, TouchdownM, FlareHeightM, FlarePathDeg;
        /// <summary>On the final, the carrot ahead on the centreline, metres (short, to pull onto it in time).</summary>
        public float FinalCarrotM;
        /// <summary>Rollout: pedal per metre off the centreline and per degree of heading.</summary>
        public float RolloutPedalPerM, RolloutPedalPerDeg;
        /// <summary>
        /// Bingo, as a STAND-IN until fuel burn exists (Air Wing L3, W47): after this many seconds of flight the pilot
        /// goes home on its own. Zero: never.
        /// </summary>
        public float BingoFlightTimeS;

        /// <summary>
        /// The fight (Pilot L1, W38, W45): the half-angle the pilot can see round its nose (degrees), how late what it
        /// sees arrives (seconds), how long a lost contact is remembered (seconds), and how far it can see (metres).
        /// </summary>
        public float FieldOfViewDeg, ReactionDelayS, MemoryS, VisualRangeM;
        /// <summary>
        /// The fighting speed (knots); lead and lag, seconds of the target's velocity ahead of or behind it (0: pure
        /// pursuit only, a rookie); lag inside this range (metres) when closing faster than this (knots), slower by this (knots).
        /// </summary>
        public float CornerKt, LeadS, LagS, LagRangeM, OvershootClosureKt, LagSlowKt;
        /// <summary>
        /// A threat: an enemy seen inside this range (metres) and this angle of the pilot's own tail (degrees) makes it
        /// break. The fight's floor above the surface (metres). A wingman with no enemy inside the clear range (metres)
        /// goes back to fingertip (W53).
        /// </summary>
        public float ThreatRangeM, ThreatConeDeg, FightFloorM, ClearRangeM;
        /// <summary>Energy sense: below the fighting speed the pilot eases its turn, to 2 G at this speed (knots); 0: none (a rookie).</summary>
        public float SlowKt;
        /// <summary>How much of the target's turn the pilot matches by feed-forward, 0 … 1 (0: a rookie, the aim alone).</summary>
        public float TurnMatch;
        /// <summary>
        /// Range control: knots of overtake per metre beyond the attack range (0: none, a rookie), the attack range
        /// (metres), and how far past the fighting speed it will chase (knots).
        /// </summary>
        public float RangeKtPerM, AttackRangeM, ChaseOverKt;
        /// <summary>Collision avoidance: another jet predicted to pass inside this distance (metres) within this time (seconds) is avoided.</summary>
        public float AvoidM, AvoidS;
        /// <summary>
        /// Collision avoidance on a join with no fight: another jet predicted to pass inside this distance (metres) within
        /// <see cref="AvoidS"/> is avoided the same way. It must lie inside the slot spacing, so a jet in its slot never
        /// sets it off (0: none).
        /// </summary>
        public float JoinAvoidM;
        /// <summary>
        /// Trigger discipline (Gunnery K6): the pilot fires when a burst fired now would pass within
        /// <see cref="TriggerMils"/> milliradians of where it believes the target will be, inside <see cref="GunRangeM"/>.
        /// A rookie sprays (wide, long); an ace waits for the shot (tight, close). 0 mils: never fires.
        /// </summary>
        public float GunRangeM, TriggerMils;
        /// <summary>
        /// Missile discipline (Air Wing W65, A16): with the seeker locked on its target, the pilot launches inside this
        /// range, metres, and not while one of its own is still flying at that target. A rookie shoots early (long), an ace
        /// close. 0: never launches.
        /// </summary>
        public float MissileRangeM;

        /// <summary>The hands in the pattern: the route hands, held to the pattern's bank.</summary>
        public HandsSettings PatternHands
        {
            get
            {
                HandsSettings h = Hands;
                h.MaxBankDeg = PatternBankDeg;
                h.GPerPathDeg = PatternGPerPathDeg;
                h.MaxDiveDeg = PatternMaxDiveDeg;
                h.FloorAglM = PatternFloorAglM;
                return h;
            }
        }

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
            Positive(InitialM, nameof(InitialM));
            Positive(InitialCaptureM, nameof(InitialCaptureM));
            Positive(InitialKt, nameof(InitialKt));
            Positive(BreakHeightM, nameof(BreakHeightM));
            Positive(BreakKt, nameof(BreakKt));
            Positive(DownwindOffsetM, nameof(DownwindOffsetM));
            Positive(DownwindHeightM, nameof(DownwindHeightM));
            Positive(ApproachKt, nameof(ApproachKt));
            Positive(GearKt, nameof(GearKt));
            Positive(PatternBankDeg, nameof(PatternBankDeg));
            Positive(PatternGPerPathDeg, nameof(PatternGPerPathDeg));
            Positive(PatternMaxDiveDeg, nameof(PatternMaxDiveDeg));
            Positive(PatternFloorAglM, nameof(PatternFloorAglM));
            Positive(GlidePathDeg, nameof(GlidePathDeg));
            Positive(FlareHeightM, nameof(FlareHeightM));
            Positive(FinalCarrotM, nameof(FinalCarrotM));
            Positive(FieldOfViewDeg, nameof(FieldOfViewDeg));
            if (ReactionDelayS < 0f) throw new ArgumentOutOfRangeException(nameof(ReactionDelayS), ReactionDelayS, "must not be negative");
            Positive(MemoryS, nameof(MemoryS));
            Positive(VisualRangeM, nameof(VisualRangeM));
            Positive(CornerKt, nameof(CornerKt));
            Positive(FightFloorM, nameof(FightFloorM));
            Positive(ClearRangeM, nameof(ClearRangeM));
            if (BingoFlightTimeS < 0f) throw new ArgumentOutOfRangeException(nameof(BingoFlightTimeS), BingoFlightTimeS, "must not be negative (zero: never)");
            if (JoinAvoidM < 0f || JoinAvoidM >= Math.Abs(SlotRightM)) throw new ArgumentOutOfRangeException(nameof(JoinAvoidM), JoinAvoidM, "must be at least 0 and inside the slot spacing, or a jet in its slot would set it off");
            if (GunRangeM < 0f) throw new ArgumentOutOfRangeException(nameof(GunRangeM), GunRangeM, "must not be negative");
            if (TriggerMils < 0f) throw new ArgumentOutOfRangeException(nameof(TriggerMils), TriggerMils, "must not be negative (zero: never fires)");
            if (MissileRangeM < 0f) throw new ArgumentOutOfRangeException(nameof(MissileRangeM), MissileRangeM, "must not be negative (zero: never launches)");
            if (!(LostSlotM > InSlotM)) throw new ArgumentOutOfRangeException(nameof(LostSlotM), LostSlotM, "losing the slot must lie outside being in it");
        }

        static void Positive(float v, string name)
        {
            if (!(v > 0f)) throw new ArgumentOutOfRangeException(name, v, "must be positive");
        }
    }
}
