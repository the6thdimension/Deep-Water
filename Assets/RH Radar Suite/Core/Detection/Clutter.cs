using System;

namespace RHRadarSuite.Detection
{
    /// <summary>
    /// The surface a radar's beam lands on (ROADMAP Phase 4): its reflectivity in the constant-γ model, σ⁰ = γ·sin ψ.
    /// γ depends on the surface, the band and (at sea) the sea state; it comes from the profile or the environment (P1),
    /// never from here.
    /// </summary>
    public struct SurfaceTerms
    {
        /// <summary>The constant-γ reflectivity, dB (negative: a calm sea is a few tens of dB below a rough one).</summary>
        public double GammaDb;
    }

    /// <summary>
    /// Surface clutter, coarse and closed-form (Barton; Skolnik ch. 7), pure C# (P6). One clutter cell at the target's
    /// range, not a map of cells:
    /// <list type="bullet">
    /// <item>The grazing angle over a 4/3-Earth: sin ψ = h/R − R/(2·k·a), zero at and beyond the radar horizon.</item>
    /// <item>The cell: the smaller of pulse-limited R·θ_az·(c/2B)·sec ψ and beam-limited π·R²·θ_az·θ_el/(4·sin ψ).</item>
    /// <item>Its radar cross-section σ⁰·A, which the radar equation turns into a clutter-to-noise ratio like any target.</item>
    /// </list>
    /// What it leaves out: clutter fluctuation and spikes, Doppler (MTI or pulse-Doppler rejection), multipath, and
    /// clutter from beyond the target's own range cell.
    /// </summary>
    public static class Clutter
    {
        /// <summary>Sine of the grazing angle at ground range ≈ slant <paramref name="rangeM"/> for an antenna <paramref name="antennaHeightM"/> up.</summary>
        public static double SinGrazing(double antennaHeightM, double rangeM)
        {
            if (!(rangeM > 0.0)) throw new ArgumentOutOfRangeException(nameof(rangeM), rangeM, "range must be positive");
            double re = RadarEquation.StandardRefraction * RadarEquation.EarthRadiusM;
            double s = Math.Max(0.0, antennaHeightM) / rangeM - rangeM / (2.0 * re);
            return Math.Max(0.0, Math.Min(1.0, s));
        }

        /// <summary>The clutter cell's area, m², at <paramref name="rangeM"/> (0 beyond the horizon).</summary>
        public static double CellAreaM2(in RadarTerms r, in MeasurementTerms m, double antennaHeightM, double rangeM)
        {
            double sinPsi = SinGrazing(antennaHeightM, rangeM);
            if (sinPsi <= 0.0) return 0.0;
            const double rad = Math.PI / 180.0;
            double az = m.AzimuthBeamwidthDeg * rad, el = m.ElevationBeamwidthDeg * rad;
            double cosPsi = Math.Sqrt(1.0 - sinPsi * sinPsi);
            double pulseLimited = cosPsi > 0.0 ? rangeM * az * (RadarEquation.SpeedOfLight / (2.0 * r.BandwidthHz)) / cosPsi : double.PositiveInfinity;
            double beamLimited = Math.PI * rangeM * rangeM * az * el / (4.0 * sinPsi);
            return Math.Min(pulseLimited, beamLimited);
        }

        /// <summary>The clutter cell's radar cross-section σ⁰·A, dBsm; negative infinity beyond the horizon.</summary>
        public static double CellRcsDbsm(in RadarTerms r, in MeasurementTerms m, in SurfaceTerms s, double antennaHeightM, double rangeM)
        {
            double area = CellAreaM2(r, m, antennaHeightM, rangeM);
            if (area <= 0.0) return double.NegativeInfinity;
            double sigma0 = RadarEquation.FromDb(s.GammaDb) * SinGrazing(antennaHeightM, rangeM);
            return RadarEquation.ToDb(sigma0 * area);
        }

        /// <summary>Clutter-to-noise ratio, dB, at <paramref name="rangeM"/>; negative infinity beyond the horizon.</summary>
        public static double CnrDb(in RadarTerms r, in MeasurementTerms m, in SurfaceTerms s, double antennaHeightM, double rangeM)
        {
            double rcs = CellRcsDbsm(r, m, s, antennaHeightM, rangeM);
            return double.IsNegativeInfinity(rcs) ? double.NegativeInfinity : RadarEquation.SnrDb(r, rcs, rangeM);
        }

        /// <summary>The target's signal to clutter plus noise, dB: SNR − 10·log(1 + CNR). Use it where the SNR was used.</summary>
        public static double ScnrDb(double snrDb, double cnrDb) =>
            double.IsNegativeInfinity(cnrDb) ? snrDb : snrDb - RadarEquation.ToDb(1.0 + RadarEquation.FromDb(cnrDb));
    }
}
