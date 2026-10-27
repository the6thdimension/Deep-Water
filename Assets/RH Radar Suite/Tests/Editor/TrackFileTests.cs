using System;
using System.Collections.Generic;
using NUnit.Framework;
using RHRadarSuite.Tracking;

namespace RHRadarSuite.Tests
{
    /// <summary>
    /// ROADMAP Phase 3: the track lifecycle (M of N, coast, drop, reacquire), gated association by prediction rather
    /// than target identity, and the track picture (quality, BestTrack), on scripted and seeded plots.
    /// </summary>
    public class TrackFileTests
    {
        const double Look = 2.0;    // seconds between looks

        static TrackFile NewFile(List<string> log = null)
        {
            var file = new TrackFile(TrackFileSettings.Textbook(measurementSigmaM: 30.0, maxSpeedMps: 700.0, maxAccelMps2: 60.0));
            if (log != null) file.StateChanged += (t, from) => log.Add($"{t.Id}:{from}->{t.State}");
            return file;
        }

        static Plot[] One(double x, double z) => new[] { new Plot(x, 1000.0, z) };
        static readonly Plot[] None = new Plot[0];

        [Test]
        public void TwoOfThree_ConfirmsOnTheSecondHit_AndDropsWhenItCannot()
        {
            var log = new List<string>();
            TrackFile f = NewFile(log);
            f.Scan(1 * Look, One(0, 20000));
            f.Scan(2 * Look, None);
            f.Scan(3 * Look, One(0, 20000 - 500));     // 250 m/s south: hit on the third look
            CollectionAssert.AreEqual(new[] { "1:Tentative->Confirmed" }, log);
            Assert.AreEqual(-125.0, f.Tracks[0].VelocityZ, 1e-9, "two-point initiation over the 4 s between hits");

            log.Clear();
            TrackFile g = NewFile(log);
            g.Scan(1 * Look, One(0, 20000));
            g.Scan(2 * Look, None);
            Assert.AreEqual(1, g.Tracks.Count, "one hit and a miss: two of three still possible");
            g.Scan(3 * Look, None);
            CollectionAssert.AreEqual(new[] { "1:Tentative->Dropped" }, log);
            Assert.AreEqual(0, g.Tracks.Count);
        }

        [Test]
        public void Confirmed_CoastsOnThePrediction_ReacquiresUnderItsId_ThenDrops()
        {
            var log = new List<string>();
            TrackFile f = NewFile(log);
            for (int k = 0; k < 6; k++) f.Scan((k + 1) * Look, One(0, 20000 - 500 * k));
            RadarTrack t = f.Tracks[0];
            Assert.AreEqual(TrackState.Confirmed, t.State);

            f.Scan(7 * Look, None);
            Assert.AreEqual(TrackState.Coasting, t.State);
            Assert.AreEqual(20000 - 500 * 6, t.Position.Z, 1e-6, "coasted to where the target would be");

            f.Scan(8 * Look, One(0, 20000 - 500 * 7));
            Assert.AreEqual(TrackState.Confirmed, t.State);
            Assert.AreEqual(1, f.Tracks.Count, "the same track, not a new one");

            for (int k = 9; k <= 11; k++) f.Scan(k * Look, None);
            CollectionAssert.AreEqual(new[]
            {
                "1:Tentative->Confirmed", "1:Confirmed->Coasting", "1:Coasting->Confirmed", "1:Confirmed->Coasting", "1:Coasting->Dropped",
            }, log);
            Assert.AreEqual(0, f.Tracks.Count);
        }

        [Test]
        public void TwoTargets_KeepTheirTracks_WhenThePlotsArriveInEitherOrder()
        {
            // Two jets 2 km apart on parallel courses with 30 m noise; the plot order flips every look.
            TrackFile f = NewFile();
            var rng = new Random(7);
            var plots = new Plot[2];
            for (int k = 0; k < 60; k++)
            {
                var a = new Plot(Noise(rng), 1000.0, 30000 - 500.0 * k + Noise(rng));
                var b = new Plot(2000 + Noise(rng), 1000.0, 30000 - 500.0 * k + Noise(rng));
                plots[k % 2] = a;
                plots[1 - k % 2] = b;
                f.Scan((k + 1) * Look, plots);
            }
            Assert.AreEqual(2, f.Tracks.Count);
            Assert.AreEqual(1, f.Tracks[0].Id);
            Assert.AreEqual(0.0, f.Tracks[0].Position.X, 100.0, "track 1 stayed on the western jet");
            Assert.AreEqual(2000.0, f.Tracks[1].Position.X, 100.0, "track 2 on the eastern one");
            Assert.AreEqual(-250.0, f.Tracks[0].VelocityZ, 10.0, "250 m/s south, to within 10 m/s");
            Assert.AreEqual(1.0, f.Tracks[0].Quality);
        }

