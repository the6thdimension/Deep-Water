using UnityEngine;
using DeepWater.GeneralQuarters.Core.DamageControl;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Modules.Hull
{
    /// <summary>
    /// Gives the ship's Rigidbody the hull's real mass properties. Sea State's <c>BuoyantBody</c> sets mass and
    /// centre of mass from the buoyancy asset; this sets the inertia tensor from the hull profile's radii of
    /// gyration, replacing Unity's automatic tensor, which assumes the colliders are solid and uniform (a
    /// ship is neither). Runs in Start, after every OnEnable has applied mass and centre of mass.
    /// From Damage Control L1 it also carries the flood water (G55): <see cref="ApplyFloodWater"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class ShipHull : MonoBehaviour
    {
        [SerializeField] ShipHullProfileSO profile;

        public ShipHullProfileSO Profile => profile;
        public Rigidbody Body { get; private set; }
        /// <summary>The flood water the Rigidbody carries now, kg, and its centroid in the ship frame.</summary>
        public float FloodWaterKg { get; private set; }
        public Vector3 FloodWaterCentroid { get; private set; }

        void Start() => Apply();

        /// <summary>Apply mass, centre of mass and inertia, dry. Safe to call again after a profile change.</summary>
        public void Apply()
        {
            Body = GetComponent<Rigidbody>();
            if (profile == null)
            {
                Debug.LogError($"[ShipHull] '{name}' has no hull profile; mass properties left as Unity's defaults.", this);
                enabled = false;
                return;
            }
            FloodWaterKg = -1f;   // force the write
            ApplyFloodWater(0f, Vector3.zero);
        }

        /// <summary>
        /// The ship plus <paramref name="waterKg"/> of flood water at <paramref name="centroidShip"/>, as one body: mass,
        /// centre of mass, and the inertia tensor carried to the new centre (<see cref="FloodMass"/>). The tensor is set
        /// last, so Unity never recomputes it from the colliders about the moved centre.
        /// </summary>
        public void ApplyFloodWater(float waterKg, Vector3 centroidShip)
        {
            if (profile == null || Body == null) return;
            if (waterKg == FloodWaterKg && centroidShip == FloodWaterCentroid) return;
            FloodWaterKg = waterKg;
            FloodWaterCentroid = centroidShip;
            FloodMass.Combine(profile.massKg, profile.centerOfMassM, profile.InertiaTensor, waterKg, centroidShip,
                out float mass, out Vector3 centre, out Vector3 inertia);
            Body.mass = mass;
            Body.centerOfMass = centre;
            Body.inertiaTensor = inertia;
            Body.inertiaTensorRotation = Quaternion.identity;
        }
    }
}
