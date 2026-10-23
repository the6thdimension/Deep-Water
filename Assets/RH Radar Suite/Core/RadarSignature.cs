using UnityEngine;

namespace RHRadarSuite
{
    /// <summary>
    /// Defines the radar signature properties of an object
    /// Attach this to any GameObject that should be detectable by radar
    /// </summary>
    [AddComponentMenu("RH Radar Suite/Radar Signature")]
    public class RadarSignature : MonoBehaviour
    {
        [Header("Basic Properties")]
        [Tooltip("Physical size of the object (affects detection probability)")]
        [Range(0.1f, 100f)]
        public float Size = 1f;
        
        [Tooltip("Radar cross-section (RCS) multiplier")]
        [Range(0.01f, 10f)]
        public float CrossSection = 1f;
        
        [Tooltip("Material type (affects radar reflection characteristics)")]
        public RadarMaterialType MaterialType = RadarMaterialType.Metal;
        
        [Header("Stealth Properties")]
        [Tooltip("Stealth coating effectiveness (reduces radar signature)")]
        [Range(0f, 1f)]
        public float StealthCoating = 0f;
        
        [Tooltip("Shape complexity (affects radar signature)")]
        [Range(0f, 1f)]
        public float ShapeComplexity = 0.5f;
        
        [Header("Classification")]
        [Tooltip("Type of contact for classification purposes")]
        public ContactClassification ContactType = ContactClassification.Unknown;
        
        [Tooltip("IFF (Identification Friend or Foe) status")]
        public IFFStatus IFF = IFFStatus.Unknown;
        
        [Header("ECM/ECCM")]
        [Tooltip("Is this object actively jamming?")]
        public bool IsJamming = false;
        
        [Tooltip("Jamming effectiveness (0-1)")]
        [Range(0f, 1f)]
        public float JammingStrength = 0f;
        
        [Tooltip("Jamming type")]
        public JammingType JammingType = JammingType.None;
        
        /// <summary>
        /// Calculate the effective radar cross-section based on all factors
        /// </summary>
        /// <returns>The effective radar cross-section</returns>
        public float GetEffectiveRCS()
        {
            // Base RCS is size * cross-section
            float baseRCS = Size * CrossSection;
            
            // Apply material modifier
            float materialModifier = GetMaterialModifier();
            
            // Apply stealth factors
            float stealthFactor = 1f - StealthCoating;
            
            // Apply shape complexity (complex shapes are easier to detect)
            float shapeFactor = 0.5f + (ShapeComplexity * 0.5f);
            
            // Calculate final RCS
            return baseRCS * materialModifier * stealthFactor * shapeFactor;
        }
        
        /// <summary>
        /// Get the material modifier for radar reflection
        /// </summary>
        private float GetMaterialModifier()
        {
            switch (MaterialType)
            {
                case RadarMaterialType.Metal:
                    return 1.0f;
                case RadarMaterialType.Composite:
                    return 0.6f;
                case RadarMaterialType.Wood:
                    return 0.3f;
                case RadarMaterialType.Plastic:
                    return 0.2f;
                case RadarMaterialType.StealthMaterial:
                    return 0.1f;
                default:
                    return 1.0f;
            }
        }
        
        /// <summary>
        /// Calculate jamming effectiveness against a specific radar
        /// </summary>
        /// <param name="radarPower">Power of the radar trying to detect this object</param>
        /// <returns>Jamming effectiveness (0-1)</returns>
        public float GetJammingEffectiveness(float radarPower)
        {
            if (!IsJamming || JammingStrength <= 0f || JammingType == JammingType.None)
                return 0f;
                
            // Calculate base jamming effectiveness
            float effectiveness = JammingStrength;
            
            // Apply radar power factor (more powerful radars are less affected)
            effectiveness /= Mathf.Max(0.1f, radarPower);
            
            // Apply jamming type modifier
            switch (JammingType)
            {
                case JammingType.Noise:
                    // Noise jamming is generally less effective but broad spectrum
                    effectiveness *= 0.8f;
                    break;
                case JammingType.Deception:
                    // Deception jamming can be more effective but more specialized
                    effectiveness *= 1.2f;
                    break;
                case JammingType.Chaff:
                    // Chaff is very effective but temporary
                    effectiveness *= 1.5f;
                    break;
            }
            
            return Mathf.Clamp01(effectiveness);
        }
    }
    
    /// <summary>
    /// Types of materials for radar reflection purposes
    /// </summary>
    public enum RadarMaterialType
    {
        Metal,
        Composite,
        Wood,
        Plastic,
        StealthMaterial
    }
    
    /// <summary>
    /// IFF (Identification Friend or Foe) status
    /// </summary>
    public enum IFFStatus
    {
        Unknown,
        Friendly,
        Hostile,
        Neutral,
        Civilian
    }
    
    /// <summary>
    /// Types of radar jamming
    /// </summary>
    public enum JammingType
    {
        None,
        Noise,      // Broadband noise jamming
        Deception,  // False target generation
        Chaff       // Reflective material clouds
    }
}
