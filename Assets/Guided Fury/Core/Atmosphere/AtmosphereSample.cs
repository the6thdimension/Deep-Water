namespace GuidedFury.Core.Atmosphere
{
    /// <summary>
    /// A point sample of atmospheric properties at some altitude. Returned by IAtmosphere.
    ///
    /// **Why a struct:** unmanaged, copy-cheap, Burst-friendly. Sampled once per fixed step
    /// per missile and passed by `in` reference into integrators.
    ///
    /// **Units (SI throughout the sim):**
    /// - Density: kg/m³ (sea-level ISA ≈ 1.225)
    /// - Pressure: Pascals (sea-level ISA = 101325)
    /// - Temperature: Kelvin (sea-level ISA = 288.15)
    /// - SpeedOfSound: m/s (sea-level ISA ≈ 340.3)
    /// </summary>
    public struct AtmosphereSample
    {
        public float Density;
        public float Pressure;
        public float Temperature;
        public float SpeedOfSound;

        /// <summary>Sea-level ISA values. Useful as a fallback when no atmosphere is provided.</summary>
        public static AtmosphereSample SeaLevelIsa => new AtmosphereSample
        {
            Density      = 1.225f,
            Pressure     = 101325f,
            Temperature  = 288.15f,
            SpeedOfSound = 340.3f,
        };
    }
}
