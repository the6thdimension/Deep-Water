using System.Collections.Generic;
using UnityEngine;

namespace GuidedFury.Core
{
    /// <summary>
    /// Handles target acquisition and tracking for missiles at LOD 3 fidelity.
    /// </summary>
    public class MissileSensor : MonoBehaviour
    {
        #region Inspector Properties
        [Header("Sensor Configuration")]
        [SerializeField] private SensorType sensorType = SensorType.Infrared;
        [SerializeField] private float detectionRange = 5000f;
        [SerializeField] private float trackingRange = 3000f;
        [SerializeField] private float detectionAngle = 45f;
        [SerializeField] private float trackingAngle = 30f;
        [SerializeField] private float updateRate = 0.1f;
        [SerializeField] private LayerMask targetLayers;
        [SerializeField] private LayerMask obstacleLayers;
        [SerializeField] private bool requireLineOfSight = true;
        
        [Header("IR Sensor Settings")]
        [SerializeField] private float heatSignatureThreshold = 0.3f;
        [SerializeField] private float sunBlindingFactor = 0.5f;
        
        [Header("Radar Sensor Settings")]
        [SerializeField] private float radarCrossSectionThreshold = 0.2f;
        [SerializeField] private bool isActiveRadar = true;
        [SerializeField] private float jammerResistance = 0.5f;
        #endregion

        #region Runtime Properties
        private MissileBase missileBase;
        private float lastSensorUpdate;
        private List<Transform> potentialTargets = new List<Transform>();
        private Transform currentTarget;
        private bool isTracking = false;
        private bool isLocked = false;
        private float lockStrength = 0f;
        #endregion

