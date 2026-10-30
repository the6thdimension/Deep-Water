using System;

namespace DeepWater.Metoc
{
    /// <summary>
    /// The constants of the clear-sky light model (E45). An <c>IlluminationProfileSO</c> fills it, with provenance;
    /// the defaults here are those values, so a test can build one without an asset.
    /// </summary>
    public sealed class IlluminationProfile
    {
        /// <summary>Solar illuminance at normal incidence above the air at 1 AU (IES; from memory, verify).</summary>
        public double SolarAboveAirLux = 127500.0;
        /// <summary>Clear-sky extinction of the direct beam per air mass (IES clear sky; from memory, verify).</summary>
        public double SolarExtinction = 0.21;
        /// <summary>Clear-sky diffuse horizontal illuminance, A + B·(sin h)^C, in klux (IES clear sky; from memory, verify).</summary>
        public double DiffuseA = 0.8, DiffuseB = 15.5, DiffuseC = 0.5;
        /// <summary>Twilight: horizontal illuminance at sun elevations below the horizon, descending (from memory, verify).</summary>
        public double[] TwilightElevationDeg = { -6.0, -12.0, -18.0 };
        public double[] TwilightLux = { 3.4, 0.008, 0.0006 };
        /// <summary>Full-moon V magnitude and phase law m = M + a|α| + b·α⁴ (Krisciunas &amp; Schaefer 1991).</summary>
        public double MoonFullMagnitude = -12.73, MoonPhaseLinear = 0.026, MoonPhaseQuartic = 4e-9;
        /// <summary>V-band extinction, magnitudes per air mass (Krisciunas &amp; Schaefer's site value).</summary>
        public double MoonExtinctionMag = 0.172;
        public double MoonMeanDistanceKm = 384400.0;
        /// <summary>Starlight and airglow on a clear moonless night (from memory, verify).</summary>
        public double NightSkyLux = 0.002;
    }

    /// <summary>Reckoned horizontal illuminance at the ground, lux, and the bodies' illuminance above the air.</summary>
    public readonly struct IlluminationState
    {
        public readonly double SunLux, MoonLux, NightSkyLux;
        /// <summary>At normal incidence above the air: what the drawn lights carry (HDRP's sky attenuates them).</summary>
        public readonly double SunAboveAirLux, MoonAboveAirLux;

        public IlluminationState(double sunLux, double moonLux, double nightSkyLux, double sunAboveAirLux, double moonAboveAirLux)
        {
            SunLux = sunLux;
            MoonLux = moonLux;
            NightSkyLux = nightSkyLux;
            SunAboveAirLux = sunAboveAirLux;
            MoonAboveAirLux = moonAboveAirLux;
        }

        public double TotalLux => SunLux + MoonLux + NightSkyLux;
    }

    /// <summary>
    /// Clear-sky ground illuminance from published pieces (E41, E45): the IES daylight model above the horizon, a
    /// twilight table interpolated in log lux below it, Krisciunas &amp; Schaefer (1991) moonlight, and a night-sky
    /// floor. Air mass from Kasten &amp; Young (1989). Clouds join in slice 2.
    /// </summary>
    public static class Illumination
    {
        /// <summary>Lux per foot-candle.</summary>
        const double LuxPerFootCandle = 10.7639;
        /// <summary>Krisciunas &amp; Schaefer: I = 10^(−0.4 (m + 16.57)) foot-candles for a source of V magnitude m.</summary>
        const double MagnitudeZeroPoint = 16.57;

        public static IlluminationState Compute(in SunState sun, in MoonState moon, IlluminationProfile p)
        {
            double sunAboveAir = p.SolarAboveAirLux / (sun.DistanceAu * sun.DistanceAu);
            double sunLux = SunHorizontalLux(sun.Sky.ElevationDeg, sunAboveAir, p);

            double moonAboveAir = MoonAboveAirLux(moon.PhaseAngleDeg, moon.DistanceKm, p);
            double moonLux = 0;
            double hm = moon.Sky.ElevationDeg;
            if (hm > 0)
                moonLux = moonAboveAir * Math.Pow(10, -0.4 * p.MoonExtinctionMag * AirMass(hm)) * Math.Sin(hm * Almanac.Deg);

            return new IlluminationState(sunLux, moonLux, p.NightSkyLux, sunAboveAir, moonAboveAir);
        }

        /// <summary>Sun on a horizontal surface: direct beam plus sky above the horizon, the twilight table below.</summary>
        public static double SunHorizontalLux(double elevationDeg, double aboveAirLux, IlluminationProfile p)
        {
            if (elevationDeg >= 0)
            {
                double s = Math.Sin(elevationDeg * Almanac.Deg);
                double direct = aboveAirLux * Math.Exp(-p.SolarExtinction * AirMass(elevationDeg)) * s;
                double diffuse = 1000.0 * (p.DiffuseA + p.DiffuseB * Math.Pow(s, p.DiffuseC));
                return direct + diffuse;
            }

            // Log-linear from the horizon's value (the model at h = 0) down the table; nothing below its last row.
            double prevH = 0, prevLog = Math.Log10(1000.0 * p.DiffuseA);
            for (int i = 0; i < p.TwilightElevationDeg.Length; i++)
            {
                double h = p.TwilightElevationDeg[i], log = Math.Log10(p.TwilightLux[i]);
                if (elevationDeg >= h)
                {
                    double f = (elevationDeg - prevH) / (h - prevH);
                    return Math.Pow(10, prevLog + f * (log - prevLog));
                }
                prevH = h;
                prevLog = log;
            }
            return 0;
        }

        /// <summary>The moon at normal incidence above the air, lux, for its phase angle and distance.</summary>
        public static double MoonAboveAirLux(double phaseAngleDeg, double distanceKm, IlluminationProfile p)
        {
            double a = Math.Abs(phaseAngleDeg);
            double magnitude = p.MoonFullMagnitude + p.MoonPhaseLinear * a + p.MoonPhaseQuartic * a * a * a * a;
            double meanDistance = p.MoonMeanDistanceKm / distanceKm;
            return Math.Pow(10, -0.4 * (magnitude + MagnitudeZeroPoint)) * LuxPerFootCandle * meanDistance * meanDistance;
        }

        /// <summary>Relative optical air mass at an apparent elevation (Kasten &amp; Young 1989); 1 overhead, about 38 at the horizon.</summary>
        public static double AirMass(double elevationDeg)
        {
            double h = Math.Max(0.0, elevationDeg);
            return 1.0 / (Math.Sin(h * Almanac.Deg) + 0.50572 * Math.Pow(h + 6.07995, -1.6364));
        }
    }
}
