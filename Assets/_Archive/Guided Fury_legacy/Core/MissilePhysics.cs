using UnityEngine;

namespace GuidedFury.Core
{
    /// <summary>
    /// Handles the physics and aerodynamic modeling for missiles at LOD 3 fidelity.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MissilePhysics : MonoBehaviour
    {
        #region Inspector Properties
        [Header("Physics Configuration")]
        [SerializeField] private float dragCoefficient = 0.1f;
        [SerializeField] private float liftCoefficient = 0.3f;
        [SerializeField] private float thrustForce = 1000f;
        [SerializeField] private float initialBoostMultiplier = 3f;
        [SerializeField] private float initialBoostDuration = 1.5f;
        [SerializeField] private float stabilityFactor = 0.8f;
        [SerializeField] private AnimationCurve thrustCurve = AnimationCurve.Linear(0, 1, 1, 0.8f);
        [SerializeField] private AnimationCurve dragCurve = AnimationCurve.Linear(0, 1, 1, 1.2f);
        
        [Header("Flight Characteristics")]
        [SerializeField] private float minSpeed = 50f;
        [SerializeField] private float cruiseAltitude = 100f;
        [SerializeField] private bool useTerrainAvoidance = false;
        [SerializeField] private float terrainAvoidanceHeight = 50f;
        [SerializeField] private LayerMask terrainLayer;
        #endregion

        #region Runtime Properties
        private MissileBase missileBase;
        private Rigidbody rb;
        private float currentSpeed;
        private float launchTime;
        private bool isInitialized = false;
        private Vector3 targetDirection;
        private Vector3 desiredRotation;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Initialize the physics component
        /// </summary>
        /// <param name="missile">The parent missile</param>
        public void Initialize(MissileBase missile)
        {
            missileBase = missile;
            isInitialized = true;
            
            // Subscribe to events
            missileBase.OnMissileLaunched += OnLaunched;
            missileBase.OnMissileDestroyed += OnDestroyed;
            
            Reset();
        }

        /// <summary>
        /// Reset the physics state
        /// </summary>
        public void Reset()
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            currentSpeed = 0f;
            targetDirection = transform.forward;
            desiredRotation = transform.eulerAngles;
        }

        /// <summary>
        /// Update the physics simulation
        /// </summary>
        public void UpdatePhysics()
        {
            if (!isInitialized) return;
            
            // Calculate thrust based on time since launch
            float timeSinceLaunch = Time.time - launchTime;
            float thrustMultiplier = timeSinceLaunch < initialBoostDuration 
                ? initialBoostMultiplier 
                : 1f;
            
            float normalizedLifetime = Mathf.Clamp01(timeSinceLaunch / missileBase.GetMaxSpeed());
            float currentThrust = thrustForce * thrustMultiplier * thrustCurve.Evaluate(normalizedLifetime);
            
            // Apply thrust in forward direction
            rb.AddForce(transform.forward * currentThrust, ForceMode.Force);
            
            // Apply drag (increases with speed)
            float currentDrag = dragCoefficient * dragCurve.Evaluate(normalizedLifetime) * rb.linearVelocity.sqrMagnitude;
            rb.AddForce(-rb.linearVelocity.normalized * currentDrag, ForceMode.Force);
            
            // Apply lift if we have lateral velocity
            Vector3 lateralVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, transform.forward);
            if (lateralVelocity.sqrMagnitude > 0.1f)
            {
                Vector3 liftForce = Vector3.Cross(transform.forward, lateralVelocity).normalized;
                liftForce = Vector3.Cross(liftForce, transform.forward);
                rb.AddForce(liftForce * liftCoefficient * lateralVelocity.magnitude, ForceMode.Force);
            }
            
            // Apply stability - tendency to align with velocity
            Vector3 velocityDirection = rb.linearVelocity.normalized;
            Vector3 stabilityTorque = Vector3.Cross(transform.forward, velocityDirection) * stabilityFactor;
            rb.AddTorque(stabilityTorque, ForceMode.Force);
            
            // Apply rotation towards target direction
            ApplyRotationTowardsTarget();
            
            // Apply terrain avoidance if enabled
            if (useTerrainAvoidance)
            {
                ApplyTerrainAvoidance();
            }
            
            // Enforce minimum speed
            if (rb.linearVelocity.magnitude < minSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * minSpeed;
            }
            
            // Clamp to max speed
            if (rb.linearVelocity.magnitude > missileBase.GetMaxSpeed())
            {
                rb.linearVelocity = rb.linearVelocity.normalized * missileBase.GetMaxSpeed();
            }
            
            // Update current speed
            currentSpeed = rb.linearVelocity.magnitude;
        }

        /// <summary>
        /// Get the current missile speed
        /// </summary>
        /// <returns>The current speed in m/s</returns>
        public float GetCurrentSpeed()
        {
            return currentSpeed;
        }
        
        /// <summary>
        /// Handle guidance updates from the guidance system
        /// </summary>
        /// <param name="targetPos">The target position to steer towards</param>
        public void OnGuidanceUpdate(Vector3 targetPos)
        {
            targetDirection = (targetPos - transform.position).normalized;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Handle missile launch
        /// </summary>
        private void OnLaunched()
        {
            launchTime = Time.time;
            
            // Initial velocity in forward direction
            rb.linearVelocity = transform.forward * minSpeed;
        }

        /// <summary>
        /// Handle missile destruction
        /// </summary>
        private void OnDestroyed()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Apply rotation towards the target direction
        /// </summary>
        private void ApplyRotationTowardsTarget()
        {
            // Calculate rotation to target
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
            
            // Calculate max rotation this frame based on turn rate
            float maxDegreesPerFrame = missileBase.GetMaxTurnRate() * Time.deltaTime;
            
            // Apply rotation with rate limiting
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxDegreesPerFrame);
        }

        /// <summary>
        /// Apply terrain avoidance logic
        /// </summary>
        private void ApplyTerrainAvoidance()
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, cruiseAltitude * 2f, terrainLayer))
            {
                float currentHeight = hit.distance;
                
                // If we're too close to terrain, apply upward force
                if (currentHeight < terrainAvoidanceHeight)
                {
                    float avoidanceForce = (terrainAvoidanceHeight - currentHeight) / terrainAvoidanceHeight;
                    rb.AddForce(Vector3.up * avoidanceForce * thrustForce, ForceMode.Force);
                    
                    // Also adjust target direction to avoid terrain
                    targetDirection = Vector3.Lerp(targetDirection, Vector3.up, avoidanceForce * 0.5f).normalized;
                }
            }
        }
        #endregion
    }
}
