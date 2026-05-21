using UnityEngine;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.ScriptableObjects.Profiles;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Fires one missile per LOD simultaneously, color-coded, at the same target. The
    /// quickest way to see how the LODs differ visually and behaviorally on identical
    /// initial conditions.
    ///
    /// Each missile spawns at a small lateral offset so trails don't overlap.
    /// </summary>
    public sealed class LodComparisonRunner : MonoBehaviour
    {
        [Header("Missile")]
        [Tooltip("Profile used for every missile in the salvo. Required.")]
        [SerializeField] private MissileProfileSO profile;

        [Header("Target")]
        [Tooltip("Target every missile homes on. Leave null for a ballistic comparison.")]
        [SerializeField] private Transform target;

        [Header("Launch")]
        [Tooltip("World position of the salvo center. Each missile spawns at a lateral offset from here.")]
        [SerializeField] private Vector3 launchPosition = Vector3.zero;

        [Tooltip("World-space direction every missile points at launch.")]
        [SerializeField] private Vector3 launchDirection = new Vector3(0f, 0.15f, 1f);

        [Tooltip("Lateral spacing between adjacent missiles in the salvo, meters.")]
        [SerializeField] private float spacingM = 4f;

        [Tooltip("Delay before the salvo fires, in seconds.")]
        [SerializeField] private float launchDelay = 1.5f;

        [Header("LODs to compare")]
        [Tooltip("Each ticked LOD spawns one missile in the salvo.")]
        [SerializeField] private bool includeL0 = true;
        [SerializeField] private bool includeL1 = true;
        [SerializeField] private bool includeL2 = true;
        [SerializeField] private bool includeL3 = true;

        [Header("Auto-Launch")]
        [Tooltip("If true, fires the salvo automatically on Start after launchDelay. Disable to only fire from a control panel.")]
        [SerializeField] private bool autoLaunchOnStart = true;

        /// <summary>Public entry point for the control panel — fires the salvo immediately, ignoring launchDelay.</summary>
        public void FireSalvoNow() => FireSalvo();

        // Distinct colors per LOD — keep these stable so a player learns the mapping.
        private static readonly Color L0Color = new Color(0.9f, 0.9f, 0.2f); // yellow
        private static readonly Color L1Color = new Color(0.2f, 0.9f, 0.3f); // green
        private static readonly Color L2Color = new Color(0.3f, 0.6f, 1.0f); // blue
        private static readonly Color L3Color = new Color(0.95f, 0.3f, 0.6f); // magenta

        private void Start()
        {
            if (profile == null)
            {
                Debug.LogError("[GuidedFury LodComparison] No MissileProfileSO assigned — cannot run.");
                return;
            }
            if (autoLaunchOnStart)
                Invoke(nameof(FireSalvo), launchDelay);
        }

        private void FireSalvo()
        {
            // Build the list of LODs to fire. The lateral offsets are evenly distributed
            // around the launch point so the salvo is symmetric regardless of which LODs
            // are enabled.
            int included = (includeL0 ? 1 : 0) + (includeL1 ? 1 : 0) + (includeL2 ? 1 : 0) + (includeL3 ? 1 : 0);
            if (included == 0)
            {
                Debug.LogWarning("[GuidedFury LodComparison] No LODs enabled.");
                return;
            }

            int slot = 0;
            float centerOffset = (included - 1) * 0.5f * spacingM;

            if (includeL0) FireOne(MissileLod.L0_Kinematic,     L0Color, slot++, centerOffset);
            if (includeL1) FireOne(MissileLod.L1_PointMass3Dof, L1Color, slot++, centerOffset);
            if (includeL2) FireOne(MissileLod.L2_RateLimited3Dof, L2Color, slot++, centerOffset);
            if (includeL3) FireOne(MissileLod.L3_PseudoRb6Dof,  L3Color, slot++, centerOffset);

            Debug.Log($"[GuidedFury LodComparison] Fired {included} missiles, one per enabled LOD.");
        }

        private void FireOne(MissileLod lod, Color color, int slot, float centerOffset)
        {
            // Lateral offset across +X axis. Slot 0 at left edge, slot N-1 at right edge.
            Vector3 lateral = Vector3.right * (slot * spacingM - centerOffset);
            Vector3 spawnPos = launchPosition + lateral;

            // Build the missile via a primitive stand-in, plus a TrailRenderer for path
            // visibility. We mirror enough of GuidedFury_TestRunner here to keep this self-
            // contained — extracting a shared helper is worth doing only when a third
            // demo runner needs the same code.
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"GF_Cmp_{lod}";
            go.transform.localScale = new Vector3(0.15f, 0.15f, 0.8f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = RangeMaterials.MakeColored(color);

            AttachTrail(go.transform, color);

            var behaviour = go.AddComponent<MissileBehaviour>();
            behaviour.Configure(profile, lod, target);

            Vector3 dir = launchDirection.sqrMagnitude > 1e-6f
                ? launchDirection.normalized
                : Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(dir, Vector3.up);

            behaviour.transform.SetPositionAndRotation(spawnPos, rotation);
            behaviour.Launch(spawnPos, rotation);
        }

        private static void AttachTrail(Transform missile, Color color)
        {
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(missile, false);

            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 6f;
            trail.startWidth = 0.6f;
            trail.endWidth = 0.05f;
            trail.minVertexDistance = 0.1f;
            trail.autodestruct = false;

            Shader unlit = Shader.Find("Unlit/Color");
            if (unlit != null)
            {
                var mat = new Material(unlit);
                mat.color = color;
                trail.material = mat;
            }

            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
