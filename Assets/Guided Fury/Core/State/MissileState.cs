using UnityEngine;

namespace GuidedFury.Core.State
{
    /// <summary>
    /// Phase of the missile's life cycle. Explicit state machine instead of ad-hoc bool flags
    /// (anti-pattern observed in legacy MissileBase: isLaunched + isActive + currentLifetime
    /// flags that drift out of sync).
    /// </summary>
    public enum MissilePhase : byte
    {
        PreLaunch  = 0, // assembled, not yet released
        Boost      = 1, // motor burning, accelerating
        Cruise     = 2, // post-boost coast / sustained flight
        Terminal   = 3, // close to target, terminal guidance gain elevated
        Detonated  = 4, // fuze triggered or impact occurred
        Failed     = 5, // out of energy, lost track, or self-destructed
    }

    /// <summary>
    /// Universal missile state — the lingua franca for every integrator (L0..L5).
    ///
    /// **Design intent (candidate C4 in METHODOLOGY.md):** every integrator reads and mutates
    /// the same struct. A higher-LOD integrator can pick up a lower-LOD state mid-flight and
    /// continue without translation. Lower LODs ignore the fields they don't need (e.g. L0
    /// ignores AngularVelocity) but never break them.
    ///
    /// **Why a struct, not a class:** unmanaged, no GC pressure, Burst/Jobs-friendly when we
    /// later move L0/L1 to ECS hot paths (P4). Pass by `ref` to integrators to avoid copies.
    ///
    /// **Coordinate conventions:**
    /// - Position, Velocity: world-space, right-handed Unity (X right, Y up, Z forward), meters.
    /// - Orientation: world-space; the missile's forward axis is its local +Z (Unity default).
    /// - AngularVelocity: body-frame, rad/s, around (X pitch, Y yaw, Z roll).
    /// - Mass: kilograms, total instantaneous mass (airframe + remaining fuel).
    /// - Fuel: kilograms remaining; when zero, thrust must be zero regardless of throttle.
    /// - TimeOfFlight: seconds since Launch was called; reset at re-pool.
    ///
    /// **Note on precision:** Vector3 is float32. For Cesium-scale worlds (origin shifts,
    /// continental ranges) we may need double-precision position later. Deferred until a
    /// Cesium-based scene actually demands it; flagged for future work.
    /// </summary>
    public struct MissileState
    {
        public Vector3    Position;          // world meters
        public Vector3    Velocity;          // world m/s
        public Quaternion Orientation;       // world rotation; local +Z is missile forward
        public Vector3    AngularVelocity;   // body-frame rad/s (pitch, yaw, roll)
        public float      Mass;              // kg (airframe + fuel remaining)
        public float      Fuel;              // kg of propellant remaining
        public float      TimeOfFlight;      // seconds since Launch
        public MissilePhase Phase;

        /// <summary>
        /// Convenience: build an initial state at rest, ready to be initialized by the
        /// integrator's Initialize() call with profile data.
        /// </summary>
        public static MissileState AtRest(Vector3 position, Quaternion orientation)
        {
            return new MissileState
            {
                Position        = position,
                Velocity        = Vector3.zero,
                Orientation     = orientation,
                AngularVelocity = Vector3.zero,
                Mass            = 0f,
                Fuel            = 0f,
                TimeOfFlight    = 0f,
                Phase           = MissilePhase.PreLaunch,
            };
        }
    }
}
