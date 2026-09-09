using UnityEngine;
using System.Collections.Generic;
using GuidedFury.Core;

namespace GuidedFury.ScriptableObjects
{
    /// <summary>
    /// ScriptableObject for storing missile configurations.
    /// </summary>
    [CreateAssetMenu(fileName = "New Missile Config", menuName = "Guided Fury/Missile Configuration")]
    public class MissileConfigSO : ScriptableObject
    {
        [Header("Missile Identification")]
        [SerializeField] private string missileId = "default";
        [SerializeField] private string missileName = "Standard Missile";
        [SerializeField] private string missileDescription = "Standard guided missile with moderate capabilities.";
        [SerializeField] private Sprite missileIcon;
        
        [Header("Base Configuration")]
        [SerializeField] private MissileBase missilePrefab;
        [SerializeField] private MissileSensor.SensorType sensorType = MissileSensor.SensorType.Infrared;
        [SerializeField] private MissileGuidance.GuidanceType guidanceType = MissileGuidance.GuidanceType.ProportionalNavigation;
        
        [Header("Performance")]
        [SerializeField] private float maxSpeed = 300f;
        [SerializeField] private float maxAcceleration = 10f;
        [SerializeField] private float maxTurnRate = 45f;
        [SerializeField] private float lifetime = 30f;
        [SerializeField] private float fuzeRadius = 5f;
        [SerializeField] private float damageAmount = 100f;
        
        [Header("Sensor Settings")]
        [SerializeField] private float detectionRange = 5000f;
        [SerializeField] private float trackingRange = 3000f;
        [SerializeField] private float detectionAngle = 45f;
        [SerializeField] private float trackingAngle = 30f;
        
        [Header("Physics Settings")]
        [SerializeField] private float dragCoefficient = 0.1f;
        [SerializeField] private float liftCoefficient = 0.3f;
        [SerializeField] private float thrustForce = 1000f;
        [SerializeField] private float initialBoostMultiplier = 3f;
        
        [Header("Advanced Modules")]
        [SerializeField] private List<ModuleConfig> advancedModules = new List<ModuleConfig>();
        
        /// <summary>
        /// Apply this configuration to a missile instance
        /// </summary>
        /// <param name="missile">The missile to configure</param>
        public void ApplyToMissile(MissileBase missile)
        {
            if (missile == null) return;
            
            // Configure base properties via reflection to avoid requiring direct property access
            ApplyBaseProperties(missile);
            
            // Configure sensor
            MissileSensor sensor = missile.GetSensor();
            if (sensor != null)
            {
                ApplySensorProperties(sensor);
            }
            
            // Configure guidance
            MissileGuidance guidance = missile.GetGuidance();
            if (guidance != null)
            {
                ApplyGuidanceProperties(guidance);
            }
            
            // Configure physics
            MissilePhysics physics = missile.GetPhysics();
            if (physics != null)
            {
                ApplyPhysicsProperties(physics);
            }
            
            // Configure advanced modules
            ApplyAdvancedModules(missile);
        }
        
        /// <summary>
        /// Get the missile ID
        /// </summary>
        /// <returns>The missile ID</returns>
        public string GetMissileId()
        {
            return missileId;
        }
        
        /// <summary>
        /// Get the missile prefab
        /// </summary>
        /// <returns>The missile prefab</returns>
        public MissileBase GetMissilePrefab()
        {
            return missilePrefab;
        }
        
        /// <summary>
        /// Get the missile name
        /// </summary>
        /// <returns>The missile name</returns>
        public string GetMissileName()
        {
            return missileName;
        }
        
        /// <summary>
        /// Get the missile description
        /// </summary>
        /// <returns>The missile description</returns>
        public string GetMissileDescription()
        {
            return missileDescription;
        }
        
        /// <summary>
        /// Get the missile icon
        /// </summary>
        /// <returns>The missile icon</returns>
        public Sprite GetMissileIcon()
        {
            return missileIcon;
        }
        
