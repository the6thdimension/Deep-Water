using UnityEngine;

namespace GuidedFury.Core
{
    /// <summary>
    /// Handles guidance logic for missiles at LOD 3 fidelity.
    /// </summary>
    public class MissileGuidance : MonoBehaviour
    {
        #region Inspector Properties
        [Header("Guidance Configuration")]
        [SerializeField] private GuidanceType guidanceType = GuidanceType.ProportionalNavigation;
        [SerializeField] private float navigationGain = 3f;
        [SerializeField] private float leadFactor = 1.5f;
        [SerializeField] private float updateRate = 0.05f;
        [SerializeField] private float activationDelay = 0.5f;
        [SerializeField] private bool usePredictor = true;
        [SerializeField] private bool allowMidCourseUpdates = true;
        
        [Header("Trajectory Settings")]
        [SerializeField] private float cruiseAltitude = 100f;
        [SerializeField] private bool useLoftedTrajectory = false;
        [SerializeField] private float loftAngle = 30f;
        [SerializeField] private float terminalPhaseDistance = 1000f;
        [SerializeField] private AnimationCurve descentCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
        #endregion

        #region Runtime Properties
        private MissileBase missileBase;
        private MissilePhysics missilePhysics;
        private MissileSensor missileSensor;
        
        private Vector3 targetPosition;
        private Vector3 targetVelocity;
        private Vector3 lastTargetPosition;
        private Vector3 guidanceCommand;
        private Vector3 desiredDirection;
        
        private float launchTime;
        private float lastUpdateTime;
        private bool isGuiding = false;
        private bool isTerminalPhase = false;
        private GuidancePhase currentPhase = GuidancePhase.Initial;
        #endregion

