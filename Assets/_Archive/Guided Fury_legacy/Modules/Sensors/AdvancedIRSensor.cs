using UnityEngine;
using GuidedFury.Core;

namespace GuidedFury.Modules.Sensors
{
    /// <summary>
    /// Advanced IR sensor module that enhances the base missile sensor with multi-band IR capabilities.
    /// </summary>
    [RequireComponent(typeof(MissileBase))]
    public class AdvancedIRSensor : MonoBehaviour, IMissileModule
    {
        #region Inspector Properties
        [Header("Advanced IR Configuration")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float enhancedDetectionRange = 7000f;
        [SerializeField] private float enhancedTrackingRange = 5000f;
        [SerializeField] private float counterMeasureResistance = 0.8f;
        [SerializeField] private bool useMultiBandDetection = true;
        [SerializeField] private bool useIRCM = true; // IR Counter-Countermeasures
        
        [Header("Multi-Band Settings")]
        [SerializeField] private bool useLongWaveIR = true;  // Better for cold targets
        [SerializeField] private bool useMidWaveIR = true;   // Better for medium temp targets
        [SerializeField] private bool useShortWaveIR = true; // Better for hot targets
        [SerializeField] private float bandSwitchingSpeed = 0.5f;
        
        [Header("Weather Penetration")]
        [SerializeField] private float fogPenetration = 0.7f;
        [SerializeField] private float rainPenetration = 0.6f;
        [SerializeField] private float snowPenetration = 0.5f;
        #endregion

        #region Runtime Properties
        private MissileBase missileBase;
        private MissileSensor baseSensor;
        private bool isInitialized = false;
        private IRBand currentBand = IRBand.MidWave;
        private float lastBandSwitchTime;
        #endregion

        #region Unity Lifecycle
        private void OnEnable()
        {
            if (isInitialized)
            {
                // Apply enhancements when enabled
                ApplySensorEnhancements();
            }
        }

        private void OnDisable()
        {
            if (isInitialized)
            {
                // Remove enhancements when disabled
                RemoveSensorEnhancements();
            }
        }
        #endregion

        #region IMissileModule Implementation
        /// <summary>
        /// Initialize the module with the parent missile
        /// </summary>
        /// <param name="missile">The parent missile instance</param>
        public void Initialize(MissileBase missile)
        {
            missileBase = missile;
            baseSensor = missile.GetSensor();
            
            if (baseSensor != null && baseSensor.GetSensorType() == MissileSensor.SensorType.Infrared)
            {
                isInitialized = true;
                
                // Register for events
                missileBase.OnMissileLaunched += OnMissileLaunched;
                missileBase.OnMissileDestroyed += OnMissileDestroyed;
                
                // Apply enhancements if enabled
                if (enabled)
                {
                    ApplySensorEnhancements();
                }
            }
            else
            {
                Debug.LogWarning("AdvancedIRSensor requires a missile with an IR sensor type. Module will be disabled.");
                enabled = false;
            }
        }

        /// <summary>
        /// Update the module logic
        /// </summary>
        public void UpdateModule()
        {
            if (!isInitialized || !enabled) return;
            
            // Update multi-band detection if enabled
            if (useMultiBandDetection)
            {
                UpdateMultiBandDetection();
            }
            
            // Apply IRCM if enabled
            if (useIRCM)
            {
                ApplyIRCM();
            }
        }

        /// <summary>
        /// Enable or disable the module
        /// </summary>
        /// <param name="isEnabled">Whether the module should be enabled</param>
        public void SetEnabled(bool isEnabled)
        {
            if (enabled == isEnabled) return;
            
            enabled = isEnabled;
            
            if (isInitialized)
            {
                if (enabled)
                {
                    ApplySensorEnhancements();
                }
                else
                {
                    RemoveSensorEnhancements();
                }
            }
        }

        /// <summary>
        /// Check if the module is currently enabled
        /// </summary>
        /// <returns>True if the module is enabled, false otherwise</returns>
        public bool IsEnabled()
        {
            return enabled;
        }

        /// <summary>
        /// Get the module name for display purposes
        /// </summary>
        /// <returns>The module name</returns>
        public string GetModuleName()
        {
            return "Advanced IR Sensor";
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Apply sensor enhancements to the base sensor
        /// </summary>
        private void ApplySensorEnhancements()
        {
            // This would ideally modify the base sensor properties
            // For now, we'll just enhance the functionality through our update method
            
            // In a full implementation, we might use reflection or a more robust
            // system to modify the base sensor's properties
        }

        /// <summary>
        /// Remove sensor enhancements from the base sensor
        /// </summary>
        private void RemoveSensorEnhancements()
        {
            // Restore original sensor properties
        }

        /// <summary>
        /// Handle missile launch
        /// </summary>
        private void OnMissileLaunched()
        {
            // Reset band switching
            lastBandSwitchTime = Time.time;
            currentBand = IRBand.MidWave;
        }

        /// <summary>
        /// Handle missile destruction
        /// </summary>
        private void OnMissileDestroyed()
        {
            // Clean up if needed
        }

        /// <summary>
        /// Update multi-band detection logic
        /// </summary>
        private void UpdateMultiBandDetection()
        {
            // Only switch bands periodically
            if (Time.time - lastBandSwitchTime < bandSwitchingSpeed)
                return;
                
            lastBandSwitchTime = Time.time;
            
            // Get current target and environmental conditions
            Transform currentTarget = missileBase.GetCurrentTarget();
            
            if (currentTarget == null) return;
            
            // Determine optimal IR band based on target and conditions
            IRBand optimalBand = DetermineOptimalBand(currentTarget);
            
            // Switch to optimal band
            currentBand = optimalBand;
        }

        /// <summary>
        /// Determine the optimal IR band for the current target and conditions
        /// </summary>
        /// <param name="target">The current target</param>
        /// <returns>The optimal IR band</returns>
        private IRBand DetermineOptimalBand(Transform target)
        {
            // Check if target has a heat source component
            var heatSource = target.GetComponent<IHeatSource>();
            float heatSignature = 0.5f; // Default medium heat
            
            if (heatSource != null)
            {
                heatSignature = heatSource.GetHeatSignature();
            }
            
            // Get weather conditions (in a real implementation, this would come from a weather system)
            bool isFoggy = false;
            bool isRaining = false;
            bool isSnowing = false;
            
            // Determine optimal band based on heat signature and weather
            if (heatSignature > 0.7f && useShortWaveIR)
            {
                // Hot target - use short wave IR (3-5 μm)
                return IRBand.ShortWave;
            }
            else if (heatSignature < 0.3f && useLongWaveIR)
            {
                // Cold target - use long wave IR (8-14 μm)
                return IRBand.LongWave;
            }
            else if (isRaining && useLongWaveIR)
            {
                // Rain penetration - use long wave IR
                return IRBand.LongWave;
            }
            else if ((isFoggy || isSnowing) && useMidWaveIR)
            {
                // Fog/snow penetration - use mid wave IR
                return IRBand.MidWave;
            }
            
            // Default to mid wave IR (3-8 μm)
            return IRBand.MidWave;
        }

        /// <summary>
        /// Apply IR counter-countermeasures to resist flares and other decoys
        /// </summary>
        private void ApplyIRCM()
        {
            // In a full implementation, this would analyze potential decoys
            // and help the missile distinguish between the real target and countermeasures
            
            // For now, this is just a placeholder for the functionality
        }
        #endregion

        /// <summary>
        /// IR bands for multi-band detection
        /// </summary>
        private enum IRBand
        {
            ShortWave,  // 1-3 μm
            MidWave,    // 3-8 μm
            LongWave    // 8-14 μm
        }
    }
}
