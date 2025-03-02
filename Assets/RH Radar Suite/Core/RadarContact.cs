using System;
using UnityEngine;

namespace RHRadarSuite
{
    /// <summary>
    /// Represents a contact detected by the radar system
    /// </summary>
    [Serializable]
    public class RadarContact
    {
        // Target information
        public GameObject Target { get; private set; }
        public int TargetID { get; private set; }
        public string TargetName { get; private set; }
        
        // Position and movement
        public Vector3 Position { get; private set; }
        public Vector3 LastPosition { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float Speed { get; private set; }
        public Vector3 Heading { get; private set; }
        
        // Radar-specific data
        public float Range { get; private set; }
        public float Azimuth { get; private set; }
        public float Elevation { get; private set; }
        public float SignalStrength { get; private set; }
        public float RadialVelocity { get; private set; }
        
        // Classification
        public ContactClassification Classification { get; private set; }
        public float ClassificationConfidence { get; private set; }
        
        // Status
        public bool IsActive { get; private set; }
        public float LastUpdateTime { get; private set; }
        public float FirstDetectionTime { get; private set; }
        public float TrackingDuration => Time.time - FirstDetectionTime;
        
        // Advanced data (available in higher LODs)
        public Vector3 Acceleration { get; private set; }
        public float Size { get; private set; }
        public float CrossSection { get; private set; }
        public bool IsJamming { get; private set; }
        public float JammingStrength { get; private set; }
        
        // LOD-specific data
        public RadarLOD DetectionLOD { get; private set; }
        public RadarSignature Signature { get; private set; }
        
        /// <summary>
        /// Create a new radar contact
        /// </summary>
        /// <param name="target">The GameObject that was detected</param>
        public RadarContact(GameObject target)
        {
            Target = target;
            TargetID = target.GetInstanceID();
            TargetName = target.name;
            FirstDetectionTime = Time.time;
            LastUpdateTime = Time.time;
            IsActive = true;
            Classification = ContactClassification.Unknown;
            ClassificationConfidence = 0f;
            
            // Try to get radar signature if available
            Signature = target.GetComponent<RadarSignature>();
            
            // Initialize position
            Position = target.transform.position;
            LastPosition = Position;
            
            // Default values
            SignalStrength = 1f;
            Size = 1f;
            CrossSection = 1f;
            
            if (Signature != null)
            {
                Size = Signature.Size;
                CrossSection = Signature.CrossSection;
            }
        }
        
        /// <summary>
        /// Update the contact with new information
        /// </summary>
        /// <param name="position">Current position</param>
        /// <param name="signalStrength">Signal strength (0-1)</param>
        /// <param name="detectionLOD">LOD level that detected this contact</param>
        public void Update(Vector3 position, float signalStrength, RadarLOD detectionLOD)
        {
            // Calculate velocity if we have previous position data
            if (LastUpdateTime < Time.time - 0.001f)
            {
                Vector3 displacement = position - Position;
                float deltaTime = Time.time - LastUpdateTime;
                Vector3 newVelocity = displacement / deltaTime;
                
                // Calculate acceleration
                Acceleration = (newVelocity - Velocity) / deltaTime;
                
                // Update velocity
                Velocity = newVelocity;
                Speed = Velocity.magnitude;
                
                if (Speed > 0.01f)
                {
                    Heading = Velocity.normalized;
                }
            }
            
            // Update position
            LastPosition = Position;
            Position = position;
            
            // Update radar-specific data
            SignalStrength = signalStrength;
            DetectionLOD = detectionLOD;
            
            // Calculate range, azimuth, and elevation from radar
            Vector3 radarPosition = Camera.main ? Camera.main.transform.position : Vector3.zero;
            Vector3 relativePosition = Position - radarPosition;
            Range = relativePosition.magnitude;
            
            // Calculate azimuth (horizontal angle)
            Azimuth = Mathf.Atan2(relativePosition.x, relativePosition.z) * Mathf.Rad2Deg;
            if (Azimuth < 0) Azimuth += 360f;
            
            // Calculate elevation (vertical angle)
            float horizontalDistance = new Vector2(relativePosition.x, relativePosition.z).magnitude;
            Elevation = Mathf.Atan2(relativePosition.y, horizontalDistance) * Mathf.Rad2Deg;
            
            // Calculate radial velocity (velocity component along the line of sight)
            Vector3 lineOfSight = relativePosition.normalized;
            RadialVelocity = Vector3.Dot(Velocity, lineOfSight);
            
            // Update timestamp
            LastUpdateTime = Time.time;
            IsActive = true;
        }
        
        /// <summary>
        /// Set the contact as inactive
        /// </summary>
        public void SetInactive()
        {
            IsActive = false;
        }
        
        /// <summary>
        /// Update the classification of this contact
        /// </summary>
        /// <param name="classification">New classification</param>
        /// <param name="confidence">Confidence level (0-1)</param>
        public void UpdateClassification(ContactClassification classification, float confidence)
        {
            Classification = classification;
            ClassificationConfidence = Mathf.Clamp01(confidence);
        }
        
        /// <summary>
        /// Update jamming information
        /// </summary>
        /// <param name="isJamming">Whether the contact is jamming</param>
        /// <param name="jammingStrength">Jamming strength (0-1)</param>
        public void UpdateJammingInfo(bool isJamming, float jammingStrength)
        {
            IsJamming = isJamming;
            JammingStrength = Mathf.Clamp01(jammingStrength);
        }
        
        /// <summary>
        /// Sets the velocity of the contact
        /// </summary>
        /// <param name="velocity">The velocity vector</param>
        public void SetVelocity(Vector3 velocity)
        {
            Velocity = velocity;
            Speed = velocity.magnitude;
            
            // Calculate radial velocity (component of velocity toward/away from radar)
            if (Position != LastPosition)
            {
                Vector3 directionToRadar = (LastPosition - Position).normalized;
                RadialVelocity = Vector3.Dot(velocity, directionToRadar);
            }
            else
            {
                RadialVelocity = 0f;
            }
        }
    }
    
    /// <summary>
    /// Classification types for radar contacts
    /// </summary>
    public enum ContactClassification
    {
        Unknown,
        Friendly,
        Hostile,
        Neutral,
        Surface,
        Air,
        Subsurface,
        Ground,
        Missile,
        Decoy,
        Chaff
    }
}
