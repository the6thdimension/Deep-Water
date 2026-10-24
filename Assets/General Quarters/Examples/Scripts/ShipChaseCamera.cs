using UnityEngine;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// A camera that follows a ship. Chase (the default) swings with her heading, from her quarter; orbit holds a
    /// fixed compass direction while she turns under it. Orbit and zoom come from <see cref="ConnInput"/>: mouse
    /// right-drag and wheel, R/F, a gamepad's right stick and triggers. The camera mode button toggles chase and orbit.
    /// </summary>
    public sealed class ShipChaseCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] ConnInput controls;
        [SerializeField] float distanceM = 240f;
        [SerializeField] float pitchDeg = 11f;
        [Tooltip("Chase: degrees off the bow the camera sits (180 is dead astern).")]
        [SerializeField] float yawOffsetDeg = 150f;
        [SerializeField] float minDistanceM = 40f, maxDistanceM = 2500f;
        [SerializeField] float pivotHeightM = 8f;

        bool _chase = true;
        float _worldYawDeg;

        public bool Chase => _chase;

        public void Follow(Transform ship, ConnInput input)
        {
            target = ship;
            controls = input;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            float heading = HeadingDeg(target);

            if (controls != null && controls.Ready)
            {
                if (controls.CameraModePressed)
                {
                    // Keep the view where it is when switching: carry the world yaw across.
                    if (_chase) _worldYawDeg = heading + yawOffsetDeg;
                    else yawOffsetDeg = _worldYawDeg - heading;
                    _chase = !_chase;
                }
                Vector2 orbit = controls.CameraOrbit * (90f * dt) + controls.CameraDrag * 0.2f;
                if (_chase) yawOffsetDeg += orbit.x; else _worldYawDeg += orbit.x;
                pitchDeg = Mathf.Clamp(pitchDeg - orbit.y, -5f, 85f);
                float zoom = controls.CameraZoom * 1.2f * dt + Mathf.Sign(controls.CameraScroll) * (controls.CameraScroll != 0f ? 0.1f : 0f);
                distanceM = Mathf.Clamp(distanceM * (1f - zoom), minDistanceM, maxDistanceM);
            }

            float yaw = _chase ? heading + yawOffsetDeg : _worldYawDeg;
            Vector3 pivot = target.position + Vector3.up * pivotHeightM;
            Vector3 position = pivot - Quaternion.Euler(pitchDeg, yaw, 0f) * Vector3.forward * distanceM;
            position.y = Mathf.Max(position.y, 3f);   // never under the sea
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(pivot - position, Vector3.up));
        }

        static float HeadingDeg(Transform t)
        {
            Vector3 f = t.forward;
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }
    }
}
