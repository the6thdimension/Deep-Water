using UnityEngine;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Propulsion
{
    /// <summary>
    /// Single-stage boost-only thrust. Constant `BoostThrustN` for the first `BoostDurationS`
    /// seconds, then zero. Equivalent behavior to the inline boost used by L0..L3.
    ///
    /// Mass burn is linear: total propellant ÷ boost duration. Stateless singleton.
    /// </summary>
    public sealed class ConstantBoostThrustModel : IThrustModel
    {
        public static readonly ConstantBoostThrustModel Instance = new ConstantBoostThrustModel();
        private ConstantBoostThrustModel() { }

        public ThrustModelKind Kind => ThrustModelKind.ConstantBoost;

        public bool Sample(in MissileState state, in MissileProfileData profile,
                           out float thrustNewtons, out float burnRateKgPerSec)
        {
            bool boosting = state.TimeOfFlight < profile.BoostDurationS;
            thrustNewtons    = boosting ? profile.BoostThrustN : 0f;
            burnRateKgPerSec = boosting && profile.BoostDurationS > 1e-6f
                ? profile.PropellantMassKg / profile.BoostDurationS
                : 0f;
            return boosting;
        }
    }
}
