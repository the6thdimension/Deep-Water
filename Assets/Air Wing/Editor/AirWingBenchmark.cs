using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeepWater.AirWing.Modules.Diagnostics;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Editor
{
    /// <summary>
    /// The W21 budget's two instruments (unity-perf-ab): the flight core timed in the Editor, and the same run in a
    /// player. The budget is ≤ 0.01 ms per aircraft per fixed step at L0, judged in the player; the Editor figure is
    /// labelled as such and never stands in for it.
    /// </summary>
    public static class AirWingBenchmark
    {
        const string ScenePath = AirWingBuilders.Root + "/Examples/Scenes/AirWing_Benchmark.unity";
        const string ProfilePath = AirWingBuilders.Root + "/ScriptableObjects/FA18E_AirWing.asset";
        const string SheetPath = AirWingBuilders.Root + "/ScriptableObjects/FA18E_FidelitySheet.asset";
        public const string PlayerPath = "Builds/AirWingBenchmark/AirWingBenchmark.exe";

        [MenuItem("Deep Water/Air Wing/Benchmark Flight Core (Editor)")]
        public static void RunInEditor()
        {
            var p = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(ProfilePath).Bake();
            var sheet = AssetDatabase.LoadAssetAtPath<FidelitySheetSO>(SheetPath).Bake();
            foreach (int n in new[] { 1, 20 })
                Debug.Log("[Air Wing] Benchmark (Editor, not the budget's instrument): " + FlightBenchmark.Median(p, sheet, n, 500, 5000, 3));
        }

        [MenuItem("Deep Water/Air Wing/Build Benchmark Scene")]
        public static void BuildScene()
        {
            if (AirWingBuilders.AnyDirtyScene()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var runner = new GameObject("Benchmark").AddComponent<FlightBenchmarkRunner>();
            AirWingBuilders.Set(runner, "profile", AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(ProfilePath));
            AirWingBuilders.Set(runner, "fidelitySheet", AssetDatabase.LoadAssetAtPath<FidelitySheetSO>(SheetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Air Wing] Built {ScenePath}: the flight-core benchmark runner, nothing else.");
        }

        /// <summary>
        /// Builds a Windows player of the benchmark scene alone into <see cref="PlayerPath"/> (git-ignored). Run it with
        /// <c>-batchmode -nographics -airwingBenchOut &lt;file&gt;</c>. The first player build of this HDRP project
        /// compiles its shaders and can lock the Editor for a long time.
        /// </summary>
        [MenuItem("Deep Water/Air Wing/Build Benchmark Player")]
        public static void BuildPlayer()
        {
            if (AirWingBuilders.AnyDirtyScene()) return;
            if (!File.Exists(ScenePath)) BuildScene();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            string line = $"[Air Wing] Benchmark player: {s.result}, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime.TotalMinutes:F1} min, {s.totalSize / 1048576.0:F0} MB → {PlayerPath}";
            if (s.result == BuildResult.Succeeded) Debug.Log(line);
            else Debug.LogError(line);
        }
    }
}
