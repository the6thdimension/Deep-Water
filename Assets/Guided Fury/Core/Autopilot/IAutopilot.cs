using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Autopilot
{
    /// <summary>
    /// Inner-loop controller that turns a guidance-produced lateral-accel command into
    /// either a torque (L3) or a control-surface deflection (L4). Pluggable so different
    /// LOD tiers can wire in different controller designs without rewriting integrators.
    ///
    /// **Contract:**
    /// - Pure function from (state, command, profile) → output. No hidden state in the
    ///   shared singleton implementations.
    /// - Deterministic. No global RNG.
    /// - Implementations that need internal state (a PID controller with accumulators, e.g.)
    ///   carry it on the instance and are constructed per missile, not as singletons.
    /// </summary>
    public interface IAutopilot
    {
        AutopilotKind Kind { get; }

        /// <summary>Compute the autopilot output for this fixed step.</summary>
        AutopilotOutput Compute(in MissileState state, in MissileCommand command, in MissileProfileData profile);
    }
}
