using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace DeepWater.LeanBuild.Editor
{
    /// <summary>The files that changed in the project since a profile's last build, as project-relative paths.</summary>
    public static class ChangeSet
    {
        static readonly string[] CodeExtensions = { ".cs", ".asmdef", ".asmref" };

        /// <summary>
        /// True when every changed file is code: a script, an asmdef or an asmref, or one of their .meta files.
        /// Then a scripts-only build gives the same player as a full one. No change at all counts as code only.
        /// </summary>
        public static bool IsCodeOnly(IReadOnlyList<string> changedPaths, out string firstNonCode)
        {
            foreach (string changed in changedPaths)
            {
                string path = changed.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ? changed.Substring(0, changed.Length - 5) : changed;
                string ext = Path.GetExtension(path);
                if (Array.FindIndex(CodeExtensions, e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)) < 0)
                {
                    firstNonCode = changed;
                    return false;
                }
            }
            firstNonCode = null;
            return true;
        }
    }

    /// <summary>How one background build runs: its options, and why it is a full or a scripts-only build.</summary>
    public readonly struct BuildPlan
    {
        public readonly BuildOptions Options;
        public readonly string Why;

        BuildPlan(BuildOptions options, string why) { Options = options; Why = why; }

        public bool ScriptsOnly => (Options & BuildOptions.BuildScriptsOnly) != 0;
        public bool Development => (Options & BuildOptions.Development) != 0;

        /// <summary>
        /// Scripts-only needs all of: the profile asks for it, a development build (Unity offers scripts-only
        /// builds only for those), an earlier build of this profile to patch, a known change list, and code-only changes.
        /// Anything else builds in full, and <see cref="Why"/> says which condition failed.
        /// </summary>
        public static BuildPlan For(bool developmentBuild, bool scriptsOnlyWhenOnlyCodeChanged, bool previousBuildExists,
                                    bool changesKnown, bool onlyCodeChanged, string firstNonCode)
        {
            var full = developmentBuild ? BuildOptions.Development : BuildOptions.None;
            if (!scriptsOnlyWhenOnlyCodeChanged) return new BuildPlan(full, "full: this profile always builds in full");
            if (!developmentBuild) return new BuildPlan(full, "full: scripts-only builds need a development build");
            if (!previousBuildExists) return new BuildPlan(full, "full: no earlier build of this profile to patch");
            if (!changesKnown) return new BuildPlan(full, "full: the changes since this profile's last build are unknown");
            if (!onlyCodeChanged) return new BuildPlan(full, $"full: {firstNonCode} changed, and it is not code");
            return new BuildPlan(full | BuildOptions.BuildScriptsOnly, "scripts only: nothing but code changed since this profile's last build");
        }
    }
}
