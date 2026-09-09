using UnityEngine;
using GuidedFury.Core;
using GuidedFury.Modules.Sensors;
using GuidedFury.Modules.Guidance;
using GuidedFury.Modules.FlightControl;
using GuidedFury.ScriptableObjects;
using System.Collections.Generic;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Example script demonstrating the use of advanced missile modules.
    /// </summary>
    public class AdvancedMissileExample : MonoBehaviour
    {
        [Header("Missile Configuration")]
        [SerializeField] private MissileConfigSO missileConfig;
        [SerializeField] private Transform launchPoint;
        [SerializeField] private Transform targetObject;
        
        [Header("Module Configuration")]
        [SerializeField] private bool useAdvancedIRSensor = true;
        [SerializeField] private bool useTerrainFollowing = true;
        [SerializeField] private bool useAdvancedAerodynamics = true;
        
        [Header("UI References")]
        [SerializeField] private UnityEngine.UI.Toggle irSensorToggle;
        [SerializeField] private UnityEngine.UI.Toggle terrainFollowingToggle;
        [SerializeField] private UnityEngine.UI.Toggle aerodynamicsToggle;
        [SerializeField] private UnityEngine.UI.Button launchButton;
        [SerializeField] private UnityEngine.UI.Text statusText;
        
        private MissileManager missileManager;
        private List<MissileBase> activeMissiles = new List<MissileBase>();
        
        private void Start()
        {
            // Get or create missile manager
            missileManager = FindObjectOfType<MissileManager>();
            if (missileManager == null)
            {
                GameObject managerObj = new GameObject("MissileManager");
                missileManager = managerObj.AddComponent<MissileManager>();
            }
            
            // Set up UI
            SetupUI();
        }
        
        private void Update()
        {
            // Update status text with active missile count and their modules
            UpdateStatusText();
        }
        
        /// <summary>
        /// Set up UI elements and callbacks
        /// </summary>
        private void SetupUI()
        {
            if (irSensorToggle != null)
            {
                irSensorToggle.isOn = useAdvancedIRSensor;
                irSensorToggle.onValueChanged.AddListener((value) => useAdvancedIRSensor = value);
            }
            
            if (terrainFollowingToggle != null)
            {
                terrainFollowingToggle.isOn = useTerrainFollowing;
                terrainFollowingToggle.onValueChanged.AddListener((value) => useTerrainFollowing = value);
            }
            
            if (aerodynamicsToggle != null)
            {
                aerodynamicsToggle.isOn = useAdvancedAerodynamics;
                aerodynamicsToggle.onValueChanged.AddListener((value) => useAdvancedAerodynamics = value);
            }
            
            if (launchButton != null)
            {
                launchButton.onClick.AddListener(LaunchMissile);
            }
        }
        
        /// <summary>
        /// Update the status text with active missile information
        /// </summary>
        private void UpdateStatusText()
        {
            if (statusText == null) return;
            
            // Remove destroyed missiles from the list
            activeMissiles.RemoveAll(m => m == null);
            
            string status = $"Active Missiles: {activeMissiles.Count}\n";
            
            foreach (var missile in activeMissiles)
            {
                status += $"\nMissile {missile.GetInstanceID()}:\n";
                
                // Check for advanced modules
                var irSensor = missile.GetComponent<AdvancedIRSensor>();
                var terrainFollowing = missile.GetComponent<TerrainFollowingGuidance>();
                var aerodynamics = missile.GetComponent<AdvancedAerodynamics>();
                
                status += $"- IR Sensor: {(irSensor != null && irSensor.IsEnabled() ? "Active" : "Inactive")}\n";
                status += $"- Terrain Following: {(terrainFollowing != null && terrainFollowing.IsEnabled() ? "Active" : "Inactive")}\n";
                status += $"- Advanced Aero: {(aerodynamics != null && aerodynamics.IsEnabled() ? "Active" : "Inactive")}\n";
                
                // Add missile state
                status += $"- State: {missile.GetMissileState()}\n";
                
                // Add distance to target if available
                if (missile.GetCurrentTarget() != null)
                {
                    float distance = Vector3.Distance(missile.transform.position, missile.GetCurrentTarget().position);
                    status += $"- Target Distance: {distance:F1}m\n";
                }
            }
            
            statusText.text = status;
        }
        
        /// <summary>
        /// Launch a missile with the selected modules
        /// </summary>
        public void LaunchMissile()
        {
            if (missileManager == null || missileConfig == null)
            {
                Debug.LogError("Missing missile manager or configuration!");
                return;
            }
            
            // Get launch position and rotation
            Vector3 position = launchPoint != null ? launchPoint.position : transform.position;
            Quaternion rotation = launchPoint != null ? launchPoint.rotation : transform.rotation;
            
            // Spawn missile from config
            MissileBase missile = missileManager.LaunchMissile(missileConfig.GetMissilePrefab().name, position, rotation, targetObject);
            
            if (missile == null)
            {
                Debug.LogError("Failed to spawn missile!");
                return;
            }
            
            // Add to active missiles list
            activeMissiles.Add(missile);
            
            // Add and configure advanced modules
            ConfigureAdvancedModules(missile);
            
            // Set target
            if (targetObject != null)
            {
                missile.SetTarget(targetObject);
            }
            
            // Launch the missile
            missile.Launch();
            
            Debug.Log($"Launched missile with ID: {missile.GetInstanceID()}");
        }
        
        /// <summary>
        /// Configure advanced modules on the missile
        /// </summary>
        /// <param name="missile">The missile to configure</param>
        private void ConfigureAdvancedModules(MissileBase missile)
        {
            // Add Advanced IR Sensor if enabled
            if (useAdvancedIRSensor)
            {
                AddOrEnableModule<AdvancedIRSensor>(missile);
            }
            
            // Add Terrain Following Guidance if enabled
            if (useTerrainFollowing)
            {
                AddOrEnableModule<TerrainFollowingGuidance>(missile);
            }
            
            // Add Advanced Aerodynamics if enabled
            if (useAdvancedAerodynamics)
            {
                AddOrEnableModule<AdvancedAerodynamics>(missile);
            }
        }
        
        /// <summary>
        /// Add or enable a module on the missile
        /// </summary>
        /// <typeparam name="T">The module type</typeparam>
        /// <param name="missile">The missile to add the module to</param>
        /// <returns>The module instance</returns>
        private T AddOrEnableModule<T>(MissileBase missile) where T : MonoBehaviour, IMissileModule
        {
            // Check if module already exists
            T module = missile.GetComponent<T>();
            
            if (module == null)
            {
                // Add module if it doesn't exist
                module = missile.gameObject.AddComponent<T>();
            }
            
            // Initialize and enable the module
            module.Initialize(missile);
            module.SetEnabled(true);
            
            return module;
        }
    }
}
