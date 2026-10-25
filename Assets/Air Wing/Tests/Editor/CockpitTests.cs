using NUnit.Framework;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.Cockpit;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.State;

namespace DeepWater.AirWing.Tests
{
    /// <summary>Slice 1d: the HUD's numbers come from the core state, converted at the edge (W22).</summary>
    public class CockpitTests
    {
        [Test]
        public void HudReadout_ConvertsTheCoreStateToAviationUnits()
        {
            var state = new AirframeState
            {
                Position = new Vector3(0f, 3048f, 0f),                         // exactly 10,000 ft
                Velocity = Quaternion.Euler(0f, 90f, 0f) * new Vector3(0f, 0f, 257.2222f) + new Vector3(0f, 5.08f, 0f),
                Attitude = Quaternion.Euler(0f, 90f, 0f),
                AngleOfAttack = 5f * Units.DegToRad,
                LoadFactor = 2.5f,
            };
            AirData air = Isa.At(3048.0);
            var inputs = new PilotInputs { Throttle = 0.75f };

            HudReadout r = HudReadout.From(state, air, inputs);

            Assert.AreEqual(10000f, r.AltitudeFt, 0.01f, "3,048 m is 10,000 ft");
            Assert.AreEqual(1000f, r.ClimbFpm, 0.1f, "5.08 m/s is 1,000 ft/min");
            Assert.AreEqual(500.1f, r.TrueAirspeedKt, 0.05f, "257.22 m/s horizontal is 500 kt; the 1,000 ft/min climb adds 0.1");
            Assert.AreEqual(90f, r.HeadingDeg, 1e-3f, "nose along +X is 090");
            Assert.AreEqual(5f, r.AngleOfAttackDeg, 1e-4f);
            Assert.AreEqual(2.5f, r.LoadFactorG);
            Assert.AreEqual(state.Velocity.magnitude / air.SpeedOfSoundMps, r.Mach, 1e-6f, "Mach is true airspeed over the local speed of sound");
            Assert.AreEqual(75f, r.ThrottlePct, 1e-4f);
            Assert.IsFalse(r.Afterburner);
            Assert.IsFalse(r.Crashed);
        }

        [Test]
        public void TheConversions_AreTheExactDefinitions()
        {
            Assert.AreEqual(0.3048, Units.FootToMetre);
            Assert.AreEqual(1852.0 / 3600.0, Units.KnotToMetrePerSecond);
            Assert.AreEqual(0.45359237, Units.PoundToKilogram);
            Assert.AreEqual(4.4482216152605, Units.PoundForceToNewton);
            Assert.AreEqual(9.80665f, Units.StandardGravity);
        }
    }
}
