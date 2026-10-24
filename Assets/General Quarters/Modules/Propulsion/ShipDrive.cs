using UnityEngine;
using DeepWater.GeneralQuarters.Core.EngineRoom;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Modules.Hull;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Modules.Propulsion
{
    /// <summary>
    /// Drives the ship's Rigidbody in the horizontal plane from the Helm and the Engine Room, each fixed step.
    /// Sea State still floats her (heave, roll, pitch); this supplies surge, sway and yaw.
    ///
    /// How the MMG model meets PhysX: the MMG equations give the accelerations u̇, v̇, ṙ with the ship's added
    /// mass in them. PhysX only knows the rigid mass, so the drive applies exactly the force and yaw torque that
    /// make the rigid body accelerate as MMG says: F = m·a_G, τ = I_zz·ṙ. Added mass is honoured without
    /// teaching PhysX about it. The hydrodynamic sway forces act below the centre of gravity, so their roll
    /// moment is applied too: a turning ship heels outward.
    ///
    /// Orders come in through <see cref="Issue"/> (the range scene will route them from the combat system).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class ShipDrive : MonoBehaviour
    {
        const float KnToMs = 0.514444f;

        [SerializeField] EngineRoomProfileSO engineRoomProfile;
        [SerializeField] HelmProfileSO helmProfile;
        [SerializeField] FidelityLevel engineRoomLevel = FidelityLevel.L0;
        [SerializeField] FidelityLevel helmLevel = FidelityLevel.L0;
        [Tooltip("Start under way at this speed (knots) on the current heading, shafts already turning.")]
        [SerializeField] float startSpeedKn;

        Rigidbody _body;
        EngineRoomProfileData _room;
        float _hullSwayDepthM, _rollLiftDamping, _rollDampingReferenceMs, _metacentricHeightM;

        public IEngineRoom EngineRoom { get; private set; }
        public IHelm Helm { get; private set; }
        public HelmCommand Command { get; private set; }
        public HelmOutput LastHelm { get; private set; }
        public MmgResult LastForces { get; private set; }
        public ShipMotion Motion { get; private set; }

        /// <summary>Apply a helm, bell, rudder or route order now.</summary>
        public void Issue(in Order order) => Command = HelmCommand.Apply(Command, order);

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            if (engineRoomProfile == null || helmProfile == null)
            {
                Debug.LogError($"[ShipDrive] '{name}' needs an Engine Room profile and a Helm profile; the ship will not move.", this);
                enabled = false;
                return;
            }
            _room = engineRoomProfile.Bake();
            _hullSwayDepthM = engineRoomProfile.hullSwayDepthM;
            _rollLiftDamping = engineRoomProfile.rollLiftDampingRatio;
            _rollDampingReferenceMs = Mathf.Max(engineRoomProfile.rollDampingReferenceSpeedMs, 0.1f);
            var hull = GetComponent<ShipHull>();
            if (hull == null || hull.Profile == null)
            {
                Debug.LogError($"[ShipDrive] '{name}' needs a ShipHull with a profile (for GM); the ship will not move.", this);
                enabled = false;
                return;
            }
            _metacentricHeightM = hull.Profile.metacentricHeightM;
            EngineRoom = EngineRoomFactory.Create(engineRoomLevel, _room);
            Helm = HelmFactory.Create(helmLevel, helmProfile.Bake(), EngineRoom);
            if (EngineRoom.Fidelity.FellBack) Debug.Log($"[ShipDrive] {EngineRoom.Fidelity}", this);
            if (Helm.Fidelity.FellBack) Debug.Log($"[ShipDrive] {Helm.Fidelity}", this);
        }

        void Start()
        {
            ShipMotion m = ReadMotion();
            var command = HelmCommand.Stopped(m.HeadingDeg);
            if (startSpeedKn != 0f)
            {
                command.Speed = SpeedMode.Hold;
                command.SpeedKn = startSpeedKn;
                if (EngineRoom is EngineRoomL0 l0) l0.SetShafts(EngineRoom.SteadyShaftRps(startSpeedKn * KnToMs));
                Vector3 forward = Flat(transform.forward);
                _body.linearVelocity = forward * (startSpeedKn * KnToMs);
            }
            Command = command;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            ShipMotion motion = ReadMotion();
            Motion = motion;
            MmgHullData h = _room.Hull;

            // MMG works at midship: sway there is the centre of gravity's sway less x_G·r.
            var state = new MmgState { U = motion.SurgeMs, Vm = motion.SwayMs - h.XgM * motion.YawRateRadS, R = motion.YawRateRadS };
            LastHelm = Helm.Step(Command, motion, dt);
            MmgResult f = EngineRoom.Step(LastHelm, state, dt);
            LastForces = f;

            // Acceleration of the centre of gravity, heading frame: a_x = u̇ − v_m·r − x_G·r², a_y = v̇_m + x_G·ṙ + u·r.
            float r = state.R;
            float ax = f.Udot - state.Vm * r - h.XgM * r * r;
            float ay = f.Vdot + h.XgM * f.Rdot + state.U * r;
            Vector3 forward = Flat(transform.forward), starboard = new Vector3(forward.z, 0f, -forward.x);
            _body.AddForce((forward * ax + starboard * ay) * _body.mass, ForceMode.Force);

            // Yaw from MMG. Roll from where the sway forces act: every lateral force on the ship is hydrodynamic, so
            // together they are m·a_y. The rudders' share acts at their depth; the rest (hull lift and the sway
            // added-mass reaction) at the hull's centre of pressure. Leaving the added-mass reaction out made the
            // first inward lurch 8° and the outward swing 18° at flank and full rudder; with it, about 3° and 15°.
            float yaw = _body.inertiaTensor.y * f.Rdot;
            float g = _body.centerOfMass.y;
            float rudders = f.YRudder0 + f.YRudder1;
            float roll = (_body.mass * ay - rudders) * (g + _hullSwayDepthM)
                         + f.YRudder0 * (g + _room.PortRudder.DepthM) + f.YRudder1 * (g + _room.StarboardRudder.DepthM);
            // Lift damping of roll, growing with speed (AUTHORED at L0; Ikeda's components and roll added inertia are L1).
            float p = Vector3.Dot(_body.angularVelocity, transform.forward);
            float zeta = _rollLiftDamping * Mathf.Clamp01(Mathf.Abs(motion.SurgeMs) / _rollDampingReferenceMs);
            roll -= zeta * 2f * Mathf.Sqrt(_body.inertiaTensor.z * _body.mass * 9.81f * _metacentricHeightM) * p;
            _body.AddRelativeTorque(new Vector3(0f, yaw, roll), ForceMode.Force);
        }

        ShipMotion ReadMotion()
        {
            Vector3 forward = Flat(transform.forward), starboard = new Vector3(forward.z, 0f, -forward.x);
            Vector3 v = _body.linearVelocity;
            return new ShipMotion
            {
                Position = transform.position,
                HeadingDeg = CompassMath.Normalize(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg),
                SurgeMs = Vector3.Dot(v, forward),
                SwayMs = Vector3.Dot(v, starboard),
                YawRateRadS = _body.angularVelocity.y,
            };
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward;
        }
    }
}
