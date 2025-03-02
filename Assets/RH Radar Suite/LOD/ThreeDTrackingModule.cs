using System.Collections.Generic;
using UnityEngine;

namespace RHRadarSuite
{
    /// <summary>
    /// LOD4: 3D Tracking Module
    /// Determines 3D spatial location: range, velocity, azimuth/elevation.
    /// Uses phased arrays/antenna patterns for angle measurement.
    /// </summary>
    [AddComponentMenu("RH Radar Suite/LOD Modules/LOD4 3D Tracking")]
    public class ThreeDTrackingModule : RadarLODModuleBase
    {
        [Header("3D Tracking Settings")]
        [Tooltip("Horizontal beam width in degrees")]
        [Range(1f, 90f)]
        [SerializeField] private float horizontalBeamWidth = 10f;
        
        [Tooltip("Vertical beam width in degrees")]
        [Range(1f, 90f)]
        [SerializeField] private float verticalBeamWidth = 10f;
        
        [Tooltip("Horizontal scan rate in degrees per second")]
        [Range(1f, 360f)]
        [SerializeField] private float horizontalScanRate = 45f;
        
        [Tooltip("Vertical scan rate in degrees per second")]
        [Range(1f, 180f)]
        [SerializeField] private float verticalScanRate = 30f;
        
        [Tooltip("Minimum signal strength required for detection")]
        [Range(0.01f, 1f)]
        [SerializeField] private float detectionThreshold = 0.1f;
        
        [Tooltip("Range accuracy (lower is better)")]
        [Range(1f, 50f)]
        [SerializeField] private float rangeAccuracy = 3f;
        
        [Tooltip("Angular accuracy in degrees (lower is better)")]
        [Range(0.1f, 10f)]
        [SerializeField] private float angularAccuracy = 1f;
        
        [Tooltip("Velocity accuracy in m/s (lower is better)")]
        [Range(0.1f, 5f)]
        [SerializeField] private float velocityAccuracy = 0.5f;
        
        [Header("Beam Control")]
        [Tooltip("Enable beam steering")]
        [SerializeField] private bool enableBeamSteering = true;
        
        [Tooltip("Number of simultaneous beams (phased array)")]
        [Range(1, 8)]
        [SerializeField] private int numBeams = 1;
        
        [Tooltip("Enable track-while-scan mode")]
        [SerializeField] private bool enableTrackWhileScan = true;
        
        [Header("Advanced Features")]
        [Tooltip("Enable terrain masking")]
        [SerializeField] private bool enableTerrainMasking = true;
        
        [Tooltip("Enable ECCM (Electronic Counter-Counter Measures)")]
        [SerializeField] private bool enableECCM = true;
        
        // Internal variables for tracking
        private float currentHorizontalAngle;
        private float currentVerticalAngle;
        private float nextScanTime;
        private readonly Dictionary<GameObject, RadarContact> contacts = new Dictionary<GameObject, RadarContact>();
        private readonly Dictionary<GameObject, Vector3> previousPositions = new Dictionary<GameObject, Vector3>();
        private readonly List<GameObject> activeTargets = new List<GameObject>();
        private readonly List<GameObject> lostTargets = new List<GameObject>();
        private Collider[] targetBuffer;
        
        // Beam tracking
        private readonly List<BeamInfo> activeBeams = new List<BeamInfo>();
        
        // Visualization
        private readonly List<Vector3> scanPoints = new List<Vector3>();
        private int maxScanPoints = 360;
        
        // Properties
        public float CurrentHorizontalAngle => currentHorizontalAngle;
        public float CurrentVerticalAngle => currentVerticalAngle;
        public IReadOnlyList<Vector3> ScanPoints => scanPoints;
        
        public override void Initialize(RadarSuiteController controller)
        {
            base.Initialize(controller);
            
            targetBuffer = new Collider[maxTargets];
            currentHorizontalAngle = transform.eulerAngles.y;
            currentVerticalAngle = 0f;
            
            // Initialize beams
            InitializeBeams();
        }
        
        private void InitializeBeams()
        {
            activeBeams.Clear();
            
            for (int i = 0; i < numBeams; i++)
            {
                activeBeams.Add(new BeamInfo
                {
                    HorizontalAngle = currentHorizontalAngle,
                    VerticalAngle = currentVerticalAngle,
                    AssignedTarget = null,
                    IsTracking = false
                });
            }
        }
        
        public override void Activate()
        {
            base.Activate();
            
            nextScanTime = Time.time;
            scanPoints.Clear();
            previousPositions.Clear();
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
            previousPositions.Clear();
            activeBeams.Clear();
            
            base.Deactivate();
        }
        
        private void Update()
        {
            if (!isActive) return;
            
            // Update scan angles
            UpdateScanAngles();
            
            // Perform scan at regular intervals
            if (Time.time >= nextScanTime)
            {
                Perform3DScan();
                nextScanTime = Time.time + updateInterval;
            }
        }
        
        private void UpdateScanAngles()
        {
            float deltaHorizontal = horizontalScanRate * Time.deltaTime;
            float deltaVertical = verticalScanRate * Time.deltaTime;
            
            currentHorizontalAngle = (currentHorizontalAngle + deltaHorizontal) % 360f;
            currentVerticalAngle = Mathf.Clamp(currentVerticalAngle + deltaVertical, -90f, 90f);
            
            // Add scan point for visualization
            Vector3 scanDirection = Quaternion.Euler(currentVerticalAngle, currentHorizontalAngle, 0) * Vector3.forward;
            AddScanPoint(transform.position + scanDirection * detectionRange);
        }
        
        private void Perform3DScan()
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
                
                // Calculate velocity
                Vector3 velocity = Vector3.zero;
                if (previousPositions.TryGetValue(target, out Vector3 prevPos))
                {
                    velocity = (target.transform.position - prevPos) / updateInterval;
                }
                
                // Store current position for next velocity calculation
                previousPositions[target] = target.transform.position;
                
                // Add to active targets
                activeTargets.Add(target);
                
                // Update or create contact
                if (contacts.TryGetValue(target, out RadarContact contact))
                {
                    // Update existing contact
                    contact.Update(target.transform.position, signalStrength, RadarLOD.LOD4_3DTracking);
                    
                    // Raise update event
                    RaiseContactUpdated(contact);
                }
                else
                {
                    // Create new contact
                    contact = new RadarContact(target);
                    contact.Update(target.transform.position, signalStrength, RadarLOD.LOD4_3DTracking);
                    
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
                    previousPositions.Remove(target);
                }
            }
        }
        
        private void AddScanPoint(Vector3 point)
        {
            scanPoints.Add(point);
            if (scanPoints.Count > maxScanPoints)
            {
                scanPoints.RemoveAt(0);
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
            
            // Apply jamming if target is jamming
            if (signature != null && signature.IsJamming)
            {
                float jammingEffectiveness = signature.GetJammingEffectiveness(radarPower);
                baseStrength *= (1f - jammingEffectiveness);
            }
            
            // Normalize to 0-1 range
            return Mathf.Clamp01(baseStrength * 1000000000f); // Scale factor to bring into reasonable range
        }
    }
    
    /// <summary>
    /// Represents a radar beam for tracking purposes
    /// </summary>
    public class BeamInfo
    {
        public float HorizontalAngle { get; set; }
        public float VerticalAngle { get; set; }
        public GameObject AssignedTarget { get; set; }
        public bool IsTracking { get; set; }
        public float TrackingTime { get; set; }
    }
}
