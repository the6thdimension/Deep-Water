using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using DeepWater.Sea.Core;
using DeepWater.Sea.ScriptableObjects;
using Debug = UnityEngine.Debug;

namespace DeepWater.Sea.Editor
{
    /// <summary>
    /// Repeatable CPU benchmark of the sea's hot paths on a fixed scenario (Douglas 7 + swell 4,
    /// seeds as authored): bake, per-step frame build, and batch height queries at several
    /// probe counts. Reports medians so one hiccup does not move the number. Editor numbers
    /// include whatever Burst safety checks are enabled — the report says which.
    /// </summary>
    public static class SeaBenchmark
    {
        public const int BenchSea = 7, BenchSwell = 4;
        static readonly int[] ProbeCounts = { 142, 1024, 8192 };
        const int WarmupRuns = 5, TimedRuns = 41;

        [MenuItem("Deep Water/Sea State/Benchmark Sea Kernel")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            var library = AssetDatabase.LoadAssetAtPath<SeaStateLibrarySO>(DouglasProfileBuilder.LibraryPath);
            if (library == null) return "[SeaBenchmark] No library — run Build Douglas Profiles first.";

            var sb = new StringBuilder();
            sb.AppendLine($"[SeaBenchmark] Douglas {BenchSea} + swell {BenchSwell}; Burst enabled={BurstCompiler.IsEnabled}, " +
                          $"safety checks={BurstCompiler.Options.EnableBurstSafetyChecks}");

            var input = library.BuildBakeInput(BenchSea, BenchSwell, 0f, 270f, 0u, 9.81f, out SeaConditions conditions);
            using (var sim = new SeaSimulation(0f, library.inverseIterations))
            {
                var bakeTimes = new List<double>();
                for (int i = 0; i < 6; i++)
                {
                    var sw = Stopwatch.StartNew();
                    sim.Apply(input, conditions, 0f);
                    bakeTimes.Add(sw.Elapsed.TotalMilliseconds);
                }
                sb.AppendLine($"bake: median {Median(bakeTimes):F3} ms ({sim.LastBake.Count} components)");

                var stepTimes = new List<double>();
                for (int i = 0; i < 200; i++)
                {
                    var sw = Stopwatch.StartNew();
                    sim.Step(0.02);
                    stepTimes.Add(sw.Elapsed.TotalMilliseconds * 1000.0);
                }
                sb.AppendLine($"step (frame build): median {Median(stepTimes):F1} µs");

                var frame = new NativeArray<SeaFrameComponent>(2 * SeaSimulation.MaxComponentsPerState, Allocator.Persistent);
                int count = sim.BuildFrame(sim.SimulationTimeS, frame);
                try
                {
                    foreach (int n in ProbeCounts)
                    {
                        var positions = RandomPositions(n, 500f, 1234u);
                        var results = new NativeArray<SeaSample>(n, Allocator.Persistent);
                        try
                        {
                            double packed = TimeIt(() => sim.Query(positions, results));
                            double reference = TimeIt(() => new SeaQueryReferenceJob
                            {
                                Components = frame, ComponentCount = count, SeaLevel = 0f,
                                InverseIterations = sim.InverseIterations, Positions = positions, Results = results,
                            }.Schedule(n, 32).Complete());
                            sb.AppendLine($"query x{n,5}: packed {packed:F4} ms ({packed * 1e6 / n:F0} ns/probe) | " +
                                          $"reference {reference:F4} ms ({reference * 1e6 / n:F0} ns/probe) | x{reference / packed:F1}");
                        }
                        finally
                        {
                            results.Dispose();
                            positions.Dispose();
                        }
                    }
                }
                finally
                {
                    frame.Dispose();
                }
                sb.AppendLine(FloatModeCost(sim, 1024));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Cost of determinism: the production kernel (FloatMode.Deterministic) against the same
        /// code compiled with FloatMode.Fast, single-threaded so dispatch overhead is excluded.
        /// Information only — the sim keeps Deterministic.
        /// </summary>
        public static string FloatModeCost(SeaSimulation sim, int n)
        {
            var positions = RandomPositions(n, 500f, 99u);
            var a = new NativeArray<SeaSample>(n, Allocator.Persistent);
            var b = new NativeArray<SeaSample>(n, Allocator.Persistent);
            try
            {
                double det = TimeIt(() => new SeaQueryJob
                {
                    Groups = sim.PhysicsGroups, GroupCount = sim.PhysicsGroupCount, SeaLevel = 0f,
                    InverseIterations = sim.InverseIterations, Positions = positions, Results = a,
                }.Run(n));
                double fast = TimeIt(() => new FastMathQueryJob
                {
                    Groups = sim.PhysicsGroups, GroupCount = sim.PhysicsGroupCount,
                    InverseIterations = sim.InverseIterations, Positions = positions, Results = b,
                }.Run(n));
                float worst = 0f;
                for (int i = 0; i < n; i++) worst = math.max(worst, math.abs(a[i].SurfaceHeight - b[i].SurfaceHeight));
                return $"single-thread x{n}: Deterministic {det * 1e6 / n:F0} ns/probe, Fast {fast * 1e6 / n:F0} ns/probe " +
                       $"(determinism costs {100.0 * (det - fast) / fast:F0} %; max height difference {worst * 1000f:F2} mm)";
            }
            finally
            {
                b.Dispose();
                a.Dispose();
                positions.Dispose();
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, CompileSynchronously = true)]
        struct FastMathQueryJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SeaComponentGroup> Groups;
            public int GroupCount;
            public int InverseIterations;
            [ReadOnly] public NativeArray<float3> Positions;
            [WriteOnly] public NativeArray<SeaSample> Results;

            public void Execute(int i)
            {
                float2 x0 = SeaFieldPacked.InverseHorizontal(Groups, GroupCount, Positions[i].xz, InverseIterations);
                SeaFieldPacked.Evaluate(Groups, GroupCount, x0, out float h, out float3 v, out float3 nrm, out float j);
                Results[i] = new SeaSample { SurfaceHeight = h, Velocity = v, Normal = nrm, Jacobian = j };
            }
        }

        public static NativeArray<float3> RandomPositions(int n, float halfExtent, uint seed)
        {
            var rng = new Random(seed);
            var p = new NativeArray<float3>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++)
            {
                float2 xz = rng.NextFloat2(-halfExtent, halfExtent);
                p[i] = new float3(xz.x, rng.NextFloat(-3f, 3f), xz.y);
            }
            return p;
        }

        public static double TimeIt(System.Action action)
        {
            for (int i = 0; i < WarmupRuns; i++) action();
            var times = new List<double>(TimedRuns);
            for (int i = 0; i < TimedRuns; i++)
            {
                var sw = Stopwatch.StartNew();
                action();
                times.Add(sw.Elapsed.TotalMilliseconds);
            }
            return Median(times);
        }

        static double Median(List<double> values)
        {
            values.Sort();
            return values[values.Count / 2];
        }
    }
}
