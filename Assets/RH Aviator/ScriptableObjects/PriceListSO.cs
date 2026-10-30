using UnityEngine;
using DeepWater.Aviator.Core;

namespace DeepWater.Aviator.ScriptableObjects
{
    /// <summary>
    /// What everything costs in a fight, in game credits (A23): the price ledger's list, shown on the scoreboard. Every
    /// number AUTHORED for the game, none a real-world price (BLUEPRINT standing constraint 8). Written by Deep Water →
    /// RH Aviator → Build Dogfight Training Scene; tune it here and watch the scoreboard before it ever becomes a reward.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPriceList", menuName = "Deep Water/RH Aviator/Price List", order = 22)]
    public class PriceListSO : ScriptableObject
    {
        [Header("Jets")]
        [Tooltip("An enemy kill earns this; losing your own jet, however it went down, costs it.")]
        [Min(0f)] public float jet = 1000f;
        [Tooltip("On top of the jet the side lost: what shooting down a friend costs the shooter.")]
        [Min(0f)] public float friendlyKillPenalty = 500f;

        [Header("Stores")]
        [Min(0f)] public float round = 0.5f;
        [Min(0f)] public float heatMissile = 50f;
        [Min(0f)] public float radarMissile = 120f;
        [Min(0f)] public float chaff = 3f;
        [Min(0f)] public float flare = 3f;
        [Tooltip("Per kilogram of fuel burned.")]
        [Min(0f)] public float fuelPerKg = 0.02f;

        [TextArea(3, 12)] public string provenance;

        public PriceList Bake()
        {
            var p = new PriceList
            {
                Jet = jet, FriendlyKillPenalty = friendlyKillPenalty,
                Round = round, HeatMissile = heatMissile, RadarMissile = radarMissile, Chaff = chaff, Flare = flare,
                FuelPerKg = fuelPerKg,
            };
            p.Validate();
            return p;
        }
    }
}