        /// <summary>
        /// Apply base properties to the missile
        /// </summary>
        /// <param name="missile">The missile to configure</param>
        private void ApplyBaseProperties(MissileBase missile)
        {
            // Use reflection to set properties
            var missileType = missile.GetType();
            
            // Try to set fields via reflection
            SetFieldValue(missile, "missileName", missileName);
            SetFieldValue(missile, "maxSpeed", maxSpeed);
            SetFieldValue(missile, "maxAcceleration", maxAcceleration);
            SetFieldValue(missile, "maxTurnRate", maxTurnRate);
            SetFieldValue(missile, "lifetime", lifetime);
            SetFieldValue(missile, "fuzeRadius", fuzeRadius);
            SetFieldValue(missile, "damageAmount", damageAmount);
        }
        
        /// <summary>
        /// Apply sensor properties to the missile sensor
        /// </summary>
        /// <param name="sensor">The missile sensor to configure</param>
        private void ApplySensorProperties(MissileSensor sensor)
        {
            SetFieldValue(sensor, "sensorType", sensorType);
            SetFieldValue(sensor, "detectionRange", detectionRange);
            SetFieldValue(sensor, "trackingRange", trackingRange);
            SetFieldValue(sensor, "detectionAngle", detectionAngle);
            SetFieldValue(sensor, "trackingAngle", trackingAngle);
        }
        
        /// <summary>
        /// Apply guidance properties to the missile guidance
        /// </summary>
        /// <param name="guidance">The missile guidance to configure</param>
        private void ApplyGuidanceProperties(MissileGuidance guidance)
        {
            SetFieldValue(guidance, "guidanceType", guidanceType);
        }
        
        /// <summary>
        /// Apply physics properties to the missile physics
        /// </summary>
        /// <param name="physics">The missile physics to configure</param>
        private void ApplyPhysicsProperties(MissilePhysics physics)
        {
            SetFieldValue(physics, "dragCoefficient", dragCoefficient);
            SetFieldValue(physics, "liftCoefficient", liftCoefficient);
            SetFieldValue(physics, "thrustForce", thrustForce);
            SetFieldValue(physics, "initialBoostMultiplier", initialBoostMultiplier);
        }
        
        /// <summary>
        /// Apply advanced modules to the missile
        /// </summary>
        /// <param name="missile">The missile to configure</param>
        private void ApplyAdvancedModules(MissileBase missile)
        {
            foreach (var moduleConfig in advancedModules)
            {
                if (!moduleConfig.enabled || string.IsNullOrEmpty(moduleConfig.moduleTypeName))
                    continue;
                
                // Check if the module is already attached
                var existingModules = missile.GetComponents<MonoBehaviour>();
                bool moduleExists = false;
                
                foreach (var existingModule in existingModules)
                {
                    if (existingModule.GetType().FullName == moduleConfig.moduleTypeName)
                    {
                        moduleExists = true;
                        
                        // Enable/disable the module
                        if (existingModule is IMissileModule missileModule)
                        {
                            missileModule.SetEnabled(moduleConfig.enabled);
                        }
                        
                        break;
                    }
                }
                
                // If module doesn't exist and should be added at runtime
                if (!moduleExists && moduleConfig.addAtRuntime)
                {
                    // Try to add the module
                    System.Type moduleType = System.Type.GetType(moduleConfig.moduleTypeName);
                    if (moduleType != null)
                    {
                        var addedModule = missile.gameObject.AddComponent(moduleType) as MonoBehaviour;
                        
                        if (addedModule is IMissileModule missileModule)
                        {
                            missileModule.Initialize(missile);
                            missileModule.SetEnabled(moduleConfig.enabled);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Set a field value using reflection
        /// </summary>
        /// <param name="target">The target object</param>
        /// <param name="fieldName">The field name</param>
        /// <param name="value">The value to set</param>
        private void SetFieldValue(object target, string fieldName, object value)
        {
            if (target == null || string.IsNullOrEmpty(fieldName)) return;
            
            var targetType = target.GetType();
            var field = targetType.GetField(fieldName, 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.NonPublic);
                
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
        
        /// <summary>
        /// Configuration for an advanced module
        /// </summary>
        [System.Serializable]
        public class ModuleConfig
        {
            public string moduleName;
            public string moduleTypeName;
            public bool enabled = true;
            public bool addAtRuntime = false;
        }
    }
}
