using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
        [Tooltip("SeaFoam.compute — integrates the residual foam (streaks along the wind). Empty = whitecaps only.")]
        [SerializeField] ComputeShader foamCompute;

        static readonly int s_ComponentsId = Shader.PropertyToID("_DWSeaComponents");
        static readonly int s_CountId = Shader.PropertyToID("_DWSeaComponentCount");
        static readonly int s_RegionId = Shader.PropertyToID("_DWSeaRegion");
        static readonly int s_FadeId = Shader.PropertyToID("_DWSeaFade");
        static readonly int s_FoamId = Shader.PropertyToID("_DWSeaFoam");
        static readonly int s_FoamResidualId = Shader.PropertyToID("_DWSeaFoamResidual");
        static readonly int s_FoamResidualDisplayId = Shader.PropertyToID("_DWSeaFoamResidualDisplay");
        static readonly int s_FoamPrevId = Shader.PropertyToID("_DWSeaFoamPrev");
        static readonly int s_FoamNextId = Shader.PropertyToID("_DWSeaFoamNext");
        static readonly int s_FoamGridId = Shader.PropertyToID("_DWSeaFoamGrid");
        static readonly int s_FoamShiftId = Shader.PropertyToID("_DWSeaFoamShift");
        static readonly int s_FoamStepId = Shader.PropertyToID("_DWSeaFoamStep");
        static readonly int s_FoamFlowId = Shader.PropertyToID("_DWSeaFoamFlow");
        static readonly int s_FoamWindrowId = Shader.PropertyToID("_DWSeaFoamWindrow");
        static readonly int s_FoamWindrow2Id = Shader.PropertyToID("_DWSeaFoamWindrow2");
        static readonly int s_FoamResolutionId = Shader.PropertyToID("_DWSeaFoamResolution");
        static readonly int s_FoamDepositsId = Shader.PropertyToID("_DWSeaFoamDeposits");
        static readonly int s_FoamDepositCountId = Shader.PropertyToID("_DWSeaFoamDepositCount");
        static readonly ProfilerMarker s_UploadMarker = new ProfilerMarker("DeepWater.Sea.Render.Upload");
        static readonly ProfilerMarker s_FoamMarker = new ProfilerMarker("DeepWater.Sea.Render.ResidualFoam");

        /// <summary>GPU sample around the residual-foam dispatch (Profiler GPU module, <c>Recorder</c>).</summary>
        public const string ResidualFoamGpuSample = "DeepWater.Sea.ResidualFoam";
        // A ProfilingSampler collects GPU time; a plain CommandBuffer.BeginSample(string) does not.
        static readonly ProfilingSampler s_FoamGpuSampler = new ProfilingSampler(ResidualFoamGpuSample);

        const int MaskResolution = 256;

        WaterSurface _surface;
        WaterDecal _decal;
        Transform _anchor;
        GraphicsBuffer _componentBuffer;
        NativeArray<SeaFrameComponent> _frame;
        Texture2D _mask;
        bool _ready;

        // Residual foam: two R32F fields over the near-field region, ping-ponged by the compute pass.
        RenderTexture _foamA, _foamB;
        bool _foamFlip, _foamHasPrevious;
        int _foamKernel, _foamDepositKernel;
        // Foam deposits queued since the last step (DepositFoam); at most MaxFoamDepositsPerStep, the rest dropped and counted.
        public const int MaxFoamDepositsPerStep = 128;
        readonly FoamDeposit[] _deposits = new FoamDeposit[MaxFoamDepositsPerStep];
        readonly Vector4[] _depositData = new Vector4[MaxFoamDepositsPerStep];
        int _depositCount;
        ComputeBuffer _depositBuffer;
        CommandBuffer _foamCmd;
        ResidualFoamGrid _foamGrid;
        readonly int[] _shiftArgs = new int[2];
        double _foamPrevTimeS;
        double _foamAlongOffsetM;
        List<ResidualFoamStepRecord> _foamRecord;
        int _foamRecordLimit;

        /// <summary>Components drawn last frame (doubles during a sea-state cross-fade).</summary>
        public int DrawnComponentCount { get; private set; }

        /// <summary>True when the distant sea is solved from a measured calibration (Hs matched).</summary>
        public bool FarFieldCalibrated { get; private set; }
        /// <summary>Wind handed to HDRP's FFT bands, km/h (may exceed the sea-state wind to reach Hs).</summary>
        public float FarFieldWindKmh { get; private set; }
        /// <summary>Band multiplier handed to HDRP's FFT bands.</summary>
        public float FarFieldMultiplier { get; private set; }

        /// <summary>True when the residual-foam integrator runs (look enables it and the compute shader is assigned).</summary>
        public bool ResidualFoamActive => _foamA != null;
        /// <summary>The residual foam field drawn this frame (R32F over the near-field region; UV x = world X, y = world Z).</summary>
        public RenderTexture ResidualFoamTexture => _foamFlip ? _foamB : _foamA;
        /// <summary>Foam deposits dropped because more than <see cref="MaxFoamDepositsPerStep"/> came in before one step.</summary>
        public int DroppedFoamDeposits { get; private set; }

        /// <summary>
        /// Put foam into the water at <paramref name="worldPosition"/> (a splash, an impact, a wake): up to
        /// <paramref name="level"/> at the centre, a Gaussian of sigma = <paramref name="radiusM"/> / 2, added after the next
        /// residual-foam step. It then drifts and fades like the sea's own foam. Outside the near-field region it does
        /// nothing; with residual foam off it is ignored. Presentation only: the sea's physics never reads foam.
        /// </summary>
        public void DepositFoam(Vector3 worldPosition, float radiusM, float level)
        {
            if (_foamA == null || radiusM <= 0f || level <= 0f) return;
            if (_depositCount >= MaxFoamDepositsPerStep) { DroppedFoamDeposits++; return; }
            _deposits[_depositCount++] = new FoamDeposit { Centre = new float2(worldPosition.x, worldPosition.z), RadiusM = radiusM, Level = level };
        }

        /// <summary>Inputs of the latest residual-foam step.</summary>
        public ResidualFoamParams LastResidualFoamParams { get; private set; }
        /// <summary>Centre (world XZ) of the residual foam grid — it drifts with the water, within a texel of the near field.</summary>
        public float2 ResidualFoamCentre => _foamGrid.CentreWorld;

        /// <summary>Near-field decal texel size, m.</summary>
        public float NearFieldTexelM => look != null ? look.nearFieldSizeM / (int)look.nearFieldResolution : 0f;
        /// <summary>Centre (world XZ) of the near-field region drawn this frame — also HDRP's decal and foam region.</summary>
        public float2 NearFieldCentre { get; private set; }

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
            Shader.SetGlobalVector(s_FoamResidualDisplayId, Vector4.zero);
            if (look.residualFoam) CreateResidualFoam();

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
            ReleaseResidualFoam();
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
                NearFieldCentre = centre;
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
                Shader.SetGlobalVector(s_FadeId, new Vector4(look.handoverStart, look.handoverEnd,
                    look.energyPreservingHandover ? 1f : 0f, 0f));
                Shader.SetGlobalVector(s_FoamId, new Vector4(director.Simulation.LastBake.FoamJacobianThreshold,
                    look.surfaceFoamGain, look.deepFoamGain, look.hdrpFoamChannelWorkaround ? 1f : 0f));
                DrawnComponentCount = n;
            }

            if (_foamA != null) StepResidualFoam(SnappedCentre(), DrawnComponentCount);
        }

        void CreateResidualFoam()
        {
            if (foamCompute == null)
            {
                Debug.LogWarning($"[HdrpSeaRenderer] '{name}': residual foam is on in the look but no SeaFoam compute shader is assigned; whitecaps only.", this);
                return;
            }
            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogWarning("[HdrpSeaRenderer] Compute shaders are not supported here; residual foam (wind streaks) is off.", this);
                return;
            }
            _foamKernel = foamCompute.FindKernel("DWSeaFoamIntegrate");
            _foamDepositKernel = foamCompute.FindKernel("DWSeaFoamDeposit");
            _depositBuffer = new ComputeBuffer(MaxFoamDepositsPerStep, 16);
            int res = (int)look.nearFieldResolution;
            _foamA = NewFoamTexture(res, "Sea Residual Foam A");
            _foamB = NewFoamTexture(res, "Sea Residual Foam B");
            _foamCmd = new CommandBuffer { name = "Deep Water Residual Foam" };
            ClearResidualFoam();
        }

        static RenderTexture NewFoamTexture(int res, string label)
        {
            var rt = new RenderTexture(res, res, 0, GraphicsFormat.R32_SFloat)
            {
                name = label,
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            rt.Create();
            return rt;
        }

        void ReleaseResidualFoam()
        {
            Shader.SetGlobalVector(s_FoamResidualDisplayId, Vector4.zero);
            if (_foamA != null) { _foamA.Release(); Destroy(_foamA); _foamA = null; }
            if (_foamB != null) { _foamB.Release(); Destroy(_foamB); _foamB = null; }
            _foamCmd?.Release();
            _foamCmd = null;
            _depositBuffer?.Release();
            _depositBuffer = null;
            _depositCount = 0;
            _foamRecord = null;
        }

        /// <summary>Zero both residual foam fields; the next step starts from clear water.</summary>
        public void ClearResidualFoam()
        {
            if (_foamA == null) return;
            _foamCmd.Clear();
            _foamCmd.SetRenderTarget(_foamA);
            _foamCmd.ClearRenderTarget(false, true, Color.clear);
            _foamCmd.SetRenderTarget(_foamB);
            _foamCmd.ClearRenderTarget(false, true, Color.clear);
            Graphics.ExecuteCommandBuffer(_foamCmd);
            _foamCmd.Clear();
            _foamHasPrevious = false;
        }

        /// <summary>
        /// One residual-foam step on the GPU (SeaFoam.compute, twin of ResidualFoamStepJob) on the
        /// render clock. The decal then feeds the field into HDRP's foam buffer at the rate that
        /// holds that buffer at the residual level.
        /// </summary>
        void StepResidualFoam(float2 centre, int componentCount)
        {
            // A full verification recording freezes the field until it has been read back.
            if (_foamRecord != null && _foamRecord.Count >= _foamRecordLimit) return;

            using (s_FoamMarker.Auto())
            {
                double t = director.RenderTimeS;
                float dt = _foamHasPrevious ? (float)math.clamp(t - _foamPrevTimeS, 0.0, look.residualMaxStepS) : 0f;
                ResidualFoamParams p = ResidualFoamParamsFor(director.Conditions, director.Simulation.LastBake.FoamJacobianThreshold, dt);
                RenderTexture prev = _foamFlip ? _foamB : _foamA, next = _foamFlip ? _foamA : _foamB;
                int res = (int)look.nearFieldResolution;

                // The grid drifts with the water and re-centres on the near field in whole texels.
                if (!_foamHasPrevious) _foamGrid = ResidualFoamGrid.At(centre, res, look.nearFieldSizeM);
                int2 shift = _foamGrid.Advance(p.DriftMs * dt * p.Downwind, centre);
                float2 gridCentre = _foamGrid.CentreWorld;

                CommandBuffer cmd = _foamCmd;
                cmd.Clear();
                s_FoamGpuSampler.Begin(cmd);
                cmd.SetComputeBufferParam(foamCompute, _foamKernel, s_ComponentsId, _componentBuffer);
                cmd.SetComputeIntParam(foamCompute, s_CountId, componentCount);
                cmd.SetComputeIntParam(foamCompute, s_FoamResolutionId, res);
                cmd.SetComputeVectorParam(foamCompute, s_FoamGridId, new Vector4(gridCentre.x, gridCentre.y, look.nearFieldSizeM, 0f));
                _shiftArgs[0] = shift.x;
                _shiftArgs[1] = shift.y;
                cmd.SetComputeIntParams(foamCompute, s_FoamShiftId, _shiftArgs);
                cmd.SetComputeVectorParam(foamCompute, s_FoamId, new Vector4(p.JacobianThreshold, p.BreakingGain, 0f, 0f));
                cmd.SetComputeVectorParam(foamCompute, s_FoamStepId, new Vector4(dt, p.LifetimeS, p.ProductionPerS, p.Cap));
                cmd.SetComputeVectorParam(foamCompute, s_FoamFlowId, new Vector4(p.Downwind.x, p.Downwind.y, p.DriftMs, p.ConvergenceMs));
                cmd.SetComputeVectorParam(foamCompute, s_FoamWindrowId, new Vector4(p.SpacingM, p.MeanderM, p.MeanderWavelengthM, p.AlongOffsetM));
                cmd.SetComputeVectorParam(foamCompute, s_FoamWindrow2Id, new Vector4(p.SpacingVariation, p.StrengthVariation, 0f, 0f));
                cmd.SetComputeTextureParam(foamCompute, _foamKernel, s_FoamPrevId, prev);
                cmd.SetComputeTextureParam(foamCompute, _foamKernel, s_FoamNextId, next);
                int groups = (res + 7) / 8;
                cmd.DispatchCompute(foamCompute, _foamKernel, groups, groups, 1);
                if (_depositCount > 0)
                {
                    for (int k = 0; k < _depositCount; k++)
                        _depositData[k] = new Vector4(_deposits[k].Centre.x, _deposits[k].Centre.y, _deposits[k].RadiusM, _deposits[k].Level);
                    cmd.SetBufferData(_depositBuffer, _depositData, 0, 0, _depositCount);
                    cmd.SetComputeBufferParam(foamCompute, _foamDepositKernel, s_FoamDepositsId, _depositBuffer);
                    cmd.SetComputeIntParam(foamCompute, s_FoamDepositCountId, _depositCount);
                    cmd.SetComputeTextureParam(foamCompute, _foamDepositKernel, s_FoamNextId, next);
                    cmd.DispatchCompute(foamCompute, _foamDepositKernel, groups, groups, 1);
                }
                s_FoamGpuSampler.End(cmd);
                Graphics.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                _foamFlip = !_foamFlip;
                float2 toFoamUv = (centre - gridCentre) / look.nearFieldSizeM;
                Shader.SetGlobalTexture(s_FoamResidualId, next);
                Shader.SetGlobalVector(s_FoamResidualDisplayId, new Vector4(1f / HdrpFoamLifetimeS(), toFoamUv.x, toFoamUv.y, 0f));

                _foamRecord?.Add(new ResidualFoamStepRecord
                {
                    Params = p,
                    DtS = dt,
                    Centre = gridCentre,
                    Shift = shift,
                    Components = _frame.GetSubArray(0, componentCount).ToArray(),
                    Deposits = CopyDeposits(),
                });
                _depositCount = 0;
                LastResidualFoamParams = p;
                _foamPrevTimeS = t;
                _foamHasPrevious = true;
            }
        }

        FoamDeposit[] CopyDeposits()
        {
            var copy = new FoamDeposit[_depositCount];
            System.Array.Copy(_deposits, copy, _depositCount);
            return copy;
        }

        /// <summary>Residual-foam inputs for this step: the sea conditions mapped through the look.</summary>
        ResidualFoamParams ResidualFoamParamsFor(in SeaConditions c, float jacobianThreshold, float dt)
        {
            float wind = c.WindSpeedMs;
            float travel = SeaUnits.TravelDirectionRad(c.WindFromDeg);
            float drift = look.windDriftFraction * wind;
            float strength = math.saturate(look.windrowStrengthByWindMs.Evaluate(wind));

            // The windrow pattern drifts with the surface; integrated (not drift × t) so a change of
            // sea state does not make it jump. Wrapped to 2× the meander wavelength, which every
            // along-wind period of the pattern divides (ResidualFoamParams.MeanderWavelengthM).
            double lambda = 2.0 * look.windrowMeanderWavelengthM;
            _foamAlongOffsetM += drift * dt;
            _foamAlongOffsetM -= math.floor(_foamAlongOffsetM / lambda) * lambda;

            return new ResidualFoamParams
            {
                Downwind = new float2(math.cos(travel), math.sin(travel)),
                DriftMs = drift,
                ConvergenceMs = look.windrowConvergenceFraction * wind * strength,
                SpacingM = look.windrowSpacingM,
                MeanderM = look.windrowMeanderM,
                MeanderWavelengthM = look.windrowMeanderWavelengthM,
                SpacingVariation = look.windrowSpacingVariation,
                StrengthVariation = look.windrowStrengthVariation,
                AlongOffsetM = (float)_foamAlongOffsetM,
                LifetimeS = look.residualLifetimeS,
                ProductionPerS = look.residualProductionPerS,
                Cap = look.residualCap,
                JacobianThreshold = jacobianThreshold,
                BreakingGain = look.surfaceFoamGain,
            };
        }

        /// <summary>
        /// Lifetime of HDRP's own foam buffer, s: it decays by exp(−dt · 0.5 / lerp(0.05, 1, persistence))
        /// per frame (HDRenderPipeline.WaterSystem.cs; WaterDecal.shader, FoamAttenuation pass).
        /// </summary>
        float HdrpFoamLifetimeS() => 2f * math.lerp(0.05f, 1f, math.saturate(_surface.foamPersistenceMultiplier));

        /// <summary>
        /// Record the next <paramref name="steps"/> residual-foam steps (inputs and wave frame) from a
        /// cleared field, then freeze the field until <see cref="EndResidualFoamRecording"/> — so a
        /// verification tool can read the GPU field back and replay the same steps on the CPU twin.
        /// </summary>
        public void BeginResidualFoamRecording(int steps)
        {
            if (_foamA == null) return;
            ClearResidualFoam();
            _foamRecord = new List<ResidualFoamStepRecord>(steps);
            _foamRecordLimit = steps;
        }

        /// <summary>Steps recorded so far (null when not recording).</summary>
        public IReadOnlyList<ResidualFoamStepRecord> ResidualFoamRecord => _foamRecord;

        /// <summary>Stop recording and resume integration.</summary>
        public void EndResidualFoamRecording() => _foamRecord = null;

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

            float repetition = math.clamp(look.repetitionPerPeakWavelength * peakWavelength, 250f, 5000f);
            float farWindKmh = windKmh, band0 = look.farBand0Multiplier, band1 = look.farBand1Multiplier;
            FarFieldCalibration cal = look.farFieldCalibration;
            FarFieldCalibrated = look.useFarFieldCalibration && cal != null && cal.IsValid;
            if (FarFieldCalibrated)
            {
                if (!cal.Solve(c.SignificantWaveHeightM, repetition, windKmh, out farWindKmh, out float m))
                    Debug.LogWarning($"[HdrpSeaRenderer] Far field cannot reach Hs {c.SignificantWaveHeightM:0.0} m within the calibrated winds; using the strongest.", this);
                band0 = band1 = m;
                if (!Mathf.Approximately(cal.chaos, look.farChaos))
                    Debug.LogWarning($"[HdrpSeaRenderer] Far-field calibration was measured at chaos {cal.chaos:0.##}, look uses {look.farChaos:0.##}; recalibrate.", this);
            }
            else if (c.SignificantWaveHeightM > 0f && look.useFarFieldCalibration)
            {
                Debug.LogWarning("[HdrpSeaRenderer] No far-field calibration: HDRP's distant sea uses the sea-state wind as-is and its Hs is not matched. " +
                                 "Run Deep Water → Sea State → Calibrate Far Field (play mode).", this);
            }
            FarFieldWindKmh = farWindKmh;
            FarFieldMultiplier = band0;

            _surface.largeWindSpeed = math.clamp(farWindKmh, 0f, 250f);
            _surface.largeOrientationValue = travelDeg;
            _surface.largeChaos = look.farChaos;
            _surface.largeBand0Multiplier = c.SignificantWaveHeightM > 0f ? band0 : 0f;
            _surface.largeBand1Multiplier = c.SignificantWaveHeightM > 0f ? band1 : 0f;
            _surface.repetitionSize = repetition;
            _surface.ripplesWindSpeed = math.clamp(windKmh * look.ripplesWindScale, 0f, 15f);
            _surface.ripplesChaos = look.ripplesChaos;
            _surface.simulationFoamAmount = math.saturate(look.farFoamByWhitecapFraction.Evaluate(c.WhitecapFraction));
            _surface.maximumHeightOverride = look.crestBoundPerHs * c.SignificantWaveHeightM;
        }

        void BuildMask()
        {
            // R, G: amplitude of HDRP's swell bands 0 and 1. The near field (SeaWaves.hlsl) scales its
            // waves by √w and the bands get √(1 − w): the two fields are independent, so variance —
            // and Hs — stays constant through the handover instead of dipping to 71 % mid-band.
            // B: ripples, always on. Clamped, so outside the region it is 1.
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
                float farAmplitude = look.energyPreservingHandover ? math.sqrt(1f - near) : 1f - near;
                byte far = (byte)math.round(255f * farAmplitude);
                pixels[y * MaskResolution + x] = new Color32(far, far, 255, 255);
            }
            _mask.SetPixels32(pixels);
            _mask.Apply(false, true);
        }
    }
}
