using System;
using NUnit.Framework;

namespace DeepWater.Metoc.Tests
{
    /// <summary>
    /// Slice 2's weather (E30, E33, E49, E51, E52): validation, the METAR and WMO labels, blends, cloud dimming, and
    /// weather orders in the reckoned core.
    /// </summary>
    public class MetocWeatherTests
    {
        const double Step = 0.02;
        static readonly MetocWeather Clear = new MetocWeather(0, 1500, 1000, 0, 30000, 1000);
        static readonly MetocWeather Overcast = new MetocWeather(8, 450, 900, 2, 8000, 450);

        static MetocWeather WithCover(double oktas) => new MetocWeather(oktas, 1500, 1000, 0, 30000, 1000);
        static MetocWeather WithVisibility(double m) => new MetocWeather(0, 1500, 1000, 0, m, 1000);

        static MetocSimulation Sim(in MetocWeather start, double daySpeed = 60) =>
            new MetocSimulation(new MetocScenario(Calendar.UtSeconds(2026, 10, 30, 14, 24, 0), new MetocPlace(30, -40), daySpeed, start),
                new IlluminationProfile());

        [Test]
        public void Weather_IsValidated_FieldByField()
        {
            Assert.IsNull(Clear.Problem());
            Assert.IsNull(Overcast.Problem());
            StringAssert.Contains("cover", WithCover(8.01).Problem());
            StringAssert.Contains("cover", WithCover(-0.1).Problem());
            StringAssert.Contains("cover", WithCover(double.NaN).Problem());
            StringAssert.Contains("high cloud", new MetocWeather(0, 1500, 1000, 9, 30000, 1000).Problem());
            StringAssert.Contains("cloud base", new MetocWeather(0, -1, 1000, 0, 30000, 1000).Problem());
            StringAssert.Contains("thickness", new MetocWeather(0, 1500, 0, 0, 30000, 1000).Problem());
            StringAssert.Contains("visibility", WithVisibility(0.5).Problem());
            StringAssert.Contains("visibility", WithVisibility(double.PositiveInfinity).Problem());
            StringAssert.Contains("obscuration top", new MetocWeather(0, 1500, 1000, 0, 30000, 0).Problem());
        }

        [Test]
        public void CoverCodes_AndTheCeiling_FollowMetar()
        {
            string[] codes = { "SKC", "FEW", "FEW", "SCT", "SCT", "BKN", "BKN", "BKN", "OVC" };
            for (int n = 0; n <= 8; n++)
            {
                Assert.AreEqual(codes[n], WithCover(n).CoverCode, $"{n} oktas");
                Assert.AreEqual(n >= 5, WithCover(n).HasCeiling, $"{n} oktas");
            }
            // A blend's fraction rounds as a report would: 4.5 oktas reports BKN with a ceiling.
            Assert.AreEqual("BKN", WithCover(4.5).CoverCode);
            Assert.AreEqual(1500, WithCover(5).CeilingM);
            Assert.AreEqual(double.PositiveInfinity, WithCover(4).CeilingM);
        }

        [Test]
        public void ObscurationLabels_FollowWmo()
        {
            Assert.AreEqual("fog", WithVisibility(999).ObscurationLabel);
            Assert.AreEqual("mist or haze", WithVisibility(1000).ObscurationLabel);
            Assert.AreEqual("mist or haze", WithVisibility(5000).ObscurationLabel);
            Assert.AreEqual("", WithVisibility(5001).ObscurationLabel);
        }

        [Test]
        public void Blend_IsLinearInCoverAndHeights_AndLogInVisibility()
        {
            var a = new MetocWeather(0, 1000, 500, 0, 100, 100);
            var b = new MetocWeather(8, 2000, 1500, 4, 10000, 10000);
            var half = MetocWeather.Blend(a, b, 0.5);
            Assert.AreEqual(4, half.CoverOktas, 1e-12);
            Assert.AreEqual(1500, half.CloudBaseM, 1e-9);
            Assert.AreEqual(1000, half.CloudThicknessM, 1e-9);
            Assert.AreEqual(2, half.HighCloudOktas, 1e-12);
            Assert.AreEqual(1000, half.VisibilityM, 1e-9, "half-way from 100 m to 10 km in log space is 1 km");
            Assert.AreEqual(1000, half.ObscurationTopM, 1e-9);
            Assert.AreEqual(a.VisibilityM, MetocWeather.Blend(a, b, 0).VisibilityM);
            Assert.AreEqual(b.VisibilityM, MetocWeather.Blend(a, b, 1).VisibilityM, "the end is exact, not exp(log)");
        }

        [Test]
        public void CloudTransmission_IsKastenAndCzeplak()
        {
            var p = new IlluminationProfile();
            Assert.AreEqual(1.0, Illumination.CloudTransmission(0, p));
            Assert.AreEqual(0.25, Illumination.CloudTransmission(8, p), 1e-15);
            // 1 − 0.75 · 0.5^3.4 = 1 − 0.75 · 0.094732 = 0.928951.
            Assert.AreEqual(0.928951, Illumination.CloudTransmission(4, p), 1e-6);
        }

