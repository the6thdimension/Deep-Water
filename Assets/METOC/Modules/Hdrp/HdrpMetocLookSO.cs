using UnityEngine;

namespace DeepWater.Metoc.Hdrp
{
    /// <summary>
    /// How METOC's reckoned sky is drawn in HDRP (E39, E40, E42, E43, E46, E48–E52). Every value here is LOOK: it
    /// changes the picture, never the report. The reckoned numbers stay what they are.
    /// </summary>
    [CreateAssetMenu(menuName = "Deep Water/METOC/HDRP Look", fileName = "HdrpMetocLook")]
    public sealed class HdrpMetocLookSO : ScriptableObject
    {
        [Header("Lights (E39)")]
        [Tooltip("LOOK. The sun light carries the reckoned above-air illuminance times this. 1.49 keeps SIX's 190 000 lux " +
                 "sun (the shared Sun prefab) over the public 127 500 lux; set 1 for the public value.")]
        [Min(0f)] public float sunLookMultiplier = 1.49f;
        [Tooltip("LOOK. The same for the moon light; 1 = the reckoned moonlight.")]
        [Min(0f)] public float moonLookMultiplier = 1f;
        [Tooltip("LOOK. The moon light's colour temperature, K (moonlight is sunlight reddened a little; from memory, verify).")]
        [Range(1500f, 20000f)] public float moonColorTemperatureK = 4100f;

        [Header("Exposure floor (E40)")]
        [Tooltip("The floor by day: SIX's shared-sky limitMin, EV100. The floor never rises above it.")]
        public float dayFloorEv = 10.021231f;
        [Tooltip("LOOK. The floor sits this far above the metered ground light, EV, so night reads as night " +
                 "instead of being lifted to mid-grey. 4 (E50, SIX, 2026-10-30): darker than slice 1's 2.")]
        public float adaptationBiasEv = 4f;
        [Tooltip("The lowest the floor goes, EV100 (a clear moonless night meters about −8).")]
        public float absoluteMinEv = -6f;

        [Header("Overlay volume (E42)")]
        [Tooltip("Priority of METOC's runtime volume; above the shared Sky and Fog Volume (0).")]
        public float overlayPriority = 100f;

        [Header("Redraw (E46)")]
        [Tooltip("Move the drawn sun and moon only when the reckoned body has moved more than this, degrees. " +
                 "0 = every visible move, about 0.02° (E46, locked at 0 by SIX, 2026-10-30).")]
        [Min(0f)] public float redrawQuantumDeg;

        [Header("Clouds (E30, E31, E48)")]
        [Tooltip("The measured cover table (Deep Water → METOC → Calibrate Cloud Cover). Empty or bad: the renderer " +
                 "draws an UNCALIBRATED linear guess and says so once.")]
        public CloudCoverCalibration cloudCover = new CloudCoverCalibration();
        [Tooltip("LOOK (E31). Volumetric cloud ray-march steps; 0 keeps the shared sky's (229). 128 (SIX, 2026-10-30): " +
                 "0.92 ms against 1.78 ms at 229 on a broken sky, 2560×1440, no visible difference at eye level.")]
        [Range(0, 1024)] public int cloudPrimarySteps = 128;
        [Tooltip("LOOK (E31). Volumetric cloud light steps; 0 keeps the shared sky's (13). 8 (SIX, 2026-10-30), with 128 above.")]
        [Range(0, 32)] public int cloudLightSteps = 8;

        [Header("Fog (E51)")]
        [Tooltip("LOOK. The share of the obscuration top that is uniform fog; above it the fog thins to nothing at the top.")]
        [Range(0.05f, 1f)] public float fogUniformFraction = 0.5f;
        [Tooltip("LOOK. Fog is computed out to this many visibilities (a beam keeps 1 % at 1.5).")]
        [Min(1f)] public float fogDistanceFactor = 1.5f;
        [Tooltip("LOOK. Fog is computed at least this far, metres (HDRP's default 5 000).")]
        [Min(0f)] public float fogMinDistanceM = 5000f;

        [Header("High cloud (E52)")]
        [Tooltip("LOOK. The 2D cloud layer's opacity at 8 oktas of high cloud; it scales linearly with the oktas. " +
                 "Opacity is not coverage: this is a look, not a measured match.")]
        [Range(0f, 1f)] public float highCloudOpacityAtEightOktas = 1f;

        [Header("Rain clouds (E55)")]
        [Tooltip("LOOK. Rain darkens the drawn clouds (their sun and ambient light dimmers) by up to this share. The " +
                 "reckoned light stays Kasten & Czeplak on the cover (SIX, 2026-10-30: drawn darker only).")]
        [Range(0f, 1f)] public float rainCloudDimMax = 0.6f;
        [Tooltip("LOOK. The rain rate that darkens the clouds by half the maximum, mm/h.")]
        [Min(0.01f)] public float rainCloudDimHalfRateMmPerHour = 8f;

        [Header("Rain drops (E53, MetocRain)")]
        [Tooltip("LOOK. Drops drawn at 10 mm/h. A real 10 mm/h rain holds about 3 000 drops per cubic metre (Marshall–" +
                 "Palmer; from memory, verify), far more than can be drawn, so the count is a look.")]
        [Min(0f)] public float rainDropsAt10MmPerHour = 40000f;
        [Tooltip("LOOK. The drawn count goes as (rate / 10 mm/h) to this power, capped by the graph's capacity.")]
        [Range(0f, 2f)] public float rainDropsExponent = 0.6f;
        [Tooltip("LOOK. The box of drops around the camera, metres (x, height, z). Drops wrap inside it.")]
        public Vector3 rainBoxSizeM = new Vector3(40f, 24f, 40f);
        [Tooltip("LOOK. How fast the drawn drops fall, m/s (a 2 mm drop falls about 6.5 m/s, Gunn & Kinzer 1949; from " +
                 "memory, verify). Straight down until wind (slice 4).")]
        [Min(0.1f)] public float rainFallSpeedMps = 6.5f;
        [Tooltip("LOOK. A drawn drop's width, metres.")]
        [Min(0.0001f)] public float rainDropWidthM = 0.004f;
        [Tooltip("LOOK. A drawn drop's streak length, metres (the drop's fall during an eye's or a camera's exposure).")]
        [Min(0.0001f)] public float rainStreakLengthM = 0.4f;
        [Tooltip("LOOK. A drop's luminance as a multiple of E/π, where E is the reckoned ground light (a sky of uniform " +
                 "luminance L gives E = πL). 3 (slice 3 spike): 0.6 vanished against the sky.")]
        [Min(0f)] public float rainLuminanceFactor = 3f;
        [Tooltip("LOOK. A drop's opacity.")]
        [Range(0f, 1f)] public float rainAlpha = 0.4f;
        [Tooltip("LOOK. Drops nearer the camera than this are fully faded, metres.")]
        [Min(0f)] public float rainFadedDistanceM = 0.3f;
        [Tooltip("LOOK. Drops farther than this are fully drawn, metres.")]
        [Min(0f)] public float rainVisibleDistanceM = 1.5f;
    }
}
