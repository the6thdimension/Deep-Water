using System;

namespace DeepWater.AirWing.Core.Atmosphere
{
    /// <summary>The air at one point: what lift, drag, thrust and the Mach number need.</summary>
    public readonly struct AirData
    {
        public readonly float DensityKgM3;
        public readonly float PressurePa;
        public readonly float TemperatureK;
        public readonly float SpeedOfSoundMps;

        public AirData(float density, float pressure, float temperature, float speedOfSound)
        {
            DensityKgM3 = density;
            PressurePa = pressure;
            TemperatureK = temperature;
            SpeedOfSoundMps = speedOfSound;
        }
    }

    /// <summary>The Atmosphere part (BLUEPRINT §4). L0: the ISA, still air.</summary>
    public interface IAtmosphere
    {
        AirData Sample(float altitudeM);
    }

    /// <summary>
    /// The International Standard Atmosphere (U.S. Standard Atmosphere 1976, which agrees with ISA to 32 km): three
    /// layers by geopotential altitude, the troposphere (−6.5 K/km to 11 km), an isothermal layer to 20 km and
    /// +1 K/km to 32 km. Geopotential and geometric altitude differ by under 0.3 % at 20 km; the game treats them as
    /// the same. Altitudes outside −610 m … 32 km are clamped to that range (no jet flies there; L0 says so here).
    /// </summary>
    public static class Isa
    {
        public const double SeaLevelTemperatureK = 288.15;
        public const double SeaLevelPressurePa = 101325.0;
        public const double GasConstant = 287.05287;          // J/(kg·K), dry air
        public const double HeatRatio = 1.4;
        public const double MinAltitudeM = -610.0;
        public const double MaxAltitudeM = 32000.0;

        const double G0 = 9.80665;
        const double Lapse0 = 0.0065;                          // K/m, 0–11 km (falling)
        const double Lapse2 = 0.001;                           // K/m, 20–32 km (rising)
        const double T11 = 216.65;

        public static readonly double SeaLevelDensity = SeaLevelPressurePa / (GasConstant * SeaLevelTemperatureK);
        static readonly double P11 = SeaLevelPressurePa * Math.Pow(T11 / SeaLevelTemperatureK, G0 / (GasConstant * Lapse0));
        static readonly double P20 = P11 * Math.Exp(-G0 * 9000.0 / (GasConstant * T11));

        public static AirData At(double altitudeM)
        {
            double h = altitudeM < MinAltitudeM ? MinAltitudeM : altitudeM > MaxAltitudeM ? MaxAltitudeM : altitudeM;
            double t, p;
            if (h <= 11000.0)
            {
                t = SeaLevelTemperatureK - Lapse0 * h;
                p = SeaLevelPressurePa * Math.Pow(t / SeaLevelTemperatureK, G0 / (GasConstant * Lapse0));
            }
            else if (h <= 20000.0)
            {
                t = T11;
                p = P11 * Math.Exp(-G0 * (h - 11000.0) / (GasConstant * T11));
            }
            else
            {
                t = T11 + Lapse2 * (h - 20000.0);
                p = P20 * Math.Pow(t / T11, -G0 / (GasConstant * Lapse2));
            }
            double rho = p / (GasConstant * t);
            return new AirData((float)rho, (float)p, (float)t, (float)Math.Sqrt(HeatRatio * GasConstant * t));
        }
    }

    /// <summary>Atmosphere L0: the ISA, still air.</summary>
    public sealed class AtmosphereL0 : IAtmosphere
    {
        public AirData Sample(float altitudeM) => Isa.At(altitudeM);
    }
}
