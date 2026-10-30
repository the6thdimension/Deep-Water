using System;
using DeepWater.AirWing.Core.Simulation;
using RHRadarSuite.Detection;
using RHRadarSuite.Tracking;
using UnityEngine;

namespace DeepWater.AirWing.Core.Radar
{
    /// <summary>What one look at one aircraft found, and why (for tests, logs and the HUD's debug view).</summary>
    public struct JetLookResult
    {
        /// <summary>False: below the radar horizon; nothing else was worked out.</summary>
        public bool AboveHorizon;
        /// <summary>True: the beam reached the sea at the target's range while looking down at it (J16).</summary>
        public bool InClutter;
        public double RangeM, RcsDbsm, SnrDb, CnrDb, ScnrDb, Pd;
        public bool Detected;
        /// <summary>The noisy measured position, when <see cref="Detected"/>.</summary>
        public Plot Plot;
    }

    /// <summary>
    /// One radar look at one aircraft, pure and deterministic (J1, J16). The radar suite's maths, the flight core's own
    /// seeded draws (System.Random is banned in the core, so the suite's <c>RadarLook</c> is not used whole):
    /// <list type="number">
    /// <item>The radar horizon over a 4/3 Earth, heights above the surface.</item>
    /// <item>The signal: the radar equation, with the target's cross-section at the aspect the radar sees.</item>
    /// <item>Sea clutter, a game rule: it competes only when the radar looks <b>down</b> at a target low enough that the
    /// beam's lower edge reaches the sea at the target's range (height ≤ range · tan(half the elevation beamwidth)). Then
    /// the suite's clutter cell at the radar's grazing angle, less the radar's clutter improvement. A target above the
    /// radar, or well above the sea, has sky behind it.</item>
    /// <item>A Swerling 1 detection draw at the false-alarm probability: one draw.</item>
    /// <item>If detected, Barton's range and angle errors: three more draws (range, azimuth, elevation), in that order.</item>
    /// </list>
    /// </summary>
    public static class JetRadarLook
    {
        const double Rad = Math.PI / 180.0;

        /// <summary>
        /// The signal-to-noise ratio, dB, of a 0 dBsm target at 1 m: the radar equation's constant part. A look adds the
        /// target's dBsm and takes 40·log₁₀ of the range, so the logs are worked once per radar, not per look.
        /// </summary>
        public static double SnrAt1mDb(in RadarProfile p) => RadarEquation.SnrDb(p.Terms, 0.0, 1.0);

        /// <summary>
        /// The target's position relative to the radar in the radar's body frame: its range, its azimuth right of the nose
        /// and its elevation up from the nose's plane, degrees.
        /// </summary>
        public static void Bearing(Vector3 radarPos, Quaternion radarAttitude, Vector3 targetPos, out float rangeM, out float azimuthDeg, out float elevationDeg)
        {
            Vector3 rel = Quaternion.Inverse(radarAttitude) * (targetPos - radarPos);
            rangeM = rel.magnitude;
            azimuthDeg = Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg;
            elevationDeg = Mathf.Atan2(rel.y, Mathf.Sqrt(rel.x * rel.x + rel.z * rel.z)) * Mathf.Rad2Deg;
        }

        /// <summary>True when the target is inside <paramref name="volume"/> (its angles and range) as the radar's nose sees it.</summary>
        public static bool InVolume(in RadarModeProfile volume, Vector3 radarPos, Quaternion radarAttitude, Vector3 targetPos, out float rangeM)
        {
            Bearing(radarPos, radarAttitude, targetPos, out rangeM, out float az, out float el);
            return rangeM > 0f && rangeM <= volume.RangeM && Mathf.Abs(az) <= volume.AzimuthHalfDeg && el >= volume.ElevationMinDeg && el <= volume.ElevationMaxDeg;
        }

        /// <summary>True when the target is inside the antenna's gimbal and <paramref name="rangeM"/>: a single-target track can hold it.</summary>
        public static bool InGimbal(in RadarProfile p, Vector3 radarPos, Quaternion radarAttitude, Vector3 targetPos)
        {
            Bearing(radarPos, radarAttitude, targetPos, out float range, out float az, out float el);
            if (!(range > 0f) || range > p.SingleTargetRangeM) return false;
            // The angle off the nose, both axes together.
            Vector3 rel = Quaternion.Inverse(radarAttitude) * (targetPos - radarPos);
            return Vector3.Angle(Vector3.forward, rel) <= p.GimbalHalfDeg;
        }

