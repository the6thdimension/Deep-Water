using DeepWater.Metoc.Hdrp;
using NUnit.Framework;

namespace DeepWater.Metoc.Tests
{
    /// <summary>
    /// The drawing maps: visibility to fog and the inversion of the cloud-cover table (slice 2, E48, E51); rain's fog,
    /// cloud darkening, drop count and drop luminance (slice 3, E53–E55).
    /// </summary>
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
            var f = MetocFog.For(fogBank, fogBank.VisibilityM, 0.5, 1.5, 5000);
            Assert.AreEqual(300 / Visibility.Ln20, f.MeanFreePathM, 1e-9);
            Assert.AreEqual(60, f.BaseHeightM, 1e-12);
            Assert.AreEqual(120, f.MaximumHeightM, 1e-12);
            Assert.AreEqual(5000, f.MaxFogDistanceM, "never less far than the minimum");
            var clear = new MetocWeather(0, 1500, 1000, 0, 30000, 1000);
            Assert.AreEqual(45000, MetocFog.For(clear, clear.VisibilityM, 0.5, 1.5, 5000).MaxFogDistanceM, 1e-9, "out to 1.5 visibilities");
        }

        [Test]
        public void UnderRain_TheFogDrawsTheEffectiveVisibility_UpToTheCloudBase()
        {
            // 20 km of air, 10 mm/h of rain under a 600 m base: the report's effective visibility, about 2.4 km.
            var rain = new MetocWeather(8, 600, 3000, 0, 20000, 450, 10);
            double effective = Visibility.EffectiveM(rain.VisibilityM, Visibility.RainExtinctionPerM(10, new IlluminationProfile()));
            var f = MetocFog.For(rain, effective, 0.5, 1.5, 5000);
            Assert.AreEqual(effective / Visibility.Ln20, f.MeanFreePathM, 1e-9);
            Assert.AreEqual(600, MetocFog.TopM(rain), "rain fills the air below the cloud");
            Assert.AreEqual(600, f.MaximumHeightM);
            Assert.AreEqual(300, f.BaseHeightM, 1e-12);
            var dry = new MetocWeather(8, 600, 3000, 0, 20000, 450);
            Assert.AreEqual(450, MetocFog.TopM(dry), "dry, the obscuration top stands");
            var fogUnderRain = new MetocWeather(8, 300, 3000, 0, 500, 450, 2);
            Assert.AreEqual(450, MetocFog.TopM(fogUnderRain), "a fog bank deeper than the base keeps its top");
        }

        [Test]
        public void RainDarkensTheDrawnClouds_ByASaturatingShare()
        {
            Assert.AreEqual(0.0, MetocFog.CloudDim(0, 0.6, 8));
            Assert.AreEqual(0.3, MetocFog.CloudDim(8, 0.6, 8), 1e-15, "half the maximum at the half rate");
            // 0.6 · 50 / 58 = 0.517241.
            Assert.AreEqual(0.517241, MetocFog.CloudDim(50, 0.6, 8), 1e-6);
            Assert.Less(MetocFog.CloudDim(500, 0.6, 8), 0.6, "never past the maximum");
        }

        [Test]
        public void DrawnDrops_FollowTheRate_UnderTheCapacity()
        {
            Assert.AreEqual(0f, MetocRain.DrawnCount(0, 40000, 0.6, MetocRainVfx.Capacity));
            Assert.AreEqual(40000f, MetocRain.DrawnCount(10, 40000, 0.6, MetocRainVfx.Capacity), 1e-2f);
            // 40 000 · 0.15^0.6 = 40 000 · 0.320372 = 12 815.
            Assert.AreEqual(12814.9f, MetocRain.DrawnCount(1.5, 40000, 0.6, MetocRainVfx.Capacity), 1f);
            Assert.AreEqual(0.95f * MetocRainVfx.Capacity, MetocRain.DrawnCount(500, 40000, 0.6, MetocRainVfx.Capacity), 1e-2f,
                "held under the graph's capacity");
        }

        [Test]
        public void ADropsLuminance_IsAMultipleOfTheGroundLightOverPi()
        {
            // A sky of uniform luminance L lights the ground with E = πL; three times that is 3E/π.
            Assert.AreEqual(3 * 10000 / System.Math.PI, MetocRain.DropLuminance(10000, 3), 1e-2);
            Assert.AreEqual(0f, MetocRain.DropLuminance(0, 3));
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
