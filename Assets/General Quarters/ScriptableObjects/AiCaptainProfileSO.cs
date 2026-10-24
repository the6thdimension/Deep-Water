using UnityEngine;
using DeepWater.GeneralQuarters.Core.Captain;

namespace DeepWater.GeneralQuarters.ScriptableObjects
{
    /// <summary>The AI captain's habits: its look period, route, threat bell, arc-turn limits and confirm delay. AUTHORED.</summary>
    [CreateAssetMenu(fileName = "NewAiCaptain", menuName = "Deep Water/General Quarters/AI Captain Profile", order = 24)]
    public class AiCaptainProfileSO : ScriptableObject
    {
        public AiCaptainProfileData profile;
        [TextArea(4, 16)] public string provenance;

        public AiCaptainProfileData Bake() => profile;
    }
}
