using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.State;
using UnityEngine;

namespace DeepWater.AirWing.Core.Cockpit
{
    /// <summary>
    /// What the HUD shows, in the units a pilot reads (W22): converted here, once, at the edge. The core stays SI.
    /// Airspeed is true airspeed: L0 has no pitot model, so no calibrated airspeed is claimed.
    /// </summary>
    public readonly struct HudReadout
    {
        public readonly float TrueAirspeedKt;
        public readonly float AltitudeFt;
        public readonly float ClimbFpm;
        public readonly float HeadingDeg;
        public readonly float AngleOfAttackDeg;
        public readonly float LoadFactorG;
        public readonly float Mach;
        /// <summary>Throttle, percent of military power.</summary>
        public readonly float ThrottlePct;
        public readonly bool Afterburner;
        public readonly bool Crashed;

        HudReadout(float tas, float alt, float climb, float heading, float aoa, float g, float mach, float throttle, bool ab, bool crashed)
        {
            TrueAirspeedKt = tas;
            AltitudeFt = alt;
            ClimbFpm = climb;
            HeadingDeg = heading;
            AngleOfAttackDeg = aoa;
            LoadFactorG = g;
            Mach = mach;
            ThrottlePct = throttle;
            Afterburner = ab;
            Crashed = crashed;
        }

        public static HudReadout From(in AirframeState s, in AirData air, in PilotInputs lastInputs)
        {
            float speed = s.Velocity.magnitude;
            Vector3 nose = s.Attitude * Vector3.forward;
            float heading = Mathf.Repeat(Mathf.Atan2(nose.x, nose.z) * Units.RadToDeg, 360f);
            return new HudReadout(
                Units.Knots(speed),
                Units.Feet(s.Position.y),
                Units.Feet(s.Velocity.y) * 60f,
                heading,
                s.AngleOfAttack * Units.RadToDeg,
                s.LoadFactor,
                air.SpeedOfSoundMps > 0f ? speed / air.SpeedOfSoundMps : 0f,
                lastInputs.Throttle * 100f,
                lastInputs.Afterburner,
                s.Status == FlightStatus.Crashed);
        }
    }
}
