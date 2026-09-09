using System.Collections;
using UnityEngine;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.ScriptableObjects.Profiles;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Fires N missiles of the same LOD at a single target with a configurable inter-shot
    /// delay. Sister to <see cref="LodComparisonRunner"/>:
    ///
    /// | Runner | Varies | Fixes |
    /// |---|---|---|
    /// | LodComparisonRunner | LOD | Time, target |
    /// | SalvoRunner | Launch time | LOD, target |
    ///
    /// Use this to exercise real salvo geometry — a leading missile reaches the target first
    /// and any later missiles see the target's reaction (or its destroyed state). Useful for
    /// validating that proximity-fuze + retarget logic behaves correctly when other missiles
    /// are inbound on the same target.
    /// </summary>
    public sealed class SalvoRunner : MonoBehaviour
    {
        [Header("Missile")]
        [Tooltip("Profile used for every missile in the salvo.")]
        [SerializeField] private MissileProfileSO profile;

        [Tooltip("LOD every missile in the salvo flies at.")]
        [SerializeField] private MissileLod lod = MissileLod.L2_RateLimited3Dof;

        [Header("Salvo")]
        [Tooltip("Number of missiles in the salvo. 1 = single shot; 8 = saturation salvo.")]
        [Range(1, 8)] [SerializeField] private int count = 4;

        [Tooltip("Seconds between consecutive shots in the salvo. 0 = ripple-fire (one per FixedUpdate); higher = staggered.")]
        [Range(0f, 5f)] [SerializeField] private float interShotDelayS = 0.4f;

        [Header("Target")]
        [Tooltip("Target every missile homes on. Leave null for a ballistic salvo (test dispersion).")]
        [SerializeField] private Transform target;

        [Header("Launch")]
        [SerializeField] private Vector3 launchPosition = Vector3.zero;
        [SerializeField] private Vector3 launchDirection = new Vector3(0f, 0.15f, 1f);

        [Tooltip("Lateral offset between adjacent rounds. Spreads the salvo so trails are visible side-by-side.")]
        [SerializeField] private float lateralSpacingM = 1.5f;

        [Header("Auto-Launch")]
        [Tooltip("Initial salvo delay after Start. Disable autoLaunch to fire only from the control panel.")]
        [SerializeField] private bool autoLaunchOnStart = true;
        [SerializeField] private float launchDelay = 1.5f;

        /// <summary>Public entry point for the control panel — fires the salvo right now.</summary>
        public void FireNow()
        {
            if (profile == null)
            {
                Debug.LogError("[GuidedFury Salvo] No profile assigned.");
                return;
            }
            StartCoroutine(SalvoCoroutine());
        }

        private void Start()
        {
            if (autoLaunchOnStart)
                Invoke(nameof(FireNow), launchDelay);
        }

        private IEnumerator SalvoCoroutine()
        {
            Vector3 dir = launchDirection.sqrMagnitude > 1e-6f
                ? launchDirection.normalized
                : Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(dir, Vector3.up);

            // Center the salvo on launchPosition so the formation is symmetric regardless
            // of N.
            float centerOffset = (count - 1) * 0.5f * lateralSpacingM;

            for (int i = 0; i < count; i++)
            {
                Vector3 lateral = Vector3.right * (i * lateralSpacingM - centerOffset);
                Vector3 spawnPos = launchPosition + lateral;
                SpawnOne(spawnPos, rotation, i);
                if (interShotDelayS > 0f)
                    yield return new WaitForSeconds(interShotDelayS);
                else
                    yield return new WaitForFixedUpdate();
            }
            Debug.Log($"[GuidedFury Salvo] Fired {count} × {lod} at {(target != null ? target.name : "no target")}.");
        }

        private void SpawnOne(Vector3 spawnPos, Quaternion rotation, int salvoIndex)
        {
            // Primitive stand-in + trail. Same shape as LodComparisonRunner but a uniform
            // color (since all rounds are the same LOD).
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"GF_Salvo_{lod}_{salvoIndex + 1}";
            go.transform.localScale = new Vector3(0.15f, 0.15f, 0.8f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            Color color = LodTrailColors.For(lod);
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = RangeMaterials.MakeColored(color);

            AttachTrail(go.transform, color);

            var behaviour = go.AddComponent<MissileBehaviour>();
            behaviour.Configure(profile, lod, target);
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
