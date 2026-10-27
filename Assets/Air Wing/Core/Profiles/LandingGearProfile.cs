using System;
using UnityEngine;

namespace DeepWater.AirWing.Core.Profiles
{
    /// <summary>One wheel and its strut: where the tyre meets the ground at full extension, and the strut's spring and damper.</summary>
    public struct WheelProfile
    {
        public string Name;
        /// <summary>The tyre's lowest point at full extension, metres, in the body frame about the centre of mass (Unity axes).</summary>
        public Vector3 ContactPointM;
        public float TravelM;
        public float SpringNPerM;
        public float DamperNsPerM;
        /// <summary>Steered by the pedals (the nosewheel).</summary>
        public bool Steerable;
        /// <summary>Has a wheel brake (the mains).</summary>
        public bool Braked;
    }

    /// <summary>
    /// Landing Gear L1's numbers, SI (BLUEPRINT §4): the wheels, the tyres' friction, the steering, the gear's transit,
    /// the sink rate the gear takes, and the hard points (belly, tail, nose, wingtips) that end the flight if they touch.
    /// </summary>
    public struct LandingGearProfile
    {
        public WheelProfile[] Wheels;
        /// <summary>Points of the airframe that must never touch a surface, body frame about the centre of mass (Unity axes).</summary>
        public Vector3[] HardPointsM;
        /// <summary>A wheel meeting the ground faster than this along its strut breaks the gear, m/s.</summary>
        public float MaxSinkRateMps;
        public float RollingFriction, BrakingFriction, LateralFriction;
        /// <summary>The tyre slip speed at which friction saturates (a smooth friction model), m/s.</summary>
        public float SlipSpeedMps;
        /// <summary>Nosewheel steering at full pedal, radians, fading out between these speeds, m/s.</summary>
        public float SteerMaxRad, SteerFadeStartMps, SteerFadeEndMps;
        /// <summary>Gear up or down, seconds.</summary>
        public float TransitTimeS;
        /// <summary>Drag of the gear down.</summary>
        public float GearDownDeltaCD;
        /// <summary>
        /// The strut's end of travel stiffens, as an oleo's air spring does near full stroke: past this fraction of the
        /// travel the spring adds <see cref="EndStopStiffening"/> × k × (x − x₀)² / (L − x₀). 0 stiffening = a linear strut.
        /// </summary>
        public float EndStopFraction, EndStopStiffening;

        public bool IsSet => Wheels != null && Wheels.Length > 0;

        public void Validate()
        {
            if (!IsSet) throw new ArgumentException("landing gear: no wheels");
            foreach (WheelProfile w in Wheels)
            {
                AirframeProfile.Positive(w.TravelM, w.Name + ".TravelM");
                AirframeProfile.Positive(w.SpringNPerM, w.Name + ".SpringNPerM");
                AirframeProfile.NonNegative(w.DamperNsPerM, w.Name + ".DamperNsPerM");
            }
            if (HardPointsM == null || HardPointsM.Length == 0) throw new ArgumentException("landing gear: no hard points");
            AirframeProfile.Positive(MaxSinkRateMps, nameof(MaxSinkRateMps));
            AirframeProfile.NonNegative(RollingFriction, nameof(RollingFriction));
            AirframeProfile.NonNegative(BrakingFriction, nameof(BrakingFriction));
            AirframeProfile.NonNegative(LateralFriction, nameof(LateralFriction));
            AirframeProfile.Positive(SlipSpeedMps, nameof(SlipSpeedMps));
            AirframeProfile.NonNegative(SteerMaxRad, nameof(SteerMaxRad));
            if (!(SteerFadeEndMps > SteerFadeStartMps)) throw new ArgumentException("landing gear: the steering fade must end above where it starts");
            AirframeProfile.Positive(TransitTimeS, nameof(TransitTimeS));
            AirframeProfile.NonNegative(GearDownDeltaCD, nameof(GearDownDeltaCD));
            AirframeProfile.NonNegative(EndStopStiffening, nameof(EndStopStiffening));
            if (EndStopStiffening > 0f && !(EndStopFraction > 0f && EndStopFraction < 1f))
                throw new ArgumentException("landing gear: the end stop must start inside the travel (0 < EndStopFraction < 1)");
        }
    }
}
