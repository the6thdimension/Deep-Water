using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Aviator.Modules;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Editor
{
    /// <summary>
    /// The AI budget's instrument (A4), as Air Wing's: a benchmark scene and a Windows player of it alone
    /// (<see cref="PlayerPath"/>, git-ignored). The budget is judged in the player only.
    /// </summary>
    public static class AviatorBenchmark
    {
        const string ScenePath = AviatorTools.Root + "/Examples/Scenes/Aviator_Benchmark.unity";
        const string AircraftPath = "Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset";
        const string SheetPath = "Assets/Air Wing/ScriptableObjects/FA18E_FidelitySheet.asset";
        public const string PlayerPath = "Builds/AviatorBenchmark/AviatorBenchmark.exe";

        [MenuItem("Deep Water/RH Aviator/Build Benchmark Scene")]
        public static void BuildScene()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty) { Debug.LogError("[RH Aviator] save or discard the open scene's changes first; nothing built."); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var runner = new SerializedObject(new GameObject("Benchmark").AddComponent<AviatorBenchmarkRunner>());
            runner.FindProperty("aircraft").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(AircraftPath);
            runner.FindProperty("fidelitySheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FidelitySheetSO>(SheetPath);
            runner.FindProperty("pilot").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(AviatorTools.LinePilotPath);
            runner.FindProperty("route").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GateCourseSO>(AviatorTools.DogfightRoutePath);
            runner.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[RH Aviator] Built {ScenePath}: the AI benchmark runner, nothing else.");
        }

        [MenuItem("Deep Water/RH Aviator/Build Benchmark Player")]
        public static void BuildPlayer()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            BuildSummary s = report.summary;
            string line = $"[RH Aviator] Benchmark player: {s.result}, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime.TotalMinutes:F1} min, {s.totalSize / 1048576.0:F0} MB → {PlayerPath}";
            if (s.result == BuildResult.Succeeded) Debug.Log(line);
            else Debug.LogError(line);
        }
    }
}
