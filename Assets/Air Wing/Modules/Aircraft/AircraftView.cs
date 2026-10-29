using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.FlightControls;
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
        /// <summary>Airborne at this transform, or on a runway's take-off point (Landing Gear L1).</summary>
        public enum StartMode { Airborne, Runway }

        [Header("Start (read once, at the start of a run)")]
        [SerializeField] int aircraftId = 1;
        [SerializeField] StartSeat seat = StartSeat.Player;
        [Tooltip("Airborne here, or on the runway below (needs Landing Gear L1 on the fidelity sheet).")]
        [SerializeField] StartMode startMode = StartMode.Airborne;
        [SerializeField] World.FlatGround startRunway;
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
        GearRig _gear;

        public int AircraftId => aircraftId;
        public StartSeat Seat => seat;
        public StartMode StartingMode => startMode;
        public World.FlatGround StartRunway => startRunway;
        public float StartSpeedMps => startSpeedKt * (float)Units.KnotToMetrePerSecond;
        public float StartFuelFraction => startFuelFraction;
        public float HoldThrottle => holdThrottle;

        /// <summary>How it is drawn (presentation only).</summary>
        public AircraftVisualsSO Visuals => visuals;

        /// <summary>The core's aircraft, once the runner has built it.</summary>
        public Core.Aircraft Aircraft => _aircraft;

        int _resets;

        internal void Bind(Core.Aircraft aircraft)
        {
            _aircraft = aircraft;
            _previous = _current = aircraft.State;
            FindSurfaces();
            // The model's origin is not the centre of mass the core flies: place it where the visuals say.
            Transform model = transform.Find("Model");
            if (model != null && visuals != null)
            {
                model.localPosition = visuals.modelOffsetM;
                // The gear rig reads its rest pose now, before anything moves it.
                if (visuals.gearParts.Length > 0) _gear = new GearRig(model, model, visuals.gearParts, this);
            }
            Apply(_current);
        }

        /// <summary>Called by the runner around each tick.</summary>
        internal void BeforeTick() => _previous = _current;
        internal void AfterTick()
        {
            _current = _aircraft.State;
            if (_aircraft.Resets != _resets)
            {
                // A reset jumps the jet back to its start: snap there rather than sweep across the map.
                _resets = _aircraft.Resets;
                _previous = _current;
            }
        }

        void Update()
        {
            if (_aircraft == null) return;
            float t = interpolate && Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            transform.SetPositionAndRotation(Vector3.Lerp(_previous.Position, _current.Position, t),
                                             Quaternion.Slerp(_previous.Attitude, _current.Attitude, t));
            AnimateSurfaces(_aircraft.LastInputs, _aircraft.Controls is FlightControlsL1 ? (ControlState?)_aircraft.State.Controls : null);
            // The gear follows the core: down at L0 (no gear part to move it), its transit at Landing Gear L1.
            if (_gear != null)
                _gear.Pose(_aircraft.Gear is Core.LandingGear.LandingGearL1 ? Mathf.Lerp(_previous.GearDeployed, _current.GearDeployed, t) : 1f);
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

        /// <summary>
        /// Moving parts: at L0 they follow the Pilot Inputs; at L1 they follow the actuators (<paramref name="l1"/>),
        /// each as its deflection over the part's own travel.
        /// </summary>
        void AnimateSurfaces(in PilotInputs inputs, ControlState? l1)
        {
            for (int i = 0; i < _surfaces.Length; i++)
            {
                if (_surfaces[i] == null) continue;
                AircraftVisualsSO.Surface s = visuals.surfaces[i];
                float drive;
                switch (s.driver)
                {
                    // At L1 the HARV signs: aileron + rolls right, stabilator + trailing edge down, rudder + trailing edge left.
                    case SurfaceDriver.Roll: drive = l1.HasValue ? l1.Value.AileronRad * Mathf.Rad2Deg / s.maxAngleDeg : inputs.StickRoll; break;
                    // The model's stabilator axes were set for AerialArcade, whose pitch is positive pushing; ours is positive pulling.
                    case SurfaceDriver.Pitch: drive = l1.HasValue ? l1.Value.StabilatorRad * Mathf.Rad2Deg / s.maxAngleDeg : -inputs.StickPitch; break;
                    case SurfaceDriver.Pedals: drive = l1.HasValue ? -l1.Value.RudderRad * Mathf.Rad2Deg / s.maxAngleDeg : inputs.Pedals; break;
                    case SurfaceDriver.Flaps: drive = inputs.Has(PilotSwitches.FlapsDown) ? 1f : 0f; break;
                    default: drive = inputs.Has(PilotSwitches.SpeedBrake) ? 1f : 0f; break;
                }
                _angles[i] = Mathf.Lerp(_angles[i], drive * s.maxAngleDeg, 1f - Mathf.Exp(-s.smoothing * Time.deltaTime));
                _surfaces[i].localRotation = Quaternion.Euler(_restEuler[i] + s.axis * _angles[i]);
            }
        }
    }
}
