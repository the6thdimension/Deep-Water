using System;
using RHRadarSuite.Tracking;

namespace RHRadarSuite.Detection
{
    /// <summary>
    /// A target's radar cross-section by aspect (ROADMAP Phase 2, "RCS in dBsm + signature aspect dependence", coarse):
    /// nose, beam and tail figures in dBsm, interpolated in dB by the angle off the nose. Every figure comes from the
    /// target's profile (P1). What it leaves out: elevation aspect, frequency dependence, glints and lobing.
    /// </summary>
    public struct AspectRcs
    {
        public double NoseDbsm, BeamDbsm, TailDbsm;

        /// <summary>The same figure from every side.</summary>
        public static AspectRcs Uniform(double dbsm) => new AspectRcs { NoseDbsm = dbsm, BeamDbsm = dbsm, TailDbsm = dbsm };

        /// <summary>The RCS, dBsm, seen <paramref name="offNoseDeg"/> off the nose (0 nose on, 90 beam, 180 tail): linear in dB between them.</summary>
        public double AtDbsm(double offNoseDeg)
        {
            double a = Math.Min(180.0, Math.Abs(offNoseDeg));
            return a <= 90.0 ? NoseDbsm + (BeamDbsm - NoseDbsm) * (a / 90.0) : BeamDbsm + (TailDbsm - BeamDbsm) * ((a - 90.0) / 90.0);
        }

        /// <summary>
        /// The angle, degrees, between a target's nose (<paramref name="noseX"/>, <paramref name="noseY"/>, <paramref name="noseZ"/>,
        /// any length) and the line from the target to the radar.
        /// </summary>
        public static double OffNoseDeg(in Plot radar, in Plot target, double noseX, double noseY, double noseZ)
        {
            double lx = radar.X - target.X, ly = radar.Y - target.Y, lz = radar.Z - target.Z;
            double ln = Math.Sqrt(lx * lx + ly * ly + lz * lz), nn = Math.Sqrt(noseX * noseX + noseY * noseY + noseZ * noseZ);
            if (!(ln > 0.0) || !(nn > 0.0)) throw new ArgumentOutOfRangeException(nameof(target), "the target must be away from the radar and have a nose");
            double c = (lx * noseX + ly * noseY + lz * noseZ) / (ln * nn);
            return Math.Acos(Math.Max(-1.0, Math.Min(1.0, c))) * (180.0 / Math.PI);
        }
    }

    /// <summary>
    /// The volume a radar searches (ROADMAP Phase 2, "elevation coverage enforced"): an elevation band and an azimuth sector,
    /// in the world frame (Unity axes: azimuth clockwise from +z toward +x). Elevation is flat-Earth geometry from the antenna.
    /// </summary>
    public struct Coverage
    {
        public double MinElevationDeg, MaxElevationDeg;
        /// <summary>Sector centre, degrees, and half-width (180: all round).</summary>
        public double AzimuthCentreDeg, AzimuthHalfWidthDeg;

        public static Coverage AllRound(double minElevationDeg, double maxElevationDeg) =>
            new Coverage { MinElevationDeg = minElevationDeg, MaxElevationDeg = maxElevationDeg, AzimuthHalfWidthDeg = 180.0 };

        public bool Contains(in Plot radar, in Plot target)
        {
            double dx = target.X - radar.X, dy = target.Y - radar.Y, dz = target.Z - radar.Z;
            double el = Math.Atan2(dy, Math.Sqrt(dx * dx + dz * dz)) * (180.0 / Math.PI);
            if (el < MinElevationDeg || el > MaxElevationDeg) return false;
            if (AzimuthHalfWidthDeg >= 180.0) return true;
            double az = Math.Atan2(dx, dz) * (180.0 / Math.PI);
            double off = Math.Abs(((az - AzimuthCentreDeg) % 360.0 + 540.0) % 360.0 - 180.0);
            return off <= AzimuthHalfWidthDeg;
        }
    }

    /// <summary>What one look at one target found, and why.</summary>
    public struct LookResult
    {
        /// <summary>False: below the radar horizon (nothing else was computed).</summary>
        public bool AboveHorizon;
        /// <summary>False: outside the searched volume (nothing else was computed).</summary>
        public bool InCoverage;
        public double RangeM, RcsDbsm, SnrDb, CnrDb, ScnrDb, Pd;
        public bool Detected;
        /// <summary>The noisy measurement, when <see cref="Detected"/>.</summary>
        public Plot Plot;
    }

    /// <summary>
    /// One look at one target, the pure core end to end (ROADMAP Phases 2 and 4): the radar horizon, the searched volume,
    /// the radar equation, sea clutter, a Swerling 1 detection draw at <c>pfa</c>, and Barton's measurement errors. Heights
    /// (<see cref="Plot.Y"/>) are above the sea surface. The generator draws once for detection and, if detected, three
    /// times for the measurement, in that order, so a seeded run replays exactly (P7).
    /// </summary>
    public static class RadarLook
    {
        public static LookResult Look(in RadarTerms r, in MeasurementTerms m, in Coverage coverage, SurfaceTerms? sea, double pfa,
                                      in Plot radar, in Plot target, double rcsDbsm, Random rng)
        {
            var result = new LookResult();
            double dx = target.X - radar.X, dy = target.Y - radar.Y, dz = target.Z - radar.Z;
            result.RangeM = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            result.AboveHorizon = result.RangeM <= RadarEquation.HorizonM(radar.Y, target.Y);
            if (!result.AboveHorizon) return result;
            result.InCoverage = coverage.Contains(radar, target);
            if (!result.InCoverage) return result;

            result.RcsDbsm = rcsDbsm;
            result.SnrDb = RadarEquation.SnrDb(r, rcsDbsm, result.RangeM);
            result.CnrDb = sea.HasValue ? Clutter.CnrDb(r, m, sea.Value, radar.Y, result.RangeM) : double.NegativeInfinity;
            result.ScnrDb = Clutter.ScnrDb(result.SnrDb, result.CnrDb);
            result.Pd = RadarEquation.PdSwerling1(result.ScnrDb, pfa);
            result.Detected = rng.NextDouble() < result.Pd;
            if (result.Detected) result.Plot = MeasurementErrors.Measure(r, m, radar, target, result.ScnrDb, rng);
            return result;
        }
    }
}
