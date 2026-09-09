namespace GuidedFury.Core.Propulsion
{
    /// <summary>
    /// Identifies a thrust model. Profile picks one; the missile instantiates the
    /// matching <see cref="IThrustModel"/> at launch.
    /// </summary>
    public enum ThrustModelKind : byte
    {
        /// <summary>Single-stage boost: constant thrust for BoostDurationS, then zero. Same as L0..L3.</summary>
        ConstantBoost = 0,

        /// <summary>Two-stage boost-sustain: high thrust for BoostDurationS, then lower SustainThrustN for SustainDurationS, then zero. Models tactical AAM motors (Sidewinder, AIM-120, ESSM).</summary>
        BoostSustain  = 1,
    }
}
