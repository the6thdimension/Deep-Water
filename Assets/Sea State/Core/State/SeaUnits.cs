using Unity.Mathematics;

namespace DeepWater.Sea.Core
{
    /// <summary>Unit definitions and direction conventions (definitions, not tuning values).</summary>
    public static class SeaUnits
    {
        /// <summary>1 international knot = 1852 m per hour.</summary>
        public const float MetersPerSecondPerKnot = 1852f / 3600f;

        /// <summary>
        /// Travel direction (radians from +X toward +Z) of waves driven from a compass bearing.
        /// Compass: 0 = from +Z (north), 90 = from +X (east). Waves travel downwind, i.e. toward
        /// bearing + 180°.
        /// </summary>
        public static float TravelDirectionRad(float fromCompassDeg)
        {
            float towardDeg = fromCompassDeg + 180f;
            return math.radians(90f - towardDeg);
        }

        /// <summary>Whitecap area fraction W = 3.84×10⁻⁶ U^3.41 (Monahan &amp; O'Muircheartaigh 1980), clamped to [0, 1].</summary>
        public static float MonahanWhitecapFraction(float windSpeedMs)
            => windSpeedMs <= 0f ? 0f : math.saturate(3.84e-6f * math.pow(windSpeedMs, 3.41f));
    }
}
