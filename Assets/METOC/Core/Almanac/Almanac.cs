using System;

namespace DeepWater.Metoc
{
    /// <summary>
    /// Where the sun and moon are (E28). Pure functions of the Julian Date and the place; latitude north positive,
    /// longitude east positive. The coefficients are the published algorithms' own and live here, beside their source:
    /// <list type="bullet">
    /// <item>Sun: NOAA's solar-position equations (the NOAA Solar Calculator spreadsheet, after Meeus ch. 25 and 28),
    /// about 0.01–0.02° for 1800–2100, with NOAA's refraction approximation.</item>
    /// <item>Moon: the Astronomical Almanac's low-precision formulas (section D), about 0.3° in longitude and 0.2° in
    /// latitude for 1900–2100; parallax applied to the altitude as −π·cos h.</item>
    /// <item>Horizon frame: Meeus ch. 13; mean sidereal time: Meeus 12.4.</item>
    /// </list>
    /// UT is used where the algorithms want TT (E44): about 0.003° of sun and 0.01° of moon.
    /// </summary>
    public static class Almanac
    {
        public const double Deg = Math.PI / 180.0;
        public const double KmPerAu = 149597870.7;
        /// <summary>The Earth's equatorial radius the Almanac's parallax refers to.</summary>
        public const double EarthRadiusKm = 6378.14;
        /// <summary>The moon's mean radius (IAU), for the disc's apparent size.</summary>
        public const double MoonRadiusKm = 1737.4;

        public static double Normalize360(double deg)
        {
            double d = deg % 360.0;
            return d < 0 ? d + 360.0 : d;
        }

        static double Sin(double deg) => Math.Sin(deg * Deg);
        static double Cos(double deg) => Math.Cos(deg * Deg);

        /// <summary>Mean obliquity of the ecliptic (Meeus 22.2, as NOAA writes it) corrected for nutation's main term.</summary>
        public static double ObliquityDeg(double t)
        {
            double mean = 23.0 + (26.0 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60.0) / 60.0;
            return mean + 0.00256 * Cos(125.04 - 1934.136 * t);
        }

        /// <summary>Greenwich mean sidereal time, degrees (Meeus 12.4).</summary>
        public static double GreenwichMeanSiderealDeg(double jd)
        {
            double t = Calendar.Centuries(jd);
            return Normalize360(280.46061837 + 360.98564736629 * (jd - Calendar.J2000) + t * t * (0.000387933 - t / 38710000.0));
        }

        /// <summary>
        /// The sun's equatorial position, NOAA's equations line for line: right ascension, declination, apparent
        /// longitude, the equation of time (minutes) and the distance (AU).
        /// </summary>
        public static void SunEquatorial(double jd, out double raDeg, out double decDeg, out double apparentLongitudeDeg,
            out double equationOfTimeMin, out double distanceAu)
        {
            double t = Calendar.Centuries(jd);
            double l0 = Normalize360(280.46646 + t * (36000.76983 + t * 0.0003032));
            double m = 357.52911 + t * (35999.05029 - 0.0001537 * t);
            double e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
            double c = Sin(m) * (1.914602 - t * (0.004817 + 0.000014 * t)) + Sin(2 * m) * (0.019993 - 0.000101 * t) + Sin(3 * m) * 0.000289;
            double trueLong = l0 + c, trueAnom = m + c;
            distanceAu = 1.000001018 * (1 - e * e) / (1 + e * Cos(trueAnom));
            apparentLongitudeDeg = Normalize360(trueLong - 0.00569 - 0.00478 * Sin(125.04 - 1934.136 * t));
            double eps = ObliquityDeg(t);
            raDeg = Normalize360(Math.Atan2(Cos(eps) * Sin(apparentLongitudeDeg), Cos(apparentLongitudeDeg)) / Deg);
            decDeg = Math.Asin(Sin(eps) * Sin(apparentLongitudeDeg)) / Deg;
            double y = Math.Tan(eps / 2 * Deg);
            y *= y;
            equationOfTimeMin = 4.0 / Deg * (y * Sin(2 * l0) - 2 * e * Sin(m) + 4 * e * y * Sin(m) * Cos(2 * l0)
                                             - 0.5 * y * y * Sin(4 * l0) - 1.25 * e * e * Sin(2 * m));
        }

        /// <summary>The sun in the local sky at a UT instant (E28).</summary>
        public static SunState Sun(double utSeconds, double latDeg, double lonEastDeg)
        {
            double jd = Calendar.JulianDateFromUt(utSeconds);
            SunEquatorial(jd, out double ra, out double dec, out double lambda, out double eot, out double r);
            // NOAA: true solar time from UT minutes, the equation of time and 4 min per degree of longitude.
            double tst = (Calendar.SecondsOfDay(utSeconds) / 60.0 + eot + 4.0 * lonEastDeg) % 1440.0;
            if (tst < 0) tst += 1440.0;
            double hourAngle = tst / 4.0 - 180.0;
            Enu trueDir = Horizon(dec, hourAngle, latDeg);
            return new SunState(Apparent(trueDir, 0.0), ra, dec, lambda, eot, r);
        }

