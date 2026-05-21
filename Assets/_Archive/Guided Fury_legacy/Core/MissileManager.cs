using System.Collections.Generic;
using UnityEngine;

namespace GuidedFury.Core
{
    /// <summary>
    /// Global manager for missile spawning, tracking, and pooling.
    /// </summary>
    public class MissileManager : MonoBehaviour
    {
        #region Singleton
        private static MissileManager _instance;
        
        /// <summary>
        /// Singleton instance of the MissileManager
        /// </summary>
        public static MissileManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MissileManager>();
                    
                    if (_instance == null)
                    {
                        GameObject managerObject = new GameObject("MissileManager");
                        _instance = managerObject.AddComponent<MissileManager>();
                    }
                }
                
                return _instance;
            }
        }
        #endregion

        #region Inspector Properties
        [Header("Missile Configuration")]
        [SerializeField] private List<MissileConfig> missileConfigs = new List<MissileConfig>();
        [SerializeField] private MissileBase defaultMissilePrefab;
        
        [Header("Pooling Settings")]
        [SerializeField] private bool useObjectPooling = true;
        [SerializeField] private int initialPoolSize = 10;
        [SerializeField] private int maxPoolSize = 30;
        [SerializeField] private Transform poolContainer;
        
        [Header("Simulation Settings")]
        [SerializeField] private bool limitSimultaneousMissiles = false;
        [SerializeField] private int maxSimultaneousMissiles = 20;
        [SerializeField] private float cullingDistance = 10000f;
        [SerializeField] private bool enableDebugLogging = false;
        #endregion

        #region Runtime Properties
        private Dictionary<string, Queue<MissileBase>> missilePools = new Dictionary<string, Queue<MissileBase>>();
        private List<MissileBase> activeMissiles = new List<MissileBase>();
        private Dictionary<string, MissileBase> missileRegistry = new Dictionary<string, MissileBase>();
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Ensure singleton behavior
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Create pool container if needed
            if (poolContainer == null)
            {
                GameObject container = new GameObject("MissilePool");
                container.transform.SetParent(transform);
                poolContainer = container.transform;
            }
            
            // Initialize pools
            if (useObjectPooling)
            {
                InitializePools();
            }
        }

        private void Update()
        {
            // Update active missiles tracking
            for (int i = activeMissiles.Count - 1; i >= 0; i--)
            {
                MissileBase missile = activeMissiles[i];
                
                // Remove null references
                if (missile == null)
                {
                    activeMissiles.RemoveAt(i);
                    continue;
                }
                
                // Cull missiles that are too far away
                if (cullingDistance > 0)
                {
                    float distance = Vector3.Distance(Camera.main.transform.position, missile.transform.position);
                    if (distance > cullingDistance)
                    {
                        ReturnToPool(missile);
                        activeMissiles.RemoveAt(i);
                        continue;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Launch a missile from a specific position and rotation
        /// </summary>
        /// <param name="missileType">The type of missile to launch (optional)</param>
        /// <param name="position">The launch position</param>
        /// <param name="rotation">The launch rotation</param>
        /// <param name="target">The initial target (optional)</param>
        /// <returns>The launched missile instance</returns>
        public MissileBase LaunchMissile(string missileType, Vector3 position, Quaternion rotation, Transform target = null)
        {
            // Check if we've reached the maximum number of missiles
            if (limitSimultaneousMissiles && activeMissiles.Count >= maxSimultaneousMissiles)
            {
                if (enableDebugLogging)
                {
                    Debug.LogWarning($"[MissileManager] Maximum number of missiles reached ({maxSimultaneousMissiles})");
                }
                
                return null;
            }
            
            // Get missile instance
            MissileBase missile = GetMissileInstance(missileType);
            
            if (missile == null)
            {
                if (enableDebugLogging)
                {
                    Debug.LogError($"[MissileManager] Failed to get missile instance of type '{missileType}'");
                }
                
                return null;
            }
            
            // Position and activate the missile
            missile.transform.position = position;
            missile.transform.rotation = rotation;
            missile.gameObject.SetActive(true);
            
            // Launch the missile
            missile.Launch(target);
            
            // Add to active missiles
            activeMissiles.Add(missile);
            
            return missile;
        }

        /// <summary>
        /// Return a missile to the pool
        /// </summary>
        /// <param name="missile">The missile to return</param>
        public void ReturnToPool(MissileBase missile)
        {
            if (missile == null) return;
            
            // Remove from active missiles
            activeMissiles.Remove(missile);
            
            if (useObjectPooling)
            {
                // Get the missile type
                string missileType = GetMissileType(missile);
                
                // Check if we have a pool for this type
                if (!missilePools.ContainsKey(missileType))
                {
                    missilePools[missileType] = new Queue<MissileBase>();
                }
                
                // Check if we've reached the maximum pool size
                if (missilePools[missileType].Count >= maxPoolSize)
                {
                    Destroy(missile.gameObject);
                    return;
                }
                
                // Return to pool
                missile.gameObject.SetActive(false);
                missile.transform.SetParent(poolContainer);
                missilePools[missileType].Enqueue(missile);
            }
            else
            {
                // Just destroy the missile
                Destroy(missile.gameObject);
            }
        }

        /// <summary>
        /// Register a missile prefab with the manager
        /// </summary>
        /// <param name="missileType">The missile type identifier</param>
        /// <param name="missilePrefab">The missile prefab</param>
        public void RegisterMissilePrefab(string missileType, MissileBase missilePrefab)
        {
            if (string.IsNullOrEmpty(missileType) || missilePrefab == null)
            {
                if (enableDebugLogging)
                {
                    Debug.LogError("[MissileManager] Invalid missile registration parameters");
                }
                
                return;
            }
            
            // Add to registry
            missileRegistry[missileType] = missilePrefab;
            
            // Initialize pool if using object pooling
            if (useObjectPooling && !missilePools.ContainsKey(missileType))
            {
                missilePools[missileType] = new Queue<MissileBase>();
                
                // Pre-populate pool
                for (int i = 0; i < initialPoolSize; i++)
                {
                    MissileBase missile = Instantiate(missilePrefab, poolContainer);
                    missile.gameObject.SetActive(false);
                    missilePools[missileType].Enqueue(missile);
                }
            }
        }

        /// <summary>
        /// Get all active missiles
        /// </summary>
        /// <returns>A list of active missiles</returns>
        public List<MissileBase> GetActiveMissiles()
        {
            return new List<MissileBase>(activeMissiles);
        }

        /// <summary>
        /// Get all active missiles of a specific type
        /// </summary>
        /// <param name="missileType">The missile type</param>
        /// <returns>A list of active missiles of the specified type</returns>
        public List<MissileBase> GetActiveMissiles(string missileType)
        {
            List<MissileBase> missiles = new List<MissileBase>();
            
            foreach (var missile in activeMissiles)
            {
                if (GetMissileType(missile) == missileType)
                {
                    missiles.Add(missile);
                }
            }
            
            return missiles;
        }

        /// <summary>
        /// Get the number of active missiles
        /// </summary>
        /// <returns>The number of active missiles</returns>
        public int GetActiveMissileCount()
        {
            return activeMissiles.Count;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Initialize missile pools
        /// </summary>
        private void InitializePools()
        {
            // Initialize pools for configured missiles
            foreach (var config in missileConfigs)
            {
                if (config.missilePrefab != null)
                {
                    RegisterMissilePrefab(config.missileType, config.missilePrefab);
                }
            }
            
            // Initialize default pool if available
            if (defaultMissilePrefab != null)
            {
                RegisterMissilePrefab("default", defaultMissilePrefab);
            }
        }

        /// <summary>
        /// Get a missile instance from the pool or create a new one
        /// </summary>
        /// <param name="missileType">The missile type</param>
        /// <returns>A missile instance</returns>
        private MissileBase GetMissileInstance(string missileType)
        {
            // If no type specified, use default
            if (string.IsNullOrEmpty(missileType))
            {
                missileType = "default";
            }
            
            MissileBase missile = null;
            
            if (useObjectPooling)
            {
                // Try to get from pool
                if (missilePools.ContainsKey(missileType) && missilePools[missileType].Count > 0)
                {
                    missile = missilePools[missileType].Dequeue();
                }
            }
            
            // If no missile from pool, create a new one
            if (missile == null)
            {
                MissileBase prefab = null;
                
                // Try to get prefab from registry
                if (missileRegistry.ContainsKey(missileType))
                {
                    prefab = missileRegistry[missileType];
                }
                // Fall back to default prefab
                else if (defaultMissilePrefab != null)
                {
                    prefab = defaultMissilePrefab;
                }
                
                if (prefab != null)
                {
                    missile = Instantiate(prefab);
                }
            }
            
            return missile;
        }

        /// <summary>
        /// Get the missile type from a missile instance
        /// </summary>
        /// <param name="missile">The missile instance</param>
        /// <returns>The missile type</returns>
        private string GetMissileType(MissileBase missile)
        {
            // Try to find the missile in the registry
            foreach (var entry in missileRegistry)
            {
                if (missile.GetType() == entry.Value.GetType())
                {
                    return entry.Key;
                }
            }
            
            // Default type
            return "default";
        }
        #endregion

        /// <summary>
        /// Configuration for a missile type
        /// </summary>
        [System.Serializable]
        public class MissileConfig
        {
            public string missileType;
            public MissileBase missilePrefab;
        }
    }
}
