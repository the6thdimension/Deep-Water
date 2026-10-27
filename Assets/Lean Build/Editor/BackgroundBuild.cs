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
    /// - Deep Water > Build > Build Active Scene In Background (Release or Iterate) starts it (tools/background-build/bg_build.ps1).
    /// - <see cref="Run"/> is the headless entry point (-executeMethod). It builds with a <see cref="LeanBuildProfile"/>
    ///   and writes a report next to the player.
    /// </summary>
    public static class BackgroundBuild
    {
        const string Script = "tools/background-build/bg_build.ps1";

        [MenuItem("Deep Water/Build/Build Active Scene In Background (Release)")]
        static void BuildRelease() => Start("Release");

        [MenuItem("Deep Water/Build/Build Active Scene In Background (Iterate)")]
        static void BuildIterate() => Start("Iterate");

        static void Start(string profile)
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scene)) { Debug.LogError("[Background Build] the active scene is not saved."); return; }
            if (!File.Exists(Script)) { Debug.LogError("[Background Build] missing " + Script); return; }
            if (LeanBuildProfile.Load(profile) == null) { Debug.LogError($"[Background Build] missing {LeanBuildProfile.Folder}/{profile}.asset"); return; }
            AssetDatabase.SaveAssets();   // the mirror copies what is on disk
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.GetFullPath(Script)}\" -Scenes \"{scene}\" -Profile {profile} -UnityExe \"{EditorApplication.applicationPath}\"")
            { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized };
            Process.Start(psi);
            Debug.Log($"[Background Build] {profile} build started for {scene}. The player and its report land in Builds/Background/{profile}/ of this project; the log is in the mirror's Logs/.");
        }

        /// <summary>
        /// Headless entry point:
        /// <c>-executeMethod DeepWater.LeanBuild.Editor.BackgroundBuild.Run -bgScenes "a.unity;b.unity" -bgOut &lt;dir&gt; -bgProfile &lt;name&gt; [-bgChanged &lt;file&gt;]</c>.
        /// <c>-bgChanged</c> lists, one per line, the files changed since this profile's last build; without it the changes are unknown.
        /// Exits with code 0 on success and 1 on failure, so the calling script can tell.
        /// </summary>
        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            string Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            string[] scenes = (Arg("-bgScenes") ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            string outDir = Arg("-bgOut");
            string profileName = Arg("-bgProfile") ?? "Release";
            string changedFile = Arg("-bgChanged");
            var profile = LeanBuildProfile.Load(profileName);
            if (scenes.Length == 0 || string.IsNullOrEmpty(outDir) || profile == null)
            {
                Debug.LogError($"[Background Build] needs -bgScenes \"a.unity;b.unity\", -bgOut <dir> and a profile in {LeanBuildProfile.Folder} (got '{profileName}')");
                EditorApplication.Exit(1);
                return;
            }

            string exe = Path.Combine(outDir, Path.GetFileNameWithoutExtension(scenes[0]).Replace(' ', '_') + ".exe");
            bool changesKnown = !string.IsNullOrEmpty(changedFile) && File.Exists(changedFile);
            string[] changed = changesKnown ? File.ReadAllLines(changedFile).Where(l => l.Trim().Length > 0).Distinct().ToArray() : Array.Empty<string>();
            bool codeOnly = ChangeSet.IsCodeOnly(changed, out string firstNonCode);
            var plan = BuildPlan.For(profile.developmentBuild, profile.scriptsOnlyWhenOnlyCodeChanged, File.Exists(exe), changesKnown, codeOnly, firstNonCode);
            Debug.Log($"[Background Build] profile {profileName}, {(changesKnown ? changed.Length + " changed files" : "changes unknown")}: {plan.Why}");

            BuildReport r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = plan.Options,
            });
            var sb = new StringBuilder();
            sb.AppendLine($"Background build {DateTime.Now:yyyy-MM-dd HH:mm}: {r.summary.result}, {r.summary.totalErrors} errors, {r.summary.totalTime.TotalSeconds:F1} s, {r.summary.totalSize / 1048576.0:F0} MB");
            // BuildOptions.ToString() names Unity's obsolete aliases too ("Il2CPP, CompressTextures…" for Development), so spell it out.
            sb.AppendLine($"profile: {profileName} ({(plan.Development ? "development build" : "non-development build")}); {plan.Why}");
            sb.AppendLine($"changed since this profile's last build: {(changesKnown ? changed.Length.ToString() : "unknown")}");
            sb.AppendLine("scenes: " + string.Join(", ", scenes));
            foreach (BuildStep s in r.steps.Where(s => s.depth <= 1)) sb.AppendLine($"{new string(' ', s.depth * 2)}{s.duration.TotalSeconds,8:F1} s  {s.name}");
            File.WriteAllText(Path.Combine(outDir, "background-build-report.txt"), sb.ToString());
            Debug.Log(sb.ToString());
            EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
