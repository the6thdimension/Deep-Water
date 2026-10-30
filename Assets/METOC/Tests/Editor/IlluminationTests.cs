using NUnit.Framework;

namespace DeepWater.Metoc.Tests
{
    /// <summary>The clear-sky light model (E45) against values worked by hand from its published formulas.</summary>
    public class IlluminationTests
    {
        static readonly IlluminationProfile P = new IlluminationProfile();

        [Test]
        public void AirMass_IsOneOverhead_AndAbout38AtTheHorizon()
        {
            Assert.AreEqual(0.999712, Illumination.AirMass(90), 1e-5);
            Assert.AreEqual(37.92, Illumination.AirMass(0), 0.05);
        }

        [Test]
        public void SunOverhead_GivesDirectPlusDiffuse_ByHand()
        {
            // 127 500 · e^(−0.21 · 0.999712) + 1000 · (0.8 + 15.5) = 103 359 + 16 300.
            Assert.AreEqual(119659, Illumination.SunHorizontalLux(90, P.SolarAboveAirLux, P), 5);
        }

        [Test]
        public void SunLight_IsContinuousAtTheHorizon_AndHitsTheTwilightTable()
        {
            // The sky term rises as √(sin h), steeply but continuously, so probe very close to the horizon.
            double above = Illumination.SunHorizontalLux(1e-10, P.SolarAboveAirLux, P);
            double below = Illumination.SunHorizontalLux(-1e-10, P.SolarAboveAirLux, P);
            Assert.AreEqual(800.0, above, 0.5);
            Assert.AreEqual(above, below, 0.5);
            Assert.AreEqual(3.4, Illumination.SunHorizontalLux(-6, P.SolarAboveAirLux, P), 1e-9);
            Assert.AreEqual(0.008, Illumination.SunHorizontalLux(-12, P.SolarAboveAirLux, P), 1e-12);
            Assert.AreEqual(0.0006, Illumination.SunHorizontalLux(-18, P.SolarAboveAirLux, P), 1e-12);
            Assert.AreEqual(0.0, Illumination.SunHorizontalLux(-25, P.SolarAboveAirLux, P));
        }

        [Test]
        public void SunLight_RisesWithTheSun()
        {
            double last = -1;
            for (double h = -18; h <= 90; h += 0.1)
            {
                double lux = Illumination.SunHorizontalLux(h, P.SolarAboveAirLux, P);
                Assert.Greater(lux, last, $"at {h:F1}°");
                last = lux;
            }
        }

        [Test]
        public void FullMoonOverhead_IsAboutAQuarterLux()
        {
            // 10^(−0.4 · 3.84) fc · 10.7639 = 0.31331 lux above the air; · 10^(−0.4 · 0.172 · 0.999712) = 0.26745 lux.
            double aboveAir = Illumination.MoonAboveAirLux(0, P.MoonMeanDistanceKm, P);
            Assert.AreEqual(0.31331, aboveAir, 1e-4);
            var sun = new SunState(new SkyPosition(-40, -40, 0), 0, 0, 0, 0, 1);
            var moon = new MoonState(new SkyPosition(90, 90, 0), 0, 0, P.MoonMeanDistanceKm, 0.95, 0, 1, false);
            var light = Illumination.Compute(sun, moon, P);
            Assert.AreEqual(0.26745, light.MoonLux, 2e-4);
            Assert.AreEqual(0.0, light.SunLux);
            Assert.AreEqual(0.26745 + P.NightSkyLux, light.TotalLux, 2e-4);
        }

        [Test]
        public void QuarterMoon_IsAboutOneEleventhOfFull()
        {
            // m = −12.73 + 2.34 + 0.2624 = −10.1276: 10^(−0.4 · 6.4424) fc = 0.028511 lux above the air.
            Assert.AreEqual(0.028511, Illumination.MoonAboveAirLux(90, P.MoonMeanDistanceKm, P), 0.028511 * 1e-3);
        }

        [Test]
        public void MoonlessNight_IsTheNightSkyFloor()
        {
            var sun = new SunState(new SkyPosition(-30, -30, 0), 0, 0, 0, 0, 1);
            var moon = new MoonState(new SkyPosition(-10, -10, 0), 0, 0, P.MoonMeanDistanceKm, 0.95, 0, 1, false);
            Assert.AreEqual(P.NightSkyLux, Illumination.Compute(sun, moon, P).TotalLux, 1e-15);
        }
    }
}
