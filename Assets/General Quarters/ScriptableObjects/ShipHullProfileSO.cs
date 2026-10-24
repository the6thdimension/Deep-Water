using System;
using UnityEngine;
using DeepWater.Sea.ScriptableObjects;

namespace DeepWater.GeneralQuarters.ScriptableObjects
{
    /// <summary>
    /// A ship's hull as General Quarters sees it (P1): mass properties, hydrostatics, compartments, and the
    /// Sea State buoyancy asset that floats it. Built from the Blender layout by the DDG-51 hull builder;
    /// <see cref="provenance"/> says which numbers were measured from the mesh and which are AUTHORED.
    /// </summary>
    [CreateAssetMenu(fileName = "NewShipHull", menuName = "Deep Water/General Quarters/Ship Hull Profile", order = 20)]
    public class ShipHullProfileSO : ScriptableObject
    {
        [Serializable]
        public struct Compartment
        {
            public string name;
            /// <summary>Box corners in the ship frame, m.</summary>
            public Vector3 min, max;
        }

        [Tooltip("Sea State buoyancy columns for this hull.")]
        public BuoyancyHullSO buoyancy;

        [Header("Mass properties")]
        [Min(1f)] public float massKg = 1000f;
        [Tooltip("Centre of mass in the ship frame, m (x starboard, y up, z forward; origin at the waterline amidships).")]
        public Vector3 centerOfMassM;
        [Tooltip("Radii of gyration about the centre of mass, m: x = pitch (about the beam axis), y = yaw, z = roll.")]
        public Vector3 gyradiusM = Vector3.one;

        [Header("Hydrostatics (from the layout)")]
        public float displacementT;
        public float designDraftM;
        public float kbM;
        public float bmM;
        public float metacentricHeightM;

        [Header("Compartments")]
        public Compartment[] compartments = Array.Empty<Compartment>();

        [TextArea(4, 14)] public string provenance;

        /// <summary>Principal moments of inertia, kg·m², about x (pitch), y (yaw), z (roll): m k².</summary>
        public Vector3 InertiaTensor => massKg * Vector3.Scale(gyradiusM, gyradiusM);

        /// <summary>Undamped natural roll period, s: 2π k_roll / √(g GM).</summary>
        public float NaturalRollPeriodS(float gravity = 9.81f) =>
            metacentricHeightM > 0f ? 2f * Mathf.PI * gyradiusM.z / Mathf.Sqrt(gravity * metacentricHeightM) : float.NaN;
    }
}
