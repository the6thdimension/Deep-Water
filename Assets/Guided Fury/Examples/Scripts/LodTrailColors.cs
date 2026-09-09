using UnityEngine;
using GuidedFury.Core.Integrators;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Shared LOD → color palette. Used by:
    /// - <see cref="LodComparisonRunner"/> for missile + trail tint
    /// - <see cref="SalvoRunner"/> for missile + trail tint
    /// - <see cref="MissileHud"/> for per-row LOD coloring
    ///
    /// Keep these stable across the project — a player learns the mapping (yellow = cheap
    /// kinematic, magenta = high-fidelity 6DOF) and we don't want to retrain them.
    /// </summary>
    public static class LodTrailColors
    {
        public static readonly Color L0 = new Color(0.9f,  0.9f,  0.2f);  // yellow — cheap kinematic
        public static readonly Color L1 = new Color(0.2f,  0.9f,  0.3f);  // green  — 3DOF point-mass
        public static readonly Color L2 = new Color(0.3f,  0.6f,  1.0f);  // blue   — rate-limited
        public static readonly Color L3 = new Color(0.95f, 0.3f,  0.6f);  // magenta — pseudo-6DOF
        public static readonly Color L4 = new Color(1.0f,  0.5f,  0.1f);  // orange — full aero (when implemented)
        public static readonly Color L5 = new Color(0.7f,  0.2f,  1.0f);  // purple — HWIL (when implemented)
        public static readonly Color Unknown = new Color(0.7f, 0.7f, 0.7f); // grey

        public static Color For(MissileLod lod)
        {
            switch (lod)
            {
                case MissileLod.L0_Kinematic:         return L0;
                case MissileLod.L1_PointMass3Dof:     return L1;
                case MissileLod.L2_RateLimited3Dof:   return L2;
                case MissileLod.L3_PseudoRb6Dof:      return L3;
                case MissileLod.L4_FullAero6Dof:      return L4;
                case MissileLod.L5_HardwareInTheLoop: return L5;
                default: return Unknown;
            }
        }

        /// <summary>HTML-style hex color (no alpha) for use in IMGUI rich text — `<color=#RRGGBB>`.</summary>
        public static string HexFor(MissileLod lod)
        {
            Color c = For(lod);
            return string.Format("#{0:X2}{1:X2}{2:X2}",
                Mathf.RoundToInt(c.r * 255f),
                Mathf.RoundToInt(c.g * 255f),
                Mathf.RoundToInt(c.b * 255f));
        }
    }
}
