using UnityEngine;

namespace DeepWater.Sea.Examples
{
    /// <summary>
    /// Minimal review camera for the sea demo: orbit a pivot (right-drag), zoom (wheel),
    /// move the pivot (WASD, Q/E down/up), and jump between preset views (F1–F4).
    /// </summary>
    public sealed class SeaDemoCamera : MonoBehaviour
    {
        [System.Serializable]
        public struct View
        {
            public string name;
            public Vector3 pivot;
            public float yawDeg, pitchDeg, distanceM;
        }

        [SerializeField] View[] views =
        {
            new View { name = "Overview",      pivot = new Vector3(0f, 0f, 0f),     yawDeg = 35f,  pitchDeg = 14f, distanceM = 420f },
            new View { name = "Bridge height", pivot = new Vector3(0f, 18f, -40f),  yawDeg = 0f,   pitchDeg = 2f,  distanceM = 1f   },
            new View { name = "Low beam sea",  pivot = new Vector3(0f, 0f, 0f),     yawDeg = 90f,  pitchDeg = 3f,  distanceM = 260f },
            new View { name = "Small craft",   pivot = new Vector3(60f, 0f, -90f),  yawDeg = 220f, pitchDeg = 10f, distanceM = 45f  },
        };
        [SerializeField] float orbitDegPerPixel = 0.25f;
        [SerializeField] float zoomFactorPerNotch = 0.12f;
        [SerializeField] float moveSpeedMs = 60f;

        View _current;
        Vector3 _lastMouse;

        void Start() => Apply(0);

        void LateUpdate()
        {
            for (int i = 0; i < views.Length && i < 4; i++)
                if (Input.GetKeyDown(KeyCode.F1 + i)) Apply(i);

            Vector3 mouse = Input.mousePosition;
            if (Input.GetMouseButton(1))
            {
                Vector3 d = mouse - _lastMouse;
                _current.yawDeg += d.x * orbitDegPerPixel;
                _current.pitchDeg = Mathf.Clamp(_current.pitchDeg - d.y * orbitDegPerPixel, -10f, 89f);
            }
            _lastMouse = mouse;

            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f) _current.distanceM = Mathf.Max(1f, _current.distanceM * (1f - scroll * zoomFactorPerNotch));

            Quaternion yaw = Quaternion.Euler(0f, _current.yawDeg, 0f);
            Vector3 move = new Vector3(Axis(KeyCode.D, KeyCode.A), Axis(KeyCode.E, KeyCode.Q), Axis(KeyCode.W, KeyCode.S));
            _current.pivot += yaw * move * (moveSpeedMs * Time.unscaledDeltaTime);

            Quaternion rot = Quaternion.Euler(_current.pitchDeg, _current.yawDeg, 0f);
            transform.SetPositionAndRotation(_current.pivot - rot * Vector3.forward * _current.distanceM, rot);
        }

        void Apply(int index)
        {
            if (index < 0 || index >= views.Length) return;
            _current = views[index];
        }

        static float Axis(KeyCode positive, KeyCode negative)
            => (Input.GetKey(positive) ? 1f : 0f) - (Input.GetKey(negative) ? 1f : 0f);
    }
}
