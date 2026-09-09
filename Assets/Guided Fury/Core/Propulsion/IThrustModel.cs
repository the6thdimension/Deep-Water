using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Propulsion
{
    /// <summary>
    /// Produces the thrust force magnitude (along the body axis) and a "still burning" flag
    /// for the current step. Used by the L4 integrator to replace its inline boost-only
    /// thrust calculation. The pattern lets us swap thrust profiles per missile (Sidewinder
    /// = boost-sustain, ESSM = boost-only, etc.) without touching the integrator.
    ///
    /// **Contract:**
    /// - Pure function. Same inputs → same outputs.
    /// - Implementations are stateless singletons; profile fields carry the per-missile data.
    /// </summary>
    public interface IThrustModel
    {
        ThrustModelKind Kind { get; }

        /// <summary>
        /// Compute thrust force magnitude for the current step.
        /// </summary>
        /// <param name="state">Missile state (used for TimeOfFlight check).</param>
        /// <param name="profile">Profile carrying boost/sustain durations and thrusts.</param>
        /// <param name="thrustNewtons">Output: thrust force magnitude (always ≥ 0). Caller applies along body forward.</param>
        /// <param name="burnRateKgPerSec">Output: mass burn rate. Caller decrements state.Fuel and state.Mass.</param>
        /// <returns>True if the motor is still producing thrust this step (so caller knows to advance phase).</returns>
        bool Sample(in MissileState state, in MissileProfileData profile,
                    out float thrustNewtons, out float burnRateKgPerSec);
    }
}
