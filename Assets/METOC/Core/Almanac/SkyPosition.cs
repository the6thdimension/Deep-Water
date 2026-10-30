using System;

namespace DeepWater.Metoc
{
    /// <summary>A unit direction in the theatre's local frame: east, north, up (Theatre E2: Unity X, Z, Y).</summary>
    public readonly struct Enu
    {
        public readonly double East, North, Up;

        public Enu(double east, double north, double up)
        {
            East = east;
            North = north;
            Up = up;
        }

        /// <summary>The direction at an elevation above the horizon and an azimuth clockwise from north.</summary>
        public static Enu FromAngles(double elevationDeg, double azimuthDeg)
        {
            double e = elevationDeg * Almanac.Deg, a = azimuthDeg * Almanac.Deg;
            return new Enu(Math.Sin(a) * Math.Cos(e), Math.Cos(a) * Math.Cos(e), Math.Sin(e));
        }
    }

    /// <summary>
    /// Where a body appears in the local sky. <see cref="ElevationDeg"/> is apparent (refracted; for the moon also
    /// topocentric); <see cref="TrueElevationDeg"/> is geometric. <see cref="Direction"/> points at the apparent body.
    /// </summary>
    public readonly struct SkyPosition
    {
        public readonly double ElevationDeg, TrueElevationDeg, AzimuthDeg;
        public readonly Enu Direction;

        public SkyPosition(double elevationDeg, double trueElevationDeg, double azimuthDeg)
        {
            ElevationDeg = elevationDeg;
            TrueElevationDeg = trueElevationDeg;
            AzimuthDeg = azimuthDeg;
            Direction = Enu.FromAngles(elevationDeg, azimuthDeg);
        }

        public bool AboveHorizon => ElevationDeg > 0;
    }

    /// <summary>The sun from NOAA's solar-position equations (E28).</summary>
    public readonly struct SunState
    {
        public readonly SkyPosition Sky;
        public readonly double RightAscensionDeg, DeclinationDeg, ApparentLongitudeDeg, EquationOfTimeMin, DistanceAu;

        public SunState(in SkyPosition sky, double rightAscensionDeg, double declinationDeg, double apparentLongitudeDeg,
            double equationOfTimeMin, double distanceAu)
        {
            Sky = sky;
            RightAscensionDeg = rightAscensionDeg;
            DeclinationDeg = declinationDeg;
            ApparentLongitudeDeg = apparentLongitudeDeg;
            EquationOfTimeMin = equationOfTimeMin;
            DistanceAu = distanceAu;
        }
    }

    /// <summary>The moon from the Astronomical Almanac's low-precision formulas, with its phase (E28).</summary>
    public readonly struct MoonState
    {
        public readonly SkyPosition Sky;
        public readonly double EclipticLongitudeDeg, EclipticLatitudeDeg, DistanceKm, HorizontalParallaxDeg;
        /// <summary>Sun–moon–Earth angle: 0 at full moon, 180 at new.</summary>
        public readonly double PhaseAngleDeg;
        /// <summary>Fraction of the disc lit, 0 (new) to 1 (full).</summary>
        public readonly double IlluminatedFraction;
        /// <summary>True from new to full, while the moon is east of the sun.</summary>
        public readonly bool Waxing;

        public MoonState(in SkyPosition sky, double eclipticLongitudeDeg, double eclipticLatitudeDeg, double distanceKm,
            double horizontalParallaxDeg, double phaseAngleDeg, double illuminatedFraction, bool waxing)
        {
            Sky = sky;
            EclipticLongitudeDeg = eclipticLongitudeDeg;
            EclipticLatitudeDeg = eclipticLatitudeDeg;
            DistanceKm = distanceKm;
            HorizontalParallaxDeg = horizontalParallaxDeg;
            PhaseAngleDeg = phaseAngleDeg;
            IlluminatedFraction = illuminatedFraction;
            Waxing = waxing;
        }
    }
}
