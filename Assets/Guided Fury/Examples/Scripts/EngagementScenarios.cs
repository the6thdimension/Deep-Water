using UnityEngine;
using GuidedFury.Core.Missile;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Quick scenario picker — spawns / replaces a target at preset positions and motions
    /// relative to the player's launcher. Lets you exercise different engagement geometries
    /// without manually placing target GameObjects each time.
    ///
    /// **Scenarios:**
    /// - **Stationary** — target sits 2 km downrange, holds. Tests basic intercept.
    /// - **Head-on** — target inbound from 3 km at 250 m/s. Tests closing-speed envelope.
    /// - **Crossing** — target at 1.5 km crossing perpendicular at 120 m/s. Tests lead/lag.
    /// - **Tail-chase** — target ahead going away at 200 m/s. Tests fuel + closing margin.
    ///
    /// **How it integrates:** the panel's <see cref="MissileControlPanel"/> has a dropdown
    /// row that calls <see cref="SpawnScenario(Scenario)"/>. The picker auto-rebinds every
    /// <see cref="GuidedFury_TestRunner"/>'s target to the new scenario target.
    /// </summary>
    public sealed class EngagementScenarios : MonoBehaviour
    {
        public enum Scenario
        {
            Stationary,
            HeadOn,
            Crossing,
            TailChase,
        }

        [Header("Layout")]
        [Tooltip("Anchor for scenario spawn. Typically the launcher's position. If null, uses this GameObject's position.")]
        [SerializeField] private Transform anchor;

        [Tooltip("Target prefab to spawn. If null, a primitive cube stand-in is created.")]
        [SerializeField] private GameObject targetPrefab;

        // Currently-spawned scenario target. Destroyed on each new spawn.
        private GameObject currentTarget;
        private Scenario currentScenario;

        public Scenario CurrentScenario => currentScenario;
        public GameObject CurrentTarget => currentTarget;

        // ============================================================
        // Public API
        // ============================================================

        public void SpawnScenario(Scenario scenario)
        {
            // Destroy any previous target first so we don't accumulate.
            if (currentTarget != null)
            {
                Destroy(currentTarget);
                currentTarget = null;
            }

            Vector3 origin = anchor != null ? anchor.position : transform.position;
            Vector3 fwd    = anchor != null ? anchor.forward  : Vector3.forward;
            Vector3 right  = anchor != null ? anchor.right    : Vector3.right;

            switch (scenario)
            {
                case Scenario.Stationary:
                    currentTarget = SpawnTarget(origin + fwd * 2000f + Vector3.up * 200f,
                                                Vector3.zero, "Target_Stationary");
                    break;

                case Scenario.HeadOn:
                    // Spawned downrange, heading back toward the launcher.
                    currentTarget = SpawnTarget(origin + fwd * 3000f + Vector3.up * 250f,
                                                -fwd * 250f, "Target_HeadOn");
                    break;

                case Scenario.Crossing:
                    // Off to one side, crossing perpendicular to the launch axis.
                    currentTarget = SpawnTarget(origin + fwd * 1500f + Vector3.up * 200f - right * 400f,
                                                right * 120f, "Target_Crossing");
                    break;

                case Scenario.TailChase:
                    // Ahead of the launcher, heading away in the same direction.
                    currentTarget = SpawnTarget(origin + fwd * 1500f + Vector3.up * 200f,
                                                fwd * 200f, "Target_TailChase");
                    break;
            }

            currentScenario = scenario;

            // Re-bind every TestRunner / LodComparisonRunner / SalvoRunner to the new target.
            RebindTargets(currentTarget != null ? currentTarget.transform : null);

            Debug.Log($"[GuidedFury Scenarios] Spawned {scenario} target.");
        }

        public void ClearScenario()
        {
            if (currentTarget != null)
            {
                Destroy(currentTarget);
                currentTarget = null;
            }
            RebindTargets(null);
        }

        // ============================================================
        // Internals
        // ============================================================

        private GameObject SpawnTarget(Vector3 position, Vector3 velocity, string name)
        {
            GameObject go;
            if (targetPrefab != null)
            {
                go = Instantiate(targetPrefab);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = new Vector3(4f, 4f, 8f);

                // Apply a bright tint so the target stands out from environment.
                var rend = go.GetComponent<Renderer>();
                if (rend != null)
                    rend.sharedMaterial = RangeMaterials.MakeColored(new Color(1f, 0.5f, 0.2f));
            }

            go.name = name;
            go.transform.position = position;

            // Hittable + Rigidbody so missiles can register impacts.
            if (go.GetComponent<HittableBox>() == null)
                go.AddComponent<HittableBox>();
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = 500f;
            // Velocity: kinematic motion if non-zero — we use a tiny mover to keep things
            // deterministic and avoid Rigidbody drift from accumulated forces.
            rb.isKinematic = true;
            rb.useGravity = false;

            if (velocity.sqrMagnitude > 1e-6f)
            {
                var mover = go.AddComponent<ConstantVelocityMover>();
                mover.WorldVelocity = velocity;
            }

            return go;
        }

        private static void RebindTargets(Transform target)
        {
            // Reflectively set the `target` field on any runner that has one. Avoids tight
            // coupling — works with TestRunner, LodComparisonRunner, SalvoRunner, future
            // runners that follow the same convention.
            var allRunners = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var b in allRunners)
            {
                if (b == null) continue;
                var t = b.GetType();
                if (t.Namespace != "GuidedFury.Examples") continue;
                if (!t.Name.EndsWith("Runner") && t.Name != "GuidedFury_TestRunner") continue;

                var field = t.GetField("target",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(Transform))
                    field.SetValue(b, target);
            }
        }

        /// <summary>Constant-velocity kinematic mover — used for scenario targets that need motion.</summary>
        private sealed class ConstantVelocityMover : MonoBehaviour
        {
            public Vector3 WorldVelocity;
            private void FixedUpdate()
            {
                transform.position += WorldVelocity * Time.fixedDeltaTime;
            }
        }
    }
}
