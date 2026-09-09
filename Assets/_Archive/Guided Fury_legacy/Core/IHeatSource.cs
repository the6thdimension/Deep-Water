using UnityEngine;

namespace GuidedFury.Core
{
    /// <summary>
    /// Interface for objects that emit heat and can be detected by IR sensors.
    /// </summary>
    public interface IHeatSource
    {
        /// <summary>
        /// Get the heat signature of the object, normalized between 0 and 1.
        /// 0 = cold, 1 = very hot
        /// </summary>
        /// <returns>Normalized heat signature value</returns>
        float GetHeatSignature();
    }

    /// <summary>
    /// Extended interface for objects that emit heat and can be detected by IR sensors.
    /// Extends the basic IHeatSource interface with additional functionality.
    /// </summary>
    public interface IAdvancedHeatSource : IHeatSource
    {
        /// <summary>
        /// Get the position of the heat source
        /// </summary>
        /// <returns>World position of the heat source</returns>
        Vector3 GetHeatSourcePosition();
        
        /// <summary>
        /// Get the size of the heat source
        /// </summary>
        /// <returns>Size of the heat source in meters</returns>
        float GetHeatSourceSize();
    }
}
