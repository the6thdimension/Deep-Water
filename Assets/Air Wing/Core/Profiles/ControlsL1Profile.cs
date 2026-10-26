using System;

namespace DeepWater.AirWing.Core.Profiles
{
    /// <summary>One surface's actuator: a first-order lag, then a rate limit, then a position limit.</summary>
    public struct ActuatorProfile
    {
        public float BandwidthRadS;
        public float RateRadS;
        public float MinRad;
        public float MaxRad;

        public void Validate(string name)
        {
            AirframeProfile.Positive(BandwidthRadS, name + "." + nameof(BandwidthRadS));
            AirframeProfile.Positive(RateRadS, name + "." + nameof(RateRadS));
            if (!(MinRad < MaxRad)) throw new ArgumentException($"{name}: travel {MinRad} … {MaxRad} rad is empty");
        }
    }

    /// <summary>
    /// Flight Controls L1's numbers, SI (W23). The stick keeps its L0 meaning (W26): the G limits and the roll rate at
    /// full stick come from <see cref="ControlsProfile"/>. Everything here is the law's own: gains AUTHORED, actuators
    /// PUBLIC (NASA TM-107601 via Chakraborty, Seiler and Balas 2011, Table 2).
    /// </summary>
    public struct ControlsL1Profile
    {
        /// <summary>The AoA limit: the G command's alpha never exceeds it.</summary>
        public float AlphaLimitRad;
        /// <summary>The G command's prefilter time constant, seconds.</summary>
        public float LoadFactorCommandTauS;
        /// <summary>Pitch rate commanded per radian of alpha error, 1/s.</summary>
        public float AlphaGain;
        /// <summary>Alpha command added per g·s of G error (model mismatch), rad/(g·s).</summary>
        public float LoadFactorIntegralGain;
        /// <summary>Pitch-, roll- and yaw-rate bandwidths of the inversion, 1/s.</summary>
        public float PitchRateBandwidth, RollRateBandwidth, YawRateBandwidth;
        /// <summary>Yaw rate commanded per radian of sideslip error, 1/s.</summary>
        public float SideslipGain;
        /// <summary>Sideslip commanded at full pedal, radians.</summary>
        public float SideslipPerPedalRad;
        public ActuatorProfile Stabilator, Aileron, Rudder;

        public bool IsSet => AlphaLimitRad > 0f;

        public void Validate()
        {
            AirframeProfile.Positive(AlphaLimitRad, nameof(AlphaLimitRad));
            AirframeProfile.Positive(LoadFactorCommandTauS, nameof(LoadFactorCommandTauS));
            AirframeProfile.Positive(AlphaGain, nameof(AlphaGain));
            AirframeProfile.NonNegative(LoadFactorIntegralGain, nameof(LoadFactorIntegralGain));
            AirframeProfile.Positive(PitchRateBandwidth, nameof(PitchRateBandwidth));
            AirframeProfile.Positive(RollRateBandwidth, nameof(RollRateBandwidth));
            AirframeProfile.Positive(YawRateBandwidth, nameof(YawRateBandwidth));
            AirframeProfile.NonNegative(SideslipGain, nameof(SideslipGain));
            AirframeProfile.NonNegative(SideslipPerPedalRad, nameof(SideslipPerPedalRad));
            Stabilator.Validate(nameof(Stabilator));
            Aileron.Validate(nameof(Aileron));
            Rudder.Validate(nameof(Rudder));
        }
    }
}
