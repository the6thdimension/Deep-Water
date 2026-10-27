using System;
using RHRadarSuite.Tracking;

namespace RHRadarSuite.Detection
{
    /// <summary>
    /// What sets a radar's measurement accuracy, beyond <see cref="RadarTerms"/> (ROADMAP Phase 2, Barton). Every value
    /// comes from the radar's profile (P1).
    /// </summary>
    public struct MeasurementTerms
    {
        /// <summary>Two-way 3 dB beamwidths, degrees.</summary>
        public double AzimuthBeamwidthDeg, ElevationBeamwidthDeg;
        /// <summary>Monopulse slope k_m (about 1.6 for a typical sum-difference feed, Barton).</summary>
        public double MonopulseSlope;
        /// <summary>The errors that do not shrink with SNR (alignment, multipath, quantisation), one sigma, added in quadrature.</summary>
        public double RangeFloorM, AngleFloorDeg;
        /// <summary>Coherent processing interval, seconds: how long one Doppler measurement integrates (0: no Doppler).</summary>
        public double DwellS;
        /// <summary>The range-rate error that does not shrink with SNR, one sigma, m/s.</summary>
        public double RangeRateFloorMps;
    }

    /// <summary>
    /// Measurement errors, pure C# (P6), Barton and Skolnik's thermal-noise relations with a fixed floor:
    /// <list type="bullet">
    /// <item>σ_R = c / (2B √(2·SNR)), the range error of a matched filter of bandwidth B.</item>
    /// <item>σ_θ = θ_B / (k_m √(2·SNR)), the monopulse angle error for a beamwidth θ_B.</item>
    /// <item>σ_ṙ = λ / (2·T √(2·SNR)), the range-rate error of a Doppler measurement integrated over T.</item>
    /// </list>
    /// Each adds its floor in quadrature, so a strong target is accurate but never perfect. <see cref="Measure"/> turns a
    /// truth into one noisy <see cref="Plot"/> with a seeded generator (P7).
    /// </summary>
    public static class MeasurementErrors
    {
        public static double RangeSigmaM(in RadarTerms r, in MeasurementTerms m, double snrDb)
        {
            if (!(r.BandwidthHz > 0.0)) throw new ArgumentOutOfRangeException(nameof(r.BandwidthHz), r.BandwidthHz, "bandwidth must be positive");
            double thermal = RadarEquation.SpeedOfLight / (2.0 * r.BandwidthHz * Math.Sqrt(2.0 * RadarEquation.FromDb(snrDb)));
            return Math.Sqrt(thermal * thermal + m.RangeFloorM * m.RangeFloorM);
        }

        public static double RangeRateSigmaMps(in RadarTerms r, in MeasurementTerms m, double snrDb)
        {
            if (!(m.DwellS > 0.0)) throw new ArgumentOutOfRangeException(nameof(m.DwellS), m.DwellS, "a Doppler measurement needs a positive dwell");
            double thermal = RadarEquation.WavelengthM(r.FrequencyHz) / (2.0 * m.DwellS * Math.Sqrt(2.0 * RadarEquation.FromDb(snrDb)));
            return Math.Sqrt(thermal * thermal + m.RangeRateFloorMps * m.RangeRateFloorMps);
        }

        /// <summary>One noisy range rate (positive opening) for a true <paramref name="rangeRateMps"/>, drawn from <paramref name="rng"/>.</summary>
        public static double MeasureRangeRate(in RadarTerms r, in MeasurementTerms m, double rangeRateMps, double snrDb, Random rng) =>
            rangeRateMps + RangeRateSigmaMps(r, m, snrDb) * Gaussian(rng);

        public static double AzimuthSigmaDeg(in MeasurementTerms m, double snrDb) => AngleSigmaDeg(m.AzimuthBeamwidthDeg, m, snrDb);
        public static double ElevationSigmaDeg(in MeasurementTerms m, double snrDb) => AngleSigmaDeg(m.ElevationBeamwidthDeg, m, snrDb);

        static double AngleSigmaDeg(double beamwidthDeg, in MeasurementTerms m, double snrDb)
        {
            if (!(beamwidthDeg > 0.0)) throw new ArgumentOutOfRangeException(nameof(beamwidthDeg), beamwidthDeg, "beamwidth must be positive");
            if (!(m.MonopulseSlope > 0.0)) throw new ArgumentOutOfRangeException(nameof(m.MonopulseSlope), m.MonopulseSlope, "monopulse slope must be positive");
            double thermal = beamwidthDeg / (m.MonopulseSlope * Math.Sqrt(2.0 * RadarEquation.FromDb(snrDb)));
            return Math.Sqrt(thermal * thermal + m.AngleFloorDeg * m.AngleFloorDeg);
        }

        /// <summary>
        /// One noisy plot of a target at <paramref name="target"/> seen from <paramref name="radar"/> at <paramref name="snrDb"/>:
        /// range, azimuth (clockwise from +z toward +x) and elevation (up from the horizontal) each take a Gaussian error
        /// of their sigma, drawn from <paramref name="rng"/> in that order.
        /// </summary>
        public static Plot Measure(in RadarTerms r, in MeasurementTerms m, in Plot radar, in Plot target, double snrDb, Random rng)
        {
            double dx = target.X - radar.X, dy = target.Y - radar.Y, dz = target.Z - radar.Z;
            double ground = Math.Sqrt(dx * dx + dz * dz);
            double range = Math.Sqrt(ground * ground + dy * dy);
            if (!(range > 0.0)) throw new ArgumentOutOfRangeException(nameof(target), "the target is at the radar");
            double az = Math.Atan2(dx, dz), el = Math.Atan2(dy, ground);

            const double rad = Math.PI / 180.0;
            range += RangeSigmaM(r, m, snrDb) * Gaussian(rng);
            az += AzimuthSigmaDeg(m, snrDb) * rad * Gaussian(rng);
            el += ElevationSigmaDeg(m, snrDb) * rad * Gaussian(rng);

            double h = range * Math.Cos(el);
            return new Plot(radar.X + h * Math.Sin(az), radar.Y + range * Math.Sin(el), radar.Z + h * Math.Cos(az));
        }

        /// <summary>A unit normal (Box–Muller, one draw from two uniforms).</summary>
        public static double Gaussian(Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