        [Test]
        public void Overcast_DimsSunMoonAndNightSky_ByTheSameShare()
        {
            var p = new IlluminationProfile();
            var sun = new SunState(new SkyPosition(45, 45, 180), 0, 0, 0, 0, 1);
            var moon = new MoonState(new SkyPosition(30, 30, 90), 0, 0, p.MoonMeanDistanceKm, 0.95, 0, 1, false);
            var clear = Illumination.Compute(sun, moon, p, 0);
            var overcast = Illumination.Compute(sun, moon, p, 8);
            Assert.AreEqual(1.0, clear.CloudTransmission);
            Assert.AreEqual(0.25, overcast.CloudTransmission, 1e-15);
            Assert.AreEqual(clear.SunLux * 0.25, overcast.SunLux, 1e-9);
            Assert.AreEqual(clear.MoonLux * 0.25, overcast.MoonLux, 1e-15);
            Assert.AreEqual(clear.NightSkyLux * 0.25, overcast.NightSkyLux, 1e-15);
            Assert.AreEqual(clear.TotalLux, overcast.ClearSkyLux, 1e-9);
            Assert.AreEqual(clear.SunAboveAirLux, overcast.SunAboveAirLux, "the drawn lights carry the above-air value; HDRP's clouds dim the sun");
        }

        [Test]
        public void WeatherOrder_BlendsOverDaySeconds_ThenHolds()
        {
            var sim = Sim(Clear);   // 60×: a 20 ms step is 1.2 day seconds
            sim.Issue(MetocOrder.SetWeather(Overcast, 120, "test"));
            for (int i = 0; i < 50; i++) sim.Step(Step);
            Assert.AreEqual(4, sim.Report.Weather.CoverOktas, 1e-9, "half-way after 60 day seconds");
            Assert.AreEqual(Math.Sqrt(Clear.VisibilityM * Overcast.VisibilityM), sim.Report.Weather.VisibilityM, 1e-6);
            Assert.AreEqual(0.5, sim.BlendProgress, 1e-9);
            for (int i = 0; i < 60; i++) sim.Step(Step);
            Assert.AreEqual(1.0, sim.BlendProgress);
            Assert.AreEqual(Overcast.CoverOktas, sim.Report.Weather.CoverOktas);
            Assert.AreEqual(Overcast.VisibilityM, sim.Report.Weather.VisibilityM);
            Assert.AreEqual(0.25, sim.Report.Light.CloudTransmission, 1e-15);
            Assert.IsTrue(sim.Report.Weather.HasCeiling);
        }

        [Test]
        public void WeatherOrder_MidBlend_StartsFromWhereTheWeatherIs()
        {
            var sim = Sim(Clear);
            sim.Issue(MetocOrder.SetWeather(Overcast, 120, "test"));
            for (int i = 0; i < 50; i++) sim.Step(Step);
            sim.Issue(MetocOrder.SetWeather(Clear, 120, "test"));
            sim.Step(Step);
            // From 4 oktas toward 0, one hundredth of the way: no jump back to 0 or on to 8.
            Assert.AreEqual(3.96, sim.Report.Weather.CoverOktas, 1e-9);
        }

        [Test]
        public void WeatherOrder_WithNoBlendTime_ChangesAtOnce()
        {
            var sim = Sim(Clear);
            sim.Issue(MetocOrder.SetWeather(Overcast, 0, "test"));
            sim.Step(Step);
            Assert.AreEqual(8, sim.Report.Weather.CoverOktas);
            Assert.AreEqual(1.0, sim.BlendProgress);
        }

        [Test]
        public void WeatherBlend_RunsOnDayTime_NotOnJumps()
        {
            var a = Sim(Clear);
            var b = Sim(Clear);
            a.Issue(MetocOrder.SetWeather(Overcast, 120, "test"));
            b.Issue(MetocOrder.SetWeather(Overcast, 120, "test"));
            b.Issue(MetocOrder.JumpTo(Calendar.UtSeconds(2026, 11, 3, 0, 0, 0), "test"));
            for (int i = 0; i < 25; i++)
            {
                a.Step(Step);
                b.Step(Step);
            }
            Assert.AreEqual(a.Report.Weather.CoverOktas, b.Report.Weather.CoverOktas, "a jump in time does not move the weather");
        }

        [Test]
        public void BadWeatherOrders_AreRefusedWithAReason_AndChangeNothing()
        {
            var sim = Sim(Clear);
            sim.Issue(MetocOrder.SetWeather(WithCover(9), 10, "test"));
            sim.Issue(MetocOrder.SetWeather(Overcast, -1, "test"));
            sim.Issue(MetocOrder.SetWeather(Overcast, double.NaN, "test"));
            sim.Issue(MetocOrder.SetWeather(Overcast, double.PositiveInfinity, "test"));
            sim.Step(Step);
            Assert.AreEqual(4, sim.Log.Count);
            foreach (var record in sim.Log) Assert.IsNotNull(record.Refused, record.ToString());
            Assert.AreEqual(0, sim.Report.Weather.CoverOktas);
            Assert.AreEqual(Clear.VisibilityM, sim.Report.Weather.VisibilityM);
        }

        [Test]
        public void TheFingerprint_SeesTheWeather()
        {
            var a = Sim(Clear);
            var b = Sim(WithVisibility(29999));
            Assert.AreNotEqual(a.Report.Fingerprint(), b.Report.Fingerprint());
        }
    }
}
