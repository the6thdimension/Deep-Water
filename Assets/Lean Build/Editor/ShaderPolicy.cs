using System;
using System.Collections.Generic;

namespace DeepWater.LeanBuild.Editor
{
    /// <summary>Why a shader is left out of a build, or <see cref="Keep"/>.</summary>
    public enum StripReason { Keep, ByName, ByPrefix, TemporalAAUnused }

    /// <summary>Pure decision: does a build keep this shader? Kept apart from Unity's build callbacks so tests can reach it.</summary>
    public static class ShaderPolicy
    {
        /// <param name="taaUsed">True if any camera in the build's scenes uses TAA, or if that could not be checked.</param>
        public static StripReason Decide(string shaderName, bool stripTemporalAAWhenUnused, string temporalAAShader, bool taaUsed,
                                         IReadOnlyList<string> names, IReadOnlyList<string> prefixes)
        {
            if (string.IsNullOrEmpty(shaderName)) return StripReason.Keep;
            if (stripTemporalAAWhenUnused && !taaUsed && string.Equals(shaderName, temporalAAShader, StringComparison.Ordinal))
                return StripReason.TemporalAAUnused;
            foreach (string n in names)
                if (string.Equals(shaderName, n, StringComparison.Ordinal)) return StripReason.ByName;
            foreach (string p in prefixes)
                if (!string.IsNullOrEmpty(p) && shaderName.StartsWith(p, StringComparison.Ordinal)) return StripReason.ByPrefix;
            return StripReason.Keep;
        }
    }
}
