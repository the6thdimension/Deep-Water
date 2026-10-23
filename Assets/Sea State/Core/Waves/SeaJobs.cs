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
    /// Writes the GPU/reference layout (<see cref="SeaFrameComponent"/>) and, when asked, the
    /// packed SIMD layout (<see cref="SeaComponentGroup"/>) used by CPU queries.
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
        public bool WritePacked;
        [WriteOnly] public NativeArray<SeaComponentGroup> Packed;
        /// <summary>[0] = components written, [1] = packed groups written.</summary>
        public NativeArray<int> OutputCount;

        public void Execute()
        {
            int n = 0;
            var group = default(SeaComponentGroup);
            if (CurrentScale > 0f)
                for (int i = 0; i < CurrentCount; i++)
                    Emit(SeaFieldMath.Prepare(Current[i], TimeS, CurrentScale), ref n, ref group);
            if (PreviousScale > 0f)
                for (int i = 0; i < PreviousCount; i++)
                    Emit(SeaFieldMath.Prepare(Previous[i], TimeS, PreviousScale), ref n, ref group);

            if (WritePacked && (n & 3) != 0) Packed[n >> 2] = group;   // flush the partial group
            OutputCount[0] = n;
            OutputCount[1] = SeaComponentGroup.GroupCount(n);
        }

        void Emit(in SeaFrameComponent c, ref int n, ref SeaComponentGroup group)
        {
            Output[n] = c;
            if (WritePacked)
            {
                int lane = n & 3;
                if (lane == 0) group = default;
                group.SetLane(lane, c);
                if (lane == 3) Packed[n >> 2] = group;
            }
            n++;
        }
    }

    /// <summary>
    /// Batch sea query on the packed SIMD layout: for each world position, the water surface
    /// height directly above or below it, plus normal, particle velocity and Jacobian. Each
    /// index is independent, so the parallel result equals a serial one (deterministic).
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
    public struct SeaQueryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SeaComponentGroup> Groups;
        public int GroupCount;
        public float SeaLevel;
        public int InverseIterations;

        [ReadOnly] public NativeArray<float3> Positions;
        [WriteOnly] public NativeArray<SeaSample> Results;

        public void Execute(int index)
        {
            float3 p = Positions[index];
            float2 x0 = SeaFieldPacked.InverseHorizontal(Groups, GroupCount, p.xz, InverseIterations);
            SeaFieldPacked.Evaluate(Groups, GroupCount, x0,
                out float height, out float3 velocity, out float3 normal, out float jacobian);

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
    /// The reference query on the plain component layout (<see cref="SeaFieldMath"/>) — the
    /// same layout and math the GPU decal uses. Kept as the fidelity yardstick for the packed
    /// kernel and for render-vs-physics verification; not used on the hot path.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
    public struct SeaQueryReferenceJob : IJobParallelFor
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
    /// Whitecap calibration, step 1 (parallel): the surface Jacobian on a grid at several
    /// instants. Groups hold one packed frame per instant, back to back.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
    public struct FoamJacobianSampleJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SeaComponentGroup> Groups;
        public int GroupsPerFrame;
        public int Grid;
        public float Spacing;

        [WriteOnly] public NativeArray<float> Jacobians;

        public void Execute(int index)
        {
            int perTime = Grid * Grid;
            int t = index / perTime;
            int cell = index - t * perTime;
            int y = cell / Grid, x = cell - y * Grid;
            Jacobians[index] = SeaFieldPacked.Jacobian(Groups, t * GroupsPerFrame, GroupsPerFrame,
                new float2(x * Spacing, y * Spacing));
        }
    }

    /// <summary>
    /// Whitecap calibration, step 2: the Jacobian threshold below which exactly the target
    /// whitecap fraction of the sampled surface lies. Foam coverage is therefore counted from
    /// the wind (Monahan's W(U)) and placed by the wave geometry (where crests compress).
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
    public struct FoamThresholdJob : IJob
    {
        public NativeArray<float> Jacobians;
        public float WhitecapFraction;

        /// <summary>[0] Jacobian threshold, [1] folded fraction (J &lt; 0).</summary>
        public NativeArray<float> Result;

        public void Execute()
        {
            int n = Jacobians.Length;
            Result[0] = FoamCalibration.NoFoamThreshold;
            Result[1] = 0f;
            if (n == 0) return;

            Jacobians.Sort();
            int folded = 0;
            while (folded < n && Jacobians[folded] < 0f) folded++;
            if (WhitecapFraction > 0f)
                Result[0] = Jacobians[math.clamp((int)(WhitecapFraction * n), 0, n - 1)];
            Result[1] = (float)folded / n;
        }
    }

    /// <summary>Shared foam-calibration constants.</summary>
    public static class FoamCalibration
    {
        /// <summary>
        /// Threshold meaning "no whitecaps": far below any real Jacobian, but finite so the GPU
        /// foam term (threshold − J) × gain can never become NaN (−∞ × 0).
        /// </summary>
        public const float NoFoamThreshold = -1e9f;

        /// <summary>Instants per dominant period sampled by the calibration.</summary>
        public const int TimeSamples = 4;
    }
}
