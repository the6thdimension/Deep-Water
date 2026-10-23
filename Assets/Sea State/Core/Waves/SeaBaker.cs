using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace DeepWater.Sea.Core
{
    /// <summary>Everything needed to bake one sea state into wave components.</summary>
    public struct SeaBakeInput
    {
        public WaveSystemSpec WindSea;
        public WaveSystemSpec Swell;
        /// <summary>Shortest wavelength the wave field carries, m (set by what the renderer can draw).</summary>
        public float MinWavelengthM;
        public float Gravity;
        /// <summary>Target whitecap area fraction for the foam threshold calibration.</summary>
        public float WhitecapFraction;
        /// <summary>Grid resolution of the foam calibration (per side).</summary>
        public int FoamCalibrationGrid;
    }

    /// <summary>What a bake produced — the numbers the inspector and the log show.</summary>
    public struct SeaBakeReport
    {
        public float WindSeaTargetHsM, WindSeaResolvedHsM;
        public float SwellTargetHsM, SwellResolvedHsM;
        public int WindSeaCount, SwellCount;
        /// <summary>Σ aᵢ: an exact upper bound on |height|, used to normalise the GPU decal.</summary>
        public float AmplitudeSum;
        /// <summary>Σ Qᵢ aᵢ: an exact upper bound on |horizontal displacement|.</summary>
        public float HorizontalAmplitudeSum;
        /// <summary>Jacobian below which the surface is whitecap (see <see cref="FoamCalibrationJob"/>).</summary>
        public float FoamJacobianThreshold;
        /// <summary>Fraction of sampled surface that folds (J &lt; 0) — over-steep crests.</summary>
        public float FoldedFraction;

        public int Count => WindSeaCount + SwellCount;
        public float TargetHsM => math.sqrt(WindSeaTargetHsM * WindSeaTargetHsM + SwellTargetHsM * SwellTargetHsM);
        public float ResolvedHsM => math.sqrt(WindSeaResolvedHsM * WindSeaResolvedHsM + SwellResolvedHsM * SwellResolvedHsM);
        /// <summary>Share of the target variance (Hs²) the wave field carries, 0–1.</summary>
        public float ResolvedVarianceFraction
        {
            get
            {
                float t = TargetHsM;
                return t > 0f ? (ResolvedHsM * ResolvedHsM) / (t * t) : 1f;
            }
        }
    }

    /// <summary>
    /// Turns a <see cref="SeaBakeInput"/> into wave components: the wind sea first, then the
    /// swell, then the whitecap calibration. Synchronous and Burst-compiled throughout, so a
    /// bake is reproducible bit for bit.
    /// </summary>
    public static class SeaBaker
    {
        static readonly ProfilerMarker s_BakeMarker = new ProfilerMarker("DeepWater.Sea.Bake");

        /// <summary>
        /// Bake into <paramref name="output"/> (must hold WindSea + Swell component counts).
        /// Returns the report; components occupy [0, report.Count).
        /// </summary>
        public static SeaBakeReport Bake(in SeaBakeInput input, NativeArray<WaveComponent> output)
        {
            using (s_BakeMarker.Auto())
            {
                var report = new SeaBakeReport();
                var scratch = new NativeArray<float>(3, Allocator.TempJob);
                try
                {
                    new SpectrumSampleJob
                    {
                        Spec = input.WindSea,
                        MinWavelengthM = input.MinWavelengthM,
                        Gravity = input.Gravity,
                        OutputOffset = 0,
                        Output = output,
                        Report = scratch,
                    }.Run();
                    report.WindSeaTargetHsM = scratch[0];
                    report.WindSeaResolvedHsM = scratch[1];
                    report.WindSeaCount = (int)scratch[2];

                    new SpectrumSampleJob
                    {
                        Spec = input.Swell,
                        MinWavelengthM = input.MinWavelengthM,
                        Gravity = input.Gravity,
                        OutputOffset = report.WindSeaCount,
                        Output = output,
                        Report = scratch,
                    }.Run();
                    report.SwellTargetHsM = scratch[0];
                    report.SwellResolvedHsM = scratch[1];
                    report.SwellCount = (int)scratch[2];

                    for (int i = 0; i < report.Count; i++)
                    {
                        report.AmplitudeSum += output[i].Amplitude;
                        report.HorizontalAmplitudeSum += output[i].Amplitude * output[i].Choppiness;
                    }

                    // Calibrate over four dominant wavelengths and one dominant period.
                    float dominantTp = math.max(
                        report.WindSeaCount > 0 ? input.WindSea.PeakPeriodS : 0f,
                        report.SwellCount > 0 ? input.Swell.PeakPeriodS : 0f);
                    float extent = 4f * (float)WaveSpectrum.DeepWaterWavelength(dominantTp, input.Gravity);

                    new FoamCalibrationJob
                    {
                        Components = output,
                        ComponentCount = report.Count,
                        WhitecapFraction = input.WhitecapFraction,
                        ExtentM = math.max(extent, 1f),
                        Grid = input.FoamCalibrationGrid,
                        TimeSamples = 4,
                        PeriodS = dominantTp,
                        Result = scratch,
                    }.Run();
                    report.FoamJacobianThreshold = scratch[0];
                    report.FoldedFraction = scratch[1];
                }
                finally
                {
                    scratch.Dispose();
                }
                return report;
            }
        }
    }
}
