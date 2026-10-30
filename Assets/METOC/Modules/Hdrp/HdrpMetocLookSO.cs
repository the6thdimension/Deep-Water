using UnityEngine;

namespace DeepWater.Metoc.Hdrp
{
    /// <summary>
    /// How METOC's reckoned sky is drawn in HDRP (E39, E40, E42, E43, E46). Every value here is LOOK: it changes the
    /// picture, never the report. The reckoned numbers stay what they are.
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
                 "instead of being lifted to mid-grey.")]
        public float adaptationBiasEv = 2f;
        [Tooltip("The lowest the floor goes, EV100 (a clear moonless night meters about −8).")]
        public float absoluteMinEv = -6f;

        [Header("Overlay volume (E42)")]
        [Tooltip("Priority of METOC's runtime volume; above the shared Sky and Fog Volume (0).")]
        public float overlayPriority = 100f;

        [Header("Redraw (E46)")]
        [Tooltip("Move the drawn sun and moon only when the reckoned body has moved more than this, degrees. " +
                 "0 = every frame. Open until measured (E46).")]
        [Min(0f)] public float redrawQuantumDeg;
    }
}
