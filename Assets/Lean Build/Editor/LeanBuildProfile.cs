using UnityEditor;
using UnityEngine;

namespace DeepWater.LeanBuild.Editor
{
    /// <summary>
    /// How a background build runs. The assets live in <see cref="Folder"/>:
    /// - "Release" builds in full, as a player ships.
    /// - "Iterate" is a development build that rebuilds only the scripts when nothing but code changed.
    /// These are not Unity's Build Profiles, which hold player settings. A Lean Build profile holds how the build itself runs.
    /// </summary>
    [CreateAssetMenu(menuName = "Deep Water/Lean Build Profile", fileName = "LeanBuildProfile")]
    public sealed class LeanBuildProfile : ScriptableObject
    {
        public const string Folder = "Assets/Lean Build/Profiles";

        [Tooltip("A development build: the profiler can connect, and scripts-only builds become possible.")]
        public bool developmentBuild;

        [Tooltip("When nothing but code changed since this profile's last build, rebuild only the scripts (BuildOptions.BuildScriptsOnly). " +
                 "Needs a development build. Scene and asset data stay as the last full build left them.")]
        public bool scriptsOnlyWhenOnlyCodeChanged;

        public static LeanBuildProfile Load(string name) => AssetDatabase.LoadAssetAtPath<LeanBuildProfile>($"{Folder}/{name}.asset");
    }
}
