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
            state.AfterburnerSpool = inputs.Afterburner ? 1f : 0f;
            float sigma = (float)(air.DensityKgM3 / Isa.SeaLevelDensity);
            return StaticThrustN(inputs) * (float)Math.Pow(sigma, _p.ThrustLapseExponent);
        }
    }

    /// <summary>
    /// Powerplant L1 (BLUEPRINT §4): the engines take time. The core spool (idle 0 … military 1) follows the throttle
    /// with a first-order lag, faster down than up; with the afterburner selected, it lights only once the core is above
    /// <see cref="PowerplantProfile.AfterburnerMinSpool"/> and fades in and out with its own lags. Thrust is
    /// military × (idle + (1 − idle) × spool) plus the afterburner's extra × its spool, with L0's density lapse. The time
    /// constants are AUTHORED (no public spool data for the F414 was found). No fuel burn (L3).
    /// </summary>
    public sealed class PowerplantL1 : IPowerplant
    {
        readonly PowerplantProfile _p;

        public PowerplantL1(in PowerplantProfile profile)
        {
            if (!profile.HasSpool) throw new ArgumentException("Powerplant L1 needs spool time constants");
            profile.Validate();
            _p = profile;
        }

        public float Step(in PilotInputs inputs, in AirData air, ref AirframeState state, float dt)
        {
            float target = inputs.Afterburner ? 1f : inputs.Throttle;
            float tau = target > state.EngineSpool ? _p.SpoolUpTauS : _p.SpoolDownTauS;
            state.EngineSpool += (target - state.EngineSpool) * (1f - (float)Math.Exp(-dt / tau));

            bool lit = inputs.Afterburner && state.EngineSpool >= _p.AfterburnerMinSpool;
            float abTarget = lit ? 1f : 0f;
            float abTau = lit ? _p.AfterburnerLightTauS : _p.AfterburnerOffTauS;
            state.AfterburnerSpool += (abTarget - state.AfterburnerSpool) * (1f - (float)Math.Exp(-dt / abTau));

            float perEngine = _p.MilitaryThrustPerEngineN * (_p.IdleThrustFraction + (1f - _p.IdleThrustFraction) * state.EngineSpool)
                            + (_p.MaxThrustPerEngineN - _p.MilitaryThrustPerEngineN) * state.AfterburnerSpool;
            float sigma = (float)(air.DensityKgM3 / Isa.SeaLevelDensity);
            return _p.EngineCount * perEngine * (float)Math.Pow(sigma, _p.ThrustLapseExponent);
        }
    }
}
