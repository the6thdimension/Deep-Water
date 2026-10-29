using UnityEngine;
using Unity.InferenceEngine;
using DeepWater.AirWing.Modules.Aircraft;

namespace DeepWater.Aviator.Learning
{
    /// <summary>
    /// One jet's own brain (A17, M32): when set, it overrides its squadron's (<see cref="TrainingSquadron"/>), so red 12 can
    /// fly a saved brain while the rest of red are line pilots. Without this component, or with "own brain" off, the jet
    /// flies its squadron's brain. The Fighter Brains window adds it when a jet is given its own brain.
    /// </summary>
    [RequireComponent(typeof(AircraftView))]
    public sealed class JetBrain : MonoBehaviour
    {
        [Tooltip("Fly this jet's own brain instead of its squadron's.")]
        [SerializeField] bool ownBrain;
        [SerializeField] FighterBrain brain = FighterBrain.Learning;
        [Tooltip("The kept brain (an .onnx in Policies/Kept) when the brain is Saved.")]
        [SerializeField] ModelAsset savedBrain;

        public bool OwnBrain => ownBrain;
        public FighterBrain Brain => brain;
        public ModelAsset SavedBrain => savedBrain;

        public void Set(bool own, FighterBrain jetBrain, ModelAsset saved)
        {
            ownBrain = own;
            brain = jetBrain;
            savedBrain = saved;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // The squadron puts the setting onto the jet's components; enabling them inside OnValidate is not allowed.
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                TrainingSquadron squadron = TrainingSquadron.Of(GetComponent<AircraftView>());
                if (squadron != null) squadron.Apply();
            };
        }
#endif
    }
}
