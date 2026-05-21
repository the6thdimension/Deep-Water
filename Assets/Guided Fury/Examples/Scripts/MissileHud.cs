using System.Collections.Generic;
using UnityEngine;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Examples
{
    /// <summary>
    /// IMGUI overlay listing every active missile in the scene with its key telemetry.
    /// Read-only — purely diagnostic. Drop on any GameObject in a scene to get a HUD.
    ///
    /// **Why IMGUI:** zero asset dependencies. No Canvas, no TextMeshPro, no prefabs to
    /// set up. The user can drop the component on the camera and have a working HUD with
    /// no clicks. The trade-off is that IMGUI doesn't scale with DPI as cleanly as uGUI,
    /// but for a test-range HUD it's the right call.
    /// </summary>
    public sealed class MissileHud : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("Pixel size of the HUD's top-left anchor.")]
        [SerializeField] private Vector2 anchor = new Vector2(12f, 12f);

        [Tooltip("Width of the HUD panel.")]
        [SerializeField] private float width = 480f;

        [Tooltip("Show entries for missiles that have detonated / failed. Useful for AAR; noisy for live ops.")]
        [SerializeField] private bool showInactive = false;

        // Cached array — refreshed each frame. Avoiding LINQ here is just polite; the
        // missile count is tiny but FindObjectsOfType already allocates.
        private MissileBehaviour[] cached;
        private float lastRefreshTime;
        private const float RefreshIntervalS = 0.25f; // 4 Hz scan; faster than the eye notices

        private GUIStyle headerStyle;
        private GUIStyle rowStyle;
        private GUIStyle lockOnStyle;

        private void Update()
        {
            if (Time.time - lastRefreshTime >= RefreshIntervalS)
            {
                // FindObjectsByType is faster and explicit about sorting (None = arbitrary order).
                cached = FindObjectsByType<MissileBehaviour>(FindObjectsSortMode.None);
                lastRefreshTime = Time.time;
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (cached == null || cached.Length == 0) return;

            // Filter to whatever should be shown this frame.
            int rowCount = 0;
            for (int i = 0; i < cached.Length; i++)
            {
                if (cached[i] == null) continue;
                if (!showInactive && !IsActive(cached[i])) continue;
                rowCount++;
            }
            if (rowCount == 0) return;

            float rowHeight = 18f;
            float headerHeight = 22f;
            float panelHeight = headerHeight + rowCount * rowHeight + 12f;

            var panelRect = new Rect(anchor.x, anchor.y, width, panelHeight);
            GUI.Box(panelRect, "");

            GUILayout.BeginArea(panelRect);
            GUILayout.Label("Missile  LOD  Phase     ToF    Speed     Range   Lock", headerStyle);
            for (int i = 0; i < cached.Length; i++)
            {
                var b = cached[i];
                if (b == null) continue;
                if (!showInactive && !IsActive(b)) continue;
                DrawRow(b);
            }
            GUILayout.EndArea();
        }

        private void DrawRow(MissileBehaviour b)
        {
            MissileState s = b.GetState();
            float speed = s.Velocity.magnitude;
            float range = b.Target != null ? Vector3.Distance(s.Position, b.Target.position) : -1f;
            string rangeStr = range >= 0f ? FormatLength(range) : "—";
            string lockStr = b.IsSeekerLocked ? "<color=#88FF88>LOCK</color>" : "—";

            string row = string.Format(
                "{0,-8} {1,-4} {2,-9} {3,5:0.0}s {4,7:0} m/s {5,7} {6}",
                Truncate(b.name, 8),
                LodShort(b),
                PhaseShort(s.Phase),
                s.TimeOfFlight,
                speed,
                rangeStr,
                lockStr);

            GUILayout.Label(row, b.IsSeekerLocked ? lockOnStyle : rowStyle);
        }

        // ---- helpers --------------------------------------------------------

        private void EnsureStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    richText = true,
                };
                headerStyle.normal.textColor = Color.white;
            }
            if (rowStyle == null)
            {
                rowStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    richText = true,
                };
                rowStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            }
            if (lockOnStyle == null)
            {
                lockOnStyle = new GUIStyle(rowStyle)
                {
                    fontStyle = FontStyle.Bold,
                };
            }
        }

        private static bool IsActive(MissileBehaviour b)
        {
            if (b == null || !b.gameObject.activeInHierarchy) return false;
            if (!b.IsLaunched) return false;
            var phase = b.GetState().Phase;
            return phase != MissilePhase.Detonated && phase != MissilePhase.Failed;
        }

        private static string LodShort(MissileBehaviour b)
        {
            // Read the LOD via the entity's integrator. The Behaviour doesn't directly expose
            // its serialized LOD field for the post-launch reading; the entity carries it.
            var lod = b.Entity?.Integrator?.Lod ?? Core.Integrators.MissileLod.L0_Kinematic;
            switch (lod)
            {
                case Core.Integrators.MissileLod.L0_Kinematic:        return "L0";
                case Core.Integrators.MissileLod.L1_PointMass3Dof:    return "L1";
                case Core.Integrators.MissileLod.L2_RateLimited3Dof:  return "L2";
                case Core.Integrators.MissileLod.L3_PseudoRb6Dof:     return "L3";
                case Core.Integrators.MissileLod.L4_FullAero6Dof:     return "L4";
                case Core.Integrators.MissileLod.L5_HardwareInTheLoop: return "L5";
                default: return "?";
            }
        }

        private static string PhaseShort(MissilePhase phase)
        {
            switch (phase)
            {
                case MissilePhase.PreLaunch: return "Pre";
                case MissilePhase.Boost:     return "Boost";
                case MissilePhase.Cruise:    return "Cruise";
                case MissilePhase.Terminal:  return "Terminal";
                case MissilePhase.Detonated: return "Detonated";
                case MissilePhase.Failed:    return "Failed";
                default: return phase.ToString();
            }
        }

        private static string FormatLength(float meters)
        {
            return meters >= 1000f
                ? $"{meters / 1000f:0.0}km"
                : $"{meters:0}m";
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