        #region Unity Lifecycle
        private void OnDrawGizmosSelected()
        {
            // Draw detection cone
            Gizmos.color = Color.yellow;
            DrawCone(transform.position, transform.forward, detectionAngle, detectionRange);
            
            // Draw tracking cone
            Gizmos.color = Color.red;
            DrawCone(transform.position, transform.forward, trackingAngle, trackingRange);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Initialize the sensor component
        /// </summary>
        /// <param name="missile">The parent missile</param>
        public void Initialize(MissileBase missile)
        {
            missileBase = missile;
            
            // Subscribe to events
            missileBase.OnMissileLaunched += OnLaunched;
            missileBase.OnMissileDestroyed += OnDestroyed;
            
            Reset();
        }

        /// <summary>
        /// Reset the sensor state
        /// </summary>
        public void Reset()
        {
            potentialTargets.Clear();
            currentTarget = null;
            isTracking = false;
            isLocked = false;
            lockStrength = 0f;
            lastSensorUpdate = 0f;
        }

        /// <summary>
        /// Update the sensor logic
        /// </summary>
        public void UpdateSensor()
        {
            // Update at specified rate
            if (Time.time - lastSensorUpdate < updateRate)
                return;
                
            lastSensorUpdate = Time.time;
            
            // If we already have a target, try to maintain tracking
            if (currentTarget != null)
            {
                if (IsTargetTrackable(currentTarget))
                {
                    UpdateTargetTracking(currentTarget);
                }
                else
                {
                    LoseTarget();
                }
            }
            // Otherwise, scan for new targets
            else
            {
                ScanForTargets();
            }
        }

        /// <summary>
        /// Set a specific target for the sensor to track
        /// </summary>
        /// <param name="target">The target to track</param>
        public void SetTarget(Transform target)
        {
            if (target == null)
            {
                LoseTarget();
                return;
            }
            
            currentTarget = target;
            isTracking = true;
            lockStrength = 0.5f; // Start with partial lock
            
            // Notify missile base
            missileBase.SetTarget(currentTarget);
        }

        /// <summary>
        /// Get the current sensor type
        /// </summary>
        /// <returns>The sensor type</returns>
        public SensorType GetSensorType()
        {
            return sensorType;
        }

        /// <summary>
        /// Check if the sensor is currently tracking a target
        /// </summary>
        /// <returns>True if tracking, false otherwise</returns>
        public bool IsTracking()
        {
            return isTracking && currentTarget != null;
        }

        /// <summary>
        /// Check if the sensor has a lock on the target
        /// </summary>
        /// <returns>True if locked, false otherwise</returns>
        public bool IsLocked()
        {
            return isLocked && currentTarget != null;
        }

        /// <summary>
        /// Get the current lock strength (0-1)
        /// </summary>
        /// <returns>The lock strength</returns>
        public float GetLockStrength()
        {
            return lockStrength;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Handle missile launch
        /// </summary>
        private void OnLaunched()
        {
            // If no target is set, scan immediately
            if (currentTarget == null)
            {
                ScanForTargets();
            }
        }

        /// <summary>
        /// Handle missile destruction
        /// </summary>
        private void OnDestroyed()
        {
            Reset();
        }

        /// <summary>
        /// Scan for potential targets
        /// </summary>
        private void ScanForTargets()
        {
            potentialTargets.Clear();
            
            // Find all potential targets in range
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRange, targetLayers);
            
            foreach (var collider in colliders)
            {
                // Skip if this is our own collider
                if (collider.transform == transform || collider.transform.IsChildOf(transform))
                    continue;
                    
                // Check if in detection cone
                Vector3 directionToTarget = collider.transform.position - transform.position;
                float angle = Vector3.Angle(transform.forward, directionToTarget);
                
                if (angle <= detectionAngle)
                {
                    // Check line of sight if required
                    if (!requireLineOfSight || HasLineOfSight(collider.transform))
                    {
                        potentialTargets.Add(collider.transform);
                    }
                }
            }
            
            // Sort targets by priority
            SortTargetsByPriority();
            
            // Select best target
            if (potentialTargets.Count > 0)
            {
                SetTarget(potentialTargets[0]);
            }
        }

        /// <summary>
        /// Sort targets by priority based on sensor type
        /// </summary>
        private void SortTargetsByPriority()
        {
            switch (sensorType)
            {
                case SensorType.Infrared:
                    // Sort by heat signature and angle to target
                    potentialTargets.Sort((a, b) => {
                        float heatA = GetHeatSignature(a);
                        float heatB = GetHeatSignature(b);
                        
                        // If heat signatures are similar, sort by angle
                        if (Mathf.Abs(heatA - heatB) < 0.1f)
                        {
                            float angleA = Vector3.Angle(transform.forward, a.position - transform.position);
                            float angleB = Vector3.Angle(transform.forward, b.position - transform.position);
                            return angleA.CompareTo(angleB);
                        }
                        
                        // Otherwise sort by heat signature (higher is better)
                        return heatB.CompareTo(heatA);
                    });
                    break;
                    
                case SensorType.Radar:
                    // Sort by radar cross-section and distance
                    potentialTargets.Sort((a, b) => {
                        float rcsA = GetRadarCrossSection(a);
                        float rcsB = GetRadarCrossSection(b);
                        
                        // If RCS values are similar, sort by distance
                        if (Mathf.Abs(rcsA - rcsB) < 0.1f)
                        {
                            float distA = Vector3.Distance(transform.position, a.position);
                            float distB = Vector3.Distance(transform.position, b.position);
                            return distA.CompareTo(distB);
                        }
                        
                        // Otherwise sort by RCS (higher is better)
                        return rcsB.CompareTo(rcsA);
                    });
                    break;
                    
                default:
                    // Default sort by distance
                    potentialTargets.Sort((a, b) => {
                        float distA = Vector3.Distance(transform.position, a.position);
                        float distB = Vector3.Distance(transform.position, b.position);
                        return distA.CompareTo(distB);
                    });
                    break;
            }
        }

        /// <summary>
        /// Update tracking for the current target
        /// </summary>
        /// <param name="target">The target to track</param>
        private void UpdateTargetTracking(Transform target)
        {
            // Calculate distance and angle to target
            Vector3 directionToTarget = target.position - transform.position;
            float distance = directionToTarget.magnitude;
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            
            // Update lock strength based on tracking parameters
            float distanceFactor = 1f - Mathf.Clamp01(distance / trackingRange);
            float angleFactor = 1f - Mathf.Clamp01(angle / trackingAngle);
            
            float newLockStrength = distanceFactor * angleFactor;
            
            // Apply sensor-specific modifiers
            switch (sensorType)
            {
                case SensorType.Infrared:
                    float heatSignature = GetHeatSignature(target);
                    newLockStrength *= Mathf.Clamp01(heatSignature / heatSignatureThreshold);
                    
                    // Check for sun blinding
                    float sunAngle = Vector3.Angle(transform.forward, Vector3.up);
                    if (sunAngle < 30f)
                    {
                        newLockStrength *= (1f - sunBlindingFactor * (1f - sunAngle / 30f));
                    }
                    break;
                    
                case SensorType.Radar:
                    float rcs = GetRadarCrossSection(target);
                    newLockStrength *= Mathf.Clamp01(rcs / radarCrossSectionThreshold);
                    
                    // Apply jamming resistance if target has jammer
                    var jammerComponent = target.GetComponent<IJammer>();
                    if (jammerComponent != null && jammerComponent.IsJamming())
                    {
                        newLockStrength *= jammerResistance;
                    }
                    break;
            }
            
            // Smooth lock strength changes
            lockStrength = Mathf.Lerp(lockStrength, newLockStrength, 0.3f);
            
            // Update lock status
            isLocked = lockStrength > 0.7f;
            
            // If lock is lost, potentially lose target
            if (lockStrength < 0.2f)
            {
                LoseTarget();
            }
        }

        /// <summary>
        /// Check if a target is trackable
        /// </summary>
        /// <param name="target">The target to check</param>
        /// <returns>True if trackable, false otherwise</returns>
        private bool IsTargetTrackable(Transform target)
        {
            if (target == null) return false;
            
            // Check if target is within tracking range
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > trackingRange) return false;
            
            // Check if target is within tracking angle
            Vector3 directionToTarget = target.position - transform.position;
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            if (angle > trackingAngle) return false;
            
            // Check line of sight if required
            if (requireLineOfSight && !HasLineOfSight(target)) return false;
            
            // Check sensor-specific requirements
            switch (sensorType)
            {
                case SensorType.Infrared:
                    if (GetHeatSignature(target) < heatSignatureThreshold) return false;
                    break;
                    
                case SensorType.Radar:
                    if (GetRadarCrossSection(target) < radarCrossSectionThreshold) return false;
                    break;
            }
            
            return true;
        }

