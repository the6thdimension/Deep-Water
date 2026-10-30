using NUnit.Framework;
using UnityEngine;

namespace DeepWater.Metoc.Tests
{
    /// <summary>
    /// The calendar and the almanac against published worked examples (Meeus, <i>Astronomical Algorithms</i>, 2nd ed.)
    /// and closed-form cases. Each example's numbers are the book's.
    /// </summary>
    public class AlmanacTests
    {
        [Test]
        public void JulianDate_MatchesMeeusExample7a()
        {
            Assert.AreEqual(2436116.31, Calendar.JulianDate(1957, 10, 4.81), 1e-6);
            Assert.AreEqual(Calendar.J2000, Calendar.JulianDate(2000, 1, 1.5), 1e-9);
        }

        [Test]
        public void FromJulianDate_MatchesMeeusExample7c()
        {
            Calendar.FromJulianDate(2436116.31, out int y, out int m, out double d);
            Assert.AreEqual(1957, y);
            Assert.AreEqual(10, m);
            Assert.AreEqual(4.81, d, 1e-6);
        }

        [Test]
        public void UtSeconds_RoundTripsThroughTheCalendar()
        {
            double ut = Calendar.UtSeconds(2026, 10, 30, 18, 7, 42.5);
            Calendar.ToCalendar(ut, out int y, out int mo, out int d, out int h, out int mi, out double s);
            Assert.AreEqual((2026, 10, 30, 18, 7), (y, mo, d, h, mi));
            Assert.AreEqual(42.5, s, 1e-6);
            Assert.AreEqual(0.0, Calendar.UtSeconds(2000, 1, 1, 12, 0, 0), 1e-9, "J2000.0 is 2000-01-01 12:00");
            Assert.AreEqual("2026-10-30 18:07:42 UT", MetocReport.FormatUt(ut));
        }

        [Test]
        public void SunEquatorial_MatchesMeeusExample25a()
        {
            // 1992 October 13.0 TD, the low-accuracy method NOAA's equations follow.
            Almanac.SunEquatorial(2448908.5, out double ra, out double dec, out double lambda, out _, out double r);
            Assert.AreEqual(199.90895, lambda, 2e-4, "apparent longitude");
            Assert.AreEqual(198.38083, ra, 2e-4, "right ascension");
            Assert.AreEqual(-7.78507, dec, 2e-4, "declination");
            Assert.AreEqual(0.99766, r, 1e-5, "distance, AU");
        }

        [Test]
        public void EquationOfTime_MatchesMeeusExample28b()
        {
            // 1992 October 13.0: E = +13 min 42.7 s.
            Almanac.SunEquatorial(2448908.5, out _, out _, out _, out double eot, out _);
            Assert.AreEqual(13.0 + 42.7 / 60.0, eot, 0.02);
        }

        [Test]
        public void MeanSiderealTime_MatchesMeeusExample12a()
        {
            // 1987 April 10, 0h UT: 13h 10m 46.3668s.
            Assert.AreEqual(197.693195, Almanac.GreenwichMeanSiderealDeg(2446895.5), 1e-5);
        }

        [Test]
        public void Horizon_MatchesMeeusExample13b()
        {
            // Venus from the US Naval Observatory, 1987 April 10, 19:21 UT: h = 15.1249°, A = 68.0337° from the south.
            Enu dir = Almanac.Horizon(-6.719892, 64.352133, 38.921389);
            double elevation = System.Math.Asin(dir.Up) / Almanac.Deg;
            double azimuth = Almanac.Normalize360(System.Math.Atan2(dir.East, dir.North) / Almanac.Deg);
            Assert.AreEqual(15.1249, elevation, 1e-3);
            Assert.AreEqual(68.0337 + 180.0, azimuth, 1e-3, "azimuth from the north");
        }

