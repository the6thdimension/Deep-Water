namespace DeepWater.GeneralQuarters.Core.Ballistics
{
    /// <summary>
    /// The G7 standard drag function (boat-tailed projectile, 7-calibre ogive): drag coefficient against Mach number.
    /// Source: US Army Ballistic Research Laboratory; McCoy, "McDrag", ARBRL-TR-02293 (1981); public domain.
    /// Values as published in the ballistics-engine data set (g7.csv). A projectile's drag coefficient is its form
    /// factor times this curve (G41), with the form factor CALIBRATED to the projectile's published range.
    /// </summary>
    public static class DragCurve
    {
        static readonly float[] Mach =
        {
            0.00f, 0.05f, 0.10f, 0.15f, 0.20f, 0.25f, 0.30f, 0.35f, 0.40f, 0.45f, 0.50f, 0.55f, 0.60f, 0.65f, 0.70f, 0.725f,
            0.75f, 0.775f, 0.80f, 0.825f, 0.85f, 0.875f, 0.90f, 0.925f, 0.95f, 0.975f, 1.00f, 1.025f, 1.05f, 1.075f, 1.10f,
            1.125f, 1.15f, 1.20f, 1.25f, 1.30f, 1.35f, 1.40f, 1.50f, 1.55f, 1.60f, 1.65f, 1.70f, 1.75f, 1.80f, 1.85f, 1.90f,
            1.95f, 2.00f, 2.05f, 2.10f, 2.15f, 2.20f, 2.25f, 2.30f, 2.35f, 2.40f, 2.45f, 2.50f, 2.55f, 2.60f, 2.65f, 2.70f,
            2.75f, 2.80f, 2.85f, 2.90f, 2.95f, 3.00f, 3.10f, 3.20f, 3.30f, 3.40f, 3.50f, 3.60f, 3.70f, 3.80f, 3.90f, 4.00f,
            4.20f, 4.40f, 4.60f, 4.80f, 5.00f,
        };

        static readonly float[] Cd =
        {
            0.1198f, 0.1197f, 0.1196f, 0.1194f, 0.1193f, 0.1194f, 0.1194f, 0.1194f, 0.1193f, 0.1193f, 0.1194f, 0.1193f, 0.1194f,
            0.1197f, 0.1202f, 0.1207f, 0.1215f, 0.1226f, 0.1242f, 0.1266f, 0.1306f, 0.1368f, 0.1464f, 0.1660f, 0.2054f, 0.2993f,
            0.3803f, 0.4015f, 0.4043f, 0.4034f, 0.4014f, 0.3987f, 0.3955f, 0.3884f, 0.3810f, 0.3732f, 0.3657f, 0.3580f, 0.3440f,
            0.3376f, 0.3315f, 0.3260f, 0.3209f, 0.3160f, 0.3117f, 0.3078f, 0.3042f, 0.3010f, 0.2980f, 0.2951f, 0.2922f, 0.2892f,
            0.2864f, 0.2835f, 0.2807f, 0.2779f, 0.2752f, 0.2725f, 0.2697f, 0.2670f, 0.2643f, 0.2615f, 0.2588f, 0.2561f, 0.2533f,
            0.2506f, 0.2479f, 0.2451f, 0.2424f, 0.2368f, 0.2313f, 0.2258f, 0.2205f, 0.2154f, 0.2106f, 0.2060f, 0.2017f, 0.1975f,
            0.1935f, 0.1861f, 0.1793f, 0.1730f, 0.1672f, 0.1618f,
        };

        /// <summary>G7 drag coefficient at <paramref name="mach"/>, linearly interpolated; held flat beyond Mach 5.</summary>
        public static float G7(float mach)
        {
            if (mach <= 0f) return Cd[0];
            if (mach >= Mach[Mach.Length - 1]) return Cd[Cd.Length - 1];
            int lo = 0, hi = Mach.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (Mach[mid] <= mach) lo = mid; else hi = mid;
            }
            float f = (mach - Mach[lo]) / (Mach[hi] - Mach[lo]);
            return Cd[lo] + (Cd[hi] - Cd[lo]) * f;
        }
    }
}
