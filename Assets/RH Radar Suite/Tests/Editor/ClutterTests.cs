using NUnit.Framework;
using RHRadarSuite.Detection;

namespace RHRadarSuite.Tests
{
    /// <summary>ROADMAP Phase 4, the coarse surface-clutter closed form, against cases worked by hand in double precision.</summary>
    public class ClutterTests
    {
        static RadarTerms Generic() => new RadarTerms
        {
            PeakPowerW = 1e6, GainDbi = 40.0, FrequencyHz = 3e9, BandwidthHz = 1e6, NoiseFigureDb = 3.0, LossesDb = 6.0, PulsesIntegrated = 1,
        };

        static MeasurementTerms Beam(double az = 1.5, double el = 3.0) => new MeasurementTerms
        {
            AzimuthBeamwidthDeg = az, ElevationBeamwidthDeg = el, MonopulseSlope = 1.6,
        };

        static readonly SurfaceTerms Sea = new SurfaceTerms { GammaDb = -20.0 };   // a test value, not a measured sea

        [Test]
        public void Grazing_FollowsTheFourThirdsEarth_AndEndsAtTheHorizon()
        {
            Assert.AreEqual(0.0014113954, Clutter.SinGrazing(20.0, 10000.0), 1e-9, "20 m mast, 10 km: 0.002 flat, less over the curve");
            double horizon = RadarEquation.HorizonM(20.0, 0.0);
            Assert.AreEqual(0.0, Clutter.SinGrazing(20.0, horizon * 1.0001), 0.0, "beyond the horizon: no surface in the beam");
            Assert.Less(Clutter.SinGrazing(20.0, horizon * 0.999), 1e-5, "and it fades to zero there");
        }

        [Test]
        public void TheCell_IsTheSmallerOfPulseAndBeamLimited()
        {
            // 20 m mast at 10 km: pulse-limited R·θ_az·(c/2B)·sec ψ = 39,243 m² (beam-limited would be 7.6e7).
            Assert.AreEqual(39242.78, Clutter.CellAreaM2(Generic(), Beam(), 20.0, 10000.0), 0.01);
            // Looking down from 3 km at 4 km: the two limits nearly meet; the beam-limited 22,975 m² is the smaller.
            Assert.AreEqual(22974.82, Clutter.CellAreaM2(Generic(), Beam(), 3000.0, 4000.0), 0.01);
            // A 0.2° pencil beam from there: beam-limited, 204.2 m².
            Assert.AreEqual(204.2207, Clutter.CellAreaM2(Generic(), Beam(0.2, 0.2), 3000.0, 4000.0), 1e-3);
            Assert.AreEqual(0.0, Clutter.CellAreaM2(Generic(), Beam(), 20.0, 30000.0), "beyond the horizon");
        }

        [Test]
        public void ClutterBuriesASeaSkimmer_AndScnrIsTheTargetOverTheCell()
        {
            // γ −20 dB: σ⁰ = 1.41e-5, cell 0.554 m² (−2.566 dBsm), CNR 59.43 dB at 10 km; a 0 dBsm target has 61.99 dB SNR
            // but only 2.57 dB over clutter plus noise: once clutter dominates, SCNR is the target's RCS over the cell's.
            Assert.AreEqual(-2.565915, Clutter.CellRcsDbsm(Generic(), Beam(), Sea, 20.0, 10000.0), 1e-5);
            double cnr = Clutter.CnrDb(Generic(), Beam(), Sea, 20.0, 10000.0);
            Assert.AreEqual(59.426965, cnr, 1e-5);
            double snr = RadarEquation.SnrDb(Generic(), 0.0, 10000.0);
            Assert.AreEqual(2.565910, Clutter.ScnrDb(snr, cnr), 1e-5);
            Assert.AreEqual(snr, Clutter.ScnrDb(snr, Clutter.CnrDb(Generic(), Beam(), Sea, 20.0, 30000.0)), 0.0, "no clutter past the horizon");
        }

        [Test]
        public void BartonSeaGamma_AndTheBeaufortScale()
        {
            // γ = −64 + 6·K_B − 10·log λ (λ in m): Beaufort 4 at 3 GHz (λ 0.09993 m) is −64 + 24 + 10.003 = −29.997 dB (by hand).
            Assert.AreEqual(-29.997, Clutter.SeaGammaDb(4.0, RadarEquation.WavelengthM(3e9)), 1e-3);
            Assert.AreEqual(6.0, Clutter.SeaGammaDb(5.0, 0.1) - Clutter.SeaGammaDb(4.0, 0.1), 1e-12, "6 dB per Beaufort force");
            Assert.AreEqual(10.0, Clutter.SeaGammaDb(4.0, 0.01) - Clutter.SeaGammaDb(4.0, 0.1), 1e-12, "10 dB more per decade shorter wavelength");
            // v = 0.836·B^1.5: force 4 blows at 6.688 m/s.
            Assert.AreEqual(4.0, Clutter.BeaufortFromWindSpeed(0.836 * 8.0), 1e-12);
            Assert.AreEqual(0.0, Clutter.BeaufortFromWindSpeed(0.0));
            Assert.AreEqual(12.0, Clutter.BeaufortFromWindSpeed(60.0), "held to the scale");
        }

        [Test]
        public void LookingDown_CostsAnAirborneRadarMoreThanTwentyDb()
        {
            double mast = Clutter.CnrDb(Generic(), Beam(), Sea, 20.0, 4000.0);
            double air = Clutter.CnrDb(Generic(), Beam(), Sea, 3000.0, 4000.0);
            Assert.Greater(air - mast, 20.0, $"look-down penalty: {air - mast:0.0} dB (σ⁰ rises with sin ψ)");
        }
    }
}