        /// <summary>
        /// Lose the current target
        /// </summary>
        private void LoseTarget()
        {
            if (currentTarget != null)
            {
                Transform lostTarget = currentTarget;
                currentTarget = null;
                isTracking = false;
                isLocked = false;
                lockStrength = 0f;
                
                // Notify missile base
                missileBase.SetTarget(null);
            }
        }

        /// <summary>
        /// Check if there is line of sight to a target
        /// </summary>
        /// <param name="target">The target to check</param>
        /// <returns>True if there is line of sight, false otherwise</returns>
        private bool HasLineOfSight(Transform target)
        {
            Vector3 directionToTarget = target.position - transform.position;
            float distance = directionToTarget.magnitude;
            
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToTarget.normalized, out hit, distance, obstacleLayers))
            {
                // Hit something that's not the target
                if (hit.transform != target && !hit.transform.IsChildOf(target))
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Get the heat signature of a target (0-1)
        /// </summary>
        /// <param name="target">The target to check</param>
        /// <returns>The heat signature value</returns>
        private float GetHeatSignature(Transform target)
        {
            // Check if target has a heat source component
            var heatSource = target.GetComponent<IHeatSource>();
            if (heatSource != null)
            {
                return heatSource.GetHeatSignature();
            }
            
            // Default heat signature based on tag
            if (target.CompareTag("Aircraft") || target.CompareTag("Helicopter"))
                return 0.8f;
            if (target.CompareTag("Vehicle") || target.CompareTag("Ship"))
                return 0.6f;
            if (target.CompareTag("Building"))
                return 0.3f;
                
            return 0.4f; // Default value
        }

        /// <summary>
        /// Get the radar cross-section of a target (0-1)
        /// </summary>
        /// <param name="target">The target to check</param>
        /// <returns>The radar cross-section value</returns>
        private float GetRadarCrossSection(Transform target)
        {
            // Check if target has a radar signature component
            var radarSignature = target.GetComponent<IRadarSignature>();
            if (radarSignature != null)
            {
                return radarSignature.GetRadarCrossSection();
            }
            
            // Default RCS based on tag and size
            float size = target.localScale.magnitude;
            
            if (target.CompareTag("Aircraft"))
                return Mathf.Clamp01(0.7f * size / 5f);
            if (target.CompareTag("Helicopter"))
                return Mathf.Clamp01(0.6f * size / 4f);
            if (target.CompareTag("Vehicle"))
                return Mathf.Clamp01(0.5f * size / 3f);
            if (target.CompareTag("Ship"))
                return Mathf.Clamp01(0.9f * size / 10f);
            if (target.CompareTag("Building"))
                return Mathf.Clamp01(0.8f * size / 20f);
                
            return Mathf.Clamp01(0.5f * size / 5f); // Default value
        }

        /// <summary>
        /// Draw a cone in the scene view
        /// </summary>
        private void DrawCone(Vector3 position, Vector3 direction, float angle, float length)
        {
            float radius = Mathf.Tan(angle * Mathf.Deg2Rad) * length;
            Vector3 endPosition = position + direction * length;
            
            // Draw center line
            Gizmos.DrawLine(position, endPosition);
            
            // Draw cone edges
            Vector3 up = Vector3.Cross(direction, Vector3.right).normalized;
            if (up == Vector3.zero)
                up = Vector3.Cross(direction, Vector3.forward).normalized;
                
            Vector3 right = Vector3.Cross(up, direction).normalized;
            
            int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * 2 * Mathf.PI / segments;
                float angle2 = (i + 1) * 2 * Mathf.PI / segments;
                
                Vector3 point1 = endPosition + (up * Mathf.Sin(angle1) + right * Mathf.Cos(angle1)) * radius;
                Vector3 point2 = endPosition + (up * Mathf.Sin(angle2) + right * Mathf.Cos(angle2)) * radius;
                
                Gizmos.DrawLine(position, point1);
                Gizmos.DrawLine(point1, point2);
            }
        }
        #endregion

        /// <summary>
        /// Sensor types
        /// </summary>
        public enum SensorType
        {
            Infrared,
            Radar,
            Laser,
            Optical
        }
    }


    /// <summary>
    /// Interface for objects with radar signature
    /// </summary>
    public interface IRadarSignature
    {
        float GetRadarCrossSection();
    }

    /// <summary>
    /// Interface for objects with jamming capability
    /// </summary>
    public interface IJammer
    {
        bool IsJamming();
        float GetJammingStrength();
    }
}
