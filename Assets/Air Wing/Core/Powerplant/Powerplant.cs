using System;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.State;

namespace DeepWater.AirWing.Core.Powerplant
{
    /// <summary>The Powerplant part (BLUEPRINT §4): throttle to thrust along the flight path.</summary>
    public interface IPowerplant
    {
        /// <summary>Total thrust this substep, newtons. May advance the engine state (spool from L1).</summary>
        float Step(in PilotInputs inputs, in AirData air, ref AirframeState state, float dt);
    }

    /// <summary>
    /// Powerplant L0: idle to military power with the throttle, maximum afterburner with the afterburner switch, and a
    /// density lapse T = T_SL σ^x. No spool (the engine answers at once), no ram or Mach effect, no fuel burn.
    /// The lapse exponent is CALIBRATED to the service ceiling (Calibration~/l0_calibration.py).
    /// </summary>
    public sealed class PowerplantL0 : IPowerplant
    {
        readonly PowerplantProfile _p;

        public PowerplantL0(in PowerplantProfile profile)
        {
            profile.Validate();
            _p = profile;
        }

        public float StaticThrustN(in PilotInputs inputs)
        {
            float perEngine = inputs.Afterburner
                ? _p.MaxThrustPerEngineN
                : _p.MilitaryThrustPerEngineN * (_p.IdleThrustFraction + (1f - _p.IdleThrustFraction) * inputs.Throttle);
            return _p.EngineCount * perEngine;
        }

        public float Step(in PilotInputs inputs, in AirData air, ref AirframeState state, float dt)
        {
            state.EngineSpool = inputs.Throttle;
            float sigma = (float)(air.DensityKgM3 / Isa.SeaLevelDensity);
            return StaticThrustN(inputs) * (float)Math.Pow(sigma, _p.ThrustLapseExponent);
        }
    }
}
