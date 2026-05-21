using UnityEngine;

namespace GuidedFury.Core
{
    /// <summary>
    /// Base interface for all missile modules
    /// </summary>
    public interface IMissileModule
    {
        /// <summary>
        /// Initialize the module with the parent missile
        /// </summary>
        /// <param name="missile">The parent missile instance</param>
        void Initialize(MissileBase missile);

        /// <summary>
        /// Update the module logic
        /// </summary>
        void UpdateModule();

        /// <summary>
        /// Enable or disable the module
        /// </summary>
        /// <param name="enabled">Whether the module should be enabled</param>
        void SetEnabled(bool enabled);

        /// <summary>
        /// Check if the module is currently enabled
        /// </summary>
        /// <returns>True if the module is enabled, false otherwise</returns>
        bool IsEnabled();

        /// <summary>
        /// Get the module name for display purposes
        /// </summary>
        /// <returns>The module name</returns>
        string GetModuleName();
    }
}
