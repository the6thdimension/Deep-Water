using NUnit.Framework;
using RHRadarSuite.Detection;

namespace RHRadarSuite.Tests
{
    /// <summary>
    /// ROADMAP Phase 2, the radar-equation core: against hand-computed cases (worked separately in double precision
    /// from the same textbook formulas) and Albersheim's published reference point.
    /// </summary>
    public class RadarEquationTests
    {
        /// <summary>1 MW, 40 dBi, 3 GHz, 1 MHz, NF 3 dB, losses 6 dB, one pulse: a generic search radar for the hand cases.</summary>
        static RadarTerms Generic() => new RadarTerms
        {
            PeakPowerW = 1e6, GainDbi = 40.0, FrequencyHz = 3e9, BandwidthHz = 1e6, NoiseFigureDb = 3.0, LossesDb = 6.0, PulsesIntegrated = 1,
        };

        [Test]
        public void Snr_MatchesTheHandComputedCase()
        {
            // λ = 0.0999 m; a 0 dBsm target at 100 km: SNR = 158.23 (21.99 dB), worked by hand.
            Assert.AreEqual(21.993, RadarEquation.SnrDb(Generic(), 0.0, 100e3), 0.005);
        }

        [Test]
        public void Snr_FallsFortyDbPerDecade_AndRisesWithRcsAndIntegration()
        {
            RadarTerms r = Generic();
            Assert.AreEqual(40.0, RadarEquation.SnrDb(r, 0.0, 10e3) - RadarEquation.SnrDb(r, 0.0, 100e3), 1e-9, "R⁴");
            Assert.AreEqual(10.0, RadarEquation.SnrDb(r, 10.0, 50e3) - RadarEquation.SnrDb(r, 0.0, 50e3), 1e-9, "dBsm adds");
            r.PulsesIntegrated = 10;
            Assert.AreEqual(10.0, RadarEquation.SnrDb(r, 0.0, 50e3) - RadarEquation.SnrDb(Generic(), 0.0, 50e3), 1e-9, "10 pulses coherent: +10 dB");
        }

        [Test]
        public void RangeForSnr_InvertsTheEquation()
        {
            RadarTerms r = Generic();
            double range = RadarEquation.RangeForSnrM(r, 0.0, 13.0);
            Assert.AreEqual(167811.6, range, 1.0, "worked by hand: 167.81 km for 13 dB");
            Assert.AreEqual(13.0, RadarEquation.SnrDb(r, 0.0, range), 1e-9);
        }

        [Test]
        public void Albersheim_GivesThePublishedReferencePoint()
        {
            // Pd 0.9, Pfa 1e-6, one pulse: 13.1 dB (Albersheim; the figure Skolnik quotes).
            Assert.AreEqual(13.11, RadarEquation.RequiredSnrDb(0.9, 1e-6, 1), 0.01);
            Assert.AreEqual(4.99, RadarEquation.RequiredSnrDb(0.9, 1e-6, 10), 0.01, "ten pulses need 8 dB less");
        }

        [Test]
        public void Swerling1Inverse_AndTheCalibrationSolve()
        {
            // Pd 0.5 at Pfa 1e-6: 1 + SNR = ln 1e-6 / ln 0.5 = 19.9316, so SNR = 18.9316 = 12.7719 dB (by hand).
            Assert.AreEqual(12.7719, RadarEquation.RequiredSnrSwerling1Db(0.5, 1e-6), 1e-4);
            Assert.AreEqual(0.9, RadarEquation.PdSwerling1(RadarEquation.RequiredSnrSwerling1Db(0.9, 1e-6), 1e-6), 1e-12, "the inverse");
            // Solve the power that puts 12.77 dB on a 0 dBsm target at 324.1 km: the equation then passes through that point.
            RadarTerms r = Generic();
            r.PeakPowerW = RadarEquation.PeakPowerForSnrW(r, 0.0, 324_100.0, 12.7719);
            Assert.AreEqual(12.7719, RadarEquation.SnrDb(r, 0.0, 324_100.0), 1e-9);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RadarEquation.RequiredSnrSwerling1Db(1e-7, 1e-6), "Pd below Pfa needs no signal");
        }

        [Test]
        public void Pd_SteadyAndSwerling1_AtTheReferenceSnr()
        {
            Assert.AreEqual(0.887, RadarEquation.PdSteady(13.1, 1e-6), 0.005, "North's approximation agrees with Albersheim to within 0.02");
            Assert.AreEqual(0.525, RadarEquation.PdSwerling1(13.1, 1e-6), 0.002, "a fluctuating target needs more: Swerling 1");
            Assert.AreEqual(0.872, RadarEquation.PdSwerling1(20.0, 1e-6), 0.002);
            Assert.Less(RadarEquation.PdSteady(0.0, 1e-6), 1e-3, "nothing at 0 dB");
            Assert.Greater(RadarEquation.PdSteady(20.0, 1e-6), 0.999);
        }

        [Test]
        public void TheHorizon_IsTheFourThirdsEarthDistance()
        {
            Assert.AreEqual(27650.0, RadarEquation.HorizonM(20.0, 5.0), 1.0, "a 20 m mast and a 5 m sea-skimmer: 27.6 km");
            Assert.AreEqual(430614.0, RadarEquation.HorizonM(20.0, 10000.0), 10.0, "and a jet at 10 km: 431 km");
            Assert.AreEqual(RadarEquation.HorizonM(20.0, 0.0), RadarEquation.HorizonM(20.0, -3.0), 1e-9, "below the surface counts as on it");
        }

        [Test]
        public void Erfc_MatchesKnownValues()
        {
            Assert.AreEqual(1.0, RadarEquation.Erfc(0.0), 1e-7);
            Assert.AreEqual(0.157299207, RadarEquation.Erfc(1.0), 2e-7);
            Assert.AreEqual(1.842700793, RadarEquation.Erfc(-1.0), 2e-7);
            Assert.AreEqual(0.004677735, RadarEquation.Erfc(2.0), 2e-8);
        }

        [Test]
        public void NonsenseTermsAreRefused()
        {
            RadarTerms r = Generic();
            r.PeakPowerW = 0.0;
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RadarEquation.SnrDb(r, 0.0, 1000.0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RadarEquation.SnrDb(Generic(), 0.0, 0.0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RadarEquation.RequiredSnrDb(1.0, 1e-6));
        }
    }
}
