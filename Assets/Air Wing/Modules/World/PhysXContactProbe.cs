using UnityEngine;
using DeepWater.AirWing.Core.Simulation;

namespace DeepWater.AirWing.Modules.World
{
    /// <summary>
    /// The scenes' contact probe (W4): a PhysX raycast against the colliders of the runway and the land, never the sea
    /// (meeting the sea is the truth feed's business: a crash). Static colliders only, so it answers the same way every
    /// run on one machine (W7).
    /// </summary>
    public sealed class PhysXContactProbe : IContactProbe
    {
        readonly int _layers;

        public PhysXContactProbe(int layerMask = Physics.DefaultRaycastLayers) => _layers = layerMask;

        public bool Cast(Vector3 origin, Vector3 direction, float maxDistanceM, out float distanceM, out Vector3 normal)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistanceM, _layers, QueryTriggerInteraction.Ignore))
            {
                distanceM = hit.distance;
                normal = hit.normal;
                return true;
            }
            distanceM = float.PositiveInfinity;
            normal = Vector3.up;
            return false;
        }
    }
}
