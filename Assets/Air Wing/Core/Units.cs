namespace DeepWater.AirWing.Core
{
    /// <summary>
    /// Unit conversions at the edge (W22): the core works in SI; public figures are quoted in aviation units and
    /// converted once, when a profile is baked, and back again only for display. Every factor here is exact by
    /// definition (international foot, avoirdupois pound, international nautical mile, standard gravity).
    /// </summary>
    public static class Units
    {
        public const float StandardGravity = 9.80665f;      // m/s², by definition
        public const double FootToMetre = 0.3048;
        public const double PoundToKilogram = 0.45359237;
        public const double PoundForceToNewton = 4.4482216152605;
        public const double KnotToMetrePerSecond = 1852.0 / 3600.0;
        public const double SquareFootToSquareMetre = FootToMetre * FootToMetre;
        /// <summary>One slug is the mass one pound-force accelerates at one foot per second squared.</summary>
        public const double SlugToKilogram = PoundForceToNewton / FootToMetre;
        public const double SlugFootSquaredToKilogramMetreSquared = SlugToKilogram * SquareFootToSquareMetre;
        public const double SlugPerCubicFootToKilogramPerCubicMetre = SlugToKilogram / (FootToMetre * FootToMetre * FootToMetre);
        public const float DegToRad = (float)(System.Math.PI / 180.0);
        public const float RadToDeg = (float)(180.0 / System.Math.PI);

        public static float Feet(double metres) => (float)(metres / FootToMetre);
        public static float Knots(double metresPerSecond) => (float)(metresPerSecond / KnotToMetrePerSecond);
    }
}
