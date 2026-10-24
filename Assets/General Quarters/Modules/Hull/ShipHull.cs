using UnityEngine;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Modules.Hull
{
    /// <summary>
    /// Gives the ship's Rigidbody the hull's real mass properties. Sea State's <c>BuoyantBody</c> sets mass and
    /// centre of mass from the buoyancy asset; this sets the inertia tensor from the hull profile's radii of
    /// gyration, replacing Unity's automatic tensor, which assumes the colliders are solid and uniform (a
    /// ship is neither). Runs in Start, after every OnEnable has applied mass and centre of mass.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class ShipHull : MonoBehaviour
    {
        [SerializeField] ShipHullProfileSO profile;

        public ShipHullProfileSO Profile => profile;
        public Rigidbody Body { get; private set; }

        void Start() => Apply();

        /// <summary>Apply mass, centre of mass and inertia. Safe to call again after a profile change.</summary>
        public void Apply()
        {
            Body = GetComponent<Rigidbody>();
            if (profile == null)
            {
                Debug.LogError($"[ShipHull] '{name}' has no hull profile; mass properties left as Unity's defaults.", this);
                enabled = false;
                return;
            }
            Body.mass = profile.massKg;
            Body.centerOfMass = profile.centerOfMassM;
            Body.inertiaTensor = profile.InertiaTensor;
            Body.inertiaTensorRotation = Quaternion.identity;
        }
    }
}
