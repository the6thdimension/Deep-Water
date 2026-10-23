using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using DeepWater.Sea.Core;

namespace DeepWater.Sea.Modules.Hdrp
{
    /// <summary>
    /// Draws the simulated sea with HDRP Water. HDRP keeps its shading, foam, underwater and
    /// caustics; this component feeds it the waves.
    ///
    /// **Near field.** A camera-following HDRP Water Decal evaluates the same wave components
    /// as physics, on the same clock (<see cref="SeaStateDirector.RenderTimeS"/>), so the
    /// wave under a hull on screen is the wave the hull is riding.
    /// **Handover.** Towards the decal edge the simulated waves fade out while a water mask
    /// fades HDRP's own swell bands in, so the far field — statistically matched to the sea
    /// state, not phase-locked — carries on to the horizon.
    /// **Ripples.** HDRP's capillary band stays on everywhere for detail shorter than the
    /// simulated cut-off.
    ///
    /// Requires the HDRP asset to support water decals; horizontal deformation needs
    /// "Water → Horizontal Deformation" enabled, otherwise crests render vertical-only.
    /// The WaterSurface must sit at the world origin with no rotation (mask and decal are
    /// placed in world XZ).
    /// </summary>
    [RequireComponent(typeof(WaterSurface))]
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class HdrpSeaRenderer : MonoBehaviour
    {
        [SerializeField] SeaStateDirector director;
        [SerializeField] HdrpSeaLookSO look;
        [Tooltip("Material made from SeaWaveDecal.shadergraph.")]
        [SerializeField] Material waveDecalMaterial;
        [Tooltip("What the near field follows. Empty = Camera.main.")]
        [SerializeField] Transform followTarget;

        static readonly int s_ComponentsId = Shader.PropertyToID("_DWSeaComponents");
        static readonly int s_CountId = Shader.PropertyToID("_DWSeaComponentCount");
        static readonly int s_RegionId = Shader.PropertyToID("_DWSeaRegion");
        static readonly int s_FadeId = Shader.PropertyToID("_DWSeaFade");
        static readonly int s_FoamId = Shader.PropertyToID("_DWSeaFoam");
        static readonly ProfilerMarker s_UploadMarker = new ProfilerMarker("DeepWater.Sea.Render.Upload");

        const int MaskResolution = 256;

        WaterSurface _surface;
        WaterDecal _decal;
        Transform _anchor;
        GraphicsBuffer _componentBuffer;
        NativeArray<SeaFrameComponent> _frame;
        Texture2D _mask;
        bool _ready;

        /// <summary>Components drawn last frame (doubles during a sea-state cross-fade).</summary>
        public int DrawnComponentCount { get; private set; }

        /// <summary>Near-field decal texel size, m.</summary>
        public float NearFieldTexelM => look != null ? look.nearFieldSizeM / (int)look.nearFieldResolution : 0f;

        void OnEnable()
        {
            _surface = GetComponent<WaterSurface>();
            if (director == null || look == null || waveDecalMaterial == null)
            {
                Debug.LogError($"[HdrpSeaRenderer] '{name}' needs a SeaStateDirector, an HdrpSeaLook and the wave decal material; rendering disabled.", this);
                enabled = false;
                return;
            }

            var asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            if (asset != null)
            {
                var settings = asset.currentPlatformRenderPipelineSettings;
                if (!settings.supportWater)
                    Debug.LogError("[HdrpSeaRenderer] The active HDRP asset has water disabled — no sea will render on this quality level.", this);
                if (!settings.supportWaterDecals)
                    Debug.LogError("[HdrpSeaRenderer] The active HDRP asset has water decals disabled — simulated waves cannot be drawn.", this);
                if (!settings.supportWaterHorizontalDeformation)
                    Debug.LogWarning("[HdrpSeaRenderer] Horizontal water deformation is off in the HDRP asset — crests render vertical-only (rounder than the physics).", this);
                // The decal occupies two square atlas regions (deformation + foam).
                int res = (int)look.nearFieldResolution, atlas = (int)settings.waterDecalAtlasSize;
                if (2 * res * res > atlas * atlas || res > atlas)
                    Debug.LogError($"[HdrpSeaRenderer] Near-field decal {res}² needs two atlas regions but the HDRP water decal atlas is {atlas}²; " +
                                   $"use {atlas / 2} texels or raise the atlas to {res * 2}.", this);
            }

            float minWavelength = director.Library != null ? director.Library.minResolvedWavelengthM : 0f;
            if (minWavelength < 5f * NearFieldTexelM)
                Debug.LogWarning($"[HdrpSeaRenderer] Simulated waves down to {minWavelength:0.#} m but the near-field texel is {NearFieldTexelM:0.##} m; " +
                                 "waves under ~5 texels will alias. Raise minResolvedWavelengthM or the decal resolution.", this);

            _frame = new NativeArray<SeaFrameComponent>(2 * SeaSimulation.MaxComponentsPerState, Allocator.Persistent);
            _componentBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _frame.Length,
                UnsafeUtility.SizeOf<SeaFrameComponent>());

            BuildMask();
            CreateNearField();
            ConfigureSurface();

            director.ConditionsChanged += ApplyConditions;
            if (director.IsReady) ApplyConditions(director.Conditions);
            _ready = true;
        }

