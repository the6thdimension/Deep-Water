using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace DeepWater.Sea.Modules.Hdrp
{
    /// <summary>
    /// How the simulated sea is drawn by HDRP — rendering tuning only. Physical values live in
    /// the sea-state profiles; nothing here changes what ships feel, only what you see.
    /// </summary>
    [CreateAssetMenu(fileName = "HdrpSeaLook", menuName = "Deep Water/Sea State/HDRP Sea Look", order = 20)]
    public class HdrpSeaLookSO : ScriptableObject
    {
        [Header("Near field (phase-locked to physics)")]
        [Tooltip("Side of the camera-following square where HDRP draws the simulated waves, m.")]
        [Min(64f)] public float nearFieldSizeM = 1024f;

        [Tooltip("Texels per side of the near-field decal. The decal takes TWO atlas regions (waves + foam), so " +
                 "it must fit twice in the HDRP asset's water decal atlas: 512 for a 1024 atlas, 1024 for a 2048 atlas.")]
        public WaterSurface.WaterDecalRegionResolution nearFieldResolution = WaterSurface.WaterDecalRegionResolution.Resolution512;

        [Tooltip("Where the near field starts handing over to HDRP's own bands (0 = centre, 1 = edge, Chebyshev).")]
        [Range(0f, 1f)] public float handoverStart = 0.6f;
        [Tooltip("Where the handover completes. Keep below 1: HDRP zeroes decal deformation at the region edge.")]
        [Range(0f, 1f)] public float handoverEnd = 0.95f;

        [Header("Far field (HDRP FFT, statistically matched)")]
        [Range(0f, 1f)] public float farBand0Multiplier = 1f;
        [Range(0f, 1f)] public float farBand1Multiplier = 1f;
        [Tooltip("HDRP swell patch size per dominant wavelength (clamped to HDRP's 250–5000 m).")]
        [Min(1f)] public float repetitionPerPeakWavelength = 3f;
        [Tooltip("HDRP 'chaos' — directional dampening of its Phillips spectrum.")]
        [Range(0f, 1f)] public float farChaos = 0.85f;

        [Header("Ripples (HDRP capillary band, everywhere)")]
        [Tooltip("Ripple wind = sea-state wind × this, clamped to HDRP's 15 km/h maximum.")]
        [Min(0f)] public float ripplesWindScale = 1f;
        [Range(0f, 1f)] public float ripplesChaos = 0.8f;

        [Header("Foam")]
        [Tooltip("Surface foam deposited per second where the near field breaks (Jacobian below threshold).")]
        [Min(0f)] public float surfaceFoamGain = 4f;
        [Tooltip("Deep (submerged) foam deposited per second where the near field breaks.")]
        [Min(0f)] public float deepFoamGain = 1f;
        [Tooltip("HDRP simulation foam amount for the far field, as a function of whitecap fraction W.")]
        public AnimationCurve farFoamByWhitecapFraction = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.02f, 0.15f), new Keyframe(0.1f, 0.5f), new Keyframe(0.3f, 0.9f), new Keyframe(1f, 1f));

        [Header("Culling")]
        [Tooltip("Crest height bound handed to HDRP (maximumHeightOverride), per metre of combined Hs.")]
        [Min(0f)] public float crestBoundPerHs = 1.5f;

        [Header("Verification")]
        [Tooltip("Keep HDRP's own script interactions (GPU readback) on — only needed to cross-check render vs physics.")]
        public bool hdrpReadbackForVerification = false;

        void OnValidate()
        {
            if (handoverEnd < handoverStart) handoverEnd = handoverStart;
        }
    }
}
