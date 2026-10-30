using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

namespace DeepWater.Metoc.Tests
{
    /// <summary>The reckoned core: determinism, the order log and its replay, the clock, and the cost per tick (E47).</summary>
    public class MetocSimulationTests
    {
        const double Step = 0.02;
        static MetocScenario Demo(double daySpeed = 600) =>
            new MetocScenario(Calendar.UtSeconds(2026, 10, 30, 18, 0, 0), new MetocPlace(30, -40), daySpeed);

        [Test]
        public void TwoRuns_OfOneScenario_AreBitIdentical()
        {
            var a = new MetocSimulation(Demo(), new IlluminationProfile());
            var b = new MetocSimulation(Demo(), new IlluminationProfile());
            for (int i = 0; i < 3600; i++)
            {
                a.Step(Step);
                b.Step(Step);
                Assert.AreEqual(a.Report.Fingerprint(), b.Report.Fingerprint(), $"tick {a.Tick}");
            }
        }

        [Test]
        public void ReplayingTheOrderLog_ReproducesEveryReport()
        {
            var live = new MetocSimulation(Demo(), new IlluminationProfile());
            var prints = new List<ulong>();
            for (int i = 0; i < 2000; i++)
            {
                if (i == 100) live.Issue(MetocOrder.DaySpeed(60, "test"));
                if (i == 700) live.Issue(MetocOrder.JumpTo(Calendar.UtSeconds(2026, 11, 2, 3, 0, 0), "test"));
                if (i == 1200) live.Issue(MetocOrder.Place(-33.9, 151.2, "test"));
                live.Step(Step);
                prints.Add(live.Report.Fingerprint());
            }
            Assert.AreEqual(3, live.Log.Count);

            var replay = new MetocSimulation(Demo(), new IlluminationProfile());
            int next = 0;
            for (int i = 0; i < 2000; i++)
            {
                while (next < live.Log.Count && live.Log[next].Tick == replay.Tick) replay.Issue(live.Log[next++].Order);
                replay.Step(Step);
                Assert.AreEqual(prints[i], replay.Report.Fingerprint(), $"tick {replay.Tick}");
            }
        }

        /// <summary>The smallest step a double can take at this magnitude.</summary>
        static double Ulp(double x) => BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(x) + 1) - x;

        [Test]
        public void Clock_AdvancesByTheStepTimesTheDaySpeed()
        {
            var sim = new MetocSimulation(Demo(daySpeed: 60), new IlluminationProfile());
            double start = sim.Report.UtSeconds;
            for (int i = 0; i < 500; i++) sim.Step(Step);
            // Exact to one rounding of a UT near 8.5e8 s (about 0.1 µs); adding each step instead lost 24 µs here.
            Assert.AreEqual(500 * Step * 60, sim.Report.UtSeconds - start, Ulp(start));
            Assert.AreEqual(500, sim.Tick);
        }

        [Test]
        public void Clock_DoesNotDrift_OverAGameDayOfFiftyHertzSteps()
        {
            double start = Calendar.UtSeconds(2026, 10, 30, 0, 0, 0);
            var clock = new MetocClock(start, 1);
            const int steps = 86400 * 50;
            for (int i = 0; i < steps; i++) clock.Advance(Step);
            UnityEngine.Debug.Log($"[METOC] Clock after {steps} steps of {Step} s: error {(clock.UtSeconds - start - 86400.0) * 1e6:F3} µs (one ulp {Ulp(start) * 1e6:F3} µs)");
            Assert.AreEqual(86400.0, clock.UtSeconds - start, 2 * Ulp(start));
            clock.SetDaySpeed(60);
            clock.Advance(Step);
            Assert.AreEqual(86400.0 + Step * 60, clock.UtSeconds - start, 2 * Ulp(start), "a new day speed re-anchors without a jump");
        }

        [Test]
        public void BadOrders_AreRefusedWithAReason_AndChangeNothing()
        {
            var sim = new MetocSimulation(Demo(daySpeed: 1), new IlluminationProfile());
            sim.Issue(MetocOrder.DaySpeed(double.NaN, "test"));
            sim.Issue(MetocOrder.Place(95, 0, "test"));
            sim.Issue(MetocOrder.JumpTo(double.PositiveInfinity, "test"));
            sim.Step(Step);
            Assert.AreEqual(3, sim.Log.Count);
            foreach (var record in sim.Log) Assert.IsNotNull(record.Refused, record.ToString());
            Assert.AreEqual(1, sim.DaySpeed);
            Assert.AreEqual(30, sim.Report.Place.LatitudeDeg);
        }

        [Test]
        public void ABadScenario_IsRefusedAtTheBoundary()
        {
            Assert.Throws<ArgumentException>(() => new MetocSimulation(new MetocScenario(0, new MetocPlace(100, 0), 1), new IlluminationProfile()));
            Assert.Throws<ArgumentException>(() => new MetocSimulation(new MetocScenario(0, new MetocPlace(0, 0), -1), new IlluminationProfile()));
            var sim = new MetocSimulation(Demo(), new IlluminationProfile());
            Assert.Throws<ArgumentOutOfRangeException>(() => sim.Step(0));
        }

        [Test]
        public void ADay_GoesFromDaylightToNight()
        {
            // 30° N, 40° W on 2026-10-30: local solar noon is near 14:24 UT, midnight near 02:24 UT.
            var sim = new MetocSimulation(new MetocScenario(Calendar.UtSeconds(2026, 10, 30, 14, 24, 0), new MetocPlace(30, -40), 1), new IlluminationProfile());
            double noonLux = sim.Report.Light.TotalLux, noonSun = sim.Report.Sun.Sky.ElevationDeg;
            sim.Issue(MetocOrder.JumpTo(Calendar.UtSeconds(2026, 10, 31, 2, 24, 0), "test"));
            sim.Step(Step);
            double midnightLux = sim.Report.Light.TotalLux;
            UnityEngine.Debug.Log($"[METOC] 30°N 40°W 2026-10-30: noon sun {noonSun:F1}°, {noonLux:F0} lux; midnight {midnightLux:F3} lux (moon {sim.Report.Moon.Sky.ElevationDeg:F1}°, {sim.Report.Moon.IlluminatedFraction:P0} lit)");
            Assert.Greater(noonLux, 50000);
            Assert.Less(midnightLux, 1);
        }

        [Test]
        public void CostPerTick_IsFarInsideE4()
        {
            var sim = new MetocSimulation(Demo(), new IlluminationProfile());
            for (int i = 0; i < 2000; i++) sim.Step(Step);
            const int n = 100000;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < n; i++) sim.Step(Step);
            sw.Stop();
            double us = sw.Elapsed.TotalMilliseconds * 1000.0 / n;
            UnityEngine.Debug.Log($"[METOC] Step cost: {us:F2} µs per tick over {n} ticks (Editor, Mono); E4's CPU budget is 500 µs");
            Assert.Less(us, 50.0);
        }
    }
}
