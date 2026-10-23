namespace DeepWater.Sea.Core
{
    /// <summary>
    /// The sea as other systems see it — the interchange struct for sensors (radar sea
    /// clutter, sonar ambient noise, EO/IR whitecap glint), AI and UI. Published by the sea
    /// director whenever the sea state changes.
    ///
    /// Two heights are reported on purpose: the profile's sourced Hs, and the Hs the
    /// simulated wave field actually carries (waves shorter than the resolved cut-off live in
    /// HDRP's ripple band only). The difference is shown, not hidden.
    /// </summary>
    public struct SeaConditions
    {
        /// <summary>Douglas wind-sea degree 0–9 (WMO code 3700).</summary>
        public int DouglasSea;
        /// <summary>Douglas swell degree 0–9.</summary>
        public int DouglasSwell;

        /// <summary>Wind-sea significant wave height from the profile, m.</summary>
        public float WindSeaHsM;
        /// <summary>Wind-sea peak period, s.</summary>
        public float WindSeaTpS;
        /// <summary>Swell significant wave height from the profile, m.</summary>
        public float SwellHsM;
        /// <summary>Swell peak period, s.</summary>
        public float SwellTpS;

        /// <summary>Combined significant wave height √(Hw² + Hs²), m — the number an observer reports.</summary>
        public float SignificantWaveHeightM;
        /// <summary>Combined Hs actually carried by the resolved wave field, m.</summary>
        public float ResolvedWaveHeightM;

        /// <summary>Sustained wind speed, m/s (NATO STANAG 4194 table mean for the degree).</summary>
        public float WindSpeedMs;
        /// <summary>Direction the wind blows FROM, compass degrees (0 = from +Z / north, 90 = from +X / east).</summary>
        public float WindFromDeg;
        /// <summary>Direction the swell comes FROM, compass degrees.</summary>
        public float SwellFromDeg;

        /// <summary>Whitecap area fraction 0–1 (Monahan &amp; O'Muircheartaigh 1980).</summary>
        public float WhitecapFraction;
    }
}
