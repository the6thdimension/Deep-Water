using System;
using NUnit.Framework;
using RHRadarSuite.Detection;
using RHRadarSuite.Tracking;

namespace RHRadarSuite.Tests
{
    /// <summary>
    /// ROADMAP Phase 2, measurement-frame errors (Barton): the sigmas against hand-computed cases, the drawn plots against
    /// their sigmas, and the whole pure chain at once: the radar equation, Swerling 1 detection, noisy plots, the track file.
    /// </summary>
    public class MeasurementErrorTests
    {
        static RadarTerms Generic() => new RadarTerms
        {
            PeakPowerW = 1e6, GainDbi = 40.0, FrequencyHz = 3e9, BandwidthHz = 1e6, NoiseFigureDb = 3.0, LossesDb = 6.0, PulsesIntegrated = 1,
        };

        static MeasurementTerms Beam(double floorM = 0.0, double floorDeg = 0.0) => new MeasurementTerms
        {
            AzimuthBeamwidthDeg = 1.5, ElevationBeamwidthDeg = 3.0, MonopulseSlope = 1.6, RangeFloorM = floorM, AngleFloorDeg = floorDeg,
        };

        [Test]
        public void Sigmas_MatchTheHandComputedCase()
        {
            // B = 1 MHz: c/2B = 149.896 m; SNR 20 dB = 100, √200 = 14.142: σ_R = 10.599 m; σ_az = 1.5° / (1.6 × 14.142) = 0.06629°.
            Assert.AreEqual(10.5992, MeasurementErrors.RangeSigmaM(Generic(), Beam(), 20.0), 1e-4);
            Assert.AreEqual(0.066291, MeasurementErrors.AzimuthSigmaDeg(Beam(), 20.0), 1e-6);
            Assert.AreEqual(2.0 * 0.066291, MeasurementErrors.ElevationSigmaDeg(Beam(), 20.0), 2e-6, "twice the beamwidth, twice the error");
            Assert.AreEqual(10.0, MeasurementErrors.RangeSigmaM(Generic(), Beam(), 13.0) / MeasurementErrors.RangeSigmaM(Generic(), Beam(), 33.0), 1e-9, "20 dB more SNR: a tenth of the error");
        }

        [Test]
        public void RangeRate_MatchesTheHandComputedCase_AndScatters()
        {
            // λ = 0.09993 m, a 10 ms dwell, SNR 20 dB: σ = 0.09993 / (2 × 0.01 × 14.142) = 0.353309 m/s.
            MeasurementTerms m = Beam();
            m.DwellS = 0.01;
            Assert.AreEqual(0.353309, MeasurementErrors.RangeRateSigmaMps(Generic(), m, 20.0), 1e-5);
            m.RangeRateFloorMps = 1.0;
            Assert.AreEqual(1.0, MeasurementErrors.RangeRateSigmaMps(Generic(), m, 90.0), 1e-4, "the floor holds");
            m.RangeRateFloorMps = 0.0;
            var rng = new Random(5);
            double s2 = 0.0;
            for (int i = 0; i < 20000; i++) { double e = MeasurementErrors.MeasureRangeRate(Generic(), m, -250.0, 20.0, rng) + 250.0; s2 += e * e; }
            Assert.AreEqual(0.353309, Math.Sqrt(s2 / 20000), 0.03 * 0.353309, "20,000 draws within 3 %");
            m.DwellS = 0.0;
            Assert.Throws<ArgumentOutOfRangeException>(() => MeasurementErrors.RangeRateSigmaMps(Generic(), m, 20.0), "no dwell, no Doppler");
        }

        [Test]
        public void TheFloor_AddsInQuadrature_AndHoldsAtHighSnr()
        {
            Assert.AreEqual(Math.Sqrt(10.5992 * 10.5992 + 25.0), MeasurementErrors.RangeSigmaM(Generic(), Beam(floorM: 5.0), 20.0), 1e-3);
            Assert.AreEqual(5.0, MeasurementErrors.RangeSigmaM(Generic(), Beam(floorM: 5.0), 90.0), 1e-3, "a strong target is never better than the floor");
            Assert.AreEqual(0.01, MeasurementErrors.AzimuthSigmaDeg(Beam(floorDeg: 0.01), 90.0), 1e-5);
        }

