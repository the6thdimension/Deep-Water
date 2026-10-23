using System;
using UnityEngine;
using DeepWater.Sea.Core;

namespace DeepWater.Sea.ScriptableObjects
{
    /// <summary>
    /// Inspector-facing description of one wave system (a wind sea or a swell). Shared by
    /// <see cref="SeaStateProfileSO"/> and <see cref="SwellProfileSO"/>; bakes to a
    /// <see cref="WaveSystemSpec"/> once a direction is known (direction belongs to the
    /// scenario, not the sea state).
    /// </summary>
    [Serializable]
    public class WaveSystemAuthoring
    {
        [Tooltip("Significant wave height Hs (mean of the highest third of waves), metres. 0 = absent.")]
        [Min(0f)] public float significantHeightM;

        [Tooltip("Spectral peak period Tp, seconds.")]
        [Min(0f)] public float peakPeriodS;

        [Tooltip("JONSWAP peak-enhancement factor γ. 1 = Pierson–Moskowitz (fully developed).")]
        [Min(1f)] public float jonswapGamma = 1f;

        [Tooltip("cos-2s directional spreading exponent s. ~10 wind sea, 25–75 swell (Goda).")]
        [Min(0.5f)] public float spreadingS = 10f;

        [Tooltip("Lagrangian choppiness Q. 1 = first-order Lagrangian (choppy-wave) model, 0 = linear Airy.")]
        [Range(0f, 1f)] public float choppiness = 1f;

        [Tooltip("Number of discrete wave components drawn from the spectrum.")]
        [Range(0, SeaSimulation.MaxComponentsPerState)] public int componentCount;

        [Tooltip("RNG seed for frequencies, directions and phases (P7).")]
        public uint seed = 1u;

        public WaveSystemSpec ToSpec(float directionRad, uint scenarioSeed) => new WaveSystemSpec
        {
            SignificantHeightM = significantHeightM,
            PeakPeriodS = peakPeriodS,
            Gamma = jonswapGamma,
            SpreadingS = spreadingS,
            DirectionRad = directionRad,
            Choppiness = choppiness,
            ComponentCount = significantHeightM > 0f ? componentCount : 0,
            Seed = seed ^ scenarioSeed,
        };
    }
}
