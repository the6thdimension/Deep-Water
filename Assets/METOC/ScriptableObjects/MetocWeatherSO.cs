using UnityEngine;

namespace DeepWater.Metoc
{
    /// <summary>
    /// A weather preset (slice 2, E30, E51, E52): the main cloud layer, high cloud, and the visibility with the top of
    /// the layer that limits it. A scenario starts from one; the panel blends between them. Values are AUTHORED
    /// archetypes unless the provenance says otherwise.
    /// </summary>
    [CreateAssetMenu(menuName = "Deep Water/METOC/Weather Preset", fileName = "MetocWeather")]
    public sealed class MetocWeatherSO : ScriptableObject
    {
        [Tooltip("The name the panel shows.")]
        public string displayName = "Clear";

        [Header("Main cloud layer")]
        [Tooltip("Cover, oktas: 0 clear, 1–2 FEW, 3–4 SCT, 5–7 BKN, 8 OVC. Sets the ceiling and dims the light (E49).")]
        [Range(0f, 8f)] public double coverOktas;
        [Tooltip("Base, metres above sea level.")]
        [Min(0f)] public double cloudBaseM = 1500.0;
        [Tooltip("Thickness, metres. HDRP draws at least 100 m.")]
        [Min(1f)] public double cloudThicknessM = 1000.0;

        [Header("High cloud (E52)")]
        [Tooltip("Cirrus, oktas. Drawn by the 2D cloud layer; sets neither the ceiling nor the dimming.")]
        [Range(0f, 8f)] public double highCloudOktas;

        [Header("Visibility (E51)")]
        [Tooltip("Meteorological optical range at the surface, metres. Below 1 km is fog, to 5 km mist or haze.")]
        [Min(1f)] public double visibilityM = 30000.0;
        [Tooltip("Top of the fog bank or haze layer, metres.")]
        [Min(1f)] public double obscurationTopM = 1000.0;

        [TextArea(2, 6)]
        public string provenance = "AUTHORED: a game-level archetype, not an observation.";

        public MetocWeather ToWeather() =>
            new MetocWeather(coverOktas, cloudBaseM, cloudThicknessM, highCloudOktas, visibilityM, obscurationTopM);

        void OnValidate()
        {
            string p = ToWeather().Problem();
            if (p != null) Debug.LogError($"[METOC] {name}: {p}; METOC refuses this weather.", this);
        }
    }
}
