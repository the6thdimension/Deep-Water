using UnityEngine;

namespace GuidedFury.Core.Atmosphere
{
    /// <summary>
    /// Simplified International Standard Atmosphere (ISA) model.
    ///
    /// Covers the troposphere (0–11 km, linear lapse) and a constant-temperature stratosphere
    /// stub (11–20 km). That's enough for any missile we're shipping in Phase 1–3; we can
    /// extend to the mesosphere when a ballistic-missile profile or high-altitude AAM
    /// actually demands it.
    ///
    /// **Reference values (sea level, ISA):**
    /// - T0 = 288.15 K
    /// - P0 = 101325 Pa
    /// - ρ0 = 1.225 kg/m³
    /// - Lapse rate L = -0.0065 K/m (troposphere)
    /// - Gas constant for dry air R = 287.05287 J/(kg·K)
    /// - Gravity (assumed constant for the atmosphere model) g = 9.80665 m/s²
    /// - γ (ratio of specific heats) = 1.4
    ///
    /// **Why a class, not a struct:** stateless, but `IAtmosphere` is an interface and boxing
    /// a struct each call would defeat the purpose. The class instance is cheap to allocate
    /// once and reuse. If we ever need to pass IAtmosphere into Burst Jobs, we'll add a
    /// struct-with-FunctionPointer variant; not needed at L0/L1.
    /// </summary>
    public sealed class StandardAtmosphere : IAtmosphere
    {
        // ISA constants. Public for any test that wants to assert against canonical values.
        public const float SeaLevelTemperatureK   = 288.15f;
        public const float SeaLevelPressurePa     = 101325f;
        public const float SeaLevelDensityKgM3    = 1.225f;
        public const float TroposphereLapseKPerM  = -0.0065f;
        public const float TropopauseAltitudeM    = 11000f;
        public const float StratosphereCeilingM   = 20000f;
        public const float DryAirGasConstant      = 287.05287f; // R, J/(kg·K)
        public const float StandardGravity        = 9.80665f;   // m/s²
        public const float HeatCapacityRatio      = 1.4f;       // γ

        /// <summary>Shared instance — atmosphere is stateless, no need for per-missile copies.</summary>
        public static readonly StandardAtmosphere Instance = new StandardAtmosphere();

        public AtmosphereSample Sample(float altitudeMetersMsl)
        {
            // Clamp into the modeled range. Outside-range altitudes return the boundary
            // sample rather than throwing — per the IAtmosphere contract.
            float h = Mathf.Clamp(altitudeMetersMsl, 0f, StratosphereCeilingM);

            float T, P;

            if (h <= TropopauseAltitudeM)
            {
                // Troposphere: linear temperature lapse, barometric pressure equation.
                // T(h) = T0 + L * h
                // P(h) = P0 * (T/T0)^(-g/(L*R))
                T = SeaLevelTemperatureK + TroposphereLapseKPerM * h;
                float exponent = -StandardGravity / (TroposphereLapseKPerM * DryAirGasConstant);
                P = SeaLevelPressurePa * Mathf.Pow(T / SeaLevelTemperatureK, exponent);
            }
            else
            {
                // Lower stratosphere (11–20 km): isothermal, exponential pressure falloff.
                // T(h) = T_trop  (constant)
                // P(h) = P_trop * exp(-g*(h - h_trop) / (R*T_trop))
                T = SeaLevelTemperatureK + TroposphereLapseKPerM * TropopauseAltitudeM;

                float tropopauseExponent = -StandardGravity / (TroposphereLapseKPerM * DryAirGasConstant);
                float Ptrop = SeaLevelPressurePa * Mathf.Pow(T / SeaLevelTemperatureK, tropopauseExponent);

                P = Ptrop * Mathf.Exp(-StandardGravity * (h - TropopauseAltitudeM) / (DryAirGasConstant * T));
            }

            // Ideal gas law: ρ = P / (R * T)
            float rho = P / (DryAirGasConstant * T);

            // Speed of sound: a = sqrt(γ * R * T)
            float a = Mathf.Sqrt(HeatCapacityRatio * DryAirGasConstant * T);

            return new AtmosphereSample
            {
                Density      = rho,
                Pressure     = P,
                Temperature  = T,
                SpeedOfSound = a,
            };
        }
    }
}
