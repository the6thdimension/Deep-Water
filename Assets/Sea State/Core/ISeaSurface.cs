using Unity.Collections;
using Unity.Mathematics;

namespace DeepWater.Sea.Core
{
    /// <summary>
    /// What every consumer of the sea — buoyancy, ship motion, sensors, AI, effects — asks of
    /// it. Queries answer for the current fixed step, synchronously, from the simulation's
    /// own wave field; never from the renderer (render-clocked state would make physics
    /// frame-rate dependent).
    /// </summary>
    public interface ISeaSurface
    {
        /// <summary>The sea as sensors and UI see it.</summary>
        SeaConditions Conditions { get; }

        /// <summary>Simulation time of the current fixed step, seconds.</summary>
        double SimulationTimeS { get; }

        /// <summary>
        /// Batch query: surface height, normal, particle velocity and Jacobian at each world
        /// position, for the current fixed step. Completes before returning.
        /// </summary>
        void Query(NativeArray<float3> worldPositions, NativeArray<SeaSample> results);

        /// <summary>Single-point convenience query. Prefer <see cref="Query"/> for more than a few points.</summary>
        SeaSample Sample(float3 worldPosition);
    }
}
