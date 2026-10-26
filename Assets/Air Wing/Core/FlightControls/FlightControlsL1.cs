using System;
using DeepWater.AirWing.Core.Airframe;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.State;
using UnityEngine;

namespace DeepWater.AirWing.Core.FlightControls
{
    /// <summary>
    /// Flight Controls L1 (W23, W26, W33): Hornet-style fly-by-wire by nonlinear dynamic inversion, after the NDI laws
    /// designed on this same HARV model (Snell, Enns and Garrard, J. Guidance 15(4), 1992). The law carries the aero
    /// model and inverts it, so one set of gains (AUTHORED) holds across the envelope.
    /// <list type="bullet">
    /// <item><b>Pitch.</b> The stick commands G exactly as at L0 (W26), prefiltered. The lift curve is inverted for the α
    /// that gives that G here (less the stabilator's own lift; plus a slow integral on the G error for what the model
    /// misses), clamped at the AoA limit, so the limiter is exact by construction. The α error becomes a pitch-rate
    /// command about the rate the flight path is turning at now, and the pitch equation is inverted for the
    /// stabilator.</item>
    /// <item><b>Roll and yaw.</b> The stick commands roll rate; the yaw rate is the coordinated-turn rate plus sideslip
    /// feedback, and the pedals command sideslip. The roll and yaw equations are inverted together for aileron and
    /// rudder.</item>
    /// <item><b>Actuators</b> (PUBLIC, NASA TM-107601 via the 2011 paper's Table 2): a first-order lag, a rate limit, a
    /// position limit. The surfaces and the law's memory live in <see cref="AirframeState.Controls"/>.</item>
    /// </list>
    /// The limits are the law's, not the airframe's: past them, or with the actuators saturated, the airframe obeys its
    /// tables and can depart (W33). A fixed-gain PI law was tried first and missed on every gain set (ROADMAP 2b).
    /// </summary>
    public sealed class FlightControlsL1 : IFlightControls
    {
        const float G0 = Units.StandardGravity;
        const float MinSpeedMps = 10f;

        readonly ControlsProfile _c;
        readonly ControlsL1Profile _k;
        readonly AirframeL1Profile _a;
        /// <summary>The lift curve's peak α: fixed per profile, so found once (slice 2c: it was a 140-point scan every substep).</summary>
        readonly float _liftPeakAlpha;

        public FlightControlsL1(in AircraftProfile profile)
        {
            if (!profile.HasL1) throw new ArgumentException("Flight Controls L1 needs the type's L1 airframe and L1 law (AirframeL1, ControlsL1)");
            profile.Validate();
            _c = profile.Controls;
            _k = profile.ControlsL1;
            _a = profile.AirframeL1;
            _liftPeakAlpha = AirframeL1.LiftPeakAlpha(_a);
        }

        /// <summary>The load factor the stick asks for (the L0 map, W26).</summary>
        public float CommandedLoadFactor(float stickPitch) => stickPitch >= 0f
            ? 1f + stickPitch * (_c.LoadFactorMax - 1f)
            : 1f + stickPitch * (1f - _c.LoadFactorMin);

