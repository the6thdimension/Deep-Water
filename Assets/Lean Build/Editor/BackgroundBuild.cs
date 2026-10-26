using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace DeepWater.LeanBuild.Editor
{
    /// <summary>
    /// Player builds that never lock the Editor. A mirror of this project (Assets, Packages and ProjectSettings,
    /// copied with robocopy /MIR) lives next to it with its own Library. A headless Unity builds there at below-normal
    /// priority. The mirror's caches stay warm between builds, and with Unity Accelerator both projects share one cache.
    /// - Deep Water > Build > Build Active Scene In Background starts it (tools/background-build/bg_build.ps1).
    /// - <see cref="Run"/> is the headless entry point (-executeMethod), and writes a report next to the player.
    /// </summary>
    public static class BackgroundBuild
    {
        const string Script = "tools/background-build/bg_build.ps1";

        [MenuItem("Deep Water/Build/Build Active Scene In Background")]
        static void BuildActiveSceneInBackground()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scene)) { Debug.LogError("[Background Build] the active scene is not saved."); return; }
            if (!File.Exists(Script)) { Debug.LogError("[Background Build] missing " + Script); return; }
            AssetDatabase.SaveAssets();   // the mirror copies what is on disk
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.GetFullPath(Script)}\" -Scenes \"{scene}\" -UnityExe \"{EditorApplication.applicationPath}\"")
            { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized };
            Process.Start(psi);
            Debug.Log($"[Background Build] started for {scene}. The player and its report land in Builds/Background/ of this project; the log is in the mirror's Logs/.");
        }

        /// <summary>
        /// Headless entry point: <c>-executeMethod DeepWater.LeanBuild.Editor.BackgroundBuild.Run -bgScenes "a.unity;b.unity" -bgOut &lt;dir&gt;</c>.
        /// Exits with code 0 on success and 1 on failure, so the calling script can tell.
        /// </summary>
        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            string Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            string[] scenes = (Arg("-bgScenes") ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            string outDir = Arg("-bgOut");
            if (scenes.Length == 0 || string.IsNullOrEmpty(outDir))
            {
                Debug.LogError("[Background Build] needs -bgScenes \"a.unity;b.unity\" and -bgOut <dir>");
                EditorApplication.Exit(1);
                return;
            }
            string exe = Path.Combine(outDir, Path.GetFileNameWithoutExtension(scenes[0]).Replace(' ', '_') + ".exe");
            BuildReport r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var sb = new StringBuilder();
            sb.AppendLine($"Background build {DateTime.Now:yyyy-MM-dd HH:mm}: {r.summary.result}, {r.summary.totalErrors} errors, {r.summary.totalTime.TotalSeconds:F1} s, {r.summary.totalSize / 1048576.0:F0} MB");
            sb.AppendLine("scenes: " + string.Join(", ", scenes));
            foreach (BuildStep s in r.steps.Where(s => s.depth <= 1)) sb.AppendLine($"{new string(' ', s.depth * 2)}{s.duration.TotalSeconds,8:F1} s  {s.name}");
            File.WriteAllText(Path.Combine(outDir, "background-build-report.txt"), sb.ToString());
            Debug.Log(sb.ToString());
            EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
