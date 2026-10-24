using UnityEngine;
using DeepWater.GeneralQuarters.Core.EngineRoom;

namespace DeepWater.GeneralQuarters.ScriptableObjects
{
    /// <summary>
    /// A ship's machinery and hull hydrodynamics for the Engine Room (MMG standard method). <see cref="provenance"/>
    /// names the source of every number and flags the AUTHORED ones.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEngineRoom", menuName = "Deep Water/General Quarters/Engine Room Profile", order = 21)]
    public class EngineRoomProfileSO : ScriptableObject
    {
        public EngineRoomProfileData profile;
        [Tooltip("Centre-of-pressure depth of the hull's sway force below the waterline, m (half draught at L0).")]
        public float hullSwayDepthM = 3f;
        [Tooltip("Extra roll damping from hull lift, as a fraction of critical, at the reference speed; it scales with speed. " +
                 "Sea State's probes already damp roll at rest.")]
        public float rollLiftDampingRatio = 0.1f;
        [Tooltip("Speed at which the lift damping reaches its full ratio, m/s.")]
        public float rollDampingReferenceSpeedMs = 16f;
        [TextArea(6, 24)] public string provenance;

        public EngineRoomProfileData Bake() => profile;
    }
}