        /// <summary>
        /// One look. Heights are above the surface under each aircraft. <paramref name="extraGainDb"/> is a single-target
        /// look's gain (0 for search); <paramref name="seaGammaDb"/> the sea's constant-γ reflectivity at this radar's
        /// wavelength, or null for no clutter. Draws from <paramref name="draws"/> once, or four times when detected.
        /// </summary>
        public static JetLookResult Look(in RadarProfile p, double snrAt1mDb, double extraGainDb, double? seaGammaDb,
                                         Vector3 radarPos, float radarHeightM, Vector3 targetPos, Quaternion targetAttitude, float targetHeightM,
                                         in AspectRcs rcs, DeterministicRng draws)
        {
            var result = new JetLookResult();
            Vector3 d = targetPos - radarPos;
            result.RangeM = d.magnitude;
            if (!(result.RangeM > 1.0)) return result;
            result.AboveHorizon = result.RangeM <= RadarEquation.HorizonM(radarHeightM, targetHeightM);
            if (!result.AboveHorizon) return result;

            // The aspect: the angle between the target's nose and the line from it back to the radar.
            Vector3 nose = targetAttitude * Vector3.forward;
            double offNose = Vector3.Angle(nose, -d);
            result.RcsDbsm = rcs.AtDbsm(offNose);
            result.SnrDb = snrAt1mDb + extraGainDb + result.RcsDbsm - 40.0 * Math.Log10(result.RangeM);

            result.CnrDb = double.NegativeInfinity;
            if (seaGammaDb.HasValue && targetHeightM < radarHeightM
                && targetHeightM <= result.RangeM * Math.Tan(0.5 * p.Measurement.ElevationBeamwidthDeg * Rad))
            {
                result.InClutter = true;
                var sea = new SurfaceTerms { GammaDb = seaGammaDb.Value };
                result.CnrDb = Clutter.CnrDb(p.Terms, p.Measurement, sea, radarHeightM, result.RangeM) + extraGainDb - p.Terms.ClutterImprovementDb;
            }
            result.ScnrDb = Clutter.ScnrDb(result.SnrDb, result.CnrDb);
            result.Pd = RadarEquation.PdSwerling1(result.ScnrDb, p.FalseAlarmProbability);
            result.Detected = draws.NextFloat01() < result.Pd;
            if (result.Detected) result.Plot = Measure(p, radarPos, targetPos, result.ScnrDb, draws);
            return result;
        }

        /// <summary>A noisy plot: range, then world azimuth (clockwise from +z toward +x), then elevation, each with Barton's sigma at the SCNR.</summary>
        static Plot Measure(in RadarProfile p, Vector3 radarPos, Vector3 targetPos, double scnrDb, DeterministicRng draws)
        {
            double dx = targetPos.x - radarPos.x, dy = targetPos.y - radarPos.y, dz = targetPos.z - radarPos.z;
            double ground = Math.Sqrt(dx * dx + dz * dz);
            double range = Math.Sqrt(ground * ground + dy * dy);
            double az = Math.Atan2(dx, dz), el = Math.Atan2(dy, ground);
            range += MeasurementErrors.RangeSigmaM(p.Terms, p.Measurement, scnrDb) * Gaussian(draws);
            az += MeasurementErrors.AzimuthSigmaDeg(p.Measurement, scnrDb) * Rad * Gaussian(draws);
            el += MeasurementErrors.ElevationSigmaDeg(p.Measurement, scnrDb) * Rad * Gaussian(draws);
            double h = range * Math.Cos(el);
            return new Plot(radarPos.x + h * Math.Sin(az), radarPos.y + range * Math.Sin(el), radarPos.z + h * Math.Cos(az));
        }

        /// <summary>A unit normal from one draw (Box–Muller's cosine branch, with the angle and radius from the two halves of one 48-bit draw).</summary>
        static double Gaussian(DeterministicRng draws)
        {
            ulong u = draws.NextULong();
            double u1 = ((u >> 40) + 1) * (1.0 / 16777217.0);            // (0, 1]: never log(0)
            double u2 = ((u >> 16) & 0xFFFFFF) * (1.0 / 16777216.0);    // [0, 1)
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
