using System;
using DeepWater.AirWing.Core.Seats;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// How one pilot flies (W45), baked from <c>PilotProfileSO</c> (P1). One brain, different numbers: rookie, line
    /// and ace are three of these. Slice 3a carries the hands and the route-flying numbers; the fight's (field of view,
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
        }

        static void Positive(float v, string name)
        {
            if (!(v > 0f)) throw new ArgumentOutOfRangeException(name, v, "must be positive");
        }
    }
}