        void OnDisable()
        {
            _ready = false;
            if (director != null) director.ConditionsChanged -= ApplyConditions;
            Shader.SetGlobalInt(s_CountId, 0);

            _componentBuffer?.Release();
            _componentBuffer = null;
            if (_frame.IsCreated) _frame.Dispose();
            if (_anchor != null) Destroy(_anchor.gameObject);
            if (_decal != null) Destroy(_decal.gameObject);
            if (_mask != null) Destroy(_mask);
            if (_surface != null)
            {
                _surface.decalRegionAnchor = null;
                _surface.waterMask = null;
                _surface.simulationMask = false;
            }
        }

        void LateUpdate()
        {
            if (!_ready || !director.IsReady) return;

            using (s_UploadMarker.Auto())
            {
                float2 centre = SnappedCentre();
                Vector3 p = new Vector3(centre.x, transform.position.y, centre.y);
                _anchor.position = p;
                _decal.transform.position = p;
                _surface.waterMaskOffset = new Vector2(centre.x, centre.y);

                int n = director.Simulation.BuildFrame(director.RenderTimeS, _frame);
                float texel = NearFieldTexelM;
                float bound = 0f;
                for (int i = 0; i < n; i++)
                {
                    SeaFrameComponent c = _frame[i];
                    if (look.compensateDeformationBlur)
                    {
                        c.B.w = DeformationBlurCompensation(c.A.z * c.A.x * texel, c.A.z * c.A.y * texel, look.maxBlurCompensation);
                        _frame[i] = c;
                    }
                    bound += c.B.w * math.max(math.abs(c.A.w), math.abs(c.B.x));
                }
                if (bound <= 0f) bound = 1f;

                _decal.amplitude = bound;
                if (n > 0) _componentBuffer.SetData(_frame, 0, 0, n);

                Shader.SetGlobalBuffer(s_ComponentsId, _componentBuffer);
                Shader.SetGlobalInt(s_CountId, n);
                Shader.SetGlobalVector(s_RegionId, new Vector4(centre.x, centre.y, look.nearFieldSizeM, 1f / bound));
                Shader.SetGlobalVector(s_FadeId, new Vector4(look.handoverStart, look.handoverEnd, 0f, 0f));
                Shader.SetGlobalVector(s_FoamId, new Vector4(director.Simulation.LastBake.FoamJacobianThreshold,
                    look.surfaceFoamGain, look.deepFoamGain, 0f));
                DrawnComponentCount = n;
            }
        }

        /// <summary>
        /// Inverse of HDRP's deformation blur for one plane wave. HDRP filters the deformation
        /// region with a 3×3 kernel w = exp(−r²/0.8²) (WaterDeformation.compute, FilterDeformation),
        /// which is separable; per axis a wave advancing θ radians per texel keeps
        /// H(θ) = (1 + 2a cos θ) / (1 + 2a), a = exp(−1/0.64). Gain = 1 / (H(θx) H(θz)), capped.
        /// </summary>
        public static float DeformationBlurCompensation(float thetaX, float thetaZ, float maxGain)
        {
            const float A = 0.20961139f;   // exp(-1 / 0.64)
            float hx = (1f + 2f * A * math.cos(thetaX)) / (1f + 2f * A);
            float hz = (1f + 2f * A * math.cos(thetaZ)) / (1f + 2f * A);
            return math.min(1f / math.max(hx * hz, 1e-3f), maxGain);
        }

        // Snap to whole texels so the decal grid does not crawl under a moving camera.
        float2 SnappedCentre()
        {
            Transform target = followTarget != null ? followTarget : (Camera.main != null ? Camera.main.transform : transform);
            float texel = NearFieldTexelM;
            float3 t = target.position;
            return math.round(new float2(t.x, t.z) / texel) * texel;
        }

