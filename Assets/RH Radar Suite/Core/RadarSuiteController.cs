using System;
using System.Collections.Generic;
using UnityEngine;

namespace RHRadarSuite
{
    /// <summary>
    /// Master controller script for the RH Radar Suite.
    /// This is the main script that should be attached to any GameObject requiring radar functionality.
    /// It provides access to all radar LOD levels and manages the radar system.
    /// </summary>
    [AddComponentMenu("RH Radar Suite/Radar Suite Controller")]
    public class RadarSuiteController : MonoBehaviour
    {
        [Tooltip("Current Level of Detail for the radar system")]
        [SerializeField] private RadarLOD currentLOD = RadarLOD.LOD1_PassiveDetection;
        
        [Tooltip("Auto-initialize radar on start")]
        [SerializeField] private bool initializeOnStart = true;
        
        [Tooltip("Auto-activate radar on start")]
        [SerializeField] private bool activateOnStart = false;
        
        [Header("Base Radar Parameters")]
        [Tooltip("Maximum detection range in meters")]
        [SerializeField] private float maxDetectionRange = 5000f;
        
        [Tooltip("Base radar power (affects detection capability)")]
        [Range(0.1f, 10f)]
        [SerializeField] private float radarPower = 1f;
        
        [Tooltip("Layers that can be detected by this radar")]
        [SerializeField] private LayerMask targetLayers = ~0;
        
        [Header("Performance")]
        [Tooltip("Update interval in seconds")]
        [Range(0.01f, 1f)]
        [SerializeField] private float updateInterval = 0.1f;
        
        [Tooltip("Maximum number of targets that can be tracked simultaneously")]
        [Range(10, 1000)]
        [SerializeField] private int maxTargets = 100;

        // Internal references to LOD implementations
        private Dictionary<RadarLOD, IRadarLODModule> lodModules;
        private IRadarLODModule activeModule;
        
        // Radar state
        private bool isInitialized = false;
        private bool isActive = false;
        
        // Events
        public delegate void ContactEventHandler(RadarContact contact);
        public event ContactEventHandler OnContactDetected;
        public event ContactEventHandler OnContactLost;
        public event ContactEventHandler OnContactUpdated;
        
        public delegate void LODChangedEventHandler(RadarLOD newLOD);
        public event LODChangedEventHandler OnLODChanged;
        public event Action OnRadarActivated;
        public event Action OnRadarDeactivated;
        
        // Properties
        public RadarLOD CurrentLOD => currentLOD;
        public bool IsActive => isActive;
        public bool IsInitialized => isInitialized;
        public float MaxDetectionRange => maxDetectionRange;
        public float RadarPower => radarPower;
        public LayerMask TargetLayers => targetLayers;
        public float UpdateInterval => updateInterval;
        public int MaxTargets => maxTargets;
        
        // Contact management
        private List<RadarContact> activeContacts = new List<RadarContact>();
        public IReadOnlyList<RadarContact> ActiveContacts => activeContacts;

        #region Unity Lifecycle Methods
        
        private void Start()
        {
            if (initializeOnStart)
            {
                Initialize();
                
                if (activateOnStart)
                {
                    Activate();
                }
            }
        }
        
        private void OnDestroy()
        {
            Deactivate();
            Cleanup();
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Initialize the radar system and all LOD modules
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;
            
            Debug.Log($"[{gameObject.name}] Initializing Radar Suite Controller");
            
            // Initialize LOD modules
            InitializeLODModules();
            
            isInitialized = true;
        }
        
        /// <summary>
        /// Activate the radar with the current LOD setting
        /// </summary>
        public void Activate()
        {
            if (!isInitialized)
            {
                Debug.LogWarning($"[{gameObject.name}] Cannot activate radar: not initialized. Call Initialize() first.");
                return;
            }
            
            if (isActive) return;
            
            Debug.Log($"[{gameObject.name}] Activating Radar Suite (LOD: {currentLOD})");
            
            // Activate the current LOD module
            activeModule = lodModules[currentLOD];
            activeModule.Activate();
            
            isActive = true;
            OnRadarActivated?.Invoke();
        }
        
