using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeepWater.LeanBuild.Editor
{
    /// <summary>
    /// Which shaders player builds leave out. The asset, not the code, holds the lists (Deep Water's rule: no tuning in code).
    /// One asset at <see cref="AssetPath"/>; edit it in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Deep Water/Lean Build Settings", fileName = "LeanBuildSettings")]
    public sealed class LeanBuildSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/Lean Build/LeanBuildSettings.asset";

        [Tooltip("Master switch. Off: player builds keep every variant, as Unity would.")]
        public bool stripInPlayerBuilds = true;

        [Tooltip("Leave out HDRP's TemporalAA shader when no camera in the build's scenes uses TAA (checked as each scene is built).")]
        public bool stripTemporalAAWhenUnused = true;

        [Tooltip("The HDRP TemporalAA shader's name.")]
        public string temporalAAShader = "Hidden/HDRP/TemporalAA";

        [Tooltip("Shaders left out by exact name. Defaults: built-in render pipeline shaders HDRP cannot render (they draw nothing, or magenta).")]
        public List<string> stripShaderNames = new List<string> { "Standard", "Standard (Specular setup)", "Autodesk Interactive", "Particles/Standard Surface" };

        [Tooltip("Shaders left out by name prefix. Defaults: the built-in pipeline's legacy, mobile and nature families.")]
        public List<string> stripShaderPrefixes = new List<string> { "Legacy Shaders/", "Mobile/", "Nature/" };

        /// <summary>The project's settings asset, or the defaults if it has not been created.</summary>
        public static LeanBuildSettings Load()
        {
            var s = AssetDatabase.LoadAssetAtPath<LeanBuildSettings>(AssetPath);
            return s != null ? s : CreateInstance<LeanBuildSettings>();
        }
    }
}
