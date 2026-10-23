using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeepWater.Sea.Core
{
    /// <summary>
    /// Builds the per-instant component list from the current sea state and, during a
    /// sea-state change, the outgoing one. The cross-fade is energy-preserving: amplitudes
    /// scale by √w and √(1 − w), so variance blends linearly instead of dipping mid-change.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
    public struct SeaFrameBuildJob : IJob
    {
        [ReadOnly] public NativeArray<WaveComponent> Current;
        public int CurrentCount;
        public float CurrentScale;

        [ReadOnly] public NativeArray<WaveComponent> Previous;
        public int PreviousCount;
        public float PreviousScale;

        public double TimeS;

        [WriteOnly] public NativeArray<SeaFrameComponent> Output;
        /// <summary>[0] = number of components written.</summary>
        public NativeArray<int> OutputCount;

        public void Execute()
        {
            int n = 0;
            if (CurrentScale > 0f)
                for (int i = 0; i < CurrentCount; i++)
                    Output[n++] = SeaFieldMath.Prepare(Current[i], TimeS, CurrentScale);
            if (PreviousScale > 0f)
                for (int i = 0; i < PreviousCount; i++)
                    Output[n++] = SeaFieldMath.Prepare(Previous[i], TimeS, PreviousScale);
            OutputCount[0] = n;
        }
    }

    /// <summary>
    /// Batch sea query: for each world position, the water surface height directly above or
    /// below it, plus normal, particle velocity and Jacobian. Each index is independent, so
    /// the parallel result is identical to a serial one (deterministic).
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
    public struct SeaQueryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SeaFrameComponent> Components;
        public int ComponentCount;
        public float SeaLevel;
        public int InverseIterations;

        [ReadOnly] public NativeArray<float3> Positions;
        [WriteOnly] public NativeArray<SeaSample> Results;

        public void Execute(int index)
        {
            float3 p = Positions[index];
            float2 x0 = SeaFieldMath.InverseHorizontal(Components, ComponentCount, p.xz, InverseIterations);
            SeaFieldMath.Evaluate(Components, ComponentCount, x0,
                out float height, out _, out float3 velocity, out float3 normal, out float jacobian);

            Results[index] = new SeaSample
            {
                SurfaceHeight = SeaLevel + height,
                Normal = normal,
                Velocity = velocity,
                Jacobian = jacobian,
            };
        }
    }

    /// <summary>
    /// Calibrates whitecaps from the wind rather than by eye: samples the surface Jacobian
    /// over a grid at several instants and picks the threshold below which exactly the target
    /// whitecap fraction of the surface lies. Foam coverage is therefore counted from the wind
    /// (Monahan's W(U)) and placed by the wave geometry (where crests actually compress).
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
    public struct FoamCalibrationJob : IJob
    {
        [ReadOnly] public NativeArray<WaveComponent> Components;
        public int ComponentCount;
        public float WhitecapFraction;
        public float ExtentM;
        public int Grid;
        public int TimeSamples;
        public double PeriodS;

        /// <summary>[0] Jacobian threshold, [1] folded fraction (J &lt; 0).</summary>
        public NativeArray<float> Result;

        /// <summary>
        /// Threshold meaning "no whitecaps": far below any real Jacobian, but finite so the GPU
        /// foam term (threshold − J) × gain can never become NaN (−∞ × 0).
        /// </summary>
        public const float NoFoamThreshold = -1e9f;

        public void Execute()
        {
            Result[0] = NoFoamThreshold;
            Result[1] = 0f;
            if (ComponentCount <= 0 || Grid <= 0 || TimeSamples <= 0) return;

            var frame = new NativeArray<SeaFrameComponent>(ComponentCount, Allocator.Temp);
            int samplesPerTime = Grid * Grid;
            var jacobians = new NativeArray<float>(samplesPerTime * TimeSamples, Allocator.Temp);
            float spacing = ExtentM / Grid;
            int folded = 0;
            int n = 0;

            for (int t = 0; t < TimeSamples; t++)
            {
                double time = PeriodS * t / TimeSamples;
                for (int i = 0; i < ComponentCount; i++)
                    frame[i] = SeaFieldMath.Prepare(Components[i], time, 1f);

                for (int y = 0; y < Grid; y++)
                for (int x = 0; x < Grid; x++)
                {
                    float2 x0 = new float2(x * spacing, y * spacing);
                    SeaFieldMath.Evaluate(frame, ComponentCount, x0, out _, out _, out _, out _, out float j);
                    jacobians[n++] = j;
                    if (j < 0f) folded++;
                }
            }

            jacobians.Sort();
            if (WhitecapFraction > 0f)
            {
                int q = math.clamp((int)(WhitecapFraction * n), 0, n - 1);
                Result[0] = jacobians[q];
            }
            Result[1] = (float)folded / n;

            jacobians.Dispose();
            frame.Dispose();
        }
    }
}
