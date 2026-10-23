using UnityEngine;

namespace DeepWater.Sea.ScriptableObjects
{
    /// <summary>
    /// One Douglas swell degree (0–9): swell that arrived from distant weather, described by
    /// the scale's length class (short &lt; 100 m, average 100–200 m, long &gt; 200 m) and height
    /// class (low &lt; 2 m, moderate 2–4 m, high &gt; 4 m). Representative values inside each class
    /// are authored and say so in <see cref="provenance"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Swell_N", menuName = "Deep Water/Sea State/Swell Profile", order = 2)]
    public class SwellProfileSO : ScriptableObject
    {
        [Header("Douglas swell")]
        [Range(0, 9)] public int douglasDegree;
        public string douglasTerm = "";
        public string lengthClass = "";
        public string heightClass = "";

        [Header("Waves")]
        public WaveSystemAuthoring swell = new WaveSystemAuthoring();

        [Header("Provenance")]
        [TextArea(4, 14)] public string provenance = "";
    }
}
