using UnityEngine;

namespace RHRadarSuite.Examples
{
    /// <summary>
    /// Minimal example mover: flies a level circular orbit around its start
    /// point, nose along the tangent. Exists to exercise radar behavior on a
    /// moving, turning platform (body-relative sectors, sweep gizmos) — it is
    /// demo scaffolding, not simulation core.
    /// </summary>
    [AddComponentMenu("RH Radar Suite/Examples/Circle Flyer")]
    public class CircleFlyer : MonoBehaviour
    {
        [Tooltip("Orbit radius in meters")]
        [Min(1f)]
        public float radiusM = 600f;

        [Tooltip("Orbit rate in degrees per second (negative = clockwise)")]
        public float degreesPerSecond = 8f;

        private Vector3 center;
        private float angleDeg;

        private void Start()
        {
            center = transform.position - transform.right * radiusM;
            angleDeg = 0f;
        }

        private void Update()
        {
            angleDeg += degreesPerSecond * Time.deltaTime;
            Quaternion spin = Quaternion.AngleAxis(angleDeg, Vector3.up);
            Vector3 radial = spin * Vector3.right * radiusM;
            Vector3 tangent = spin * Vector3.forward;

            transform.position = center + radial;
            transform.rotation = Quaternion.LookRotation(
                degreesPerSecond >= 0f ? tangent : -tangent, Vector3.up);
        }
    }
}