        /// <summary>
        /// The moon's geocentric ecliptic longitude, latitude and horizontal parallax, degrees: the Astronomical
        /// Almanac's low-precision formulas.
        /// </summary>
        public static void MoonEcliptic(double jd, out double lambdaDeg, out double betaDeg, out double parallaxDeg)
        {
            double t = Calendar.Centuries(jd);
            lambdaDeg = Normalize360(218.32 + 481267.881 * t
                                     + 6.29 * Sin(135.0 + 477198.87 * t) - 1.27 * Sin(259.3 - 413335.36 * t)
                                     + 0.66 * Sin(235.7 + 890534.22 * t) + 0.21 * Sin(269.9 + 954397.74 * t)
                                     - 0.19 * Sin(357.5 + 35999.05 * t) - 0.11 * Sin(186.5 + 966404.03 * t));
            betaDeg = 5.13 * Sin(93.3 + 483202.02 * t) + 0.28 * Sin(228.2 + 960400.89 * t)
                      - 0.28 * Sin(318.3 + 6003.15 * t) - 0.17 * Sin(217.6 - 407332.21 * t);
            parallaxDeg = 0.9508 + 0.0518 * Cos(135.0 + 477198.87 * t) + 0.0095 * Cos(259.3 - 413335.36 * t)
                          + 0.0078 * Cos(235.7 + 890534.22 * t) + 0.0028 * Cos(269.9 + 954397.74 * t);
        }

        /// <summary>The moon in the local sky at a UT instant, with its phase from the sun's position (E28).</summary>
        public static MoonState Moon(double utSeconds, double latDeg, double lonEastDeg, in SunState sun)
        {
            double jd = Calendar.JulianDateFromUt(utSeconds);
            MoonEcliptic(jd, out double lambda, out double beta, out double parallax);
            double eps = ObliquityDeg(Calendar.Centuries(jd));
            double ra = Math.Atan2(Sin(lambda) * Cos(eps) - Math.Tan(beta * Deg) * Sin(eps), Cos(lambda)) / Deg;
            double dec = Math.Asin(Sin(beta) * Cos(eps) + Cos(beta) * Sin(eps) * Sin(lambda)) / Deg;
            double hourAngle = GreenwichMeanSiderealDeg(jd) + lonEastDeg - ra;
            Enu trueDir = Horizon(dec, hourAngle, latDeg);
            double distanceKm = EarthRadiusKm / Math.Sin(parallax * Deg);

            // Phase (Meeus ch. 48): the geocentric elongation, then the phase angle seen from the moon.
            double dLambda = lambda - sun.ApparentLongitudeDeg;
            double cosPsi = Cos(beta) * Cos(dLambda);
            double psi = Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosPsi)));
            double sunKm = sun.DistanceAu * KmPerAu;
            double phaseAngle = Math.Atan2(sunKm * Math.Sin(psi), distanceKm - sunKm * Math.Cos(psi)) / Deg;
            double lit = (1 + Math.Cos(phaseAngle * Deg)) / 2;
            bool waxing = Sin(dLambda) > 0;

            return new MoonState(Apparent(trueDir, parallax), lambda, beta, distanceKm, parallax, phaseAngle, lit, waxing);
        }

        /// <summary>The moon disc's apparent diameter, degrees: about 0.49° at apogee and 0.56° at perigee.</summary>
        public static double MoonAngularDiameterDeg(double distanceKm) => 2.0 * Math.Atan(MoonRadiusKm / distanceKm) / Deg;

        /// <summary>
        /// The geometric direction of a body at declination δ and local hour angle H (west positive) from latitude φ
        /// (Meeus ch. 13), as east, north, up.
        /// </summary>
        public static Enu Horizon(double decDeg, double hourAngleDeg, double latDeg)
        {
            double east = -Cos(decDeg) * Sin(hourAngleDeg);
            double north = Cos(latDeg) * Sin(decDeg) - Sin(latDeg) * Cos(decDeg) * Cos(hourAngleDeg);
            double up = Sin(latDeg) * Sin(decDeg) + Cos(latDeg) * Cos(decDeg) * Cos(hourAngleDeg);
            return new Enu(east, north, up);
        }

        /// <summary>
        /// The apparent position of a geometric direction: the moon's parallax lowers it by π·cos h, then
        /// refraction lifts it. The azimuth is unchanged.
        /// </summary>
        public static SkyPosition Apparent(in Enu trueDir, double parallaxDeg)
        {
            double trueElevation = Math.Asin(Math.Max(-1.0, Math.Min(1.0, trueDir.Up))) / Deg;
            double azimuth = Normalize360(Math.Atan2(trueDir.East, trueDir.North) / Deg);
            double topocentric = trueElevation - parallaxDeg * Cos(trueElevation);
            return new SkyPosition(topocentric + RefractionDeg(topocentric), trueElevation, azimuth);
        }

        /// <summary>NOAA's atmospheric refraction approximation, degrees, by geometric elevation.</summary>
        public static double RefractionDeg(double elevationDeg)
        {
            double e = elevationDeg, arcsec;
            if (e > 85.0) return 0.0;
            if (e > 5.0)
            {
                double tanE = Math.Tan(e * Deg);
                arcsec = 58.1 / tanE - 0.07 / (tanE * tanE * tanE) + 0.000086 / Math.Pow(tanE, 5);
            }
            else if (e > -0.575) arcsec = 1735.0 + e * (-518.2 + e * (103.4 + e * (-12.79 + e * 0.711)));
            else arcsec = -20.772 / Math.Tan(e * Deg);
            return arcsec / 3600.0;
        }
    }
}
