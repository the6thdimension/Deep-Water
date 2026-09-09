using UnityEngine;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Propulsion
{
    /// <summary>
    /// Two-stage boost-sustain motor. The standard real-world tactical missile profile:
    /// - High thrust for `BoostDurationS` seconds (gets the missile up to speed).
    /// - Lower `SustainThrustN` thrust for `SustainDurationS` seconds (holds speed against drag).
    /// - Then nothing.
    ///
    /// Examples: Sidewinder AIM-9L (boost-sustain), AIM-120 AMRAAM (boost-sustain),
    /// ESSM RIM-162 (typically boost-only, depending on variant). Author per missile.
    ///
    /// Mass burn is computed per-stage so the missile loses propellant at different rates
    /// during boost vs sustain. The total burned during boost is the fraction of total
    /// propellant proportional to (BoostThrust × BoostDuration) / (total impulse).
    /// </summary>
    public sealed class BoostSustainThrustModel : IThrustModel
    {
        public static readonly BoostSustainThrustModel Instance = new BoostSustainThrustModel();
        private BoostSustainThrustModel() { }

        public ThrustModelKind Kind => ThrustModelKind.BoostSustain;

        public bool Sample(in MissileState state, in MissileProfileData profile,
                           out float thrustNewtons, out float burnRateKgPerSec)
        {
            float t = state.TimeOfFlight;
            float boostEnd = profile.BoostDurationS;
            float sustainEnd = boostEnd + profile.SustainDurationS;

            // Total impulse used to split propellant mass between the two stages.
            float boostImpulse = profile.BoostThrustN  * profile.BoostDurationS;
            float sustImpulse  = profile.SustainThrustN * profile.SustainDurationS;
            float totalImpulse = boostImpulse + sustImpulse;
            float boostFraction = totalImpulse > 1e-6f ? boostImpulse / totalImpulse : 1f;
            float sustFraction  = 1f - boostFraction;

            if (t < boostEnd && profile.BoostDurationS > 1e-6f)
            {
                thrustNewtons    = profile.BoostThrustN;
                burnRateKgPerSec = (profile.PropellantMassKg * boostFraction) / profile.BoostDurationS;
                return true;
            }
            if (t < sustainEnd && profile.SustainDurationS > 1e-6f)
            {
                thrustNewtons    = profile.SustainThrustN;
                burnRateKgPerSec = (profile.PropellantMassKg * sustFraction) / profile.SustainDurationS;
                return true;
            }

            thrustNewtons    = 0f;
            burnRateKgPerSec = 0f;
            return false;
        }
    }
}
