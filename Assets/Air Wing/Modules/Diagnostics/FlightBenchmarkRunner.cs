using System;
using System.IO;
using System.Text;
using UnityEngine;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Modules.Diagnostics
{
    /// <summary>
    /// The benchmark scene's one job (W21): on Start, run <see cref="FlightBenchmark"/> for each aircraft count, write
    /// the results to a text file, and quit. The file is the path after <c>-airwingBenchOut</c> on the command line,
    /// else <c>airwing_benchmark.txt</c> in <see cref="Application.persistentDataPath"/>. Run the player with
    /// <c>-batchmode -nographics -airwingBenchOut &lt;path&gt;</c>.
    /// </summary>
    public sealed class FlightBenchmarkRunner : MonoBehaviour
    {
        [SerializeField] AircraftProfileSO profile;
        [SerializeField] FidelitySheetSO fidelitySheet;
        [SerializeField] int[] aircraftCounts = { 1, 20 };
        [SerializeField] int warmupTicks = 500;
        [SerializeField] int ticks = 5000;
        [SerializeField] int repeats = 3;
        [Tooltip("Quit when done (the player run); off in the Editor.")]
        [SerializeField] bool quitWhenDone = true;

        void Start()
        {
            string path = OutputPath();
            var report = new StringBuilder();
            report.AppendLine($"Air Wing flight-core benchmark, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"{(Application.isEditor ? "Editor" : "Player")}, {Application.platform}, Unity {Application.unityVersion}, {SystemInfo.processorType}, " +
                              $"development build {Debug.isDebugBuild}, fixed step {Time.fixedDeltaTime} s");
            try
            {
                if (profile == null || fidelitySheet == null) throw new InvalidOperationException("assign the aircraft profile and the fidelity sheet");
                var p = profile.Bake();
                var sheet = fidelitySheet.Bake();
                // The asset's sheet, then every part at L0, for the A/B (unity-perf-ab).
                var l0 = new Core.Fidelity.FidelitySheet(Core.Fidelity.FidelityLevel.L0, Core.Fidelity.FidelityLevel.L0, Core.Fidelity.FidelityLevel.L0,
                                                         Core.Fidelity.FidelityLevel.L0, Core.Fidelity.FidelityLevel.L0, sheet.SubstepsPerTick, 1442);
                foreach (var s in new[] { sheet, l0 })
                    foreach (int n in aircraftCounts)
                        report.AppendLine("RESULT " + FlightBenchmark.Median(p, s, n, warmupTicks, ticks, repeats));
                report.AppendLine("FINISHED");
            }
            catch (Exception e)
            {
                report.AppendLine("FAILED " + e);
                Debug.LogException(e, this);
            }
            File.WriteAllText(path, report.ToString());
            Debug.Log($"[Air Wing] Benchmark written to {path}:\n{report}", this);
            if (quitWhenDone && !Application.isEditor) Application.Quit();
        }

        static string OutputPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-airwingBenchOut") return args[i + 1];
            return Path.Combine(Application.persistentDataPath, "airwing_benchmark.txt");
        }
    }
}
