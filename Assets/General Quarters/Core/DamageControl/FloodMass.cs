using UnityEngine;

namespace DeepWater.GeneralQuarters.Core.DamageControl
{
    /// <summary>
    /// The ship and her flood water as one rigid body (G55): mass, centre of mass and principal moments of inertia
    /// about the combined centre, in the ship frame. The water is a point mass at its centroid: free water does not turn
    /// with the hull, so it adds no inertia of its own, only its parallel-axis share. Products of inertia (an off-centre
    /// water mass couples roll and yaw) are left out at L1.
    /// </summary>
    public static class FloodMass
    {
        public static void Combine(float shipKg, Vector3 shipCentre, Vector3 shipInertia, float waterKg, Vector3 waterCentre,
            out float massKg, out Vector3 centre, out Vector3 inertia)
        {
            massKg = shipKg + Mathf.Max(0f, waterKg);
            if (waterKg <= 0f)
            {
                centre = shipCentre;
                inertia = shipInertia;
                return;
            }
            centre = (shipKg * shipCentre + waterKg * waterCentre) / massKg;
            inertia = shipInertia + shipKg * ParallelAxis(shipCentre - centre) + waterKg * ParallelAxis(waterCentre - centre);
        }

        /// <summary>The parallel-axis terms of a unit mass at offset <paramref name="d"/>, about x, y and z.</summary>
        static Vector3 ParallelAxis(Vector3 d) => new Vector3(d.y * d.y + d.z * d.z, d.x * d.x + d.z * d.z, d.x * d.x + d.y * d.y);
    }
}
