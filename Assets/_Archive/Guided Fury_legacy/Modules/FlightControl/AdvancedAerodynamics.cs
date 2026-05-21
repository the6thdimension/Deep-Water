using UnityEngine;
using GuidedFury.Core;

namespace GuidedFury.Modules.FlightControl
{
    /// <summary>
    /// Advanced aerodynamics module that enhances missile flight physics with high-fidelity forces.
    /// </summary>
    [RequireComponent(typeof(MissileBase))]
    public class AdvancedAerodynamics : MonoBehaviour, IMissileModule
    {
        #region Inspector Properties
        [Header("Aerodynamic Configuration")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float referenceArea = 0.05f; // m²
        [SerializeField] private float finArea = 0.02f; // m²
        [SerializeField] private float missileLength = 3.5f; // m
        [SerializeField] private float missileDiameter = 0.25f; // m
        
        [Header("Aerodynamic Coefficients")]
        [SerializeField] private AnimationCurve dragCoefficientCurve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.8f, 0.3f),
            new Keyframe(1.2f, 0.5f),
            new Keyframe(2f, 0.7f),
            new Keyframe(3f, 0.9f)
        );
        [SerializeField] private AnimationCurve liftCoefficientCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(15f, 0.2f),
            new Keyframe(30f, 0.4f),
            new Keyframe(45f, 0.3f),
            new Keyframe(90f, 0f)
        );
        [SerializeField] private AnimationCurve momentCoefficientCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(15f, -0.05f),
            new Keyframe(30f, -0.1f),
            new Keyframe(45f, -0.05f),
            new Keyframe(90f, 0f)
        );
        
        [Header("Environmental Effects")]
        [SerializeField] private bool useAtmosphericModel = true;
        [SerializeField] private bool useWindEffects = true;
        [SerializeField] private Vector3 windDirection = new Vector3(1, 0, 0);
        [SerializeField] private float windStrength = 5f; // m/s
        [SerializeField] private float windGustFrequency = 0.1f;
        [SerializeField] private float windGustMagnitude = 2f;
        #endregion

        #region Runtime Properties
        private MissileBase missileBase;
        private MissilePhysics missilePhysics;
        private Rigidbody rb;
        private bool isInitialized = false;
        private float currentMach = 0f;
        private float airDensity = 1.225f; // kg/m³ at sea level
        private Vector3 currentWindVector = Vector3.zero;
        private float timeSinceLastGust = 0f;
        #endregion

        #region Unity Lifecycle
        private void Update()
        {
            if (isInitialized && enabled && useWindEffects)
            {
                UpdateWindEffects();
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
            missilePhysics = missile.GetPhysics();
            rb = GetComponent<Rigidbody>();
            
            if (missilePhysics != null && rb != null)
            {
                isInitialized = true;
                
                // Register for events
                missileBase.OnMissileLaunched += OnMissileLaunched;
                missileBase.OnMissileDestroyed += OnMissileDestroyed;
            }
            else
            {
                Debug.LogWarning("AdvancedAerodynamics requires a missile with physics and Rigidbody. Module will be disabled.");
                enabled = false;
            }
        }

        /// <summary>
        /// Update the module logic
        /// </summary>
        public void UpdateModule()
        {
            if (!isInitialized || !enabled) return;
            
            // Calculate current Mach number based on speed and altitude
            UpdateMachNumber();
            
            // Calculate and apply aerodynamic forces
            ApplyAerodynamicForces();
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
            return "Advanced Aerodynamics";
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Handle missile launch
        /// </summary>
        private void OnMissileLaunched()
        {
            // Initialize values
            currentMach = 0f;
            currentWindVector = windDirection.normalized * windStrength;
            timeSinceLastGust = 0f;
        }

        /// <summary>
        /// Handle missile destruction
        /// </summary>
        private void OnMissileDestroyed()
        {
            // Clean up if needed
        }

        /// <summary>
        /// Update the current Mach number based on speed and altitude
        /// </summary>
        private void UpdateMachNumber()
        {
            // Get current speed
            float speed = missilePhysics.GetCurrentSpeed();
            
            // Get current altitude
            float altitude = transform.position.y;
            
            // Calculate speed of sound based on altitude (simplified model)
            float speedOfSound = CalculateSpeedOfSound(altitude);
            
            // Calculate Mach number
            currentMach = speed / speedOfSound;
            
            // Update air density if using atmospheric model
            if (useAtmosphericModel)
            {
                airDensity = CalculateAirDensity(altitude);
            }
        }

        /// <summary>
        /// Calculate the speed of sound based on altitude
        /// </summary>
        /// <param name="altitude">The current altitude in meters</param>
        /// <returns>The speed of sound in m/s</returns>
        private float CalculateSpeedOfSound(float altitude)
        {
            // Simplified model: speed of sound decreases with altitude
            // At sea level: ~340 m/s
            // Decreases by ~4 m/s per 1000m up to 11km
            
            if (altitude < 11000f)
            {
                return 340f - (altitude / 1000f) * 4f;
            }
            else
            {
                return 295f; // Constant in the stratosphere
            }
        }

        /// <summary>
        /// Calculate air density based on altitude
        /// </summary>
        /// <param name="altitude">The current altitude in meters</param>
        /// <returns>The air density in kg/m³</returns>
        private float CalculateAirDensity(float altitude)
        {
            // Simplified model: density decreases exponentially with altitude
            // Sea level: 1.225 kg/m³
            
            float seaLevelDensity = 1.225f;
            float scaleHeight = 8500f; // Scale height in meters
            
            return seaLevelDensity * Mathf.Exp(-altitude / scaleHeight);
        }

        /// <summary>
        /// Update wind effects including gusts
        /// </summary>
        private void UpdateWindEffects()
        {
            // Update gust timer
            timeSinceLastGust += Time.deltaTime;
            
            // Check if it's time for a new gust
            if (timeSinceLastGust > 1f / windGustFrequency)
            {
                // Apply random gust
                Vector3 gustVector = Random.insideUnitSphere * windGustMagnitude;
                gustVector.y *= 0.5f; // Reduce vertical component
                
                // Add gust to base wind
                currentWindVector = windDirection.normalized * windStrength + gustVector;
                
                // Reset timer
                timeSinceLastGust = 0f;
            }
            else
            {
                // Gradually return to base wind
                currentWindVector = Vector3.Lerp(currentWindVector, 
                    windDirection.normalized * windStrength, 
                    Time.deltaTime * windGustFrequency);
            }
        }

        /// <summary>
        /// Calculate and apply aerodynamic forces to the missile
        /// </summary>
        private void ApplyAerodynamicForces()
        {
            // Get missile velocity relative to air
            Vector3 velocity = rb.linearVelocity;
            
            // Apply wind if enabled
            if (useWindEffects)
            {
                velocity -= currentWindVector;
            }
            
            // Skip if velocity is too low
            if (velocity.magnitude < 1f) return;
            
            // Calculate angle of attack
            float angleOfAttack = Vector3.Angle(transform.forward, velocity);
            
            // Calculate dynamic pressure
            float dynamicPressure = 0.5f * airDensity * velocity.sqrMagnitude;
            
            // Get aerodynamic coefficients
            float dragCoefficient = dragCoefficientCurve.Evaluate(currentMach);
            float liftCoefficient = liftCoefficientCurve.Evaluate(angleOfAttack);
            float momentCoefficient = momentCoefficientCurve.Evaluate(angleOfAttack);
            
            // Calculate reference areas
            float bodyReferenceArea = Mathf.PI * missileDiameter * missileDiameter / 4f;
            float totalReferenceArea = bodyReferenceArea + finArea;
            
            // Calculate force directions
            Vector3 dragDirection = -velocity.normalized;
            Vector3 liftDirection = Vector3.Cross(velocity.normalized, 
                Vector3.Cross(transform.forward, velocity.normalized)).normalized;
            
            // Calculate forces
            Vector3 dragForce = dragDirection * dragCoefficient * dynamicPressure * totalReferenceArea;
            Vector3 liftForce = liftDirection * liftCoefficient * dynamicPressure * totalReferenceArea;
            
            // Apply forces
            rb.AddForce(dragForce, ForceMode.Force);
            rb.AddForce(liftForce, ForceMode.Force);
            
            // Calculate and apply moment (torque)
            if (angleOfAttack > 1f)
            {
                Vector3 momentAxis = Vector3.Cross(transform.forward, velocity.normalized);
                float momentMagnitude = momentCoefficient * dynamicPressure * totalReferenceArea * missileLength;
                Vector3 moment = momentAxis.normalized * momentMagnitude;
                
                rb.AddTorque(moment, ForceMode.Force);
            }
            
            // Apply additional stability at high speeds
            if (currentMach > 1.5f)
            {
                // Add stabilizing torque
                Vector3 stabilizingTorque = -rb.angularVelocity * dynamicPressure * 0.01f;
                rb.AddTorque(stabilizingTorque, ForceMode.Force);
            }
        }
        #endregion
    }
}