        public ControlDemand Demand(in PilotInputs inputs, ref AirframeState s, in AirData air, float dt)
        {
            ref ControlState c = ref s.Controls;
            Quaternion att = s.Attitude;
            Vector3 uvw = AirframeL1.ToNasa(Quaternion.Inverse(att) * s.Velocity);
            float v = uvw.magnitude;
            if (v < MinSpeedMps) return Hold(ref c, dt);

            Vector3 pqr = AirframeL1.RatesToNasa(s.BodyRates);
            float p = pqr.x, q = pqr.y, r = pqr.z;
            float alpha = Mathf.Atan2(uvw.z, uvw.x), beta = Mathf.Asin(Mathf.Clamp(uvw.y / v, -1f, 1f));
            float a = Mathf.Clamp(alpha, _a.AlphaMinRad, _a.AlphaMaxRad);
            float qS = 0.5f * air.DensityKgM3 * v * v * _a.ReferenceAreaM2;
            float n = s.LoadFactor;
            // Gravity's components in the body: cos θ cos φ along the body's down, sin φ cos θ along its right wing.
            float cosThCosPhi = (att * Vector3.up).y;
            float sinPhiCosTh = -(att * Vector3.right).y;

            // ── Pitch ─────────────────────────────────────────────────
            float nCmd = Mathf.Clamp(CommandedLoadFactor(inputs.StickPitch), _c.LoadFactorMin, _c.LoadFactorMax);
            float nf = c.LoadFactorCommand + (nCmd - c.LoadFactorCommand) * (1f - (float)Math.Exp(-dt / _k.LoadFactorCommandTauS));
            float en = nf - n;
            float integral = c.PitchIntegralRadS + _k.LoadFactorIntegralGain * en * dt;
            float clReq = qS > 1f ? nf * s.MassKg * G0 / qS - AirframeL1.LiftPerStabilator(_a, a) * c.StabilatorRad : 0f;
            float alphaCmd = AirframeL1.AlphaForBasicLift(_a, clReq / Mathf.Cos(2f * beta / 3f), _liftPeakAlpha) + integral;
            if (alphaCmd > _k.AlphaLimitRad)
            {
                alphaCmd = _k.AlphaLimitRad;
                if (en > 0f) integral = c.PitchIntegralRadS;          // anti-windup at the AoA limit
            }
            if (alphaCmd < _a.AlphaMinRad) alphaCmd = _a.AlphaMinRad;
            // The flight path turns at g (n − cos θ cos φ)/V with the G the jet has now, so α̇ = q − that.
            float qCmd = G0 * (n - cosThCosPhi) / v + _k.AlphaGain * (alphaCmd - alpha);
            float qDotDes = _k.PitchRateBandwidth * (qCmd - q);
            float mReq = _a.IyyKgM2 * qDotDes - (_a.IzzKgM2 - _a.IxxKgM2) * p * r - (r * r - p * p) * _a.IxzKgM2;
            float stabCmd = qS > 1f
                ? (mReq / (qS * _a.ChordM) - AirframeL1.PitchMomentFree(_a, a, q, v)) / AirframeL1.PitchMomentPerStabilator(_a, a)
                : c.StabilatorRad;
            if ((stabCmd < _k.Stabilator.MinRad && en > 0f) || (stabCmd > _k.Stabilator.MaxRad && en < 0f))
                integral = c.PitchIntegralRadS;                       // anti-windup at the stabilator's travel

            // ── Roll and yaw, inverted together ───────────────────────
            float pCmd = inputs.StickRoll * _c.RollRateMaxRadS;
            float betaCmd = -inputs.Pedals * _k.SideslipPerPedalRad;  // right pedal: nose right, negative sideslip
            float rCmd = G0 * sinPhiCosTh / v + p * Mathf.Tan(a) + _k.SideslipGain * (beta - betaCmd);
            float pDotDes = _k.RollRateBandwidth * (pCmd - p);
            float rDotDes = _k.YawRateBandwidth * (rCmd - r);
            float ixx = _a.IxxKgM2, iyy = _a.IyyKgM2, izz = _a.IzzKgM2, ixz = _a.IxzKgM2;
            float den = ixx * izz - ixz * ixz;
            float cp = -(ixz * (iyy - ixx - izz) * p + (ixz * ixz + izz * (izz - iyy)) * r) * q;
            float cr = (ixz * (iyy - ixx - izz) * r + (ixz * ixz + ixx * (ixx - iyy)) * p) * q;
            float bl = den * pDotDes - cp, bn = den * rDotDes - cr;   // [Izz Ixz; Ixz Ixx]·[L; N] = [bl; bn]
            float lReq = (ixx * bl - ixz * bn) / den;
            float nReq = (izz * bn - ixz * bl) / den;
            float ailCmd = c.AileronRad, rudCmd = c.RudderRad;
            if (qS > 1f)
            {
                AirframeL1.LateralFree(_a, alpha, beta, p, r, v, out float clFree, out float cnFree);
                float qSb = qS * _a.SpanM;
                float rl = lReq / qSb - clFree, rn = nReq / qSb - cnFree;
                Vector4 e = AirframeL1.LateralControl(_a, alpha);     // Clδa, Clδr, Cnδa, Cnδr
                float d = e.x * e.w - e.y * e.z;
                if (Mathf.Abs(d) > 1e-6f)
                {
                    ailCmd = (rl * e.w - e.y * rn) / d;
                    rudCmd = (e.x * rn - rl * e.z) / d;
                }
            }

            c.StabilatorRad = Actuate(c.StabilatorRad, stabCmd, _k.Stabilator, dt);
            c.AileronRad = Actuate(c.AileronRad, ailCmd, _k.Aileron, dt);
            c.RudderRad = Actuate(c.RudderRad, rudCmd, _k.Rudder, dt);
            c.PitchIntegralRadS = integral;
            c.LoadFactorCommand = nf;
            return new ControlDemand(new ControlSurfaces(c.StabilatorRad, c.AileronRad, c.RudderRad));
        }

        /// <summary>Too slow to fly: hold the surfaces where they are.</summary>
        static ControlDemand Hold(ref ControlState c, float dt) => new ControlDemand(new ControlSurfaces(c.StabilatorRad, c.AileronRad, c.RudderRad));

        /// <summary>One actuator over <paramref name="dt"/>: the exact first-order lag, then the rate limit, then the travel.</summary>
        public static float Actuate(float position, float command, in ActuatorProfile a, float dt)
        {
            float target = position + (command - position) * (1f - (float)Math.Exp(-a.BandwidthRadS * dt));
            float step = Mathf.Clamp(target - position, -a.RateRadS * dt, a.RateRadS * dt);
            return Mathf.Clamp(position + step, a.MinRad, a.MaxRad);
        }
    }
}
