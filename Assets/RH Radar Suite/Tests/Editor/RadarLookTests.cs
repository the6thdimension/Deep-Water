using System;
using NUnit.Framework;
using RHRadarSuite.Detection;
using RHRadarSuite.Tracking;

namespace RHRadarSuite.Tests
{
    /// <summary>
    /// ROADMAP Phase 2: aspect RCS, the searched volume, and one look end to end, including the Phase 2 test case:
    /// the detection range the equations predict against the one seeded looks produce, and the horizon cut-off.
    /// </summary>
    public class RadarLookTests
    {
        static RadarTerms Generic() => new RadarTerms
        {
            PeakPowerW = 1e6, GainDbi = 40.0, FrequencyHz = 3e9, BandwidthHz = 1e6, NoiseFigureDb = 3.0, LossesDb = 6.0, PulsesIntegrated = 1,
        };

        static MeasurementTerms Beam() => new MeasurementTerms { AzimuthBeamwidthDeg = 1.5, ElevationBeamwidthDeg = 3.0, MonopulseSlope = 1.6 };

        static readonly Coverage Everywhere = Coverage.AllRound(-90.0, 90.0);
        const double Pfa = 1e-6;
        static readonly Plot Mast = new Plot(0, 20, 0);

        [Test]
        public void AspectRcs_InterpolatesInDb_ByTheAngleOffTheNose()
        {
            var rcs = new AspectRcs { NoseDbsm = 0.0, BeamDbsm = 10.0, TailDbsm = 3.0 };
            Assert.AreEqual(0.0, rcs.AtDbsm(0.0));
            Assert.AreEqual(5.0, rcs.AtDbsm(45.0), 1e-12);
            Assert.AreEqual(10.0, rcs.AtDbsm(-90.0), 1e-12, "either side");
            Assert.AreEqual(6.5, rcs.AtDbsm(135.0), 1e-12);
            Assert.AreEqual(3.0, rcs.AtDbsm(200.0), 1e-12, "past the tail is the tail");

            var target = new Plot(0, 0, 0);
            Assert.AreEqual(0.0, AspectRcs.OffNoseDeg(new Plot(0, 0, 1000), target, 0, 0, 1), 1e-9, "nose on");
            Assert.AreEqual(90.0, AspectRcs.OffNoseDeg(new Plot(1000, 0, 0), target, 0, 0, 1), 1e-9, "beam on");
            Assert.AreEqual(180.0, AspectRcs.OffNoseDeg(new Plot(0, 0, -5), target, 0, 0, 2), 1e-9, "tail on");
        }

        [Test]
        public void Coverage_HoldsItsElevationBand_AndItsSector_AcrossNorth()
        {
            var c = new Coverage { MinElevationDeg = 0.0, MaxElevationDeg = 30.0, AzimuthCentreDeg = 350.0, AzimuthHalfWidthDeg = 20.0 };
            var radar = new Plot(0, 0, 0);
            Assert.IsTrue(c.Contains(radar, new Plot(Math.Sin(5 * Math.PI / 180) * 1000, 100, Math.Cos(5 * Math.PI / 180) * 1000)), "5° is 15° from 350°");
            Assert.IsFalse(c.Contains(radar, new Plot(1000, 100, 0)), "east is outside");
            Assert.IsFalse(c.Contains(radar, new Plot(0, 1000, 1000)), "45° up is above the band");
            Assert.IsFalse(c.Contains(radar, new Plot(0, -10, 1000)), "below the horizontal");
        }

        [Test]
        public void BeyondTheHorizon_NothingIsSeen_AndNoRandomNumberIsSpent()
        {
            // A 5 m target 30 km from a 20 m mast: the 4/3-Earth horizon is 27.6 km. Its SNR would be huge.
            var rng = new Random(11);
            LookResult look = RadarLook.Look(Generic(), Beam(), Everywhere, null, Pfa, Mast, new Plot(0, 5, 30000), 30.0, rng);
            Assert.IsFalse(look.AboveHorizon);
            Assert.IsFalse(look.Detected);
            Assert.AreEqual(new Random(11).NextDouble(), rng.NextDouble(), "the generator was not touched");

            LookResult inside = RadarLook.Look(Generic(), Beam(), Everywhere, null, Pfa, Mast, new Plot(0, 5, 25000), 30.0, new Random(11));
            Assert.IsTrue(inside.AboveHorizon && inside.Detected, $"at 25 km it is seen (Pd {inside.Pd:0.000})");
        }

        [Test]
        public void SeededLooks_DetectAtThePredictedRanges()
        {
            // The Phase 2 test case. Swerling 1 inverts exactly: SNR = ln Pfa / ln Pd − 1. Predict the range for Pd 0.5 and
            // Pd 0.9 from the radar equation, then look 4,000 times at each with seeded draws.
            RadarTerms r = Generic();
            foreach (double pd in new[] { 0.5, 0.9 })
            {
                double snrDb = RadarEquation.ToDb(Math.Log(Pfa) / Math.Log(pd) - 1.0);
                double range = RadarEquation.RangeForSnrM(r, 0.0, snrDb);
                double h = 3000.0, ground = Math.Sqrt(range * range - (h - 20.0) * (h - 20.0));
                var target = new Plot(0, h, ground);
                var rng = new Random(2026);
                int seen = 0;
                const int n = 4000;
                for (int i = 0; i < n; i++)
                    if (RadarLook.Look(r, Beam(), Everywhere, null, Pfa, Mast, target, 0.0, rng).Detected) seen++;
                Assert.AreEqual(pd, (double)seen / n, pd == 0.5 ? 0.03 : 0.02, $"Pd {pd}: predicted {range / 1000:0.0} km at {snrDb:0.00} dB, seen {seen} of {n}");
            }
        }

        [Test]
        public void SeaClutter_HidesASeaSkimmer_ThatClearAirWouldShow()
        {
            var sea = new SurfaceTerms { GammaDb = -20.0 };   // a test value, not a measured sea
            var skimmer = new Plot(0, 5, 10000);
            LookResult clear = RadarLook.Look(Generic(), Beam(), Everywhere, null, Pfa, Mast, skimmer, 0.0, new Random(3));
            LookResult cluttered = RadarLook.Look(Generic(), Beam(), Everywhere, sea, Pfa, Mast, skimmer, 0.0, new Random(3));
            Assert.Greater(clear.Pd, 0.99, "62 dB of SNR");
            Assert.Less(cluttered.Pd, 0.02, $"SCNR {cluttered.ScnrDb:0.0} dB");
        }

        [Test]
        public void ClutterImprovement_ComesOffTheClutter_NotTheTarget()
        {
            var sea = new SurfaceTerms { GammaDb = -20.0 };   // a test value, not a measured sea
            var skimmer = new Plot(0, 5, 10000);
            RadarTerms processed = Generic();
            processed.ClutterImprovementDb = 30.0;
            LookResult raw = RadarLook.Look(Generic(), Beam(), Everywhere, sea, Pfa, Mast, skimmer, 0.0, new Random(3));
            LookResult mti = RadarLook.Look(processed, Beam(), Everywhere, sea, Pfa, Mast, skimmer, 0.0, new Random(3));
            Assert.AreEqual(raw.SnrDb, mti.SnrDb, 0.0, "the target is untouched");
            Assert.AreEqual(raw.CnrDb - 30.0, mti.CnrDb, 1e-9);
            Assert.Greater(mti.Pd, 0.9, $"SCNR {mti.ScnrDb:0.0} dB once 30 dB of clutter is rejected");
        }
    }
}