        [Test]
        public void Moon_WithinTheLowPrecisionAccuracy_OfMeeusExample47a()
        {
            // 1992 April 12, 0h TD, from the full theory: λ = 133.162655°, β = −3.229126°, Δ = 368 409.7 km.
            Almanac.MoonEcliptic(2448724.5, out double lambda, out double beta, out double parallax);
            double distance = Almanac.EarthRadiusKm / System.Math.Sin(parallax * Almanac.Deg);
            Debug.Log($"[METOC] Moon 1992-04-12: λ {lambda:F4}° (Δ {lambda - 133.162655:+0.000;-0.000}), β {beta:F4}° (Δ {beta + 3.229126:+0.000;-0.000}), {distance:F0} km");
            Assert.AreEqual(133.162655, lambda, 0.3, "the Almanac claims 0.3° in longitude");
            Assert.AreEqual(-3.229126, beta, 0.2, "and 0.2° in latitude");
            Assert.AreEqual(368409.7, distance, 368409.7 * 0.01);
        }

        [Test]
        public void MoonPhase_MatchesMeeusExample48a()
        {
            // 1992 April 12, 0h TD: k = 0.6786, waxing (first quarter was 1992 April 10).
            double ut = (2448724.5 - Calendar.J2000) * Calendar.SecondsPerDay;
            var sun = Almanac.Sun(ut, 0, 0);
            var moon = Almanac.Moon(ut, 0, 0, sun);
            Assert.AreEqual(0.6786, moon.IlluminatedFraction, 0.01);
            Assert.IsTrue(moon.Waxing);
        }

        [Test]
        public void MoonDisc_IsAboutHalfADegree_AtTheMeanDistance()
        {
            // 2 · atan(1 737.4 / 384 400) = 0.51793°.
            Assert.AreEqual(0.51793, Almanac.MoonAngularDiameterDeg(384400.0), 1e-5);
        }

        [TestCase(2026, 6, 21)]
        [TestCase(2026, 12, 21)]
        [TestCase(2026, 3, 20)]
        public void SunAtLocalNoon_StandsAt90MinusLatitudeMinusDeclination_DueSouth(int year, int month, int day)
        {
            const double lat = 51.4769, lon = 0.0;
            double midnight = Calendar.UtSeconds(year, month, day, 0, 0, 0);
            Almanac.SunEquatorial(Calendar.JulianDateFromUt(midnight + 43200), out _, out _, out _, out double eot, out _);
            double noon = midnight + (720.0 - eot - 4.0 * lon) * 60.0;
            var sun = Almanac.Sun(noon, lat, lon);
            Assert.AreEqual(90.0 - (lat - sun.DeclinationDeg), sun.Sky.TrueElevationDeg, 0.01);
            Assert.AreEqual(180.0, sun.Sky.AzimuthDeg, 0.1);
            Assert.Greater(sun.Sky.ElevationDeg, sun.Sky.TrueElevationDeg, "refraction lifts the sun");
        }

        [Test]
        public void HighArctic_HasPolarNightInDecember_AndMidnightSunInJune()
        {
            double minJune = double.MaxValue, maxDecember = double.MinValue;
            for (int m = 0; m < 1440; m += 10)
            {
                minJune = System.Math.Min(minJune, Almanac.Sun(Calendar.UtSeconds(2026, 6, 21, 0, 0, 0) + m * 60, 80, 15).Sky.ElevationDeg);
                maxDecember = System.Math.Max(maxDecember, Almanac.Sun(Calendar.UtSeconds(2026, 12, 21, 0, 0, 0) + m * 60, 80, 15).Sky.ElevationDeg);
            }
            Assert.Greater(minJune, 0, "midnight sun");
            Assert.Less(maxDecember, 0, "polar night");
        }

        [Test]
        public void Refraction_IsAboutHalfADegreeAtTheHorizon_AndZeroOverhead()
        {
            Assert.AreEqual(1735.0 / 3600.0, Almanac.RefractionDeg(0), 1e-9);
            Assert.AreEqual(0.0, Almanac.RefractionDeg(89), 1e-12);
            Assert.Less(Almanac.RefractionDeg(45), 1.0 / 60.0, "about one arcminute at 45°");
        }
    }
}
