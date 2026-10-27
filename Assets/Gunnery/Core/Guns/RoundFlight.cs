using DeepWater.Gunnery.Ballistics;
using UnityEngine;

namespace DeepWater.Gunnery.Guns
{
    /// <summary>
    /// A round's flight in three dimensions, with the same physics as the firing table (gravity, ISA density, G7 drag)
    /// so a ship's shell and a jet's round fly alike (K1). Midpoint (RK2) steps; pure.
    /// </summary>
    public static class RoundFlight
    {
        /// <summary>The drag constant k = form factor × area / (2 m): |a_drag| = k ρ Cd(M) v².</summary>
        public static float DragConstant(in ProjectileData p) => p.FormFactor * p.AreaM2 / (2f * Mathf.Max(1e-6f, p.MassKg));

        public static Vector3 Accel(Vector3 position, Vector3 velocity, float k)
        {
            float speed = velocity.magnitude;
            Atmosphere.At(position.y, out float rho, out float sound);
            float drag = k * rho * DragCurve.G7(speed / sound) * speed;   // |a_drag| / |v|
            return new Vector3(-drag * velocity.x, -FiringTable.Gravity - drag * velocity.y, -drag * velocity.z);
        }

        /// <summary>One midpoint step of <paramref name="dt"/> seconds.</summary>
        public static void Step(ref Vector3 position, ref Vector3 velocity, float k, float dt)
        {
            Vector3 a1 = Accel(position, velocity, k);
            Vector3 vMid = velocity + a1 * (dt * 0.5f);
            Vector3 a2 = Accel(position + velocity * (dt * 0.5f), vMid, k);
            position += vMid * dt;
            velocity += a2 * dt;
        }
    }
}