        #region Unity Lifecycle
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying && isGuiding)
            {
                // Draw guidance vectors
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, transform.position + desiredDirection * 10f);
                
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, targetPosition);
                
                if (usePredictor)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(targetPosition, targetPosition + targetVelocity * 3f);
                    
                    Gizmos.color = Color.cyan;
                    Vector3 interceptPoint = PredictInterceptPoint();
                    Gizmos.DrawWireSphere(interceptPoint, 2f);
                }
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Initialize the guidance component
        /// </summary>
        /// <param name="missile">The parent missile</param>
        public void Initialize(MissileBase missile)
        {
            missileBase = missile;
            missilePhysics = missile.GetPhysics();
            missileSensor = missile.GetSensor();
            
            // Subscribe to events
            missileBase.OnMissileLaunched += OnLaunched;
            missileBase.OnMissileDestroyed += OnDestroyed;
            missileBase.OnTargetAcquired += OnTargetAcquired;
            missileBase.OnTargetLost += OnTargetLost;
            
            Reset();
        }

        /// <summary>
        /// Reset the guidance state
        /// </summary>
        public void Reset()
        {
            targetPosition = Vector3.zero;
            targetVelocity = Vector3.zero;
            lastTargetPosition = Vector3.zero;
            guidanceCommand = Vector3.zero;
            desiredDirection = transform.forward;
            
            isGuiding = false;
            isTerminalPhase = false;
            currentPhase = GuidancePhase.Initial;
        }

        /// <summary>
        /// Update the guidance logic
        /// </summary>
        public void UpdateGuidance()
        {
            // Don't guide until activation delay has passed
            if (Time.time - launchTime < activationDelay)
                return;
                
            // Don't update too frequently
            if (Time.time - lastUpdateTime < updateRate)
                return;
                
            lastUpdateTime = Time.time;
            
            // Update guidance based on current phase
            switch (currentPhase)
            {
                case GuidancePhase.Initial:
                    UpdateInitialPhase();
                    break;
                    
                case GuidancePhase.MidCourse:
                    UpdateMidCoursePhase();
                    break;
                    
                case GuidancePhase.Terminal:
                    UpdateTerminalPhase();
                    break;
            }
            
            // Notify missile of guidance update - use a public method instead of directly invoking the event
            if (missileBase != null)
            {
                // Use a different approach to update the guidance direction
                missileBase.GetPhysics().OnGuidanceUpdate(targetPosition);
            }
        }

        /// <summary>
        /// Set a specific target position
        /// </summary>
        /// <param name="position">The target position</param>
        public void SetTargetPosition(Vector3 position)
        {
            targetPosition = position;
            
            // If we're in mid-course and allow updates, use this position
            if (currentPhase == GuidancePhase.MidCourse && allowMidCourseUpdates)
            {
                isGuiding = true;
            }
        }

        /// <summary>
        /// Get the current guidance type
        /// </summary>
        /// <returns>The guidance type</returns>
        public GuidanceType GetGuidanceType()
        {
            return guidanceType;
        }

        /// <summary>
        /// Get the current guidance phase
        /// </summary>
        /// <returns>The guidance phase</returns>
        public GuidancePhase GetGuidancePhase()
        {
            return currentPhase;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Handle missile launch
        /// </summary>
        private void OnLaunched()
        {
            launchTime = Time.time;
            lastUpdateTime = Time.time;
            currentPhase = GuidancePhase.Initial;
            
            // If we have a target, start guiding immediately
            Transform currentTarget = missileBase.GetCurrentTarget();
            if (currentTarget != null)
            {
                targetPosition = currentTarget.position;
                isGuiding = true;
            }
        }

        /// <summary>
        /// Handle missile destruction
        /// </summary>
        private void OnDestroyed()
        {
            isGuiding = false;
        }

        /// <summary>
        /// Handle target acquisition
        /// </summary>
        /// <param name="target">The acquired target</param>
        private void OnTargetAcquired(Transform target)
        {
            if (target != null)
            {
                targetPosition = target.position;
                lastTargetPosition = targetPosition;
                isGuiding = true;
                
                // If we're in initial phase, transition to mid-course
                if (currentPhase == GuidancePhase.Initial)
                {
                    currentPhase = GuidancePhase.MidCourse;
                }
            }
        }

        /// <summary>
        /// Handle target loss
        /// </summary>
        /// <param name="target">The lost target</param>
        private void OnTargetLost(Transform target)
        {
            // Continue guiding to last known position
            targetPosition = missileBase.GetLastKnownTargetPosition();
        }

        /// <summary>
        /// Update the initial guidance phase
        /// </summary>
        private void UpdateInitialPhase()
        {
            // In initial phase, we climb to cruise altitude or follow lofted trajectory
            if (useLoftedTrajectory)
            {
                // Lofted trajectory - climb at loft angle
                desiredDirection = Quaternion.Euler(-loftAngle, 0, 0) * transform.forward;
            }
            else
            {
                // Climb to cruise altitude
                float currentAltitude = transform.position.y;
                if (currentAltitude < cruiseAltitude)
                {
                    float pitchAngle = Mathf.Lerp(30f, 5f, currentAltitude / cruiseAltitude);
                    desiredDirection = Quaternion.Euler(-pitchAngle, 0, 0) * transform.forward;
                }
                else
                {
                    // Transition to mid-course phase
                    currentPhase = GuidancePhase.MidCourse;
                }
            }
            
            // If we have a target and enough time has passed, transition to mid-course
            if (isGuiding && Time.time - launchTime > 2f)
            {
                currentPhase = GuidancePhase.MidCourse;
            }
        }

        /// <summary>
        /// Update the mid-course guidance phase
        /// </summary>
        private void UpdateMidCoursePhase()
        {
            // If we have a target, update target information
            Transform currentTarget = missileBase.GetCurrentTarget();
            if (currentTarget != null)
            {
                // Update target position
                targetPosition = currentTarget.position;
                
                // Calculate target velocity
                targetVelocity = (targetPosition - lastTargetPosition) / updateRate;
                lastTargetPosition = targetPosition;
                
                // Check if we should transition to terminal phase
                float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
                if (distanceToTarget <= terminalPhaseDistance)
                {
                    currentPhase = GuidancePhase.Terminal;
                    isTerminalPhase = true;
                }
            }
            
            // Apply guidance based on type
            ApplyGuidanceLogic();
        }

        /// <summary>
        /// Update the terminal guidance phase
        /// </summary>
        private void UpdateTerminalPhase()
        {
            // In terminal phase, we use more aggressive guidance
            Transform currentTarget = missileBase.GetCurrentTarget();
            if (currentTarget != null)
            {
                // Update target position
                targetPosition = currentTarget.position;
                
                // Calculate target velocity with higher update rate
                targetVelocity = (targetPosition - lastTargetPosition) / (updateRate * 0.5f);
                lastTargetPosition = targetPosition;
            }
            
            // Apply guidance with increased gain
            float originalGain = navigationGain;
            navigationGain *= 1.5f;
            ApplyGuidanceLogic();
            navigationGain = originalGain;
        }

        /// <summary>
        /// Apply guidance logic based on the selected guidance type
        /// </summary>
        private void ApplyGuidanceLogic()
        {
            if (!isGuiding) return;
            
            Vector3 interceptPoint = targetPosition;
            
            // Predict intercept point if enabled
            if (usePredictor)
            {
                interceptPoint = PredictInterceptPoint();
            }
            
            switch (guidanceType)
            {
                case GuidanceType.DirectPursuit:
                    // Direct pursuit - aim directly at target
                    desiredDirection = (interceptPoint - transform.position).normalized;
                    break;
                    
                case GuidanceType.LeadPursuit:
                    // Lead pursuit - aim ahead of target based on velocities
                    Vector3 relativePosition = interceptPoint - transform.position;
                    Vector3 leadOffset = targetVelocity * leadFactor;
                    desiredDirection = (relativePosition + leadOffset).normalized;
                    break;
                    
                case GuidanceType.ProportionalNavigation:
                    // Proportional Navigation
                    ApplyProportionalNavigation(interceptPoint);
                    break;
                    
                case GuidanceType.PureProportionalNavigation:
                    // Pure Proportional Navigation (no gravity compensation)
                    ApplyPureProportionalNavigation(interceptPoint);
                    break;
            }
        }

        /// <summary>
        /// Apply proportional navigation guidance
        /// </summary>
        /// <param name="interceptPoint">The predicted intercept point</param>
        private void ApplyProportionalNavigation(Vector3 interceptPoint)
        {
            Vector3 missileVelocity = missilePhysics.GetCurrentSpeed() * transform.forward;
            Vector3 lineOfSight = (interceptPoint - transform.position).normalized;
            Vector3 relativeVelocity = missileVelocity - targetVelocity;
            
            // Calculate closing velocity
            float closingSpeed = Vector3.Dot(relativeVelocity, lineOfSight);
            
            // Calculate line of sight rate
            Vector3 losRate = Vector3.Cross(lineOfSight, 
                Vector3.Cross(lineOfSight, targetVelocity - missileVelocity) / 
                Vector3.Distance(transform.position, interceptPoint));
            
            // Calculate acceleration command perpendicular to line of sight
            Vector3 acceleration = navigationGain * closingSpeed * Vector3.Cross(lineOfSight, losRate);
            
            // Add gravity compensation
            acceleration += Vector3.up * 9.81f;
            
            // Apply guidance command
            guidanceCommand = acceleration;
            
            // Convert to desired direction
            desiredDirection = (missileVelocity + guidanceCommand * Time.deltaTime).normalized;
        }

        /// <summary>
        /// Apply pure proportional navigation guidance (no gravity compensation)
        /// </summary>
        /// <param name="interceptPoint">The predicted intercept point</param>
        private void ApplyPureProportionalNavigation(Vector3 interceptPoint)
        {
            Vector3 missileVelocity = missilePhysics.GetCurrentSpeed() * transform.forward;
            Vector3 lineOfSight = (interceptPoint - transform.position).normalized;
            
            // Calculate closing velocity
            float closingSpeed = Vector3.Dot(missileVelocity, lineOfSight);
            
            // Calculate line of sight rate
            Vector3 previousLOS = (lastTargetPosition - transform.position).normalized;
            Vector3 currentLOS = lineOfSight;
            Vector3 losRate = Vector3.Cross(previousLOS, currentLOS) / Time.deltaTime;
            
            // Calculate acceleration command perpendicular to velocity
            Vector3 acceleration = navigationGain * closingSpeed * Vector3.Cross(missileVelocity.normalized, losRate);
            
            // Apply guidance command
            guidanceCommand = acceleration;
            
            // Convert to desired direction
            desiredDirection = (missileVelocity + guidanceCommand * Time.deltaTime).normalized;
        }

        /// <summary>
        /// Predict the intercept point based on missile and target velocities
        /// </summary>
        /// <returns>The predicted intercept point</returns>
        private Vector3 PredictInterceptPoint()
        {
            Vector3 targetPos = targetPosition;
            Vector3 targetVel = targetVelocity;
            Vector3 missilePos = transform.position;
            float missileSpeed = missilePhysics.GetCurrentSpeed();
            
            // Simple iterative solution
            Vector3 interceptPoint = targetPos;
            float interceptTime = 0;
            
            for (int i = 0; i < 3; i++) // 3 iterations is usually enough
            {
                float distance = Vector3.Distance(missilePos, interceptPoint);
                interceptTime = distance / missileSpeed;
                interceptPoint = targetPos + targetVel * interceptTime;
            }
            
            return interceptPoint;
        }
        #endregion

        /// <summary>
        /// Guidance types
        /// </summary>
        public enum GuidanceType
        {
            DirectPursuit,
            LeadPursuit,
            ProportionalNavigation,
            PureProportionalNavigation
        }

        /// <summary>
        /// Guidance phases
        /// </summary>
        public enum GuidancePhase
        {
            Initial,
            MidCourse,
            Terminal
        }
    }
}
