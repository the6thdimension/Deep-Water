using System.Collections.Generic;
using UnityEngine;

namespace RHRadarSuite
{
    /// <summary>
    /// LOD2: Basic Radar Detection Module
    /// Actively transmits pulses and measures range to targets.
    /// Provides simple target detection and tracking.
    /// </summary>
    [AddComponentMenu("RH Radar Suite/LOD Modules/LOD2 Basic Radar")]
    public class BasicRadarModule : RadarLODModuleBase
    {
        [Header("Basic Radar Settings")]
        [Tooltip("Radar beam width in degrees")]
        [Range(1f, 120f)]
        [SerializeField] public float beamWidth = 30f;
        
        [Tooltip("Radar rotation speed in degrees per second")]
        [Range(1f, 360f)]
        [SerializeField] private float rotationSpeed = 60f;
        
        [Tooltip("Enable 360-degree scanning")]
        [SerializeField] private bool enableFullRotation = true;
        
        [Tooltip("Scan sector size in degrees when not in 360 mode")]
        [Range(10f, 180f)]
        [SerializeField] private float sectorSize = 90f;
        
        [Tooltip("Center angle for sector scan")]
        [Range(0f, 360f)]
        [SerializeField] private float sectorCenter = 0f;
        
        [Tooltip("Minimum signal strength required for detection")]
        [Range(0.01f, 1f)]
        [SerializeField] private float detectionThreshold = 0.1f;
        
        [Tooltip("Range accuracy (lower is better)")]
        [Range(1f, 100f)]
        [SerializeField] private float rangeAccuracy = 10f;
        
        // Internal variables
        private float currentScanAngle;
        private float nextScanTime;
        private readonly Dictionary<GameObject, RadarContact> contacts = new Dictionary<GameObject, RadarContact>();
        private readonly List<GameObject> activeTargets = new List<GameObject>();
        private readonly List<GameObject> lostTargets = new List<GameObject>();
        private Collider[] targetBuffer;
        
        // Visualization
        private readonly List<Vector3> scanPoints = new List<Vector3>();
        private int maxScanPoints = 360;
        
        // Properties
        public float CurrentScanAngle => currentScanAngle;
        public IReadOnlyList<Vector3> ScanPoints => scanPoints;
        
        public override void Initialize(RadarSuiteController controller)
        {
            base.Initialize(controller);
            
            targetBuffer = new Collider[maxTargets];
            currentScanAngle = transform.eulerAngles.y;
        }
        
        public override void Activate()
        {
            base.Activate();
            
            nextScanTime = Time.time;
            scanPoints.Clear();
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
            scanPoints.Clear();
            
            base.Deactivate();
        }
        
        private void Update()
        {
            if (!isActive) return;
            
            // Update scan angle
            UpdateScanAngle();
            
            // Perform scan at regular intervals
            if (Time.time >= nextScanTime)
            {
                PerformRadarScan();
                nextScanTime = Time.time + updateInterval;
            }
        }
        
        private void UpdateScanAngle()
        {
            float deltaAngle = rotationSpeed * Time.deltaTime;
            currentScanAngle = (currentScanAngle + deltaAngle) % 360f;
            
            // Check if we should scan at this angle
            if (enableFullRotation || IsAngleInSector(currentScanAngle))
            {
                // Add scan point for visualization
                Vector3 scanDirection = Quaternion.Euler(0, currentScanAngle, 0) * Vector3.forward;
                AddScanPoint(transform.position + scanDirection * detectionRange);
            }
        }
        
        private bool IsAngleInSector(float angle)
        {
            float halfSector = sectorSize / 2f;
            float minAngle = (sectorCenter - halfSector + 360f) % 360f;
            float maxAngle = (sectorCenter + halfSector + 360f) % 360f;
            
            if (minAngle > maxAngle)
            {
                return angle >= minAngle || angle <= maxAngle;
            }
            
            return angle >= minAngle && angle <= maxAngle;
        }
        
        private void AddScanPoint(Vector3 point)
        {
            scanPoints.Add(point);
            
            if (scanPoints.Count > maxScanPoints)
            {
                scanPoints.RemoveAt(0);
            }
        }
        
        private void PerformRadarScan()
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
                
