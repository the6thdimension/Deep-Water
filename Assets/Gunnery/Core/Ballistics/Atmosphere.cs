using UnityEngine;

namespace DeepWater.Gunnery.Ballistics
{
    /// <summary>
    /// The International Standard Atmosphere (ISO 2533): the troposphere to 11 km, the isothermal layer above.
    /// Density and the speed of sound at an altitude, for drag. Pure, and tabulated every 50 m so the firing-table
    /// integration does not call <c>Pow</c> on every step.
    /// </summary>
    public static class Atmosphere
    {
        public const float SeaLevelDensityKgM3 = 1.225f;
        const float T0 = 288.15f, P0 = 101325f, Lapse = 0.0065f, R = 287.053f, G0 = 9.80665f, Gamma = 1.4f;
        const float TropopauseM = 11000f, TropopauseT = 216.65f;
        const float StepM = 50f, TopM = 30000f;

        static readonly float[] s_density, s_soundSpeed;

        static Atmosphere()
        {
            int n = Mathf.CeilToInt(TopM / StepM) + 1;
            s_density = new float[n];
            s_soundSpeed = new float[n];
            for (int i = 0; i < n; i++)
            {
                Exact(i * StepM, out s_density[i], out s_soundSpeed[i]);
            }
        }

        /// <summary>The ISA formulas themselves: density (kg/m³) and speed of sound (m/s) at <paramref name="altitudeM"/>.</summary>
        public static void Exact(float altitudeM, out float density, out float soundSpeed)
        {
            double h = altitudeM, t, p;
            if (h <= TropopauseM)
            {
                t = T0 - Lapse * h;
                p = P0 * System.Math.Pow(t / T0, G0 / (Lapse * R));
            }
            else
            {
                double p11 = P0 * System.Math.Pow(TropopauseT / T0, G0 / (Lapse * R));
                t = TropopauseT;
                p = p11 * System.Math.Exp(-G0 * (h - TropopauseM) / (R * t));
            }
            density = (float)(p / (R * t));
            soundSpeed = (float)System.Math.Sqrt(Gamma * R * t);
        }

        /// <summary>Density and speed of sound from the 50 m table, linearly interpolated; clamped to 0–30 km.</summary>
        public static void At(float altitudeM, out float density, out float soundSpeed)
        {
            float u = Mathf.Clamp(altitudeM, 0f, TopM) / StepM;
            int i = Mathf.Min((int)u, s_density.Length - 2);
            float f = u - i;
            density = s_density[i] + (s_density[i + 1] - s_density[i]) * f;
            soundSpeed = s_soundSpeed[i] + (s_soundSpeed[i + 1] - s_soundSpeed[i]) * f;
        }
    }
}
