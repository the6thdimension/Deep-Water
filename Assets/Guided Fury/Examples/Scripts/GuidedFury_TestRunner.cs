using UnityEngine;
using GuidedFury.Core.Missile;
using GuidedFury.Core.Integrators;
using GuidedFury.ScriptableObjects.Profiles;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Developer-facing demo runner. Drop on an empty GameObject in any scene with a
    /// MissileProfileSO and (optionally) a target Transform assigned.
    ///
    /// Phase 2: L0 + L1 + homing.
    /// Phase 3: L2 + cone seeker.
    /// Phase 4: L3 pseudo-6DOF.
    /// Phase 5: visual differentiation (color tint + optional trail) so multiple test
    ///   runners in the same scene produce distinguishable missiles.
    ///
    /// **Not gameplay code.** When the RH Testing Suite has an integration-test seam, the
    /// equivalent logic moves into a test fixture (methodology candidate C3).
    /// </summary>
    public sealed class GuidedFury_TestRunner : MonoBehaviour
    {
        [Header("Missile")]
        [Tooltip("Profile to launch. Required.")]
        [SerializeField] private MissileProfileSO profile;

        [Tooltip("Optional missile prefab. If null, a primitive capsule is used as stand-in visual.")]
        [SerializeField] private GameObject missilePrefab;

        [Tooltip("LOD to fly the missile at.")]
        [SerializeField] private MissileLod lod = MissileLod.L1_PointMass3Dof;

        [Header("Target")]
        [Tooltip("Optional target Transform. If set, missile uses the profile's guidance law to home on it.")]
        [SerializeField] private Transform target;

        [Header("Launch")]
        [Tooltip("World position to launch from.")]
        [SerializeField] private Vector3 launchPosition = Vector3.zero;

        [Tooltip("World-space direction the missile points at launch.")]
        [SerializeField] private Vector3 launchDirection = new Vector3(0f, 0.2f, 1f);

        [Tooltip("Delay before launch, in seconds.")]
        [SerializeField] private float launchDelay = 0.5f;

        [Header("Visual (Phase 5)")]
        [Tooltip("Color applied to the spawned missile (primitive stand-in only). Helps distinguish multiple missiles in the same scene.")]
        [SerializeField] private Color missileColor = Color.white;

        [Tooltip("If true, attach a TrailRenderer to the missile so its flight path stays visible.")]
        [SerializeField] private bool attachTrail = true;

        [Tooltip("Trail width in meters.")]
        [SerializeField] private float trailWidth = 0.4f;

        [Tooltip("How many seconds of trail to keep behind the missile.")]
        [SerializeField] private float trailTime = 4f;

        [Header("Auto-Launch")]
        [Tooltip("If true, fires once automatically on Start after launchDelay. Disable if you only want to fire from a control panel.")]
        [SerializeField] private bool autoLaunchOnStart = true;

        private void Start()
        {
            if (profile == null)
            {
                Debug.LogError("[GuidedFury TestRunner] No MissileProfileSO assigned — cannot run.");
                return;
            }

            if (autoLaunchOnStart)
                Invoke(nameof(SpawnAndLaunch), launchDelay);
        }

        /// <summary>
        /// Public entry point for the control panel. Fires one missile immediately,
        /// ignoring launchDelay. Safe to call repeatedly.
        /// </summary>
        public void FireOnce() => SpawnAndLaunch();

        private void SpawnAndLaunch()
        {
            GameObject go = missilePrefab != null
                ? Instantiate(missilePrefab)
                : BuildPrimitiveStandIn();

            // Apply color (and tag the GO name for HUD readability) for the primitive path.
            // For prefab missiles, color application is the prefab author's job.
            if (missilePrefab == null)
            {
                ApplyMissileColor(go, missileColor);
                go.name = $"GF_{(profile != null ? profile.missileId : "?")}_{lod}";
            }

            if (attachTrail)
                AttachTrail(go, missileColor);

            var behaviour = go.GetComponent<MissileBehaviour>() ?? go.AddComponent<MissileBehaviour>();
            behaviour.Configure(profile, lod, target);

            Vector3 dir = launchDirection.sqrMagnitude > 1e-6f
                ? launchDirection.normalized
                : Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(dir, Vector3.up);

            behaviour.transform.SetPositionAndRotation(launchPosition, rotation);
            behaviour.Launch(launchPosition, rotation);

            string targetInfo = target != null ? $"homing on '{target.name}'" : "no target (ballistic)";
            Debug.Log($"[GuidedFury TestRunner] Launched '{profile.displayName}' at {lod}, {targetInfo}, " +
                      $"from {launchPosition} heading {dir:F2}.");
        }

        private GameObject BuildPrimitiveStandIn()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"GF_Missile_{(profile != null ? profile.missileId : "?")}";
            go.transform.localScale = new Vector3(0.15f, 0.15f, 0.8f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            return go;
        }

        private static void ApplyMissileColor(GameObject missile, Color color)
        {
            var rend = missile.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = RangeMaterials.MakeColored(color);
        }

        private void AttachTrail(GameObject missile, Color color)
        {
            // A trail child anchored at the missile's center. We use a child rather than
            // adding TrailRenderer directly to `missile` so the trail's transform / material
            // are independent of whatever else lives on the missile root.
            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(missile.transform, false);

            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.startWidth = trailWidth;
            trail.endWidth = trailWidth * 0.1f;
            trail.minVertexDistance = 0.1f;
            trail.autodestruct = false;

            // TrailRenderer needs a material with a transparent / unlit shader. Unlit/Color
            // is the most pipeline-portable choice — works in built-in, URP, and HDRP without
            // any package dependency.
            Shader unlit = Shader.Find("Unlit/Color");
            if (unlit != null)
            {
                var mat = new Material(unlit);
                mat.color = color;
                trail.material = mat;
            }
            // If Unlit/Color isn't found (rare — only on heavily stripped projects), the
            // TrailRenderer just renders magenta. Not worth more elaborate fallback for
            // example-tier code.

            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