        /// <summary>
        /// Deactivate the radar system
        /// </summary>
        public void Deactivate()
        {
            if (!isActive) return;
            
            Debug.Log($"[{gameObject.name}] Deactivating Radar Suite");
            
            // Deactivate the current LOD module
            if (activeModule != null)
            {
                activeModule.Deactivate();
                activeModule = null;
            }
            
            // Clear contacts
            activeContacts.Clear();
            
            isActive = false;
            OnRadarDeactivated?.Invoke();
        }
        
        /// <summary>
        /// Change the radar's Level of Detail
        /// </summary>
        /// <param name="newLOD">The new LOD level to use</param>
        public void SetLOD(RadarLOD newLOD)
        {
            if (currentLOD == newLOD) return;
            
            bool wasActive = isActive;
            
            // Deactivate current module if active
            if (wasActive)
            {
                Deactivate();
            }
            
            // Change LOD
            currentLOD = newLOD;
            Debug.Log($"[{gameObject.name}] Changing Radar LOD to {currentLOD}");
            
            // Reactivate if it was active
            if (wasActive)
            {
                Activate();
            }
            
            OnLODChanged?.Invoke(currentLOD);
        }
        
        /// <summary>
        /// Get the current radar module
        /// </summary>
        /// <typeparam name="T">Type of radar module to retrieve</typeparam>
        /// <returns>The radar module of the specified type, or null if not found</returns>
        public T GetRadarModule<T>() where T : class, IRadarLODModule
        {
            foreach (var module in lodModules.Values)
            {
                if (module is T typedModule)
                {
                    return typedModule;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Set a radar parameter that will be applied to all LOD modules
        /// </summary>
        /// <param name="paramName">Name of the parameter</param>
        /// <param name="value">Value to set</param>
        public void SetRadarParameter(string paramName, object value)
        {
            foreach (var module in lodModules.Values)
            {
                module.SetParameter(paramName, value);
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private void InitializeLODModules()
        {
            lodModules = new Dictionary<RadarLOD, IRadarLODModule>();
            
            // Create and initialize all LOD modules
            lodModules[RadarLOD.LOD1_PassiveDetection] = gameObject.AddComponent<PassiveDetectionModule>();
            lodModules[RadarLOD.LOD2_BasicRadar] = gameObject.AddComponent<BasicRadarModule>();
            lodModules[RadarLOD.LOD3_DopplerRadar] = gameObject.AddComponent<DopplerRadarModule>();
            lodModules[RadarLOD.LOD4_3DTracking] = gameObject.AddComponent<ThreeDTrackingModule>();
            lodModules[RadarLOD.LOD5_HighFidelity] = gameObject.AddComponent<HighFidelityModule>();
            
            // Initialize each module
            foreach (var module in lodModules.Values)
            {
                module.Initialize(this);
                
                // Subscribe to events
                module.OnContactDetected += HandleContactDetected;
                module.OnContactLost += HandleContactLost;
                module.OnContactUpdated += HandleContactUpdated;
            }
        }
        
        private void Cleanup()
        {
            if (lodModules != null)
            {
                foreach (var module in lodModules.Values)
                {
                    // Unsubscribe from events
                    module.OnContactDetected -= HandleContactDetected;
                    module.OnContactLost -= HandleContactLost;
                    module.OnContactUpdated -= HandleContactUpdated;
                    
                    // Destroy component if it's a MonoBehaviour
                    if (module is MonoBehaviour mb)
                    {
                        Destroy(mb);
                    }
                }
                
                lodModules.Clear();
            }
            
            isInitialized = false;
        }
        
        private void HandleContactDetected(RadarContact contact)
        {
            if (!activeContacts.Contains(contact))
            {
                activeContacts.Add(contact);
            }
            
            // Raise the event
            OnContactDetected?.Invoke(contact);
        }
        
        private void HandleContactLost(RadarContact contact)
        {
            if (activeContacts.Contains(contact))
            {
                activeContacts.Remove(contact);
            }
            
            // Raise the event
            OnContactLost?.Invoke(contact);
        }
        
        private void HandleContactUpdated(RadarContact contact)
        {
            // Raise the event
            OnContactUpdated?.Invoke(contact);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Radar Level of Detail options
    /// </summary>
    public enum RadarLOD
    {
        LOD1_PassiveDetection,   // Passive detection only
        LOD2_BasicRadar,         // Basic radar with range
        LOD3_DopplerRadar,       // Doppler radar with velocity
        LOD4_3DTracking,         // 3D tracking with angle measurement
        LOD5_HighFidelity        // High-fidelity imaging and advanced features
    }
}
