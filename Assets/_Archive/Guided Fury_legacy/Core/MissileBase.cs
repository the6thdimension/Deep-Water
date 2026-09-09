using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuidedFury.Core
{
    /// <summary>
    /// Base class for all missiles in the Guided Fury system.
    /// Provides core LOD 3 functionality with hooks for advanced modules.
    /// </summary>
    public class MissileBase : MonoBehaviour
    {
        #region Events
        // Events for module integration
        public event Action OnMissileLaunched;
        public event Action OnMissileImpact;
        public event Action OnMissileDestroyed;
        public event Action<Transform> OnTargetAcquired;
        public event Action<Transform> OnTargetLost;
        public event Action<Vector3> OnGuidanceUpdate;
        #endregion

        #region Inspector Properties
        [Header("Missile Configuration")]
        [SerializeField] private string missileName = "Standard Missile";
        [SerializeField] private float maxSpeed = 300f;
        [SerializeField] private float maxAcceleration = 10f;
        [SerializeField] private float maxTurnRate = 45f;
        [SerializeField] private float lifetime = 30f;
        [SerializeField] private float fuzeRadius = 5f;
        [SerializeField] private float damageAmount = 100f;
        [SerializeField] private bool useProximityFuze = true;

        [Header("References")]
        [SerializeField] private Transform missileBody;
        [SerializeField] private ParticleSystem engineEffect;
        [SerializeField] private ParticleSystem explosionEffect;
        #endregion

        #region Runtime Properties
        // Core components
        private MissilePhysics missilePhysics;
        private MissileSensor missileSensor;
        private MissileGuidance missileGuidance;

        // State tracking
        private bool isLaunched = false;
        private bool isActive = false;
        private float currentLifetime = 0f;
        private Transform currentTarget;
        private Vector3 lastKnownTargetPosition;

        // Module management
        private List<IMissileModule> attachedModules = new List<IMissileModule>();
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Get or add required components
            missilePhysics = GetComponent<MissilePhysics>() ?? gameObject.AddComponent<MissilePhysics>();
            missileSensor = GetComponent<MissileSensor>() ?? gameObject.AddComponent<MissileSensor>();
            missileGuidance = GetComponent<MissileGuidance>() ?? gameObject.AddComponent<MissileGuidance>();

            // Initialize components
            missilePhysics.Initialize(this);
            missileSensor.Initialize(this);
            missileGuidance.Initialize(this);

            // Find and initialize all attached modules
            InitializeModules();
        }

        private void OnEnable()
        {
            Reset();
        }

        private void Update()
        {
            if (!isActive) return;

            // Update lifetime
            currentLifetime += Time.deltaTime;
            if (currentLifetime >= lifetime)
            {
                SelfDestruct();
                return;
            }

            // Update sensor
            missileSensor.UpdateSensor();

            // Update guidance
            missileGuidance.UpdateGuidance();

            // Update physics
            missilePhysics.UpdatePhysics();

            // Update modules
            UpdateModules();

            // Check for proximity detonation
            if (useProximityFuze && currentTarget != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
                if (distanceToTarget <= fuzeRadius)
                {
                    Detonate();
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isActive) return;
            
            // Handle impact
            Detonate();
            
            // Notify listeners
            OnMissileImpact?.Invoke();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Launch the missile
        /// </summary>
        /// <param name="initialTarget">Optional initial target</param>
        public void Launch(Transform initialTarget = null)
        {
            if (isLaunched) return;

            isLaunched = true;
            isActive = true;
            currentLifetime = 0f;

            // Set initial target if provided
            if (initialTarget != null)
            {
                SetTarget(initialTarget);
            }

            // Activate engine effects
            if (engineEffect != null)
            {
                engineEffect.Play();
            }

            // Notify listeners
            OnMissileLaunched?.Invoke();
        }

        /// <summary>
        /// Set the missile's target
        /// </summary>
        /// <param name="target">The target transform</param>
        public void SetTarget(Transform target)
        {
            if (target == currentTarget) return;

            Transform previousTarget = currentTarget;
            currentTarget = target;

            if (currentTarget != null)
            {
                lastKnownTargetPosition = currentTarget.position;
                OnTargetAcquired?.Invoke(currentTarget);
            }
            else if (previousTarget != null)
            {
                OnTargetLost?.Invoke(previousTarget);
            }
        }

        /// <summary>
        /// Register a module with the missile
        /// </summary>
        /// <param name="module">The module to register</param>
        public void RegisterModule(IMissileModule module)
        {
            if (!attachedModules.Contains(module))
            {
                attachedModules.Add(module);
                module.Initialize(this);
            }
        }

        /// <summary>
        /// Unregister a module from the missile
        /// </summary>
        /// <param name="module">The module to unregister</param>
        public void UnregisterModule(IMissileModule module)
        {
            if (attachedModules.Contains(module))
            {
                attachedModules.Remove(module);
            }
        }

        /// <summary>
        /// Get the current target
        /// </summary>
        /// <returns>The current target transform, or null if no target</returns>
        public Transform GetCurrentTarget()
        {
            return currentTarget;
        }

        /// <summary>
        /// Get the last known target position
        /// </summary>
        /// <returns>The last known target position</returns>
        public Vector3 GetLastKnownTargetPosition()
        {
            return lastKnownTargetPosition;
        }

        /// <summary>
        /// Get the missile physics component
        /// </summary>
        /// <returns>The missile physics component</returns>
        public MissilePhysics GetPhysics()
        {
            return missilePhysics;
        }

        /// <summary>
        /// Get the missile sensor component
        /// </summary>
        /// <returns>The missile sensor component</returns>
        public MissileSensor GetSensor()
        {
            return missileSensor;
        }

        /// <summary>
        /// Get the missile guidance component
        /// </summary>
        /// <returns>The missile guidance component</returns>
        public MissileGuidance GetGuidance()
        {
            return missileGuidance;
        }

        /// <summary>
        /// Get the missile's maximum speed
        /// </summary>
        /// <returns>The maximum speed in m/s</returns>
        public float GetMaxSpeed()
        {
            return maxSpeed;
        }

        /// <summary>
        /// Get the missile's maximum acceleration
        /// </summary>
        /// <returns>The maximum acceleration in m/s²</returns>
        public float GetMaxAcceleration()
        {
            return maxAcceleration;
        }

        /// <summary>
        /// Get the missile's maximum turn rate
        /// </summary>
        /// <returns>The maximum turn rate in degrees/s</returns>
        public float GetMaxTurnRate()
        {
            return maxTurnRate;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Reset the missile state
        /// </summary>
        private void Reset()
        {
            isLaunched = false;
            isActive = false;
            currentLifetime = 0f;
            currentTarget = null;
            lastKnownTargetPosition = Vector3.zero;

            // Reset components
            if (missilePhysics != null) missilePhysics.Reset();
            if (missileSensor != null) missileSensor.Reset();
            if (missileGuidance != null) missileGuidance.Reset();

            // Reset modules
            foreach (var module in attachedModules)
            {
                module.SetEnabled(false);
            }

            // Stop effects
            if (engineEffect != null && engineEffect.isPlaying)
            {
                engineEffect.Stop();
            }
        }

        /// <summary>
        /// Initialize all attached modules
        /// </summary>
        private void InitializeModules()
        {
            // Find all components that implement IMissileModule
            var moduleComponents = GetComponents<MonoBehaviour>();
            foreach (var component in moduleComponents)
            {
                if (component is IMissileModule module && !attachedModules.Contains(module))
                {
                    attachedModules.Add(module);
                    module.Initialize(this);
                }
            }
        }

        /// <summary>
        /// Update all attached modules
        /// </summary>
        private void UpdateModules()
        {
            foreach (var module in attachedModules)
            {
                if (module.IsEnabled())
                {
                    module.UpdateModule();
                }
            }
        }

        /// <summary>
        /// Detonate the missile
        /// </summary>
        private void Detonate()
        {
            if (!isActive) return;

            isActive = false;

            // Play explosion effect
            if (explosionEffect != null)
            {
                explosionEffect.transform.parent = null;
                explosionEffect.Play();
                Destroy(explosionEffect.gameObject, explosionEffect.main.duration);
            }

            // Apply damage to target if available
            if (currentTarget != null)
            {
                var damageable = currentTarget.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damageAmount);
                }
            }

            // Notify listeners
            OnMissileDestroyed?.Invoke();

            // Return to pool or destroy
            MissileManager.Instance?.ReturnToPool(this);
        }

        /// <summary>
        /// Self-destruct the missile
        /// </summary>
        private void SelfDestruct()
        {
            Detonate();
        }
        #endregion
        /// <summary>
        /// Get a string representing the missile's current state
        /// </summary>
        public string GetMissileState()
        {
            if (!isLaunched && !isActive)
                return "Idle";
            if (isLaunched && isActive)
                return "Active";
            if (isLaunched && !isActive)
                return "Destroyed";
            return "Unknown";
        }
    } // Close MissileBase class

    /// <summary>
    /// Interface for objects that can take damage
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float amount);
    }
}
