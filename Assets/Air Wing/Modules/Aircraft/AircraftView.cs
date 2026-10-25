using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Modules.Aircraft
{
    /// <summary>
    /// One aircraft in a scene: how it starts (read by <see cref="AirWingRunner"/>) and how it is drawn. The flight
    /// core owns the state; this view only follows it. The pose is interpolated between the last two ticks (Pat5),
    /// so the jet moves on every rendered frame at a cost of one tick of latency, and the moving parts follow the
    /// last Pilot Inputs (presentation only). No vendor script and no Rigidbody (AP8): the core writes the motion.
    /// </summary>
    public sealed class AircraftView : MonoBehaviour
    {
        public enum StartSeat { Player, Hold }

        [Header("Start (read once, at the start of a run)")]
        [SerializeField] int aircraftId = 1;
        [SerializeField] StartSeat seat = StartSeat.Player;
        [Tooltip("Start speed, knots true airspeed (W22).")]
        [SerializeField] float startSpeedKt = 400f;
        [Range(0f, 1f)] [SerializeField] float startFuelFraction = 0.5f;
        [Tooltip("Throttle of a Hold seat, 0 … 1 of military power.")]
        [Range(0f, 1f)] [SerializeField] float holdThrottle = 0.8f;

        [Header("Presentation")]
        [SerializeField] AircraftVisualsSO visuals;
        [SerializeField] bool interpolate = true;

        Core.Aircraft _aircraft;
        AirframeState _previous, _current;
        Transform[] _surfaces;
        Vector3[] _restEuler;
        float[] _angles;

        public int AircraftId => aircraftId;
        public StartSeat Seat => seat;
        public float StartSpeedMps => startSpeedKt * (float)Units.KnotToMetrePerSecond;
        public float StartFuelFraction => startFuelFraction;
        public float HoldThrottle => holdThrottle;

        /// <summary>The core's aircraft, once the runner has built it.</summary>
        public Core.Aircraft Aircraft => _aircraft;

        internal void Bind(Core.Aircraft aircraft)
        {
            _aircraft = aircraft;
            _previous = _current = aircraft.State;
            FindSurfaces();
            Apply(_current);
        }

        /// <summary>Called by the runner around each tick.</summary>
        internal void BeforeTick() => _previous = _current;
        internal void AfterTick() => _current = _aircraft.State;

        void Update()
        {
            if (_aircraft == null) return;
            float t = interpolate && Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            transform.SetPositionAndRotation(Vector3.Lerp(_previous.Position, _current.Position, t),
                                             Quaternion.Slerp(_previous.Attitude, _current.Attitude, t));
            AnimateSurfaces(_aircraft.LastInputs);
        }

        void Apply(in AirframeState s) => transform.SetPositionAndRotation(s.Position, s.Attitude);

        void FindSurfaces()
        {
            int n = visuals != null ? visuals.surfaces.Length : 0;
            _surfaces = new Transform[n];
            _restEuler = new Vector3[n];
            _angles = new float[n];
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < n; i++)
            {
                string name = visuals.surfaces[i].transformName;
                foreach (Transform t in all)
                    if (t.name == name) { _surfaces[i] = t; _restEuler[i] = t.localEulerAngles; break; }
                if (_surfaces[i] == null)
                    Debug.LogWarning($"[Air Wing] {name}: no '{name}' transform under the model; that surface will not move.", this);
            }
        }

        void AnimateSurfaces(in PilotInputs inputs)
        {
            for (int i = 0; i < _surfaces.Length; i++)
            {
                if (_surfaces[i] == null) continue;
                AircraftVisualsSO.Surface s = visuals.surfaces[i];
                float drive;
                switch (s.driver)
                {
                    case SurfaceDriver.Roll: drive = inputs.StickRoll; break;
                    // The model's stabilator axes were set for AerialArcade, whose pitch is positive pushing; ours is positive pulling.
                    case SurfaceDriver.Pitch: drive = -inputs.StickPitch; break;
                    case SurfaceDriver.Pedals: drive = inputs.Pedals; break;
                    case SurfaceDriver.Flaps: drive = inputs.Has(PilotSwitches.FlapsDown) ? 1f : 0f; break;
                    default: drive = inputs.Has(PilotSwitches.SpeedBrake) ? 1f : 0f; break;
                }
                _angles[i] = Mathf.Lerp(_angles[i], drive * s.maxAngleDeg, 1f - Mathf.Exp(-s.smoothing * Time.deltaTime));
                _surfaces[i].localRotation = Quaternion.Euler(_restEuler[i] + s.axis * _angles[i]);
            }
        }
    }
}
