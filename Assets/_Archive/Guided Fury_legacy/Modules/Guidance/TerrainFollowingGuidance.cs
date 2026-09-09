using UnityEngine;
using GuidedFury.Core;

namespace GuidedFury.Modules.Guidance
{
    /// <summary>
    /// Advanced terrain-following guidance module that allows missiles to fly at low altitudes.
    /// </summary>
    [RequireComponent(typeof(MissileBase))]
    public class TerrainFollowingGuidance : MonoBehaviour, IMissileModule
    {
        #region Inspector Properties
        [Header("Terrain Following Configuration")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float desiredAltitude = 30f;
        [SerializeField] private float lookaheadDistance = 200f;
        [SerializeField] private float maxClimbAngle = 30f;
        [SerializeField] private float maxDiveAngle = 20f;
        [SerializeField] private float terrainSmoothness = 0.5f;
        [SerializeField] private LayerMask terrainLayers;
        
        [Header("Advanced Settings")]
        [SerializeField] private bool useAdaptiveAltitude = true;
        [SerializeField] private float minAltitude = 15f;
        [SerializeField] private float maxAltitude = 100f;
        [SerializeField] private float adaptationSpeed = 0.5f;
        [SerializeField] private bool useObstacleAvoidance = true;
        [SerializeField] private float obstacleDetectionRange = 300f;
        [SerializeField] private float obstacleAvoidanceStrength = 0.7f;
        #endregion

        #region Runtime Properties
        private MissileBase missileBase;
        private MissileGuidance baseGuidance;
        private MissilePhysics missilePhysics;
        private bool isInitialized = false;
        private float currentDesiredAltitude;
        private Vector3 terrainNormal = Vector3.up;
        private float terrainHeight = 0f;
        private Vector3 obstacleAvoidanceDirection = Vector3.zero;
        #endregion

        #region Unity Lifecycle
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying && isInitialized && enabled)
            {
                // Draw terrain detection rays
                Gizmos.color = Color.green;
                Vector3 origin = transform.position;
                Vector3 forward = transform.forward;
                
                // Draw lookahead ray
                Gizmos.DrawLine(origin, origin + forward * lookaheadDistance);
                
                // Draw desired altitude
                Gizmos.color = Color.yellow;
                Vector3 desiredPoint = new Vector3(origin.x, terrainHeight + currentDesiredAltitude, origin.z);
                Gizmos.DrawWireSphere(desiredPoint, 2f);
                
                // Draw terrain normal
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(origin, origin + terrainNormal * 10f);
                
                // Draw obstacle avoidance direction
                if (useObstacleAvoidance && obstacleAvoidanceDirection.sqrMagnitude > 0.1f)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(origin, origin + obstacleAvoidanceDirection.normalized * 20f);
                }
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
            baseGuidance = missile.GetGuidance();
            missilePhysics = missile.GetPhysics();
            
            if (baseGuidance != null)
            {
                isInitialized = true;
                
                // Register for events
                missileBase.OnMissileLaunched += OnMissileLaunched;
                missileBase.OnMissileDestroyed += OnMissileDestroyed;
                
                // Initialize values
                currentDesiredAltitude = desiredAltitude;
            }
            else
            {
                Debug.LogWarning("TerrainFollowingGuidance requires a missile with guidance. Module will be disabled.");
                enabled = false;
            }
        }

        /// <summary>
        /// Update the module logic
        /// </summary>
        public void UpdateModule()
        {
            if (!isInitialized || !enabled) return;
            
            // Only apply terrain following in mid-course phase
            if (baseGuidance.GetGuidancePhase() != MissileGuidance.GuidancePhase.MidCourse)
                return;
                
            // Update terrain information
            UpdateTerrainInfo();
            
            // Update adaptive altitude if enabled
            if (useAdaptiveAltitude)
            {
                UpdateAdaptiveAltitude();
            }
            
            // Update obstacle avoidance if enabled
            if (useObstacleAvoidance)
            {
                UpdateObstacleAvoidance();
            }
            
            // Apply terrain following guidance
            ApplyTerrainFollowingGuidance();
        }

