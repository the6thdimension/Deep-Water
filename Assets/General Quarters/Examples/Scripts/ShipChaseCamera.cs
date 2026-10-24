using UnityEngine;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// A camera that follows a ship. Chase (the default) swings with her heading, from her quarter; orbit holds a
    /// fixed compass direction while she turns under it. Orbit and zoom come from <see cref="ConnInput"/>: mouse
    /// right-drag and wheel, R/F, a gamepad's right stick and triggers. The camera mode button toggles chase and orbit.
    /// On the range it can also watch something else for a while (a track or a missile in flight, <see cref="Watch"/>),
    /// chasing along that object's forward axis, and come back to the ship with <see cref="WatchShip"/>.
    /// </summary>
    [DefaultExecutionOrder(1000)]   // after whatever it follows has moved this frame (the range's stand-ins move in LateUpdate)
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
        Transform _ship;
        float _shipDistanceM, _shipPivotM;

        public bool Chase => _chase;
        /// <summary>What the camera is on now: the ship, or what it is watching.</summary>
        public Transform Target => target;
        public bool OnShip => target != null && target == _ship;

        void Awake()
        {
            if (_ship == null) _ship = target;   // a ship wired in the scene is the one it comes home to
        }

        public void Follow(Transform ship, ConnInput input)
        {
            target = _ship = ship;
            controls = input;
        }

        /// <summary>Watch <paramref name="other"/> from <paramref name="distance"/> metres until <see cref="WatchShip"/>.</summary>
        public void Watch(Transform other, float distance)
        {
            if (other == null) { WatchShip(); return; }
            if (OnShip) { _shipDistanceM = distanceM; _shipPivotM = pivotHeightM; }
            target = other;
            distanceM = distance;
            pivotHeightM = 0f;
        }

        /// <summary>Back on the ship, at the distance it had before it watched anything else.</summary>
        public void WatchShip()
        {
            if (OnShip || _ship == null) return;
            target = _ship;
            distanceM = _shipDistanceM > 0f ? _shipDistanceM : distanceM;
            pivotHeightM = _shipDistanceM > 0f ? _shipPivotM : pivotHeightM;
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
