using UnityEngine;
using DeepWater.Aviator.Core;

namespace DeepWater.Aviator.ScriptableObjects
{
    /// <summary>
    /// What a learned pilot is paid for (TRAINING-CONCEPT M5, M24), signed: outcomes first, lightly shaped. Written by
    /// Deep Water → RH Aviator → Build Dogfight Training Scene with its provenance; kept with every policy trained on it
    /// (M19). Every number AUTHORED.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRewardWeights", menuName = "Deep Water/RH Aviator/Reward Weights", order = 21)]
    public class RewardWeightsSO : ScriptableObject
    {
        [Header("Outcomes")]
        public float kill = 1f;
        public float shotDown = -1f;
        public float crash = -1f;
        public float collision = -1f;
        public float friendlyKill = -1f;
        public float leftArena = -1f;

        [Header("Shaping, per round and per second")]
        public float hitLanded = 0.05f;
        public float hitTaken = -0.05f;
        public float friendlyHit = -0.05f;
        [Tooltip("Per second inside an enemy's rear cone and pointing at it (A11's measure).")]
        public float pointedConePerS = 0.02f;
        [Range(1f, 180f)] public float coneDeg = 30f;
        [Min(1f)] public float coneRangeM = 1000f;

        [Header("The squadron")]
        [Tooltip("The share of a squadmate's kill or loss each other flying squadmate gets.")]
        [Range(0f, 1f)] public float squadronShare = 0.25f;

        [TextArea(3, 12)] public string provenance;

        public RewardWeights Bake()
        {
            var w = new RewardWeights
            {
                Kill = kill, ShotDown = shotDown, FriendlyKill = friendlyKill,
                Crash = crash, Collision = collision, LeftArena = leftArena,
                HitLanded = hitLanded, HitTaken = hitTaken, FriendlyHit = friendlyHit,
                PointedConePerS = pointedConePerS, ConeDeg = coneDeg, ConeRangeM = coneRangeM,
                SquadronShare = squadronShare,
            };
            w.Validate();
            return w;
        }
    }
}