        [Test]
        public void SparseFalseAlarms_NeverConfirm_AndTheRealTargetDoes()
        {
            // One false plot a look, anywhere in a 200 km square, against one real target. (Denser clutter does confirm
            // false tracks under 2 of 3 with a 700 m/s initiation gate: a one-hit gate is ~1.5 km after one look.)
            TrackFile f = NewFile();
            var rng = new Random(20261027);
            var plots = new List<Plot>();
            for (int k = 0; k < 100; k++)
            {
                plots.Clear();
                plots.Add(new Plot(Noise(rng), 1000.0, 40000 - 500.0 * k + Noise(rng)));
                plots.Add(new Plot(rng.NextDouble() * 2e5 - 1e5, 0.0, rng.NextDouble() * 2e5 - 1e5));
                f.Scan((k + 1) * Look, plots);
            }
            int confirmed = 0;
            foreach (RadarTrack t in f.Tracks)
                if (t.State != TrackState.Tentative) confirmed++;
            Assert.AreEqual(1, confirmed, "only the real target");
            Assert.AreEqual(1, f.Tracks[0].Id, "and its track never broke");
        }

        [Test]
        public void SameInputs_SameTrackFile()
        {
            string Run()
            {
                TrackFile f = NewFile();
                var rng = new Random(99);
                var plots = new List<Plot>();
                for (int k = 0; k < 50; k++)
                {
                    plots.Clear();
                    for (int c = 0; c < 4; c++) plots.Add(new Plot(rng.NextDouble() * 2e4, 0.0, rng.NextDouble() * 2e4));
                    f.Scan((k + 1) * Look, plots);
                }
                var sb = new System.Text.StringBuilder();
                foreach (RadarTrack t in f.Tracks) sb.Append($"{t.Id} {t.State} {t.Position.X:R} {t.Position.Z:R};");
                return sb.ToString();
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void BestTrack_PicksTheSector_ThenQuality_ThenRange()
        {
            TrackFile f = NewFile();
            // Three steady targets: north at 20 km, north at 10 km, east at 15 km. The near northern one misses a look.
            for (int k = 0; k < 5; k++)
            {
                var plots = new List<Plot> { new Plot(0, 0, 20000), new Plot(15000, 0, 0) };
                if (k != 4) plots.Add(new Plot(0, 0, 10000));
                f.Scan((k + 1) * Look, plots);
            }
            RadarTrack north = f.BestTrack(0, 0, 0, 10);
            Assert.AreEqual(20000.0, north.Position.Z, 1.0, "both north; the far one has the better quality");
            Assert.AreEqual(15000.0, f.BestTrack(0, 0, 90, 10).Position.X, 1.0, "east");
            Assert.AreEqual(15000.0, f.BestTrack(0, 0, 450, 10).Position.X, 1.0, "bearings wrap");
            Assert.IsNull(f.BestTrack(0, 0, 180, 10), "nothing south");
            for (int k = 5; k < 8; k++) f.Scan((k + 1) * Look, new List<Plot> { new Plot(0, 0, 20000), new Plot(15000, 0, 0), new Plot(0, 0, 10000) });
            Assert.AreEqual(10000.0, f.BestTrack(0, 0, 0, 10).Position.Z, 1.0, "three hits in a row again, equal quality: the nearer");
        }

        [Test]
        public void LastPlot_NamesThePlotEachTrackTook_AndDropRemovesATrackAtOnce()
        {
            var log = new List<string>();
            TrackFile f = NewFile(log);
            f.Scan(1 * Look, new[] { new Plot(0, 1000, 20000), new Plot(5000, 1000, 20000) });
            Assert.AreEqual(0, f.Tracks[0].LastPlot);
            Assert.AreEqual(1, f.Tracks[1].LastPlot);
            f.Scan(2 * Look, new[] { new Plot(5000, 1000, 19500), new Plot(0, 1000, 19500) });   // the other order
            Assert.AreEqual(1, f.Tracks[0].LastPlot, "each track names the plot it took, wherever it sat in the list");
            Assert.AreEqual(0, f.Tracks[1].LastPlot);
            f.Scan(3 * Look, new[] { new Plot(0, 1000, 19000) });
            Assert.AreEqual(0, f.Tracks[0].LastPlot);
            Assert.AreEqual(-1, f.Tracks[1].LastPlot, "a miss");

            log.Clear();
            Assert.IsTrue(f.Drop(1));
            CollectionAssert.AreEqual(new[] { "1:Confirmed->Dropped" }, log);
            Assert.AreEqual(1, f.Tracks.Count);
            Assert.AreEqual(2, f.Tracks[0].Id);
            Assert.IsFalse(f.Drop(1), "already gone");
            // Its gate no longer claims its target's next plot: a new tentative track takes it.
            f.Scan(4 * Look, new[] { new Plot(0, 1000, 18500) });
            Assert.AreEqual(3, f.Tracks[1].Id);
            Assert.AreEqual(TrackState.Tentative, f.Tracks[1].State);
        }

        [Test]
        public void TimeMustMoveForward_AndNonsenseSettingsAreRefused()
        {
            TrackFile f = NewFile();
            f.Scan(1.0, None);
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Scan(1.0, None));
            TrackFileSettings s = TrackFileSettings.Textbook(30, 700, 60);
            s.ConfirmHits = 4;
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrackFile(s), "M > N");
        }

        static double Noise(Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return 30.0 * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
