using UnityEngine;
using CarrierOps.Core.Carrier;
using CarrierOps.Core.Recovery;

namespace CarrierOps.Examples
{
    /// <summary>
    /// Companion `MonoBehaviour` that gives any aircraft `IRecoveringAircraft` behavior
    /// without modifying the underlying flight model (AerialArcade F-18, MouseFlight plane,
    /// custom controller — they all stay vendor-clean).
    ///
    /// **What it does:**
    /// - Implements <see cref="IRecoveringAircraft"/> against the aircraft's Rigidbody + hook tip transform.
    /// - Finds the nearest <see cref="CarrierBehaviour"/> in the scene and registers itself.
    /// - Re-registers when the nearest carrier changes.
    /// - Exposes a Hook Down toggle (Inspector + `H` key for manual flight).
    /// - Translates wire-applied deceleration into a velocity reduction on the Rigidbody.
    ///
    /// **What it does NOT do:**
    /// - Modify the parent aircraft's controller, audio, animator, or flight model in any way.
    /// - Spawn or destroy anything. Just observes and reports.
    /// - Touch the rotation — wire engagement only decelerates linearly; the aircraft's
    ///   own flight model handles attitude.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TailhookHook : MonoBehaviour, IRecoveringAircraft
    {
        [Header("References")]
        [Tooltip("Rigidbody of the aircraft (or any child of the aircraft hierarchy).")]
        [SerializeField] private Rigidbody aircraftBody;

        [Tooltip("Transform marking the hook tip — used for wire engagement geometry. If null, falls back to this GameObject's transform.")]
        [SerializeField] private Transform hookTipTransform;

        [Header("State")]
        [Tooltip("Hook is down — wire engagement is possible. Toggle with H during play.")]
        [SerializeField] private bool hookDown = false;

        [Tooltip("Keyboard key that toggles the hook (Phase 2 manual testing).")]
        [SerializeField] private KeyCode toggleKey = KeyCode.H;

        [Header("Carrier Binding")]
        [Tooltip("If true, automatically registers with the nearest CarrierBehaviour found in the scene.")]
        [SerializeField] private bool autoRegister = true;

        [Tooltip("How often (seconds) to re-scan for the nearest carrier. Cheap operation; 1 Hz is fine.")]
        [SerializeField] private float carrierScanIntervalS = 1f;

        // -- Runtime ---------------------------------------------------------
        private CarrierBehaviour boundCarrier;
        private float nextScan;
        private int registrationId;

        // -- IRecoveringAircraft -------------------------------------------
        public int RegistrationId
        {
            get => registrationId;
            set => registrationId = value;
        }

        public Vector3 Position =>
            aircraftBody != null ? aircraftBody.position : transform.position;

        public Vector3 HookTipPosition =>
            hookTipTransform != null ? hookTipTransform.position : transform.position;

        public bool HookDown => hookDown;

        public Vector3 Velocity =>
            aircraftBody != null ? aircraftBody.linearVelocity : Vector3.zero;

        public void ApplyDeceleration(float decelMagnitudeMps2, float dt)
        {
            if (aircraftBody == null) return;

            Vector3 v = aircraftBody.linearVelocity;
            float speed = v.magnitude;
            if (speed < 1e-3f) return;

            // Subtract decelMagnitude × dt from the speed, clamped to zero.
            float newSpeed = Mathf.Max(0f, speed - decelMagnitudeMps2 * dt);
            aircraftBody.linearVelocity = v * (newSpeed / speed);
        }

        // -- Unity lifecycle -----------------------------------------------
        private void Awake()
        {
            if (aircraftBody == null)
                aircraftBody = GetComponentInParent<Rigidbody>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                hookDown = !hookDown;

            if (autoRegister && Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + carrierScanIntervalS;
                BindNearestCarrier();
            }
        }

        private void OnDisable()
        {
            // Drop the registration so the carrier doesn't hold a dead reference.
            if (boundCarrier != null && boundCarrier.Entity != null && registrationId != 0)
                boundCarrier.Entity.UnregisterRecoveringAircraft(this);
        }

        // -- Carrier binding -----------------------------------------------
        private void BindNearestCarrier()
        {
            CarrierBehaviour nearest = FindNearestCarrier();
            if (nearest == boundCarrier) return; // unchanged

            // Unregister from old.
            if (boundCarrier != null && boundCarrier.Entity != null && registrationId != 0)
                boundCarrier.Entity.UnregisterRecoveringAircraft(this);

            // Register with new.
            boundCarrier = nearest;
            if (boundCarrier != null && boundCarrier.Entity != null)
                boundCarrier.Entity.RegisterRecoveringAircraft(this);
        }

        private CarrierBehaviour FindNearestCarrier()
        {
            // O(N) scan. N is small (usually 1 carrier per scene); the 1 Hz scan cadence
            // makes this trivial.
            var all = FindObjectsByType<CarrierBehaviour>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0) return null;

            CarrierBehaviour best = null;
            float bestDistSq = float.MaxValue;
            Vector3 me = Position;
            foreach (var c in all)
            {
                if (c == null) continue;
                float d = (c.transform.position - me).sqrMagnitude;
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    best = c;
                }
            }
            return best;
        }
    }
}
