using System;
using Unity.Mathematics;
using UnityEngine;
using DeepWater.Sea.Core;

namespace DeepWater.Sea.ScriptableObjects
{
    /// <summary>
    /// A hull as buoyancy sees it: a set of vertical water-column probes plus damping. Authored
    /// once per hull form; the same asset drives every instance of that hull.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBuoyancyHull", menuName = "Deep Water/Sea State/Buoyancy Hull", order = 10)]
    public class BuoyancyHullSO : ScriptableObject
    {
        [Serializable]
        public struct Probe
        {
            [Tooltip("Probe centre in the body's local frame, m.")]
            public Vector3 localPosition;
            [Tooltip("Horizontal cross-section this probe stands for, m².")]
            [Min(0f)] public float areaM2;
            [Tooltip("Vertical extent of the probe column, m.")]
            [Min(0.01f)] public float heightM;
        }

        [Header("Probes")]
        public Probe[] probes = Array.Empty<Probe>();

        [Header("Mass")]
        [Tooltip("Rigidbody mass, kg. For a box hull: seawater density × waterplane area × design draft.")]
        [Min(1f)] public float massKg = 1000f;
        [Tooltip("Use the centre of mass below instead of the one Unity derives from the colliders " +
                 "(uniform density). Turn on for ballasted or loaded hulls.")]
        public bool overrideCenterOfMass;
        [Tooltip("Centre of mass in the body's local frame, m. A box hull's centre is (0, 0, 0); " +
                 "negative y is lower (ballast). Roll stability: GM = KB + BM − KG.")]
        public Vector3 centerOfMassM;

        [Header("Damping")]
        [Tooltip("Heave damping as a fraction of critical damping (ζ). Typical ships 0.1–0.3.")]
        [Range(0f, 1f)] public float heaveDampingRatio = 0.2f;
        [Tooltip("Design draft, m — sets the heave natural frequency used to turn ζ into a damping speed.")]
        [Min(0.01f)] public float designDraftM = 1f;
        [Tooltip("Quadratic drag coefficient Cd on the relative water velocity.")]
        [Min(0f)] public float quadraticDragCd = 0.5f;
        [Tooltip("Probe drag per axis of the hull's frame: x sway, y heave, z surge (1 = full). A ship " +
                 "whose propulsion model supplies its own resistance and turning forces sets x and z to 0.")]
        public Vector3 dragAxisScale = Vector3.one;

        /// <summary>
        /// Linear damping speed c (m/s) giving damping ratio ζ for a wall-sided hull at the design
        /// draft: per unit waterplane, damping ρ c against critical 2 ρ √(g d) ⇒ c = 2 ζ √(g d).
        /// </summary>
        public BuoyancyDamping BakeDamping(float gravity) => new BuoyancyDamping
        {
            LinearMs = 2f * heaveDampingRatio * math.sqrt(gravity * designDraftM),
            QuadraticCd = quadraticDragCd,
            PerAxis = dragAxisScale != Vector3.one,
            AxisScale = dragAxisScale,
        };

        /// <summary>
        /// Mass, and the centre of mass when this hull overrides it, onto a Rigidbody. Without
        /// the override the Rigidbody keeps whatever centre of mass it has (Unity's automatic
        /// one, or one set by hand). The inertia tensor stays Unity's (from the colliders).
        /// </summary>
        public void ApplyMassProperties(Rigidbody body)
        {
            body.mass = massKg;
            if (overrideCenterOfMass) body.centerOfMass = centerOfMassM;
        }

        public BuoyancyProbe BakeProbe(int index) => new BuoyancyProbe
        {
            LocalPosition = probes[index].localPosition,
            AreaM2 = probes[index].areaM2,
            HeightM = probes[index].heightM,
        };

        /// <summary>
        /// Fill with an nx × nz grid of probes covering a box hull (centred on the body origin),
        /// each column spanning the full hull height.
        /// </summary>
        public void FillBoxGrid(float lengthM, float beamM, float heightM, int nx, int nz)
        {
            nx = Mathf.Max(1, nx);
            nz = Mathf.Max(1, nz);
            probes = new Probe[nx * nz];
            float cellArea = lengthM * beamM / (nx * nz);
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float x = ((ix + 0.5f) / nx - 0.5f) * beamM;
                float z = ((iz + 0.5f) / nz - 0.5f) * lengthM;
                probes[iz * nx + ix] = new Probe
                {
                    localPosition = new Vector3(x, 0f, z),
                    areaM2 = cellArea,
                    heightM = heightM,
                };
            }
        }
    }
}
