using DeepWater.Metoc.Hdrp;
using NUnit.Framework;

namespace DeepWater.Metoc.Tests
{
    /// <summary>Slice 2's drawing maps (E48, E51): visibility to fog, and the inversion of the cloud-cover table.</summary>
    public class MetocDrawingTests
    {
        [Test]
        public void OneKilometreVisibility_IsAMeanFreePathOf334Metres()
        {
            // ln 20 = 2.995732: 1000 / 2.995732 = 333.808 m. The beam keeps e^(−ln 20) = 5 % at 1 km.
            Assert.AreEqual(333.808, MetocFog.MeanFreePathM(1000), 1e-3);
        }

        [Test]
        public void Fog_IsUniformToTheShareOfTheTop_AndEndsAtTheTop()
        {
            var fogBank = new MetocWeather(0, 1500, 1000, 0, 300, 120);
            var f = MetocFog.For(fogBank, 0.5, 1.5, 5000);
            Assert.AreEqual(300 / MetocFog.Ln20, f.MeanFreePathM, 1e-9);
            Assert.AreEqual(60, f.BaseHeightM, 1e-12);
            Assert.AreEqual(120, f.MaximumHeightM, 1e-12);
            Assert.AreEqual(5000, f.MaxFogDistanceM, "never less far than the minimum");
            var clear = new MetocWeather(0, 1500, 1000, 0, 30000, 1000);
            Assert.AreEqual(45000, MetocFog.For(clear, 0.5, 1.5, 5000).MaxFogDistanceM, 1e-9, "out to 1.5 visibilities");
        }

        static readonly float[] Sf = { 1.0f, 0.9f, 0.8f, 0.6f, 0.4f, 0.0f };
        static readonly float[] Cover = { 0.0f, 0.1f, 0.3f, 0.25f, 0.8f, 1.0f };   // a noisy dip at 0.6

        [Test]
        public void Inversion_IsPiecewiseLinear()
        {
            Assert.AreEqual(0.95f, CloudCoverCalibration.Solve(Sf, Cover, 0.05), 1e-6f);
            Assert.AreEqual(0.85f, CloudCoverCalibration.Solve(Sf, Cover, 0.2), 1e-6f);
        }

        [Test]
        public void Inversion_WalksPastANoisyDip_AndClampsAtTheEnds()
        {
            // The envelope holds 0.3 from 0.8 to 0.6, then rises to 0.8 at 0.4: 0.55 is half-way from 0.6 to 0.4.
            Assert.AreEqual(0.5f, CloudCoverCalibration.Solve(Sf, Cover, 0.55), 1e-6f);
            Assert.AreEqual(1.0f, CloudCoverCalibration.Solve(Sf, Cover, -0.1), 0f);
            Assert.AreEqual(0.0f, CloudCoverCalibration.Solve(Sf, Cover, 1.2), 0f);
        }

        [Test]
        public void ABadTable_SaysWhy()
        {
            Assert.IsNotNull(new CloudCoverCalibration().Problem());
            var c = new CloudCoverCalibration { shapeFactor = new[] { 0.5f, 0.9f }, coverMean = new[] { 0.2f, 0.1f } };
            StringAssert.Contains("descending", c.Problem());
            c = new CloudCoverCalibration { shapeFactor = Sf, coverMean = Cover };
            Assert.IsNull(c.Problem());
        }
    }
}
