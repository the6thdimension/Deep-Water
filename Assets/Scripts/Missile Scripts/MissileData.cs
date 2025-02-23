using UnityEngine;

namespace DeepWater.Missiles
{
    /// <summary>
    /// Enumeration of different missile guidance types.
    /// Expand as needed for your specific project.
    /// </summary>
    public enum GuidanceType
    {
        IR,        // Infrared
        Radar,     // Radar-homing
        Laser,     // Laser-guided
        GPS_INS,   // GPS or INS-based
        TV,        // Television-guided
        Command,   // Command-guided (wire/radio)
    }

    /// <summary>
    /// ScriptableObject storing basic missile parameters for easy, data-driven setup.
    /// Create assets via: Assets → Create → Missiles → Missile Data
    /// </summary>
    [CreateAssetMenu(fileName = "MissileType", menuName = "Missiles/Missile Data", order = 1)]
    public class MissileData : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Short code or identifier (e.g., 'AGM-114')")]
        public string missileName;

        [Tooltip("Friendly display name (e.g., 'Hellfire')")]
        public string displayName;

        [Header("Flight Characteristics")]
        [Tooltip("Maximum speed in meters per second (m/s).")]
        public float maxSpeed = 1000f;

        [Tooltip("Time in seconds that the rocket motor is providing thrust.")]
        public float burnTime = 5f;

        [Tooltip("Thrust in Newtons (N).")]
        public float thrust = 10000f;

        [Tooltip("Maximum turn rate in degrees per second (or G-limit).")]
        public float maxTurnRate = 30f;

        [Header("Guidance")]
        [Tooltip("Type of guidance this missile uses.")]
        public GuidanceType guidanceType = GuidanceType.IR;

        [Tooltip("The maximum distance at which the missile can lock onto the target (meters).")]
        public float lockOnRange = 5000f;

        [Tooltip("Field of view (in degrees) for the seeker head.")]
        public float seekerFOV = 30f;

        [Header("Damage Parameters")]
        [Tooltip("Blast radius in meters.")]
        public float blastRadius = 10f;

        [Tooltip("Damage dealt at the center of the explosion.")]
        public float damage = 500f;

        [Header("Visual/Prefab References")]
        [Tooltip("Prefab representing the physical missile model in the scene.")]
        public GameObject missilePrefab;

        [Tooltip("Optional particle effect (e.g., smoke trail) to attach.")]
        public GameObject trailEffect;
    }
}
