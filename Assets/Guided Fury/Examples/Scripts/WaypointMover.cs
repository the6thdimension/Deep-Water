using UnityEngine;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Kinematic waypoint-following mover for range targets and platforms (racetrack
    /// drones, the airborne testbed). Follows a closed or open chain of world-space
    /// waypoints at constant speed, facing its direction of travel.
    ///
    /// Simulates on FixedUpdate (deterministic — missiles sample this transform during
    /// their own FixedUpdate) and interpolates the rendered pose in Update, the same
    /// pattern as MissileBehaviour / ConstantVelocityMover (Pat5). The negative execution
    /// order guarantees the exact sim pose is restored before any missile's FixedUpdate
    /// reads it.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class WaypointMover : MonoBehaviour
    {
        [Tooltip("World-space waypoints, visited in order. With Loop on, the path closes back to the first point.")]
        [SerializeField] private Vector3[] waypoints;

        [Tooltip("Constant travel speed, m/s.")]
        [SerializeField] private float speedMps = 80f;

        [Tooltip("Close the path back to waypoint 0 (racetrack). Off = stop at the last waypoint.")]
        [SerializeField] private bool loop = true;

        [Tooltip("Rotate to face the direction of travel.")]
        [SerializeField] private bool faceVelocity = true;

        // -- Sim state (advanced in FixedUpdate only) ----------------------------
        private int nextIndex;
        private bool seeded;
        private bool finished;
        private Vector3 prevSimPos, currSimPos;
        private Quaternion prevSimRot = Quaternion.identity, currSimRot = Quaternion.identity;

        /// <summary>Current sim velocity (world m/s) — the segment direction at speed.</summary>
        public Vector3 Velocity { get; private set; }

        public void SetPath(Vector3[] points, float speed, bool looped)
        {
            waypoints = points;
            speedMps = speed;
            loop = looped;
            seeded = false;
            finished = false;
            nextIndex = 0;
        }

        private void FixedUpdate()
        {
            if (waypoints == null || waypoints.Length == 0 || finished) return;

            if (!seeded)
            {
                seeded = true;
                prevSimPos = currSimPos = transform.position;
                prevSimRot = currSimRot = transform.rotation;
                nextIndex = 0;
            }

            // Advance toward the next waypoint, consuming leftover distance across
            // waypoint boundaries so speed stays exact through corners.
            float remaining = speedMps * Time.fixedDeltaTime;
            Vector3 pos = currSimPos;
            Vector3 dir = currSimRot * Vector3.forward;
            int guard = waypoints.Length + 1; // at most one full lap per step
            while (remaining > 1e-6f && guard-- > 0)
            {
                Vector3 target = waypoints[nextIndex];
                Vector3 to = target - pos;
                float dist = to.magnitude;
                if (dist <= remaining)
                {
                    pos = target;
                    remaining -= dist;
                    nextIndex++;
                    if (nextIndex >= waypoints.Length)
                    {
                        if (loop) nextIndex = 0;
                        else { finished = true; break; }
                    }
                }
                else
                {
                    dir = to / dist;
                    pos += dir * remaining;
                    remaining = 0f;
                }
            }

            prevSimPos = currSimPos;
            prevSimRot = currSimRot;
            currSimPos = pos;
            Velocity = finished ? Vector3.zero : dir * speedMps;
            if (faceVelocity && dir.sqrMagnitude > 1e-6f)
                currSimRot = Quaternion.LookRotation(dir, Vector3.up);

            // Exact sim pose for fixed-phase consumers (missile guidance reads this).
            transform.SetPositionAndRotation(currSimPos, currSimRot);
        }

        private void Update()
        {
            if (!seeded) return;
            float step = Time.fixedDeltaTime;
            float alpha = step > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / step) : 1f;
            transform.SetPositionAndRotation(
                Vector3.Lerp(prevSimPos, currSimPos, alpha),
                Quaternion.Slerp(prevSimRot, currSimRot, alpha));
        }

        private void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Length < 2) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                Vector3 a = waypoints[i];
                Vector3 b = waypoints[(i + 1) % waypoints.Length];
                if (i == waypoints.Length - 1 && !loop) break;
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
