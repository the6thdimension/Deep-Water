using UnityEngine;
using DeepWater.Sea.ScriptableObjects;

namespace DeepWater.Sea.Modules.Buoyancy
{
    /// <summary>
    /// Marks a Rigidbody as floating with a given hull. The work happens in the
    /// <see cref="BuoyancySystem"/>, which batches every body's probes into one job per fixed
    /// step; this component only registers itself and receives the result.
    ///
    /// The system reference is explicit (no scene search): wire it in the Inspector or via
    /// <see cref="Configure"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BuoyantBody : MonoBehaviour
    {
        [SerializeField] BuoyancySystem system;
        [SerializeField] BuoyancyHullSO hull;
        [Tooltip("Set the Rigidbody's mass (and centre of mass, if the hull asset overrides it) from the hull asset on enable.")]
        [SerializeField] bool applyHullMass = true;

        /// <summary>Fraction of the hull's probe volume under water after the last step, 0–1.</summary>
        public float SubmergedFraction { get; internal set; }

        public Rigidbody Body { get; private set; }
        public BuoyancyHullSO Hull => hull;
        /// <summary>The system that floats this body (and, through it, the sea it floats on).</summary>
        public BuoyancySystem System => system;

        /// <summary>Runtime wiring (e.g. for spawned hulls).</summary>
        public void Configure(BuoyancySystem buoyancySystem, BuoyancyHullSO hullAsset)
        {
            bool wasActive = Application.isPlaying && isActiveAndEnabled;
            if (wasActive) OnDisable();
            system = buoyancySystem;
            hull = hullAsset;
            if (wasActive) OnEnable();
        }

        void OnEnable()
        {
            Body = GetComponent<Rigidbody>();
            if (system == null || hull == null || hull.probes == null || hull.probes.Length == 0)
            {
                Debug.LogError($"[BuoyantBody] '{name}' needs a BuoyancySystem and a BuoyancyHull with probes; buoyancy disabled.", this);
                enabled = false;
                return;
            }
            if (applyHullMass) hull.ApplyMassProperties(Body);
            system.Register(this);
        }

        void OnDisable()
        {
            if (system != null) system.Unregister(this);
        }
    }
}
