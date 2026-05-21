using UnityEngine;
using UnityEngine.Rendering;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Pipeline-aware material factory for the test range.
    ///
    /// **Why this exists:** the test range builds primitives and assigns colored materials.
    /// `Shader.Find("Standard")` is built-in-pipeline only — under URP or HDRP it returns
    /// null and Unity falls back to the magenta error shader. We need to pick the right
    /// shader for whichever pipeline the project has active.
    ///
    /// Equally important: URP/Lit and HDRP/Lit don't have a `_Color` property — they use
    /// `_BaseColor`. `material.color = c` (which writes `_Color`) silently no-ops on URP/HDRP
    /// materials. So we have to set whichever property the shader actually has.
    /// </summary>
    public static class RangeMaterials
    {
        /// <summary>
        /// Create a new opaque colored material that renders correctly in whatever pipeline
        /// is currently active.
        /// </summary>
        public static Material MakeColored(Color color)
        {
            var mat = new Material(GetPipelineLitShader());
            SetMaterialColor(mat, color);
            return mat;
        }

        /// <summary>
        /// Set a material's main color in a pipeline-agnostic way. Writes whichever of
        /// `_BaseColor` (URP/HDRP) or `_Color` (built-in) the shader has — typically only
        /// one, but writing both is harmless.
        /// </summary>
        public static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;

            // URP / HDRP lit shaders use _BaseColor.
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            // Built-in Standard shader uses _Color. Also harmless on URP/HDRP if the property
            // doesn't exist (HasProperty guards us).
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        /// <summary>
        /// Pick a lit shader appropriate for the currently active render pipeline.
        /// Falls through built-in → URP → HDRP → unlit, so we always return *something*.
        /// </summary>
        public static Shader GetPipelineLitShader()
        {
            // First preference: borrow the active pipeline's own default-material shader.
            // This is the most robust path because it works for custom render-pipeline assets
            // that don't match standard URP/HDRP shader names.
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var defaultMat = GraphicsSettings.currentRenderPipeline.defaultMaterial;
                if (defaultMat != null && defaultMat.shader != null)
                    return defaultMat.shader;
            }

            // Fallbacks by name. Order matters — try modern pipelines first since they're
            // most common in this project.
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s != null) return s;

            s = Shader.Find("HDRP/Lit");
            if (s != null) return s;

            s = Shader.Find("Standard");
            if (s != null) return s;

            // Last resort: unlit color works in all pipelines. Won't look as nice (no
            // lighting response) but at least it won't be magenta.
            s = Shader.Find("Unlit/Color");
            if (s != null) return s;

            Debug.LogError("[GuidedFury RangeMaterials] No usable shader found. Materials will be magenta.");
            return null;
        }
    }
}
