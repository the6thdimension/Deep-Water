using System;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.State;

namespace DeepWater.AirWing.Core.FlightControls
{
    /// <summary>What the control law asks the airframe for this substep. The airframe may not be able to give it all.</summary>
    public readonly struct ControlDemand
    {
        /// <summary>Load factor, g.</summary>
        public readonly float LoadFactor;
        /// <summary>Roll rate about the flight path, radians per second, positive = right wing down.</summary>
        public readonly float RollRateRadS;

        public ControlDemand(float loadFactor, float rollRateRadS)
        {
            LoadFactor = loadFactor;
            RollRateRadS = rollRateRadS;
        }
    }

    /// <summary>The Flight Controls part (BLUEPRINT §4): Pilot Inputs to what the airframe is asked for.</summary>
    public interface IFlightControls
    {
        ControlDemand Demand(in PilotInputs inputs, in AirframeState state, float dt);
    }

    /// <summary>
    /// Flight Controls L0 (W23, W26): the stick commands a load factor and a roll rate, each followed with a
    /// first-order lag and clipped to the profile's limits. Neutral stick holds 1 g, full aft stick the positive limit,
    /// full forward stick the negative limit, linear in between on each side. The pedals do nothing at L0: a point mass
    /// flies with no sideslip. Autopilot switches arrive with the instructor and the AI (slices 1c, Phase 3).
    /// </summary>
    public sealed class FlightControlsL0 : IFlightControls
    {
        readonly ControlsProfile _c;

        public FlightControlsL0(in ControlsProfile profile)
        {
            profile.Validate();
            _c = profile;
        }

        /// <summary>The load factor the stick asks for, before the lag.</summary>
        public float CommandedLoadFactor(float stickPitch) => stickPitch >= 0f
            ? 1f + stickPitch * (_c.LoadFactorMax - 1f)
            : 1f + stickPitch * (1f - _c.LoadFactorMin);

        public ControlDemand Demand(in PilotInputs inputs, in AirframeState state, float dt)
        {
            float n = Lag(state.LoadFactor, CommandedLoadFactor(inputs.StickPitch), _c.LoadFactorTimeConstantS, dt);
            float rollNow = -state.BodyRates.z;   // right roll is negative about +Z in Unity's sense
            float roll = Lag(rollNow, inputs.StickRoll * _c.RollRateMaxRadS, _c.RollTimeConstantS, dt);
            n = n > _c.LoadFactorMax ? _c.LoadFactorMax : n < _c.LoadFactorMin ? _c.LoadFactorMin : n;
            return new ControlDemand(n, roll);
        }

        // Exact discrete form of a first-order lag over dt, so the response does not depend on the substep count.
        static float Lag(float current, float target, float tau, float dt) =>
            current + (target - current) * (1f - (float)Math.Exp(-dt / tau));
    }
}
