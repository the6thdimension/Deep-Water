using UnityEngine;
using GuidedFury.Core;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Simple implementation of the IAdvancedHeatSource interface for testing IR sensors.
    /// </summary>
    public class SimpleHeatSource : MonoBehaviour, IAdvancedHeatSource
    {
        [Header("Heat Source Settings")]
        [SerializeField, Range(0f, 1f)] private float heatSignature = 0.7f;
        [SerializeField] private float heatSourceSize = 2f;
        [SerializeField] private Transform heatSourcePoint;
        [SerializeField] private bool fluctuateHeat = false;
        [SerializeField] private float fluctuationSpeed = 1f;
        [SerializeField] private float fluctuationAmount = 0.2f;
        
        [Header("Visualization")]
        [SerializeField] private bool showDebugVisuals = true;
        [SerializeField] private Color coldColor = Color.blue;
        [SerializeField] private Color hotColor = Color.red;
        
        private float initialHeatSignature;
        private float time;
        
        private void Start()
        {
            initialHeatSignature = heatSignature;
        }
        
        private void Update()
        {
            // Update heat fluctuation if enabled
            if (fluctuateHeat)
            {
                time += Time.deltaTime * fluctuationSpeed;
                heatSignature = initialHeatSignature + Mathf.Sin(time) * fluctuationAmount;
                heatSignature = Mathf.Clamp01(heatSignature);
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebugVisuals) return;
            
            // Get heat source position
            Vector3 position = GetHeatSourcePosition();
            
            // Calculate color based on heat signature
            Color heatColor = Color.Lerp(coldColor, hotColor, heatSignature);
            Gizmos.color = heatColor;
            
            // Draw heat source sphere
            Gizmos.DrawWireSphere(position, heatSourceSize * 0.5f);
            
            // Draw heat waves
            float waveCount = Mathf.Lerp(1, 3, heatSignature);
            for (int i = 0; i < waveCount; i++)
            {
                float waveSize = heatSourceSize * (1f + i * 0.5f);
                float alpha = 1f - (i / waveCount);
                
                Gizmos.color = new Color(heatColor.r, heatColor.g, heatColor.b, alpha * 0.5f);
                Gizmos.DrawWireSphere(position, waveSize);
            }
        }
        
        #region IAdvancedHeatSource Implementation
        /// <summary>
        /// Get the heat signature of the object
        /// </summary>
        /// <returns>Normalized heat signature value</returns>
        public float GetHeatSignature()
        {
            return heatSignature;
        }
        
        /// <summary>
        /// Get the position of the heat source
        /// </summary>
        /// <returns>World position of the heat source</returns>
        public Vector3 GetHeatSourcePosition()
        {
            return heatSourcePoint != null ? heatSourcePoint.position : transform.position;
        }
        
        /// <summary>
        /// Get the size of the heat source
        /// </summary>
        /// <returns>Size of the heat source in meters</returns>
        public float GetHeatSourceSize()
        {
            return heatSourceSize;
        }
        #endregion
        
        /// <summary>
        /// Set the heat signature value
        /// </summary>
        /// <param name="value">New heat signature value (0-1)</param>
        public void SetHeatSignature(float value)
        {
            heatSignature = Mathf.Clamp01(value);
            initialHeatSignature = heatSignature;
        }
        
        /// <summary>
        /// Enable or disable heat fluctuation
        /// </summary>
        /// <param name="enabled">Whether fluctuation should be enabled</param>
        public void SetFluctuationEnabled(bool enabled)
        {
            fluctuateHeat = enabled;
        }
    }
}
