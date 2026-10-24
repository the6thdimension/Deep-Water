using UnityEngine;

namespace DeepWater.GeneralQuarters.Core.World
{
    /// <summary>
    /// One object in the true world, as the truth feed reports it each tick. The combat core never looks at
    /// the scene; whatever hosts it (a test, the range scene) builds these. Positions are world metres,
    /// y up, sea level at y = 0.
    /// </summary>
    public struct TruthTarget
    {
        public int Id;
        public TargetKind Kind;
        public Identity Identity;
        public Vector3 Position;
        public Vector3 Velocity;
        public bool Alive;
        /// <summary>Radar cross-section, dBsm. Unused by radar L0; the radar equation (L1) reads it.</summary>
        public float RcsDbsm;
    }

    /// <summary>
    /// The own ship's true state. Heading is a compass bearing in degrees (0 = +Z, clockwise seen from above,
    /// so 90 = +X). Ship frame: +Z forward, +X starboard, +Y up, origin at the waterline amidships.
    /// </summary>
    public struct OwnShipState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float HeadingDeg;

        public Quaternion HeadingRotation => Quaternion.Euler(0f, HeadingDeg, 0f);

        public Vector3 ToShipFrame(Vector3 world) => Quaternion.Inverse(HeadingRotation) * (world - Position);

        public Vector3 ToWorld(Vector3 shipFrame) => Position + HeadingRotation * shipFrame;

        /// <summary>Bearing of a world point relative to the bow, degrees in (-180, 180]; positive is starboard.</summary>
        public float RelativeBearingDeg(Vector3 world)
        {
            Vector3 local = ToShipFrame(world);
            return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        }
    }

    /// <summary>A threat that reached the ship: what the damage model applies (BLUEPRINT §4, Damage Control).</summary>
    public struct ShipHit
    {
        public int ThreatId;
        public TargetKind Kind;
        /// <summary>World point of impact.</summary>
        public Vector3 Point;
        /// <summary>Warhead mass, kg (the damage model scales by it).</summary>
        public float WarheadKg;
    }
}
