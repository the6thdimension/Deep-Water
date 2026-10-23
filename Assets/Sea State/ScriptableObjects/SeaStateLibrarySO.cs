using UnityEngine;

namespace DeepWater.Sea.ScriptableObjects
{
    /// <summary>
    /// The ordered Douglas scales plus the settings every sea state shares: how fine the wave
    /// field is, how queries invert it, how fast states change, and seawater properties.
    /// </summary>
    [CreateAssetMenu(fileName = "SeaStateLibrary", menuName = "Deep Water/Sea State/Sea State Library", order = 0)]
    public class SeaStateLibrarySO : ScriptableObject
    {
        [Header("Douglas scales (index = degree)")]
        public SeaStateProfileSO[] seaStates = new SeaStateProfileSO[10];
        public SwellProfileSO[] swells = new SwellProfileSO[10];

        [Header("Wave field")]
        [Tooltip("Shortest wavelength the simulated field carries, m. Must be ≥ ~5 texels of the render decal; " +
                 "shorter waves are drawn by HDRP's ripple band only and are not in physics.")]
        [Min(0.5f)] public float minResolvedWavelengthM = 10f;

        [Tooltip("Fixed iterations of the Lagrangian inverse in height queries (cost is linear in this).")]
        [Range(1, 8)] public int inverseIterations = 4;

        [Tooltip("Grid resolution (per side) of the whitecap Jacobian calibration run at each bake.")]
        [Range(16, 128)] public int foamCalibrationGrid = 64;

        [Header("Transitions")]
        [Tooltip("Simulation seconds over which a sea-state change cross-fades.")]
        [Min(0f)] public float transitionSeconds = 8f;

        [Header("Seawater")]
        [Tooltip("Seawater density, kg/m³ (typical open ocean surface water ≈ 1025).")]
        [Min(900f)] public float waterDensityKgM3 = 1025f;

        public SeaStateProfileSO GetSea(int degree)
            => seaStates != null && degree >= 0 && degree < seaStates.Length ? seaStates[degree] : null;

        public SwellProfileSO GetSwell(int degree)
            => swells != null && degree >= 0 && degree < swells.Length ? swells[degree] : null;
    }
}