        /// <summary>
        /// Enable or disable the module
        /// </summary>
        /// <param name="isEnabled">Whether the module should be enabled</param>
        public void SetEnabled(bool isEnabled)
        {
            enabled = isEnabled;
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
            return "Terrain Following Guidance";
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Handle missile launch
        /// </summary>
        private void OnMissileLaunched()
        {
            // Reset values
            currentDesiredAltitude = desiredAltitude;
            terrainNormal = Vector3.up;
            terrainHeight = 0f;
            obstacleAvoidanceDirection = Vector3.zero;
        }

        /// <summary>
        /// Handle missile destruction
        /// </summary>
        private void OnMissileDestroyed()
        {
            // Clean up if needed
        }

        /// <summary>
        /// Update terrain information using raycasts
        /// </summary>
        private void UpdateTerrainInfo()
        {
            Vector3 origin = transform.position;
            Vector3 direction = Vector3.down;
            RaycastHit hit;
            
            // Cast ray downward to find terrain
            if (Physics.Raycast(origin, direction, out hit, 1000f, terrainLayers))
            {
                terrainHeight = hit.point.y;
                terrainNormal = hit.normal;
            }
            
            // Cast ray forward to detect terrain slope
            Vector3 forwardOrigin = origin + transform.forward * lookaheadDistance * 0.5f;
            if (Physics.Raycast(forwardOrigin, direction, out hit, 1000f, terrainLayers))
            {
                // Blend with current normal for smoothness
                terrainNormal = Vector3.Lerp(terrainNormal, hit.normal, terrainSmoothness * Time.deltaTime);
            }
            
            // Cast ray far forward to detect upcoming terrain
            Vector3 farForwardOrigin = origin + transform.forward * lookaheadDistance;
            if (Physics.Raycast(farForwardOrigin, direction, out hit, 1000f, terrainLayers))
            {
                // Calculate upcoming terrain height
                float upcomingTerrainHeight = hit.point.y;
                
                // Calculate terrain slope
                float heightDifference = upcomingTerrainHeight - terrainHeight;
                float slope = heightDifference / lookaheadDistance;
                
                // Adjust desired altitude based on slope
                if (slope > 0.1f) // Upward slope
                {
                    // Increase altitude to clear upcoming terrain
                    currentDesiredAltitude = Mathf.Lerp(currentDesiredAltitude, 
                        desiredAltitude + heightDifference * 1.5f, 
                        terrainSmoothness * Time.deltaTime);
                }
                else if (slope < -0.1f) // Downward slope
                {
                    // Gradually decrease altitude to follow terrain
                    currentDesiredAltitude = Mathf.Lerp(currentDesiredAltitude, 
                        desiredAltitude, 
                        terrainSmoothness * 0.5f * Time.deltaTime);
                }
            }
        }

        /// <summary>
        /// Update adaptive altitude based on speed and terrain
        /// </summary>
        private void UpdateAdaptiveAltitude()
        {
            // Adjust altitude based on speed
            float speed = missilePhysics.GetCurrentSpeed();
            float speedFactor = Mathf.Clamp01((speed - 100f) / 300f); // Normalize speed between 100-400 m/s
            
            // Higher speed = higher altitude for safety
            float speedBasedAltitude = Mathf.Lerp(minAltitude, maxAltitude, speedFactor);
            
            // Blend with current desired altitude
            currentDesiredAltitude = Mathf.Lerp(currentDesiredAltitude, 
                speedBasedAltitude, 
                adaptationSpeed * Time.deltaTime);
                
            // Ensure minimum altitude
            currentDesiredAltitude = Mathf.Max(currentDesiredAltitude, minAltitude);
        }

        /// <summary>
        /// Update obstacle avoidance logic
        /// </summary>
        private void UpdateObstacleAvoidance()
        {
            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;
            
            // Reset avoidance direction
            obstacleAvoidanceDirection = Vector3.zero;
            
            // Cast rays in a cone to detect obstacles
            int rayCount = 5;
            float angleStep = 15f;
            
            for (int i = 0; i < rayCount; i++)
            {
                // Central ray
                if (i == 0)
                {
                    CastObstacleRay(origin, forward, 1.0f);
                    continue;
                }
                
                // Calculate angle for this ray
                float angle = angleStep * i;
                
                // Cast rays at positive and negative angles
                Quaternion rotationPos = Quaternion.AngleAxis(angle, Vector3.up);
                Quaternion rotationNeg = Quaternion.AngleAxis(-angle, Vector3.up);
                
                Vector3 dirPos = rotationPos * forward;
                Vector3 dirNeg = rotationNeg * forward;
                
                // Weight decreases with angle
                float weight = 1.0f - (angle / (rayCount * angleStep));
                
                CastObstacleRay(origin, dirPos, weight);
                CastObstacleRay(origin, dirNeg, weight);
            }
            
            // Normalize avoidance direction if significant
            if (obstacleAvoidanceDirection.sqrMagnitude > 0.1f)
            {
                obstacleAvoidanceDirection.Normalize();
            }
        }

        /// <summary>
        /// Cast a ray to detect obstacles and update avoidance direction
        /// </summary>
        /// <param name="origin">Ray origin</param>
        /// <param name="direction">Ray direction</param>
        /// <param name="weight">Importance weight of this ray</param>
        private void CastObstacleRay(Vector3 origin, Vector3 direction, float weight)
        {
            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, obstacleDetectionRange, terrainLayers))
            {
                // Calculate avoidance vector (perpendicular to hit normal)
                Vector3 avoidDir = Vector3.Cross(hit.normal, Vector3.up).normalized;
                
                // If avoidance direction is ambiguous, use more information
                if (avoidDir.sqrMagnitude < 0.1f)
                {
                    // Use cross product with forward direction
                    avoidDir = Vector3.Cross(hit.normal, direction).normalized;
                }
                
                // Weight by distance (closer obstacles have stronger influence)
                float distanceFactor = 1.0f - (hit.distance / obstacleDetectionRange);
                
                // Add to overall avoidance direction
                obstacleAvoidanceDirection += avoidDir * weight * distanceFactor * obstacleAvoidanceStrength;
            }
        }

        /// <summary>
        /// Apply terrain following guidance to missile
        /// </summary>
        private void ApplyTerrainFollowingGuidance()
        {
            // Calculate current altitude above terrain
            float currentAltitude = transform.position.y - terrainHeight;
            
            // Calculate desired position
            Vector3 currentPos = transform.position;
            Vector3 desiredPos = new Vector3(currentPos.x, terrainHeight + currentDesiredAltitude, currentPos.z);
            
            // Get target position from missile
            Vector3 targetPos = missileBase.GetLastKnownTargetPosition();
            
            // Calculate direction to target (horizontal only)
            Vector3 targetDirection = targetPos - currentPos;
            targetDirection.y = 0; // Remove vertical component
            targetDirection.Normalize();
            
            // Calculate altitude error
            float altitudeError = currentDesiredAltitude - currentAltitude;
            
            // Calculate pitch angle based on altitude error
            float pitchAngle = Mathf.Clamp(altitudeError * 3f, -maxDiveAngle, maxClimbAngle);
            
            // Apply terrain normal influence
            Vector3 terrainInfluence = terrainNormal * 0.5f;
            
            // Calculate final direction incorporating terrain following
            Vector3 finalDirection = targetDirection;
            
            // Apply pitch adjustment
            finalDirection = Quaternion.Euler(-pitchAngle, 0, 0) * finalDirection;
            
            // Apply terrain normal influence
            finalDirection = Vector3.Lerp(finalDirection, terrainInfluence, 0.3f);
            
            // Apply obstacle avoidance if needed
            if (obstacleAvoidanceDirection.sqrMagnitude > 0.1f)
            {
                finalDirection = Vector3.Lerp(finalDirection, obstacleAvoidanceDirection, 0.5f);
            }
            
            // Normalize the final direction
            finalDirection.Normalize();
            
            // Apply to missile physics
            missilePhysics.OnGuidanceUpdate(currentPos + finalDirection * 100f);
        }
        #endregion
    }
}
