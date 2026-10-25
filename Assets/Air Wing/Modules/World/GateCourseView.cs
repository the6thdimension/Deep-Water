using UnityEngine;
using UnityEngine.Rendering;
using DeepWater.AirWing.Core.Gates;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Modules.World
{
    /// <summary>
    /// Flies a gate course in a scene (W14). It bakes the course, draws each gate as a ring (the next one in its own
    /// colour), and each fixed step shows the course one tick of the watched aircraft's flight. Each gate flown is
    /// logged with what the jet held and the gate's bands. Execution order −40: after the runner has stepped (−50).
    /// It only reads the flight, so it cannot change it.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class GateCourseView : MonoBehaviour
    {
        [SerializeField] AirWingRunner runner;
        [SerializeField] GateCourseSO course;
        [Tooltip("The aircraft whose flight the course watches.")]
        [SerializeField] int aircraftId = 1;
        [Tooltip("The next gate's ring.")]
        [SerializeField] Material nextGateMaterial;
        [Tooltip("Every other ring.")]
        [SerializeField] Material gateMaterial;
        [Tooltip("Width of a ring's line, m.")]
        [SerializeField] float ringWidthM = 5f;
        [SerializeField, Range(16, 128)] int ringSegments = 64;

        GateCourse _course;
        Core.Aircraft _aircraft;
        AirframeState _before;
        bool _haveBefore;
        LineRenderer[] _rings;

        /// <summary>The course being flown (null until Start).</summary>
        public GateCourse Course => _course;

        void Start()
        {
            if (runner == null || course == null)
            {
                Debug.LogError($"[Air Wing] {name}: assign the runner and the gate course.", this);
                enabled = false;
                return;
            }
            _course = course.Bake();
            _rings = new LineRenderer[_course.Gates.Count];
            for (int i = 0; i < _rings.Length; i++) _rings[i] = Ring(_course.Gates[i]);
            Paint();
            Debug.Log($"[Air Wing] Gate course '{course.name}': {_rings.Length} gates, watching aircraft {aircraftId}.", this);
        }

        void FixedUpdate()
        {
            if (_course == null || runner.Flight == null) return;
            if (_aircraft == null && !Find()) return;

            AirframeState now = _aircraft.State;
            if (_haveBefore && _course.Observe(runner.Flight.Tick - 1, _before, now, out GatePass pass))
            {
                Gate g = _course.Gates[pass.Gate];
                Debug.Log($"[Air Wing] {g.Name} ({pass.Gate + 1}/{_course.Gates.Count}, lap {pass.Lap + 1}) at tick {pass.Tick}: " +
                          $"{pass.SpeedKt:F0} kt ({g.SpeedKt.Describe("kt")}, {(pass.SpeedOk ? "held" : "missed")}), " +
                          $"{pass.AltitudeFt:F0} ft ({g.AltitudeFt.Describe("ft")}, {(pass.AltitudeOk ? "held" : "missed")}), " +
                          $"{pass.OffCentreM:F0} m off centre.", this);
                Paint();
            }
            _before = now;
            _haveBefore = true;
        }

        bool Find()
        {
            foreach (Core.Aircraft a in runner.Flight.Aircraft)
                if (a.Id == aircraftId) { _aircraft = a; return true; }
            return false;
        }

        LineRenderer Ring(in Gate gate)
        {
            var go = new GameObject(gate.Name);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(gate.Centre, Quaternion.Euler(0f, gate.HeadingDeg, 0f));
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = ringWidthM;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = ringSegments;
            for (int i = 0; i < ringSegments; i++)
            {
                float a = 2f * Mathf.PI * i / ringSegments;
                line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * gate.RadiusM);   // upright, facing local +Z
            }
            return line;
        }

        void Paint()
        {
            for (int i = 0; i < _rings.Length; i++)
                _rings[i].sharedMaterial = i == _course.Next ? nextGateMaterial : gateMaterial;
        }

        void OnDrawGizmos()
        {
            if (course == null || course.gates == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            foreach (GateCourseSO.GateEntry g in course.gates)
            {
                Gizmos.matrix = Matrix4x4.TRS(g.centreM, Quaternion.Euler(0f, g.headingDeg, 0f), Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(2f * g.radiusM, 2f * g.radiusM, 1f));
                Gizmos.DrawLine(Vector3.zero, Vector3.forward * g.radiusM);
            }
        }
    }
}