                // Check if target is in current scan area
                if (!IsTargetInCurrentScanArea(target)) continue;
                
                // Calculate signal strength based on distance and signature
                RadarSignature signature = target.GetComponent<RadarSignature>();
                float distance = Vector3.Distance(transform.position, target.transform.position);
                float signalStrength = CalculateSignalStrength(signature, distance);
                
                // Apply jamming if target is jamming
                if (signature != null && signature.IsJamming)
                {
                    float jammingEffectiveness = signature.GetJammingEffectiveness(radarPower);
                    signalStrength *= (1f - jammingEffectiveness);
                }
                
                // If signal is too weak, skip
                if (signalStrength < detectionThreshold) continue;
                
                // Add to active targets
                activeTargets.Add(target);
                
                // Update or create contact
                if (contacts.TryGetValue(target, out RadarContact contact))
                {
                    // Update existing contact
                    contact.Update(target.transform.position, signalStrength, RadarLOD.LOD2_BasicRadar);
                    
                    // Raise update event
                    RaiseContactUpdated(contact);
                }
                else
                {
                    // Create new contact
                    contact = new RadarContact(target);
                    
                    // Add range inaccuracy
                    Vector3 actualPosition = target.transform.position;
                    Vector3 detectedPosition = AddRangeInaccuracy(actualPosition);
                    
                    contact.Update(detectedPosition, signalStrength, RadarLOD.LOD2_BasicRadar);
                    
                    // Update jamming info if applicable
                    if (signature != null && signature.IsJamming)
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
        
        private bool IsTargetInCurrentScanArea(GameObject target)
        {
            // If full rotation is enabled, target is always in scan area
            if (enableFullRotation) return true;
            
            // Calculate angle to target
            Vector3 directionToTarget = target.transform.position - transform.position;
            directionToTarget.y = 0; // Ignore height difference
            
            float angleToTarget = Vector3.SignedAngle(Vector3.forward, directionToTarget.normalized, Vector3.up);
            angleToTarget = (angleToTarget + 360f) % 360f;
            
            // Check if angle is within current sector
            return IsAngleInSector(angleToTarget);
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
        
        private Vector3 AddRangeInaccuracy(Vector3 actualPosition)
        {
            // Calculate direction to target
            Vector3 directionToTarget = actualPosition - transform.position;
            float distance = directionToTarget.magnitude;
            
            // Add inaccuracy based on range accuracy setting
            // More inaccuracy at longer ranges
            float inaccuracyFactor = rangeAccuracy * (distance / detectionRange);
            
            // Add random range deviation (only along the line of sight)
            float rangeDeviation = Random.Range(-inaccuracyFactor, inaccuracyFactor);
            
            // Return position with added inaccuracy
            return transform.position + directionToTarget.normalized * (distance + rangeDeviation);
        }
        
        public override void SetParameter(string paramName, object value)
        {
            base.SetParameter(paramName, value);
            
            switch (paramName)
            {
                case "beamWidth":
                    if (value is float width)
                        beamWidth = width;
                    break;
                case "rotationSpeed":
                    if (value is float speed)
                        rotationSpeed = speed;
                    break;
                case "enableFullRotation":
                    if (value is bool enable)
                        enableFullRotation = enable;
                    break;
                case "sectorSize":
                    if (value is float size)
                        sectorSize = size;
                    break;
                case "sectorCenter":
                    if (value is float center)
                        sectorCenter = center;
                    break;
                case "detectionThreshold":
                    if (value is float threshold)
                        detectionThreshold = threshold;
                    break;
                case "rangeAccuracy":
                    if (value is float accuracy)
                        rangeAccuracy = accuracy;
                    break;
            }
        }
        
        public override object GetParameter(string paramName)
        {
            switch (paramName)
            {
                case "beamWidth":
                    return beamWidth;
                case "rotationSpeed":
                    return rotationSpeed;
                case "enableFullRotation":
                    return enableFullRotation;
                case "sectorSize":
                    return sectorSize;
                case "sectorCenter":
                    return sectorCenter;
                case "detectionThreshold":
                    return detectionThreshold;
                case "rangeAccuracy":
                    return rangeAccuracy;
                default:
                    return base.GetParameter(paramName);
            }
        }
    }
}
