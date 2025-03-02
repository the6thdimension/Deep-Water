using System;
using UnityEngine;

namespace RHRadarSuite
{
    /// <summary>
    /// Interface for all radar LOD modules
    /// </summary>
    public interface IRadarLODModule
    {
        /// <summary>
        /// Initialize the radar module with a reference to the controller
        /// </summary>
        /// <param name="controller">The RadarSuiteController that owns this module</param>
        void Initialize(RadarSuiteController controller);
        
        /// <summary>
        /// Activate the radar module
        /// </summary>
        void Activate();
        
        /// <summary>
        /// Deactivate the radar module
        /// </summary>
        void Deactivate();
        
        /// <summary>
        /// Set a parameter value
        /// </summary>
        /// <param name="paramName">Name of the parameter</param>
        /// <param name="value">Value to set</param>
        void SetParameter(string paramName, object value);
        
        /// <summary>
        /// Get a parameter value
        /// </summary>
        /// <param name="paramName">Name of the parameter</param>
        /// <returns>The parameter value, or null if not found</returns>
        object GetParameter(string paramName);
        
        /// <summary>
        /// Event fired when a new contact is detected
        /// </summary>
        event Action<RadarContact> OnContactDetected;
        
        /// <summary>
        /// Event fired when a contact is lost
        /// </summary>
        event Action<RadarContact> OnContactLost;
        
        /// <summary>
        /// Event fired when a contact is updated
        /// </summary>
        event Action<RadarContact> OnContactUpdated;
    }
    
    /// <summary>
    /// Base class for all radar LOD modules
    /// </summary>
    public abstract class RadarLODModuleBase : MonoBehaviour, IRadarLODModule
    {
        protected RadarSuiteController controller;
        protected bool isInitialized = false;
        public bool isActive = false;
        
        // Common parameters
        protected float detectionRange;
        protected float radarPower;
        protected LayerMask targetLayers;
        protected float updateInterval;
        protected int maxTargets;
        
        // Events
        public event Action<RadarContact> OnContactDetected;
        public event Action<RadarContact> OnContactLost;
        public event Action<RadarContact> OnContactUpdated;
        
        public virtual void Initialize(RadarSuiteController controller)
        {
            this.controller = controller;
            
            // Initialize with controller parameters
            detectionRange = controller.MaxDetectionRange;
            radarPower = controller.RadarPower;
            targetLayers = controller.TargetLayers;
            updateInterval = controller.UpdateInterval;
            maxTargets = controller.MaxTargets;
            
            isInitialized = true;
            
            Debug.Log($"[{GetType().Name}] Initialized");
        }
        
        public virtual void Activate()
        {
            if (!isInitialized)
            {
                Debug.LogWarning($"[{GetType().Name}] Cannot activate: not initialized");
                return;
            }
            
            isActive = true;
            
            Debug.Log($"[{GetType().Name}] Activated");
        }
        
        public virtual void Deactivate()
        {
            isActive = false;
            
            Debug.Log($"[{GetType().Name}] Deactivated");
        }
        
        public virtual void SetParameter(string paramName, object value)
        {
            switch (paramName)
            {
                case "detectionRange":
                    if (value is float range)
                        detectionRange = range;
                    break;
                case "radarPower":
                    if (value is float power)
                        radarPower = power;
                    break;
                case "targetLayers":
                    if (value is LayerMask layers)
                        targetLayers = layers;
                    break;
                case "updateInterval":
                    if (value is float interval)
                        updateInterval = interval;
                    break;
                case "maxTargets":
                    if (value is int targets)
                        maxTargets = targets;
                    break;
                default:
                    Debug.LogWarning($"[{GetType().Name}] Unknown parameter: {paramName}");
                    break;
            }
        }
        
        public virtual object GetParameter(string paramName)
        {
            switch (paramName)
            {
                case "detectionRange":
                    return detectionRange;
                case "radarPower":
                    return radarPower;
                case "targetLayers":
                    return targetLayers;
                case "updateInterval":
                    return updateInterval;
                case "maxTargets":
                    return maxTargets;
                default:
                    Debug.LogWarning($"[{GetType().Name}] Unknown parameter: {paramName}");
                    return null;
            }
        }
        
        protected virtual void RaiseContactDetected(RadarContact contact)
        {
            OnContactDetected?.Invoke(contact);
        }
        
        protected virtual void RaiseContactLost(RadarContact contact)
        {
            OnContactLost?.Invoke(contact);
        }
        
        protected virtual void RaiseContactUpdated(RadarContact contact)
        {
            OnContactUpdated?.Invoke(contact);
        }
    }
}
