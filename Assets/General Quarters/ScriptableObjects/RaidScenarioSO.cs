using UnityEngine;
using DeepWater.GeneralQuarters.Core.Scenario;

namespace DeepWater.GeneralQuarters.ScriptableObjects
{
    /// <summary>
    /// A scripted raid (the scenario half of the world). Bearings are true bearings from where the own ship is when
    /// the raid starts; launch ticks count from the start. AUTHORED: these are exercise raids, not a real threat.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRaid", menuName = "Deep Water/General Quarters/Raid Scenario", order = 23)]
    public class RaidScenarioSO : ScriptableObject
    {
        [Tooltip("The name the range shows.")]
        public string displayName = "Raid";
        public RaidThreatSpec[] threats = new RaidThreatSpec[0];
        [TextArea(4, 16)] public string provenance;

        /// <summary>The threats with launch ticks moved to start at <paramref name="startTick"/>.</summary>
        public RaidThreatSpec[] StartingAt(long startTick)
        {
            var specs = new RaidThreatSpec[threats.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                specs[i] = threats[i];
                specs[i].LaunchTick += startTick;
            }
            return specs;
        }
    }
}
