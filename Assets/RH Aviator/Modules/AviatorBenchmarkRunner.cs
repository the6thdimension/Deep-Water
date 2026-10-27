using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.ScriptableObjects;
using Debug = UnityEngine.Debug;

namespace DeepWater.Aviator.Modules
{
    /// <summary>
    /// The AI budget's instrument (A4, unity-perf-ab): on Start, times N aircraft flying the gate course with AI pilots
    /// against the same N aircraft on hold seats from the same starts, and writes both and their difference, the AI's
    /// own cost per pilot per tick. The flight core alone, no scene, the median of three; 500 warm-up ticks discarded.
    /// Run the player with <c>-batchmode -nographics -aviatorBenchOut &lt;file&gt;</c>.
    /// </summary>
    public sealed class AviatorBenchmarkRunner : MonoBehaviour
    {
        [SerializeField] AircraftProfileSO aircraft;
        [SerializeField] FidelitySheetSO fidelitySheet;
        [SerializeField] PilotProfileSO pilot;
        [SerializeField] GateCourseSO route;
        [SerializeField] int[] aircraftCounts = { 1, 4 };
        [SerializeField] int warmupTicks = 500;
        [SerializeField] int ticks = 5000;
        [SerializeField] int repeats = 3;
        [SerializeField] bool quitWhenDone = true;

        void Start()
        {
            string path = OutputPath();
            var report = new StringBuilder();
            report.AppendLine($"RH Aviator AI benchmark, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"{(Application.isEditor ? "Editor" : "Player")}, {Application.platform}, Unity {Application.unityVersion}, {SystemInfo.processorType}, development build {Debug.isDebugBuild}");
            try
            {
                AircraftProfile e = aircraft.Bake();
                FidelitySheet sheet = fidelitySheet.Bake();
                PilotProfile p = pilot.Bake();
                foreach (int n in aircraftCounts)
                {
                    double hold = Median(() => Run(e, sheet, p, n, ai: false));
                    double ai = Median(() => Run(e, sheet, p, n, ai: true));
                    report.AppendLine($"RESULT {n} aircraft: hold seats {hold * 1000.0 / n:F2} µs, AI pilots {ai * 1000.0 / n:F2} µs per aircraft per tick; " +
                                      $"the AI's own cost {(ai - hold) * 1000.0 / n:F2} µs per pilot per tick (budget 10, A4)");
                }
                report.AppendLine("FINISHED");
            }
            catch (Exception ex)
            {
                report.AppendLine("FAILED " + ex);
                Debug.LogException(ex, this);
            }
            File.WriteAllText(path, report.ToString());
            Debug.Log($"[RH Aviator] Benchmark written to {path}:\n{report}", this);
            if (quitWhenDone && !Application.isEditor) Application.Quit();
        }

        double Median(Func<double> run)
        {
            var r = new double[Mathf.Max(1, repeats)];
            for (int i = 0; i < r.Length; i++) r[i] = run();
            Array.Sort(r);
            return r[r.Length / 2];
        }

        /// <summary>Milliseconds per tick for <paramref name="n"/> aircraft, spread 300 m apart at Dogfight's start.</summary>
        double Run(in AircraftProfile e, in FidelitySheet sheet, in PilotProfile p, int n, bool ai)
        {
            var core = new FlightCore(new SubstepClock(0.02, sheet.SubstepsPerTick), new FlatSurface(8f));
            for (int i = 0; i < n; i++)
            {
                var start = AircraftFactory.LevelStart(e, sheet, new Vector3(300f * i, 1524f, -2500f), 0f, 400f * (float)Units.KnotToMetrePerSecond, 0.6f * e.Airframe.InternalFuelKg, 0.7f);
                ISeat seat = ai
                    ? new AviatorSeat(core, e, p, route.Bake(), new RuleLog())
                    : new HoldSeat(new PilotInputs { Throttle = 0.7f });
                core.Add(AircraftFactory.Build(i + 1, e, sheet, start), seat);
            }
            for (int t = 0; t < warmupTicks; t++) core.Step();
            var clock = Stopwatch.StartNew();
            for (int t = 0; t < ticks; t++) core.Step();
            clock.Stop();
            return clock.Elapsed.TotalMilliseconds / ticks;
        }

        static string OutputPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-aviatorBenchOut") return args[i + 1];
            return Path.Combine(Application.persistentDataPath, "aviator_benchmark.txt");
        }
    }
}
