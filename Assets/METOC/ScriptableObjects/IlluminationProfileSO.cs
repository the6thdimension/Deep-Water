using UnityEngine;

namespace DeepWater.Metoc
{
    /// <summary>
    /// The light model's constants (E45, E49, E54), with provenance. Values marked "from memory, verify" are labelled
    /// until each is checked against its source (blueprint §8).
    /// </summary>
    [CreateAssetMenu(menuName = "Deep Water/METOC/Illumination Profile", fileName = "IlluminationProfile")]
    public sealed class IlluminationProfileSO : ScriptableObject
    {
        [Header("Sun (IES clear-sky daylight; from memory, verify)")]
        [Tooltip("Solar illuminance at normal incidence above the air at 1 AU, lux.")]
        public double solarAboveAirLux = 127500.0;
        [Tooltip("Clear-sky extinction of the direct beam per air mass.")]
        public double solarExtinction = 0.21;
        [Tooltip("Clear-sky diffuse horizontal illuminance A + B·(sin h)^C, klux.")]
        public double diffuseA = 0.8, diffuseB = 15.5, diffuseC = 0.5;

        [Header("Twilight (horizontal lux by sun elevation, descending; from memory, verify)")]
        public double[] twilightElevationDeg = { -6.0, -12.0, -18.0 };
        public double[] twilightLux = { 3.4, 0.008, 0.0006 };

        [Header("Moon (Krisciunas & Schaefer 1991)")]
        public double moonFullMagnitude = -12.73;
        public double moonPhaseLinear = 0.026, moonPhaseQuartic = 4e-9;
        [Tooltip("V-band extinction, magnitudes per air mass (their site value).")]
        public double moonExtinctionMag = 0.172;
        public double moonMeanDistanceKm = 384400.0;

        [Header("Night sky (from memory, verify)")]
        [Tooltip("Starlight and airglow on a clear moonless night, lux.")]
        public double nightSkyLux = 0.002;

        [Header("Cloud dimming (Kasten & Czeplak 1980; from memory, verify)")]
        [Tooltip("T = 1 − A·(N/8)^B for N oktas of the main layer; overcast lets 1 − A through.")]
        public double cloudDimmingA = 0.75, cloudDimmingB = 3.4;

        [Header("Rain extinction (E54; from memory, verify)")]
        [Tooltip("Rain's extinction of visible light, a·R^b per km for R mm/h. It adds to the air's ln 20 / visibility.")]
        public double rainExtinctionPerKm = 0.25, rainExtinctionExponent = 0.63;

        [TextArea(3, 8)]
        public string provenance =
            "IES Lighting Handbook clear-sky daylight model (direct E0·exp(-c·m)·sin h + diffuse A + B·sin^C h): from memory, verify. " +
            "Twilight table after Bond & Henderson (1963) / USNO: from memory, verify. " +
            "Moon: Krisciunas & Schaefer (1991), PASP 103, 1033. Air mass: Kasten & Young (1989), Applied Optics 28, 4735. " +
            "Night sky 0.002 lux: from memory, verify. " +
            "Cloud dimming 1 − 0.75·(N/8)^3.4: Kasten & Czeplak (1980), Solar Energy 24, 177; from memory, verify. " +
            "Rain extinction 0.25·R^0.63 per km (R mm/h): from memory, verify; the free-space-optics fit 1.076·R^0.67 dB/km " +
            "(= 0.248·R^0.67 per km) agrees within 10 % up to about 13 mm/h and reads 16 % more at 50 mm/h, also from memory.";

        public IlluminationProfile ToProfile() => new IlluminationProfile
        {
            SolarAboveAirLux = solarAboveAirLux,
            SolarExtinction = solarExtinction,
            DiffuseA = diffuseA,
            DiffuseB = diffuseB,
            DiffuseC = diffuseC,
            TwilightElevationDeg = (double[])twilightElevationDeg.Clone(),
            TwilightLux = (double[])twilightLux.Clone(),
            MoonFullMagnitude = moonFullMagnitude,
            MoonPhaseLinear = moonPhaseLinear,
            MoonPhaseQuartic = moonPhaseQuartic,
            MoonExtinctionMag = moonExtinctionMag,
            MoonMeanDistanceKm = moonMeanDistanceKm,
            NightSkyLux = nightSkyLux,
            CloudDimmingA = cloudDimmingA,
            CloudDimmingB = cloudDimmingB,
            RainExtinctionPerKm = rainExtinctionPerKm,
            RainExtinctionExponent = rainExtinctionExponent,
        };

        void OnValidate()
        {
            if (twilightElevationDeg.Length != twilightLux.Length)
                Debug.LogError($"[METOC] {name}: the twilight table has {twilightElevationDeg.Length} elevations but {twilightLux.Length} lux values.", this);
        }
    }
}
