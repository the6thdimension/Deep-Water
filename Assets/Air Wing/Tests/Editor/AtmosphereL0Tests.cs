using NUnit.Framework;
using DeepWater.AirWing.Core.Atmosphere;

namespace DeepWater.AirWing.Tests
{
    /// <summary>Slice 1b: the ISA against the U.S. Standard Atmosphere 1976 table (geopotential altitude).</summary>
    public class AtmosphereL0Tests
    {
        static void AssertAir(double h, double t, double p, double rho, double a)
        {
            AirData air = Isa.At(h);
            Assert.AreEqual(t, air.TemperatureK, t * 1e-5, $"T at {h} m");
            Assert.AreEqual(p, air.PressurePa, p * 1e-4, $"p at {h} m");
            Assert.AreEqual(rho, air.DensityKgM3, rho * 1e-4, $"ρ at {h} m");
            Assert.AreEqual(a, air.SpeedOfSoundMps, a * 1e-4, $"a at {h} m");
        }

        [Test]
        public void Isa_MatchesTheStandardTable_AtTheLayerBoundaries()
        {
            AssertAir(0, 288.15, 101325, 1.2250, 340.29);
            AssertAir(11000, 216.65, 22632, 0.36392, 295.07);
            AssertAir(20000, 216.65, 5474.9, 0.088035, 295.07);
            AssertAir(32000, 228.65, 868.02, 0.013225, 303.13);
        }

        [Test]
        public void Isa_ClampsOutsideItsRange_AndTheL0PartIsTheIsa()
        {
            Assert.AreEqual(Isa.At(32000).DensityKgM3, Isa.At(45000).DensityKgM3, "above 32 km the air is held at 32 km");
            Assert.AreEqual(Isa.At(-610).DensityKgM3, Isa.At(-2000).DensityKgM3);
            Assert.AreEqual(Isa.At(5000).DensityKgM3, new AtmosphereL0().Sample(5000f).DensityKgM3);
        }
    }
}
