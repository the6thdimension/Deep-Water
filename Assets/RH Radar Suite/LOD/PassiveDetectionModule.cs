using System.Collections.Generic;
using UnityEngine;

namespace RHRadarSuite
{
    /// <summary>
    /// LOD1: Passive Detection Module
    /// Detects and identifies signals or emissions from external sources without transmitting.
    /// Provides basic directional awareness of where the signal is coming from.
    /// </summary>
    [AddComponentMenu("RH Radar Suite/LOD Modules/LOD1 Passive Detection")]
    public class PassiveDetectionModule : RadarLODModuleBase
    {
        [Header("Passive Detection Settings")]
        [Tooltip("Sensitivity to emissions (higher values detect weaker signals)")]
        [Range(0.1f, 10f)]
        [SerializeField] private float sensitivity = 1f;
        
        [Tooltip("Directional accuracy in degrees (lower is better)")]
        [Range(1f, 45f)]
        [SerializeField] private float directionalAccuracy = 15f;
        
        [Tooltip("Minimum signal strength required for detection")]
        [Range(0.01f, 1f)]
        [SerializeField] private float detectionThreshold = 0.1f;
        
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
                PerformPassiveScan();
                nextScanTime = Time.time + updateInterval;
            }
        }
        
        private void PerformPassiveScan()
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
                
                // Check if the target has a radar signature or is emitting
                RadarSignature signature = target.GetComponent<RadarSignature>();
                if (signature == null) continue;
                
                // Check if target is emitting (jamming counts as emission)
                bool isEmitting = signature.IsJamming;
                
                // If not emitting, skip
                if (!isEmitting) continue;
                
                // Calculate signal strength based on distance and signature
                float distance = Vector3.Distance(transform.position, target.transform.position);
                float signalStrength = CalculateSignalStrength(signature, distance);
                
                // If signal is too weak, skip
                if (signalStrength < detectionThreshold) continue;
                
                // Add to active targets
                activeTargets.Add(target);
                
                // Update or create contact
                if (contacts.TryGetValue(target, out RadarContact contact))
                {
                    // Add directional inaccuracy based on module settings
                    Vector3 actualPosition = target.transform.position;
                    Vector3 detectedPosition = AddDirectionalInaccuracy(actualPosition);
                    
                    // Update existing contact
                    contact.Update(detectedPosition, signalStrength, RadarLOD.LOD1_PassiveDetection);
                    
                    // Update jamming info
                    if (signature.IsJamming)
                    {
                        contact.UpdateJammingInfo(true, signature.JammingStrength);
                    }
                }
                else
                {
                    // Create new contact
                    contact = new RadarContact(target);
                    
                    // Add directional inaccuracy
                    Vector3 actualPosition = target.transform.position;
                    Vector3 detectedPosition = AddDirectionalInaccuracy(actualPosition);
                    
                    contact.Update(detectedPosition, signalStrength, RadarLOD.LOD1_PassiveDetection);
                    
                    // Update jamming info
                    if (signature.IsJamming)
                    {
                        contact.UpdateJammingInfo(true, signature.JammingStrength);
                    }
                    
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
            // Base signal strength is inversely proportional to square of distance
            float baseStrength = 1f / (distance * distance);
            
            // Apply jamming strength if target is jamming
            if (signature.IsJamming)
            {
                baseStrength *= signature.JammingStrength;
            }
            
            // Apply sensitivity multiplier
            baseStrength *= sensitivity;
            
            // Normalize to 0-1 range
            return Mathf.Clamp01(baseStrength * 1000000f); // Scale factor to bring into reasonable range
        }
        
        private Vector3 AddDirectionalInaccuracy(Vector3 actualPosition)
        {
            // Calculate direction to target
            Vector3 directionToTarget = actualPosition - transform.position;
            float distance = directionToTarget.magnitude;
            
            // Add inaccuracy based on directional accuracy setting
            // More inaccuracy at longer ranges
            float inaccuracyFactor = directionalAccuracy * (distance / detectionRange);
            
            // Add random deviation
            Vector3 deviation = Random.insideUnitSphere * inaccuracyFactor;
            
            // Return position with added inaccuracy
            return transform.position + directionToTarget.normalized * distance + deviation;
        }
        
        public override void SetParameter(string paramName, object value)
        {
            base.SetParameter(paramName, value);
            
            switch (paramName)
            {
                case "sensitivity":
                    if (value is float sens)
                        sensitivity = sens;
                    break;
                case "directionalAccuracy":
                    if (value is float acc)
                        directionalAccuracy = acc;
                    break;
                case "detectionThreshold":
                    if (value is float threshold)
                        detectionThreshold = threshold;
                    break;
            }
        }
        
        public override object GetParameter(string paramName)
        {
            switch (paramName)
            {
                case "sensitivity":
                    return sensitivity;
                case "directionalAccuracy":
                    return directionalAccuracy;
                case "detectionThreshold":
                    return detectionThreshold;
                default:
                    return base.GetParameter(paramName);
            }
        }
    }
}
