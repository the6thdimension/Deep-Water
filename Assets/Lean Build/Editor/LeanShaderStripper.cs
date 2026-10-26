using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace DeepWater.LeanBuild.Editor
{
    /// <summary>
    /// Leaves shaders out of player builds when they cannot render here (LeanBuildSettings lists them), and HDRP's TemporalAA
    /// when no camera in the build uses TAA.
    /// - Scenes are built before shaders are compiled, so each scene's cameras are checked as it is built (<see cref="OnProcessScene"/>).
    /// - If a shader arrives before any scene has been checked, TAA is kept: no guess.
    /// - Every build ends with one line of what was left out, and the full list goes to Library/LeanBuild/last-build.txt.
    /// </summary>
    public sealed class LeanShaderStripper : IPreprocessBuildWithReport, IProcessSceneWithReport, IPreprocessShaders, IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;   // after HDRP's own stripping (order 0)

        // One build at a time: Unity calls these on the main thread, in order.
        static bool _active, _taaUsed, _scenesSeen;
        static readonly List<string> TaaCameras = new List<string>();
        static readonly Dictionary<string, (StripReason why, int variants)> Stripped = new Dictionary<string, (StripReason, int)>();
        static LeanBuildSettings _settings;

        public void OnPreprocessBuild(BuildReport report)
        {
            _settings = LeanBuildSettings.Load();
            bool hdrp = GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset;
            _active = _settings.stripInPlayerBuilds && hdrp;
            _taaUsed = false;
            _scenesSeen = false;
            TaaCameras.Clear();
            Stripped.Clear();
            if (!hdrp && _settings.stripInPlayerBuilds)
                Debug.LogWarning("[Lean Build] the render pipeline is not HDRP: nothing stripped (the strip lists assume HDRP).");
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || !_active) return;   // report is null when a scene is processed in play mode
            _scenesSeen = true;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (HDAdditionalCameraData cam in root.GetComponentsInChildren<HDAdditionalCameraData>(true))
                    if (cam.antialiasing == HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing)
                    {
                        _taaUsed = true;
                        TaaCameras.Add(scene.path + " : " + cam.name);
                    }
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (!_active || data.Count == 0) return;
            bool taaUsed = _taaUsed || !_scenesSeen;   // no scene checked yet: keep TAA
            StripReason why = ShaderPolicy.Decide(shader.name, _settings.stripTemporalAAWhenUnused, _settings.temporalAAShader, taaUsed,
                                                  _settings.stripShaderNames, _settings.stripShaderPrefixes);
            if (why == StripReason.Keep) return;
            Stripped.TryGetValue(shader.name, out var seen);
            Stripped[shader.name] = (why, seen.variants + data.Count);
            data.Clear();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!_active) return;
            int variants = Stripped.Values.Sum(v => v.variants);
            var sb = new StringBuilder();
            sb.AppendLine($"Lean Build, {report.summary.platform}, {System.DateTime.Now:yyyy-MM-dd HH:mm}: {Stripped.Count} shaders, {variants} variants left out.");
            sb.AppendLine(_taaUsed ? "TAA kept: used by " + string.Join(", ", TaaCameras) : _scenesSeen ? "TAA: no camera in the build uses it." : "TAA kept: no scene was checked.");
            foreach (var kv in Stripped.OrderByDescending(k => k.Value.variants))
                sb.AppendLine($"  {kv.Value.variants,6} variants  {kv.Value.why,-17} {kv.Key}");
            Directory.CreateDirectory("Library/LeanBuild");
            File.WriteAllText("Library/LeanBuild/last-build.txt", sb.ToString());
            string top = string.Join(", ", Stripped.OrderByDescending(k => k.Value.variants).Take(4).Select(k => $"{k.Key} {k.Value.variants}"));
            Debug.Log($"[Lean Build] left out {variants} shader variants of {Stripped.Count} shaders ({top}). Full list: Library/LeanBuild/last-build.txt");
        }
    }
}
