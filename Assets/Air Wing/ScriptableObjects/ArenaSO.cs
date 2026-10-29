using UnityEngine;
using DeepWater.AirWing.Core;

namespace DeepWater.AirWing.ScriptableObjects
{
    /// <summary>
    /// An arena's rules (W59–W61): respawn delay and protection, the respawn jitter and its seed, the collision distance
    /// and the box. Baked into <see cref="ArenaSettings"/> when a run starts. Every number is AUTHORED; the provenance
    /// names its source. A zero turns its rule off.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArena", menuName = "Deep Water/Air Wing/Arena", order = 20)]
    public class ArenaSO : ScriptableObject
    {
        [Header("Respawn (W59)")]
        [Tooltip("Seconds after a jet leaves the fight (shot down, crashed, collided, out of the box) before it is back at its slot.")]
        [Min(0f)] public float respawnDelayS = 3f;
        [Tooltip("Seconds after a respawn in which the jet can't be hit and can't fire.")]
        [Min(0f)] public float protectionS = 5f;
        [Tooltip("A respawn's height is its slot's ± this, metres.")]
        [Min(0f)] public float jitterHeightM = 300f;
        [Tooltip("A respawn's heading is its slot's ± this, degrees.")]
        [Range(0f, 180f)] public float jitterHeadingDeg = 15f;
        [Tooltip("Seed of the respawn jitter: same seed, same respawns.")]
        public long seed = 2026;

        [Header("Collisions (W60)")]
        [Tooltip("Two jets whose paths pass within this, metres, both go down. 0: no collisions.")]
        [Min(0f)] public float collisionM = 15f;

        [Header("The box (W61)")]
        [Tooltip("The box's centre (only x and z count).")]
        public Vector3 centreM = new Vector3(0f, 0f, 12000f);
        [Tooltip("Half-widths east-west (x) and north-south (y), metres. 0: no box.")]
        public Vector2 halfWidthM = new Vector2(20000f, 20000f);

        [TextArea(3, 12)] public string provenance;

        public ArenaSettings Bake()
        {
            var s = new ArenaSettings
            {
                RespawnDelayS = respawnDelayS,
                ProtectionS = protectionS,
                JitterHeightM = jitterHeightM,
                JitterHeadingDeg = jitterHeadingDeg,
                CollisionM = collisionM,
                CentreM = centreM,
                HalfWidthXM = halfWidthM.x,
                HalfWidthZM = halfWidthM.y,
            };
            s.Validate();
            return s;
        }

        public ulong Seed => unchecked((ulong)seed);
    }
}
