using System.Collections.Generic;
using UnityEngine;

namespace RHRadarSuite
{
    /// <summary>
    /// LOD5: High-Fidelity Active Tracking Module
    /// Provides SAR/ISAR-like features for detailed imaging and advanced clutter modeling.
    /// </summary>
    [AddComponentMenu("RH Radar Suite/LOD Modules/LOD5 High-Fidelity Tracking")]
    public class HighFidelityModule : RadarLODModuleBase
    {
        [Header("High-Fidelity Settings")]
        [Tooltip("Resolution in range (meters)")]
        [Range(0.1f, 10f)]
        [SerializeField] private float rangeResolution = 0.5f;
        
        [Tooltip("Resolution in velocity (m/s)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float velocityResolution = 0.1f;
        
        [Tooltip("Resolution in angle (degrees)")]
        [Range(0.1f, 5f)]
        [SerializeField] private float angleResolution = 0.5f;
        
        [Tooltip("Enable synthetic aperture radar (SAR) mode")]
        [SerializeField] private bool enableSAR = true;
        
        [Tooltip("Enable inverse synthetic aperture radar (ISAR) mode")]
        [SerializeField] private bool enableISAR = true;
        
        [Tooltip("Enable advanced clutter modeling")]
        [SerializeField] private bool enableClutterModeling = true;
        
        [Tooltip("Enable micro-Doppler effects")]
        [SerializeField] private bool enableMicroDoppler = true;
        
        [Tooltip("Minimum signal strength required for detection")]
        [Range(0.01f, 1f)]
        [SerializeField] private float detectionThreshold = 0.1f;
        
        [Header("Environmental Effects")]
        [Tooltip("Enable terrain masking")]
        [SerializeField] private bool enableTerrainMasking = true;
        
        [Tooltip("Enable sea clutter modeling")]
        [SerializeField] private bool enableSeaClutter = true;
        
        // Internal variables
        private float nextScanTime;
        private readonly Dictionary<GameObject, RadarContact> contacts = new Dictionary<GameObject, RadarContact>();
        private readonly List<GameObject> activeTargets = new List<GameObject>();
        private readonly List<GameObject> lostTargets = new List<GameObject>();
        private Collider[] targetBuffer;
        
        public override void Initialize(RadarSuiteController controller)
        {
            base.Initialize(controller);
            
            targetBuffer = new Collider[maxTargets];
        }
        
        public override void Activate()
        {
            base.Activate();
            
            nextScanTime = Time.time;
        }
        
        public override void Deactivate()
        {
            // Clear all contacts when deactivated
            foreach (var contact in contacts.Values)
            {
                contact.SetInactive();
                RaiseContactLost(contact);
            }
            
            contacts.Clear();
            activeTargets.Clear();
            
            base.Deactivate();
        }
        
        private void Update()
        {
            if (!isActive) return;
            
            // Perform scan at regular intervals
            if (Time.time >= nextScanTime)
            {
                PerformHighFidelityScan();
                nextScanTime = Time.time + updateInterval;
            }
        }
        
        private void PerformHighFidelityScan()
        {
            // Clear temporary lists
            activeTargets.Clear();
            lostTargets.Clear();
            
            // Get all potential targets within range
            int targetsFound = Physics.OverlapSphereNonAlloc(
                transform.position, 
                detectionRange, 
                targetBuffer, 
                targetLayers
            );
            
            // Process detected targets
            for (int i = 0; i < targetsFound; i++)
            {
                GameObject target = targetBuffer[i].gameObject;
                
                // Skip self
                if (target == gameObject) continue;
                
                // Calculate signal strength based on distance and signature
                RadarSignature signature = target.GetComponent<RadarSignature>();
                float distance = Vector3.Distance(transform.position, target.transform.position);
                float signalStrength = CalculateSignalStrength(signature, distance);
                
                // If signal is too weak, skip
                if (signalStrength < detectionThreshold) continue;
                
                // Add to active targets
                activeTargets.Add(target);
                
                // Update or create contact
                if (contacts.TryGetValue(target, out RadarContact contact))
                {
                    // Update existing contact
                    contact.Update(target.transform.position, signalStrength, RadarLOD.LOD5_HighFidelity);
                    
                    // Raise update event
                    RaiseContactUpdated(contact);
                }
                else
                {
                    // Create new contact
                    contact = new RadarContact(target);
                    contact.Update(target.transform.position, signalStrength, RadarLOD.LOD5_HighFidelity);
                    
                    // Add to contacts dictionary
                    contacts.Add(target, contact);
                    
                    // Raise event
                    RaiseContactDetected(contact);
                }
            }
            
            // Find lost contacts
            foreach (var target in contacts.Keys)
            {
                if (!activeTargets.Contains(target))
                {
                    lostTargets.Add(target);
                }
            }
            
            // Remove lost contacts
            foreach (var target in lostTargets)
            {
                if (contacts.TryGetValue(target, out RadarContact contact))
                {
                    contact.SetInactive();
                    RaiseContactLost(contact);
                    contacts.Remove(target);
                }
            }
        }
        
        private float CalculateSignalStrength(RadarSignature signature, float distance)
        {
            // Base signal strength is inversely proportional to fourth power of distance (radar equation)
            float baseStrength = 1f / (distance * distance * distance * distance);
            
            // Apply radar power
            baseStrength *= radarPower;
            
            // Apply target cross-section if available
            if (signature != null)
            {
                baseStrength *= signature.GetEffectiveRCS();
            }
            
            // Normalize to 0-1 range
            return Mathf.Clamp01(baseStrength * 1000000000f); // Scale factor to bring into reasonable range
        }
    }
}
