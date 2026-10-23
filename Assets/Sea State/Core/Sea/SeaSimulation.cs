using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace DeepWater.Sea.Core
{
    /// <summary>
    /// The sea itself — pure C#, no scene dependencies (P6). Owns the baked wave components,
    /// the fixed-step clock and the sea-state cross-fade, and answers queries for the current
    /// step. <see cref="SeaStateDirector"/> is its MonoBehaviour adapter.
    ///
    /// Lifecycle: construct → <see cref="Apply"/> a sea state → <see cref="Step"/> once per
    /// FixedUpdate → <see cref="Query"/> any number of times per step → <see cref="Dispose"/>.
    /// Holds persistent native memory, so it must be disposed.
    /// </summary>
    public sealed class SeaSimulation : ISeaSurface, IDisposable
    {
        /// <summary>Capacity of one baked sea state (wind sea + swell components).</summary>
        public const int MaxComponentsPerState = 256;

        static readonly ProfilerMarker s_StepMarker = new ProfilerMarker("DeepWater.Sea.Step");
        static readonly ProfilerMarker s_QueryMarker = new ProfilerMarker("DeepWater.Sea.Query");

        NativeArray<WaveComponent> _current;
        NativeArray<WaveComponent> _previous;
        NativeArray<SeaFrameComponent> _physicsFrame;
        NativeArray<SeaComponentGroup> _physicsGroups;
        NativeArray<SeaComponentGroup> _unusedGroups;
        NativeArray<int> _countScratch;
        int _currentCount, _previousCount, _physicsFrameCount, _physicsGroupCount;

        double _transitionStartS;
        double _transitionDurationS;
        bool _hasState;
        SeaClock _clock;

        /// <summary>World-space Y of mean water level.</summary>
        public float SeaLevel { get; set; }

        /// <summary>Fixed iteration count of the Lagrangian inverse in height queries.</summary>
        public int InverseIterations { get; set; }

        public SeaConditions Conditions { get; private set; }
        public SeaBakeReport LastBake { get; private set; }
        public double SimulationTimeS => _clock.TimeS;
        public long StepCount => _clock.TotalSteps;

        /// <summary>Cross-fade progress of the latest sea-state change, 0–1 (1 = settled).</summary>
        public float TransitionProgress => (float)TransitionWeight(_clock.TimeS);

        /// <summary>Components active right now (doubles while a cross-fade is running).</summary>
        public int ActiveComponentCount => _physicsFrameCount;

        /// <summary>
        /// The current fixed step's wave field in the packed SIMD layout, for jobs that evaluate
        /// the sea themselves (e.g. batched buoyancy). Read-only; valid until the next
        /// <see cref="Step"/> or <see cref="Apply"/>.
        /// </summary>
        public NativeArray<SeaComponentGroup> PhysicsGroups => _physicsGroups;
        public int PhysicsGroupCount => _physicsGroupCount;

        public SeaSimulation(float seaLevel, int inverseIterations, double startTimeS = 0.0)
        {
            SeaLevel = seaLevel;
            InverseIterations = inverseIterations;
            _clock = SeaClock.StartingAt(startTimeS);
            _current = new NativeArray<WaveComponent>(MaxComponentsPerState, Allocator.Persistent);
            _previous = new NativeArray<WaveComponent>(MaxComponentsPerState, Allocator.Persistent);
            _physicsFrame = new NativeArray<SeaFrameComponent>(2 * MaxComponentsPerState, Allocator.Persistent);
            _physicsGroups = new NativeArray<SeaComponentGroup>(SeaComponentGroup.GroupCount(2 * MaxComponentsPerState), Allocator.Persistent);
            _unusedGroups = new NativeArray<SeaComponentGroup>(1, Allocator.Persistent);
            _countScratch = new NativeArray<int>(2, Allocator.Persistent);
        }

        /// <summary>
        /// Bake and switch to a new sea state. With <paramref name="transitionSeconds"/> &gt; 0 the
        /// old state cross-fades out over that much simulation time; the blend weight is a
        /// function of simulation time only, so the change replays identically.
        /// </summary>
        public SeaBakeReport Apply(in SeaBakeInput input, in SeaConditions conditions, float transitionSeconds)
        {
            int requested = math.max(0, input.WindSea.ComponentCount) + math.max(0, input.Swell.ComponentCount);
            if (requested > MaxComponentsPerState)
                throw new ArgumentException(
                    $"Sea state asks for {requested} components; the limit is {MaxComponentsPerState}.");

            bool fade = _hasState && transitionSeconds > 0f;
            if (fade)
            {
                // The outgoing state is whatever is currently on screen: if a previous fade is
                // still running, collapse it by keeping only the dominant set.
                if (TransitionWeight(_clock.TimeS) >= 0.5)
                {
                    NativeArray<WaveComponent>.Copy(_current, _previous, _currentCount);
                    _previousCount = _currentCount;
                }
            }
            else
            {
                _previousCount = 0;
            }

            var report = SeaBaker.Bake(input, _current);
            _currentCount = report.Count;
            _transitionStartS = _clock.TimeS;
            _transitionDurationS = fade ? transitionSeconds : 0.0;
            _hasState = true;

            var published = conditions;
            published.ResolvedWaveHeightM = report.ResolvedHsM;
            Conditions = published;
            LastBake = report;

            RebuildPhysicsFrame();
            return report;
        }

        /// <summary>Advance one fixed step and rebuild the wave field for the new time.</summary>
        public void Step(double stepS)
        {
            using (s_StepMarker.Auto())
            {
                _clock.Step(stepS);
                if (_previousCount > 0 && TransitionWeight(_clock.TimeS) >= 1.0)
                    _previousCount = 0;
                RebuildPhysicsFrame();
            }
        }

        /// <summary>
        /// Prepare the component list for an arbitrary time (used by the renderer for the
        /// interpolated render time). Returns how many entries of <paramref name="output"/> were
        /// written. <paramref name="output"/> must hold 2 × <see cref="MaxComponentsPerState"/>.
        /// </summary>
        public int BuildFrame(double timeS, NativeArray<SeaFrameComponent> output)
            => BuildFrame(timeS, output, false, _unusedGroups, out _);

        int BuildFrame(double timeS, NativeArray<SeaFrameComponent> output, bool packed,
            NativeArray<SeaComponentGroup> groups, out int groupCount)
        {
            double w = TransitionWeight(timeS);
            new SeaFrameBuildJob
            {
                Current = _current,
                CurrentCount = _currentCount,
                CurrentScale = (float)math.sqrt(w),
                Previous = _previous,
                PreviousCount = _previousCount,
                PreviousScale = (float)math.sqrt(1.0 - w),
                TimeS = timeS,
                Output = output,
                WritePacked = packed,
                Packed = groups,
                OutputCount = _countScratch,
            }.Run();
            groupCount = _countScratch[1];
            return _countScratch[0];
        }

        public void Query(NativeArray<float3> worldPositions, NativeArray<SeaSample> results)
        {
            if (results.Length < worldPositions.Length)
                throw new ArgumentException("Results array is shorter than the positions array.");

            using (s_QueryMarker.Auto())
            {
                new SeaQueryJob
                {
                    Groups = _physicsGroups,
                    GroupCount = _physicsGroupCount,
                    SeaLevel = SeaLevel,
                    InverseIterations = InverseIterations,
                    Positions = worldPositions,
                    Results = results,
                }.Schedule(worldPositions.Length, 32).Complete();
            }
        }

        public SeaSample Sample(float3 worldPosition)
        {
            var positions = new NativeArray<float3>(1, Allocator.TempJob);
            var results = new NativeArray<SeaSample>(1, Allocator.TempJob);
            try
            {
                positions[0] = worldPosition;
                new SeaQueryJob
                {
                    Groups = _physicsGroups,
                    GroupCount = _physicsGroupCount,
                    SeaLevel = SeaLevel,
                    InverseIterations = InverseIterations,
                    Positions = positions,
                    Results = results,
                }.Run(1);
                return results[0];
            }
            finally
            {
                results.Dispose();
                positions.Dispose();
            }
        }

        public void Dispose()
        {
            if (_current.IsCreated) _current.Dispose();
            if (_previous.IsCreated) _previous.Dispose();
            if (_physicsFrame.IsCreated) _physicsFrame.Dispose();
            if (_physicsGroups.IsCreated) _physicsGroups.Dispose();
            if (_unusedGroups.IsCreated) _unusedGroups.Dispose();
            if (_countScratch.IsCreated) _countScratch.Dispose();
        }

        void RebuildPhysicsFrame()
            => _physicsFrameCount = BuildFrame(_clock.TimeS, _physicsFrame, true, _physicsGroups, out _physicsGroupCount);

        double TransitionWeight(double timeS)
        {
            if (_previousCount == 0 || _transitionDurationS <= 0.0) return 1.0;
            return math.saturate((timeS - _transitionStartS) / _transitionDurationS);
        }
    }
}
