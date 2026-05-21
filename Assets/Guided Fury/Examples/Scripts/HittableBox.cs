using UnityEngine;
using GuidedFury.Core.Damage;

namespace GuidedFury.Examples
{
    /// <summary>
    /// A target box that responds to missile impacts with a physics impulse.
    ///
    /// **Behavior:**
    /// - On impact, applies an impulse (momentum transfer) at the impact point, so the box
    ///   spins and topples realistically rather than translating uniformly.
    /// - Tints the renderer red briefly to make the hit visually obvious.
    /// - Optional: tracks number of hits, useful for diagnostics in PIE.
    ///
    /// **Requires:** Rigidbody and a Collider on the same GameObject. The collider does not
    /// need to be a trigger — the missile's fuze trigger handles the detection side.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class HittableBox : MonoBehaviour, IHittable
    {
        [Header("Impact Response")]
        [Tooltip("Multiplier applied to the missile's momentum (mass × velocity) when computing impulse. "
                 + "1.0 = full elastic transfer; values >1 exaggerate for visibility.")]
        [SerializeField] private float impulseMultiplier = 2f;

        [Tooltip("How long to tint the box red after a hit, in seconds.")]
        [SerializeField] private float hitFlashDuration = 0.5f;

        private Rigidbody rb;
        private Renderer rend;
        private Color originalColor;
        private float flashEndsAt;
        private bool flashing;
        private int hitCount;

        public int HitCount => hitCount;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rend = GetComponent<Renderer>();
            if (rend != null)
            {
                // Pull whichever color property the active shader uses (URP/HDRP _BaseColor,
                // built-in _Color). Material.color alone reads _Color only, which silently
                // returns black on URP/HDRP.
                originalColor = ReadMaterialColor(rend.material);
            }
        }

        private void Update()
        {
            // Restore the original colour after the flash duration.
            if (flashing && rend != null && Time.time > flashEndsAt)
            {
                RangeMaterials.SetMaterialColor(rend.material, originalColor);
                flashing = false;
            }
        }

        public void OnMissileHit(Vector3 impactPosition, Vector3 missileVelocity, float missileMassKg)
        {
            hitCount++;

            // Momentum transfer: impulse = m_missile × v_missile × multiplier. ForceMode.Impulse
            // applies it as a single instantaneous change in momentum (kg·m/s), so the box
            // gets a kick proportional to what hit it.
            if (rb != null)
            {
                Vector3 impulse = missileVelocity * (missileMassKg * impulseMultiplier);
                rb.AddForceAtPosition(impulse, impactPosition, ForceMode.Impulse);
            }

            // Visual flash so the hit is obvious during PIE. Use the pipeline-aware setter
            // so URP/HDRP materials actually respond.
            if (rend != null)
            {
                RangeMaterials.SetMaterialColor(rend.material, Color.red);
                flashEndsAt = Time.time + hitFlashDuration;
                flashing = true;
            }
        }

        private static Color ReadMaterialColor(Material material)
        {
            // Read whichever property the shader has. URP/HDRP use _BaseColor; built-in
            // Standard uses _Color. Default to white on a shader with neither.
            if (material == null) return Color.white;
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color"))     return material.GetColor("_Color");
            return Color.white;
        }
    }
}