        [Test]
        public void DrawnPlots_ScatterWithTheirSigmas()
        {
            RadarTerms r = Generic();
            MeasurementTerms m = Beam();
            var radar = new Plot(0, 20, 0);
            // 50 km out at azimuth 30°, elevation 2°.
            double az0 = 30.0 * Math.PI / 180.0, el0 = 2.0 * Math.PI / 180.0, r0 = 50000.0;
            var target = new Plot(r0 * Math.Cos(el0) * Math.Sin(az0), 20 + r0 * Math.Sin(el0), r0 * Math.Cos(el0) * Math.Cos(az0));
            var rng = new Random(424242);
            const int n = 20000;
            double sR = 0, sR2 = 0, sA = 0, sA2 = 0, sE2 = 0;
            for (int i = 0; i < n; i++)
            {
                Plot p = MeasurementErrors.Measure(r, m, radar, target, 20.0, rng);
                double dx = p.X, dy = p.Y - 20, dz = p.Z, g = Math.Sqrt(dx * dx + dz * dz);
                double eR = Math.Sqrt(g * g + dy * dy) - r0;
                double eA = (Math.Atan2(dx, dz) - az0) * 180.0 / Math.PI, eE = (Math.Atan2(dy, g) - el0) * 180.0 / Math.PI;
                sR += eR; sR2 += eR * eR; sA += eA; sA2 += eA * eA; sE2 += eE * eE;
            }
            Assert.AreEqual(0.0, sR / n, 0.3, "unbiased in range");
            Assert.AreEqual(0.0, sA / n, 0.002, "unbiased in azimuth");
            Assert.AreEqual(10.5992, Math.Sqrt(sR2 / n), 0.03 * 10.5992, "range sigma within 3 %");
            Assert.AreEqual(0.066291, Math.Sqrt(sA2 / n), 0.03 * 0.066291, "azimuth sigma within 3 %");
            Assert.AreEqual(0.132582, Math.Sqrt(sE2 / n), 0.03 * 0.132582, "elevation sigma within 3 %");
        }

        [Test]
        public void ThePureChain_EquationToDetectionToPlotsToATrack()
        {
            // A 0 dBsm jet at 3 km altitude, 60 km north, closing at 250 m/s; the generic radar on a 20 m mast looks every
            // 2 s. Each look: SNR from the radar equation, a Swerling 1 detection draw at Pfa 1e-6, a noisy plot, the track file.
            RadarTerms r = Generic();
            MeasurementTerms m = Beam(floorM: 5.0, floorDeg: 0.01);
            var radar = new Plot(0, 20, 0);
            var file = new TrackFile(TrackFileSettings.Textbook(measurementSigmaM: 30.0, maxSpeedMps: 700.0, maxAccelMps2: 60.0));
            var rng = new Random(1987);
            var plots = new Plot[1];
            int detections = 0;
            const double look = 2.0;
            Plot truth = default;
            for (int k = 0; k < 30; k++)
            {
                double t = (k + 1) * look;
                truth = new Plot(0, 3000, 60000 - 250.0 * t);
                double dx = truth.X - radar.X, dy = truth.Y - radar.Y, dz = truth.Z - radar.Z;
                double snr = RadarEquation.SnrDb(r, 0.0, Math.Sqrt(dx * dx + dy * dy + dz * dz));
                bool seen = rng.NextDouble() < RadarEquation.PdSwerling1(snr, 1e-6);
                if (seen)
                {
                    detections++;
                    plots[0] = MeasurementErrors.Measure(r, m, radar, truth, snr, rng);
                }
                file.Scan(t, seen ? plots : Array.Empty<Plot>());
            }
            Assert.Greater(detections, 26, "Pd ≈ 0.99 at 31 dB: nearly every look");
            Assert.AreEqual(1, file.Tracks.Count);
            RadarTrack track = file.Tracks[0];
            Assert.AreNotEqual(TrackState.Tentative, track.State);
            Plot at = track.PredictAt(30 * look);
            double err = Math.Sqrt(Math.Pow(at.X - truth.X, 2) + Math.Pow(at.Y - truth.Y, 2) + Math.Pow(at.Z - truth.Z, 2));
            Assert.Less(err, 60.0, "within 60 m of the truth at 45 km (cross-range sigma ~22 m, range ~6 m)");
            Assert.AreEqual(-250.0, track.VelocityZ, 15.0, "closing at 250 m/s");
        }
    }
}
