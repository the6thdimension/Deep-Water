using System;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.State;

namespace DeepWater.AirWing.Core.FlightControls
{
    /// <summary>
    /// Where the control surfaces stand, radians, in the sign convention of the F/A-18 aero data (W32; NASA TM-107601
    /// Table 5.2): stabilator positive trailing edge down (nose down); aileron positive rolls right (right wing down);
    /// rudder positive trailing edge left (nose left). Symmetric stabilator only; no differential tail at L1.
    /// </summary>
    public readonly struct ControlSurfaces
    {
        public readonly float StabilatorRad;
        public readonly float AileronRad;
        public readonly float RudderRad;

        public ControlSurfaces(float stabilatorRad, float aileronRad, float rudderRad)
        {
            StabilatorRad = stabilatorRad;
            AileronRad = aileronRad;
            RudderRad = rudderRad;
        }
    }

    /// <summary>
    /// What the control law asks the airframe for this substep. The airframe may not be able to give it all. Airframe
    /// L0 flies the load factor and roll rate; Airframe L1 flies the surfaces, which only Flight Controls L1 sets.
    /// </summary>
    public readonly struct ControlDemand
    {
        /// <summary>Load factor, g.</summary>
        public readonly float LoadFactor;
        /// <summary>Roll rate about the flight path, radians per second, positive = right wing down.</summary>
        public readonly float RollRateRadS;
        /// <summary>The surface positions (Airframe L1).</summary>
        public readonly ControlSurfaces Surfaces;

        public ControlDemand(float loadFactor, float rollRateRadS)
        {
            LoadFactor = loadFactor;
            RollRateRadS = rollRateRadS;
            Surfaces = default;
        }

        public ControlDemand(in ControlSurfaces surfaces)
        {
            LoadFactor = float.NaN;
            RollRateRadS = float.NaN;
            Surfaces = surfaces;
        }
    }

    /// <summary>The Flight Controls part (BLUEPRINT §4): Pilot Inputs to what the airframe is asked for.</summary>
    public interface IFlightControls
    {
        /// <summary>
        /// This substep's demand. <paramref name="state"/> is the aircraft's own: a law with memory (L1's actuators and
        /// filters) keeps it in <see cref="AirframeState.Controls"/>, so the Fix covers it and a reset clears it.
        /// </summary>
        ControlDemand Demand(in PilotInputs inputs, ref AirframeState state, in AirData air, float dt);
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

        public ControlDemand Demand(in PilotInputs inputs, ref AirframeState state, in AirData air, float dt)
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
