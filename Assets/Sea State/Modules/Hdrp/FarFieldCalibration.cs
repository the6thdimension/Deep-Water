using System;
using Unity.Mathematics;
using UnityEngine;

namespace DeepWater.Sea.Modules.Hdrp
{
    /// <summary>
    /// What HDRP's own ocean actually produces: significant wave height of its FFT bands
    /// (band multipliers = 1) measured over a grid of wind speed × swell patch size. HDRP's
    /// Phillips spectrum is driven by wind and patch size, not by Hs, so the far field is
    /// matched to a sea state by solving this table — counted, not declared.
    ///
    /// Filled by <c>Deep Water → Sea State → Calibrate Far Field (play mode)</c>. Valid only for
    /// the HDRP settings recorded in <see cref="provenance"/> (chaos, simulation resolution).
    /// </summary>
    [Serializable]
    public class FarFieldCalibration
    {
        [Tooltip("Wind speed axis, km/h (ascending).")]
        public float[] windKmh = Array.Empty<float>();
        [Tooltip("Swell patch (repetition) size axis, m (ascending).")]
        public float[] repetitionM = Array.Empty<float>();
        [Tooltip("Measured Hs (4σ of surface height), m, row-major [wind, repetition].")]
        public float[] hsM = Array.Empty<float>();
        [Tooltip("HDRP 'chaos' the table was measured at; a different chaos needs a new calibration.")]
        public float chaos;
        [TextArea(2, 6)] public string provenance = "";

        public bool IsValid =>
            windKmh != null && repetitionM != null && hsM != null &&
            windKmh.Length >= 2 && repetitionM.Length >= 2 && hsM.Length == windKmh.Length * repetitionM.Length;

        /// <summary>Measured Hs at (wind, repetition), bilinear in wind and log(repetition), clamped to the grid.</summary>
        public float Hs(float windKmhValue, float repetition)
        {
            float wi = AxisIndex(windKmh, windKmhValue, false);
            float ri = AxisIndex(repetitionM, repetition, true);
            int w0 = (int)math.floor(wi), r0 = (int)math.floor(ri);
            int w1 = math.min(w0 + 1, windKmh.Length - 1), r1 = math.min(r0 + 1, repetitionM.Length - 1);
            float fw = wi - w0, fr = ri - r0;
            float a = math.lerp(At(w0, r0), At(w0, r1), fr);
            float b = math.lerp(At(w1, r0), At(w1, r1), fr);
            return math.lerp(a, b, fw);
        }

        /// <summary>
        /// HDRP wind and band multiplier (≤ 1) that make the far field's Hs equal
        /// <paramref name="targetHsM"/> at the given patch size. Keeps the sea state's own wind
        /// when HDRP can reach the target with it (multiplier only scales down); otherwise raises
        /// the wind along the measured table until it can. Returns false if even the strongest
        /// measured wind falls short (multiplier is then 1 at that wind).
        /// </summary>
        public bool Solve(float targetHsM, float repetition, float seaStateWindKmh, out float windOut, out float multiplier)
        {
            windOut = seaStateWindKmh;
            multiplier = 0f;
            if (targetHsM <= 0f) return true;

            float hs = Hs(windOut, repetition);
            if (hs >= targetHsM) { multiplier = targetHsM / hs; return true; }

            // Walk up the wind axis to the first measured wind that reaches the target, then
            // interpolate within that cell.
            float prevWind = windOut, prevHs = hs;
            for (int i = 0; i < windKmh.Length; i++)
            {
                if (windKmh[i] <= prevWind) continue;
                float h = Hs(windKmh[i], repetition);
                if (h >= targetHsM)
                {
                    float f = h > prevHs ? (targetHsM - prevHs) / (h - prevHs) : 1f;
                    windOut = math.lerp(prevWind, windKmh[i], math.saturate(f));
                    float hAt = Hs(windOut, repetition);
                    multiplier = hAt > 0f ? math.min(1f, targetHsM / hAt) : 1f;
                    return true;
                }
                prevWind = windKmh[i];
                prevHs = h;
            }
            windOut = windKmh[windKmh.Length - 1];
            multiplier = 1f;
            return false;
        }

        float At(int w, int r) => hsM[w * repetitionM.Length + r];

        static float AxisIndex(float[] axis, float value, bool logarithmic)
        {
            if (value <= axis[0]) return 0f;
            int last = axis.Length - 1;
            if (value >= axis[last]) return last;
            for (int i = 0; i < last; i++)
            {
                if (value > axis[i + 1]) continue;
                float a = axis[i], b = axis[i + 1], v = value;
                if (logarithmic) { a = math.log(a); b = math.log(b); v = math.log(v); }
                return i + (v - a) / (b - a);
            }
            return last;
        }
    }
}