        void CreateNearField()
        {
            _anchor = new GameObject("Sea Near Field Anchor") { hideFlags = HideFlags.DontSave }.transform;
            _anchor.SetParent(transform, false);

            var decalGo = new GameObject("Sea Wave Decal") { hideFlags = HideFlags.DontSave };
            decalGo.transform.SetParent(transform, false);
            decalGo.SetActive(false);
            _decal = decalGo.AddComponent<WaterDecal>();
            _decal.scaleMode = DecalScaleMode.ScaleInvariant;
            _decal.regionSize = new Vector2(look.nearFieldSizeM, look.nearFieldSizeM);
            int res = (int)look.nearFieldResolution;
            _decal.resolution = new Vector2Int(res, res);
            _decal.updateMode = CustomRenderTextureUpdateMode.Realtime;
            _decal.material = waveDecalMaterial;
            _decal.amplitude = 1f;
            _decal.surfaceFoamDimmer = 1f;
            _decal.deepFoamDimmer = 1f;
            decalGo.SetActive(true);
        }

        void ConfigureSurface()
        {
            _surface.deformation = true;
            _surface.deformationRes = look.nearFieldResolution;
            _surface.decalRegionSize = new Vector2(look.nearFieldSizeM, look.nearFieldSizeM);
            _surface.decalRegionAnchor = _anchor;
            _surface.simulationMask = true;
            _surface.waterMask = _mask;
            _surface.waterMaskExtent = new Vector2(look.nearFieldSizeM, look.nearFieldSizeM);
            _surface.waterMaskRemap = new Vector2(0f, 1f);
            _surface.ripples = true;
            _surface.foam = true;
            _surface.scriptInteractions = look.hdrpReadbackForVerification;
        }

        /// <summary>Map the sea conditions onto HDRP's far-field and ripple parameters.</summary>
        void ApplyConditions(SeaConditions c)
        {
            if (_surface == null) return;

            const float KmhPerMs = 3.6f;
            float windKmh = c.WindSpeedMs * KmhPerMs;

            // HDRP orientation: degrees in world XZ (0 = +X), the direction its waves travel.
            float travelDeg = math.degrees(SeaUnits.TravelDirectionRad(c.WindFromDeg));
            float dominantTp = math.max(c.WindSeaTpS, c.SwellHsM > 0f ? c.SwellTpS : 0f);
            float peakWavelength = (float)WaveSpectrum.DeepWaterWavelength(dominantTp, math.max(director.Gravity, 0.01f));

            _surface.largeWindSpeed = math.clamp(windKmh, 0f, 250f);
            _surface.largeOrientationValue = travelDeg;
            _surface.largeChaos = look.farChaos;
            _surface.largeBand0Multiplier = c.SignificantWaveHeightM > 0f ? look.farBand0Multiplier : 0f;
            _surface.largeBand1Multiplier = c.SignificantWaveHeightM > 0f ? look.farBand1Multiplier : 0f;
            _surface.repetitionSize = math.clamp(look.repetitionPerPeakWavelength * peakWavelength, 250f, 5000f);
            _surface.ripplesWindSpeed = math.clamp(windKmh * look.ripplesWindScale, 0f, 15f);
            _surface.ripplesChaos = look.ripplesChaos;
            _surface.simulationFoamAmount = math.saturate(look.farFoamByWhitecapFraction.Evaluate(c.WhitecapFraction));
            _surface.maximumHeightOverride = look.crestBoundPerHs * c.SignificantWaveHeightM;
        }

        void BuildMask()
        {
            // R, G: weight of HDRP's swell bands 0 and 1 = 1 − near-field weight (same smoothstep
            // as SeaWaves.hlsl). B: ripples, always on. Clamped, so outside the region it is 1.
            _mask = new Texture2D(MaskResolution, MaskResolution, TextureFormat.RGBA32, false, true)
            {
                name = "Sea Near Field Mask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[MaskResolution * MaskResolution];
            for (int y = 0; y < MaskResolution; y++)
            for (int x = 0; x < MaskResolution; x++)
            {
                float2 uv = (new float2(x, y) + 0.5f) / MaskResolution;
                float2 c = math.abs(uv - 0.5f) * 2f;
                float r = math.max(c.x, c.y);
                float near = 1f - math.smoothstep(look.handoverStart, look.handoverEnd, r);
                byte far = (byte)math.round(255f * (1f - near));
                pixels[y * MaskResolution + x] = new Color32(far, far, 255, 255);
            }
            _mask.SetPixels32(pixels);
            _mask.Apply(false, true);
        }
    }
}
