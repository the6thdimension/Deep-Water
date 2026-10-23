using Unity.Mathematics;
using UnityEngine;
using DeepWater.Sea.Core;

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

        /// <summary>
        /// The one place a (sea, swell, scenario) choice becomes bake input and published
        /// conditions — shared by the director, the tests and the benchmark so they can never
        /// disagree about what "Douglas 7 + swell 4" means. Missing profiles yield an absent
        /// system (check <see cref="GetSea"/> / <see cref="GetSwell"/> to report them).
        /// </summary>
        public SeaBakeInput BuildBakeInput(int douglasSea, int douglasSwell, float windFromDeg, float swellFromDeg,
            uint scenarioSeed, float gravity, out SeaConditions conditions)
        {
            SeaStateProfileSO sea = GetSea(douglasSea);
            SwellProfileSO swell = GetSwell(douglasSwell);

            var input = new SeaBakeInput
            {
                WindSea = sea != null ? sea.windSea.ToSpec(SeaUnits.TravelDirectionRad(windFromDeg), scenarioSeed) : default,
                Swell = swell != null ? swell.swell.ToSpec(SeaUnits.TravelDirectionRad(swellFromDeg), scenarioSeed) : default,
                MinWavelengthM = minResolvedWavelengthM,
                Gravity = gravity,
                WhitecapFraction = sea != null ? sea.whitecapFraction : 0f,
                FoamCalibrationGrid = foamCalibrationGrid,
            };

            float windHs = input.WindSea.ComponentCount > 0 ? input.WindSea.SignificantHeightM : 0f;
            float swellHs = input.Swell.ComponentCount > 0 ? input.Swell.SignificantHeightM : 0f;
            conditions = new SeaConditions
            {
                DouglasSea = douglasSea,
                DouglasSwell = douglasSwell,
                WindSeaHsM = sea != null ? sea.windSea.significantHeightM : 0f,
                WindSeaTpS = sea != null ? sea.windSea.peakPeriodS : 0f,
                SwellHsM = swell != null ? swell.swell.significantHeightM : 0f,
                SwellTpS = swell != null ? swell.swell.peakPeriodS : 0f,
                SignificantWaveHeightM = math.sqrt(windHs * windHs + swellHs * swellHs),
                WindSpeedMs = sea != null ? sea.WindSpeedMs : 0f,
                WindFromDeg = windFromDeg,
                SwellFromDeg = swellFromDeg,
                WhitecapFraction = input.WhitecapFraction,
            };
            return input;
        }
    }
}
