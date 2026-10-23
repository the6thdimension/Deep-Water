using UnityEngine;
using DeepWater.Sea.Core;

namespace DeepWater.Sea.ScriptableObjects
{
    /// <summary>
    /// One Douglas wind-sea degree (0–9): the physical sea that degree stands for. Values are
    /// generated from cited tables by <c>Deep Water → Sea State → Build Douglas Profiles</c>;
    /// the <see cref="provenance"/> text says where every number came from and which ones
    /// are authored rather than sourced.
    /// </summary>
    [CreateAssetMenu(fileName = "Douglas_N", menuName = "Deep Water/Sea State/Sea State Profile", order = 1)]
    public class SeaStateProfileSO : ScriptableObject
    {
        [Header("Douglas wind sea")]
        [Range(0, 9)] public int douglasDegree;
        public string douglasTerm = "";

        [Header("Waves")]
        public WaveSystemAuthoring windSea = new WaveSystemAuthoring();

        [Header("Wind")]
        [Tooltip("Sustained wind speed, knots (NATO STANAG 4194 open-ocean table mean).")]
        [Min(0f)] public float windSpeedKnots;

        [Header("Whitecaps")]
        [Tooltip("Whitecap area fraction 0–1. Default from Monahan & O'Muircheartaigh (1980) at this wind.")]
        [Range(0f, 1f)] public float whitecapFraction;

        [Header("Provenance")]
        [TextArea(4, 14)] public string provenance = "";

        public float WindSpeedMs => windSpeedKnots * SeaUnits.MetersPerSecondPerKnot;
    }
}
