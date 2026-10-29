using UnityEngine;
using DeepWater.Aviator.Core;

namespace DeepWater.Aviator.ScriptableObjects
{
    /// <summary>
    /// A learned pilot's set-up (TRAINING-CONCEPT M1, M17, M23): the pilot profile it sees and flies under (the ace's eyes
    /// and hands), how often it decides, how long a life may run, and what its actions mean. Written by
    /// Deep Water → RH Aviator → Build Dogfight Training Scene (its numbers and their provenance together). Every
    /// number AUTHORED.
    /// </summary>
    [CreateAssetMenu(fileName = "NewLearnedPilot", menuName = "Deep Water/RH Aviator/Learned Pilot", order = 20)]
    public class LearnedPilotSO : ScriptableObject
    {
        [Tooltip("The eyes and hands it flies under (M17: the ace's).")]
        public PilotProfileSO eyesAndHands;

        [Header("Deciding")]
        [Tooltip("Ticks between decisions (5 = ten a second at the 50 Hz tick).")]
        [Min(1)] public int decisionPeriodTicks = 5;
        [Tooltip("The longest one life runs before its episode is cut, seconds.")]
        [Min(1f)] public float maxLifeS = 180f;

        [Header("What its actions mean")]
        [Tooltip("Turn ±1: a track this many degrees either side of the present one.")]
        [Range(1f, 180f)] public float turnRangeDeg = 180f;
        [Tooltip("Climb ±1: a height this many metres above or below the present one.")]
        [Min(1f)] public float climbRangeM = 1500f;
        [Tooltip("Speed −1 … +1: from this, knots true …")]
        [Min(1f)] public float minKt = 150f;
        [Tooltip("… to this.")]
        [Min(1f)] public float maxKt = 600f;
        [Tooltip("G cap −1: this many g; +1 is the pilot's own most.")]
        [Min(1f)] public float minG = 2f;

        [TextArea(3, 12)] public string provenance;

        public LearnedPilotSettings Bake()
        {
            var s = new LearnedPilotSettings
            {
                DecisionPeriodTicks = decisionPeriodTicks,
                MaxLifeS = maxLifeS,
                TurnRangeDeg = turnRangeDeg,
                ClimbRangeM = climbRangeM,
                MinKt = minKt,
                MaxKt = maxKt,
                MinG = minG,
            };
            s.Validate();
            return s;
        }
    }
}
