using UnityEngine;
using DeepWater.GeneralQuarters.Core.Helm;

namespace DeepWater.GeneralQuarters.ScriptableObjects
{
    /// <summary>Telegraph bells, autopilot gains and route steering for the Helm. AUTHORED unless <see cref="provenance"/> says otherwise.</summary>
    [CreateAssetMenu(fileName = "NewHelm", menuName = "Deep Water/General Quarters/Helm Profile", order = 22)]
    public class HelmProfileSO : ScriptableObject
    {
        public HelmProfileData profile;
        [TextArea(4, 16)] public string provenance;

        public HelmProfileData Bake() => profile;
    }
}
