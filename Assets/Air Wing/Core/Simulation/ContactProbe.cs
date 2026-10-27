using UnityEngine;

namespace DeepWater.AirWing.Core.Simulation
{
    /// <summary>
    /// Where a wheel meets the ground (W4): a ray from the top of a strut down its axis. The scenes answer it with
    /// PhysX raycasts against the runway's and the land's colliders (<c>PhysXContactProbe</c>); the core only sees this
    /// interface, so it stays free of the physics engine (P6) and tests can stand in a flat ground. Ground contact is
    /// same-machine deterministic only (W7), and the Fix flags every tick in contact.
    /// </summary>
    public interface IContactProbe
    {
        /// <summary>
        /// The first surface along the ray within <paramref name="maxDistanceM"/>, if any, and its unit normal there.
        /// A negative distance means the origin is already below the surface (a strut driven through it).
        /// </summary>
        bool Cast(Vector3 originWorld, Vector3 directionWorld, float maxDistanceM, out float distanceM, out Vector3 normalWorld);
    }

    /// <summary>The truth feed's ground as a contact probe: flat under each ray's start (tests, scenes without colliders).</summary>
    public sealed class SurfaceProbe : IContactProbe
    {
        readonly ISurfaceTruth _surface;

        public SurfaceProbe(ISurfaceTruth surface) => _surface = surface;

        public bool Cast(Vector3 origin, Vector3 direction, float maxDistanceM, out float distanceM, out Vector3 normal)
        {
            distanceM = float.PositiveInfinity;
            normal = Vector3.up;
            if (direction.y > -1e-4f) return false;   // only rays that point down can meet a level ground
            float h = _surface.HeightAt(origin.x, origin.z);
            distanceM = (h - origin.y) / direction.y;
            return distanceM <= maxDistanceM;
        }
    }
}
