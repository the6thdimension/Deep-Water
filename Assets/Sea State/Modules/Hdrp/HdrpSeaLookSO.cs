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
        [Tooltip("Cross-fade near and far fields by √w / √(1 − w) so Hs stays constant through the handover " +
                 "(the fields are independent). Off = linear w / (1 − w), which dips Hs to 71 % mid-band.")]
        public bool energyPreservingHandover = true;

        [Tooltip("Pre-boost each drawn wave by the inverse of HDRP's 3×3 deformation blur at its wavenumber, " +
                 "so the blurred surface matches the physics surface. Rendering only; physics is untouched.")]
        public bool compensateDeformationBlur = true;
        [Tooltip("Upper limit on the per-wave compensation gain (guards waves near the texel Nyquist limit).")]
        [Range(1f, 2f)] public float maxBlurCompensation = 1.5f;

        [Header("Far field (HDRP FFT, statistically matched)")]
        [Tooltip("Measured HDRP Hs over wind × patch size. When valid, the renderer solves it so the far field's Hs " +
                 "equals the sea state's combined Hs (and the two band multipliers below are not used).")]
        public FarFieldCalibration farFieldCalibration = new FarFieldCalibration();
        [Tooltip("Solve the calibration for the far field (off = sea-state wind as-is, band multipliers below).")]
        public bool useFarFieldCalibration = true;
        [Tooltip("Used only without a valid calibration.")]
        [Range(0f, 1f)] public float farBand0Multiplier = 1f;
        [Tooltip("Used only without a valid calibration.")]
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
        [Tooltip("Deep (submerged) foam deposited per second where the near field breaks. Has no effect while " +
                 "the HDRP foam channel workaround is on.")]
        [Min(0f)] public float deepFoamGain = 1f;
        [Tooltip("HDRP 17.4: water-decal graphs write foam as (R surface, G deep) but HDRP reads (G, B), so the " +
                 "decal's surface foam is dropped and its deep foam shows as surface foam. On = send our surface foam " +
                 "(whitecaps + residual) through the deep-foam output; HDRP's deep foam then gets nothing from the " +
                 "decal. Turn off once HDRP reads the channels it writes.")]
        public bool hdrpFoamChannelWorkaround = true;
        [Tooltip("HDRP simulation foam amount for the far field, as a function of whitecap fraction W.")]
        public AnimationCurve farFoamByWhitecapFraction = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.02f, 0.15f), new Keyframe(0.1f, 0.5f), new Keyframe(0.3f, 0.9f), new Keyframe(1f, 1f));

        [Header("Residual foam (streaks along the wind)")]
        [Tooltip("Keep the foam breaking crests leave behind, drift it downwind and gather it into windrows " +
                 "(Langmuir convergence lines) — the 'foam blown in streaks along the direction of the wind' of " +
                 "Beaufort force 7+. Near field only; needs the SeaFoam compute shader on the renderer.")]
        public bool residualFoam = true;
        [Tooltip("Residual foam e-folding lifetime, s. Authored for the look, not sourced: HDRP's own foam buffer " +
                 "fades within 2 s, while foam streaks at sea outlast single whitecaps by far. Long enough that most " +
                 "residual foam reaches a windrow before it fades (gathering takes ~14 s at Douglas 7, ~20 s at 6), " +
                 "so it reads as streaks rather than a sheet.")]
        [Min(0.5f)] public float residualLifetimeS = 60f;
        [Tooltip("Residual foam level produced per second of full breaking. Authored: steady mean ≈ production × " +
                 "lifetime × mean breaking (≈ 0.21 W measured at Douglas 7). 0.3/s × 25 s blanketed Douglas 7 in foam " +
                 "(97 % of the near field ≥ 0.1) and left a seam at the far field; 0.07/s × 60 s keeps the streaks.")]
        [Min(0f)] public float residualProductionPerS = 0.07f;
        [Tooltip("Upper bound on the residual foam level (HDRP renders 1 as solid foam, low levels eroded).")]
        [Range(0.05f, 1f)] public float residualCap = 0.8f;
        [Tooltip("Surface wind drift as a fraction of the wind speed. About 3 % is the usual rule of thumb " +
                 "for wind-driven surface drift (e.g. Wu 1983, J. Phys. Oceanogr. 13).")]
        [Range(0f, 0.06f)] public float windDriftFraction = 0.03f;
        [Tooltip("Distance between windrows (Langmuir convergence lines), m. Observed from a few metres to a few " +
                 "hundred (Thorpe 2004, Annu. Rev. Fluid Mech. 36); the value is authored. Keep ≥ 4 foam texels.")]
        [Min(2f)] public float windrowSpacingM = 18f;
        [Tooltip("Cross-wind speed toward the windrows at full strength, as a fraction of the wind speed. Authored " +
                 "(order of 1 %).")]
        [Range(0f, 0.05f)] public float windrowConvergenceFraction = 0.012f;
        [Tooltip("Windrow strength 0–1 by wind speed, m/s. Default follows the Beaufort descriptions: streaks begin " +
                 "at force 7 (13.9 m/s), are well marked at 8 (17.2 m/s) and dense from 9 (20.8 m/s).")]
        public AnimationCurve windrowStrengthByWindMs = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(10.8f, 0.15f), new Keyframe(13.9f, 0.5f), new Keyframe(17.2f, 0.85f), new Keyframe(20.8f, 1f));
        [Tooltip("Spacing varies by ± this fraction across the wind (drifting slowly along it), so lines bunch, " +
                 "spread and wander instead of forming a regular grid. Authored.")]
        [Range(0f, 0.6f)] public float windrowSpacingVariation = 0.35f;
        [Tooltip("Convergence varies by up to this fraction along each line, so streaks come in segments of uneven " +
                 "strength. Authored.")]
        [Range(0f, 1f)] public float windrowStrengthVariation = 0.5f;
        [Tooltip("Lateral meander of the windrows, m. Capped so every point still flows to its nearest line " +
                 "(1 − spacing variation − 3.55 × meander / spacing > 0).")]
        [Min(0f)] public float windrowMeanderM = 2.5f;
        [Tooltip("Along-wind wavelength of the meander, m.")]
        [Min(1f)] public float windrowMeanderWavelengthM = 90f;
        [Tooltip("Longest render step the integrator takes, s (a hitch must not throw foam across the grid).")]
        [Range(0.005f, 0.25f)] public float residualMaxStepS = 0.1f;

        [Header("Culling")]
        [Tooltip("Crest height bound handed to HDRP (maximumHeightOverride), per metre of combined Hs.")]
        [Min(0f)] public float crestBoundPerHs = 1.5f;

        [Header("Verification")]
        [Tooltip("Keep HDRP's own script interactions (GPU readback) on — only needed to cross-check render vs physics.")]
        public bool hdrpReadbackForVerification = false;

        void OnValidate()
        {
            if (handoverEnd < handoverStart) handoverEnd = handoverStart;
            // Beyond this the warp and meander fold the windrow coordinate (see ResidualFoamMath).
            float maxMeander = (0.9f - windrowSpacingVariation) * windrowSpacingM / 3.55f;
            if (windrowMeanderM > maxMeander) windrowMeanderM = maxMeander;
        }
    }
}
