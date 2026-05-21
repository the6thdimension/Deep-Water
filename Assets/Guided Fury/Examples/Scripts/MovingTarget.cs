using UnityEngine;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Drifts back and forth along a single axis at constant speed. Useful as a "crossing
    /// target" to exercise lead pursuit / ProNav. Stays kinematic in physics so the
    /// missile's impulse doesn't disrupt the patrol pattern; the path is purely procedural.
    /// </summary>
    public sealed class MovingTarget : MonoBehaviour
    {
        [Header("Drift")]
        [Tooltip("World-space axis the target moves along. Magnitude is treated as the half-width of the patrol.")]
        [SerializeField] private Vector3 driftAxis = new Vector3(50f, 0f, 0f);

        [Tooltip("Speed of patrol motion in m/s.")]
        [SerializeField] private float speedMps = 30f;

        [Tooltip("Phase offset (0..1) to stagger multiple targets so they're not in lockstep.")]
        [SerializeField, Range(0f, 1f)] private float phaseOffset = 0f;

        private Vector3 origin;
        private float phase;

        private void Awake()
        {
            origin = transform.position;
            // Phase is measured in radians of the sinusoidal patrol; convert from 0..1.
            phase = phaseOffset * Mathf.PI * 2f;
        }

        private void Update()
        {
            // Use a sinusoid so the target slows, reverses, and accelerates again — much more
            // interesting than a linear back-and-forth (which would have step discontinuities
            // in velocity at the endpoints).
            float halfWidth = driftAxis.magnitude;
            Vector3 axis = halfWidth > 1e-6f ? driftAxis / halfWidth : Vector3.zero;

            // Angular frequency such that one full back-and-forth covers 2 × halfWidth × π distance
            // at the requested speed. (Sin moves at peak speed = halfWidth × ω; we set that to speedMps.)
            float omega = halfWidth > 1e-6f ? speedMps / halfWidth : 0f;
            float t = Time.time * omega + phase;

            transform.position = origin + axis * (halfWidth * Mathf.Sin(t));
        }
    }
}
