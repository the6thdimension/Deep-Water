using Unity.InferenceEngine;
using DeepWater.Aviator.Core;

namespace DeepWater.Aviator.Learning
{
    /// <summary>
    /// Where trained brains live, and whether one fits the pilot as it is now (A17, M34). The auto-trainer copies the newest
    /// brain to <see cref="LatestPath"/> after each training session (git-ignored: it changes every session); "Keep this
    /// brain" saves a named copy with its settings to <see cref="KeptFolder"/> (committed, M19). A brain trained on another
    /// version of the senses would read the wrong numbers, and ML-Agents only checks a model's version, not its shape:
    /// <see cref="Fits(Model, out string)"/> checks the size of the observation it was trained on.
    /// </summary>
    public static class BrainLibrary
    {
        public const string PoliciesFolder = "Assets/RH Aviator/Policies";
        public const string LatestPath = PoliciesFolder + "/Latest/Fighter.onnx";
        public const string KeptFolder = PoliciesFolder + "/Kept";
        /// <summary>ML-Agents' name for the vector observation input of a model it exports (TensorNames.ObservationPlaceholderPrefix + 0).</summary>
        public const string ObservationInput = "obs_0";

        /// <summary>The latest trained brain, in the Editor; null in a build or before the first training session.</summary>
        public static ModelAsset Latest()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>(LatestPath);
#else
            return null;
#endif
        }

        /// <summary>
        /// True when the brain reads senses the pilot can feed: the senses as they are now (<see cref="LearnedSenses.Size"/>
        /// numbers), or version 2's (<see cref="LearnedSenses.SizeVersion2"/>), which are their first numbers.
        /// </summary>
        public static bool Fits(ModelAsset brain, out string why)
        {
            if (brain == null) { why = "no brain"; return false; }
            return Fits(ModelLoader.Load(brain), out why);
        }

        public static bool Fits(Model model, out string why)
        {
            int size = SensesOf(model);
            if (size == LearnedSenses.Size || size == LearnedSenses.SizeVersion2) { why = null; return true; }
            why = size < 0 && size != -1 ? $"it has no '{ObservationInput}' input: not an ML-Agents brain"
                : $"it was trained on {size} senses, the pilot now has {LearnedSenses.Size} (senses version {LearnedSenses.Version}) and can still feed version 2's {LearnedSenses.SizeVersion2}";
            return false;
        }

        /// <summary>How many senses the brain was trained on (-1: not known; -2: not an ML-Agents brain).</summary>
        public static int SensesOf(ModelAsset brain) => brain == null ? -1 : SensesOf(ModelLoader.Load(brain));

        public static int SensesOf(Model model)
        {
            foreach (Model.Input input in model.inputs)
                if (input.name == ObservationInput)
                    return input.shape.isRankDynamic || input.shape.rank < 2 ? -1 : input.shape.Get(-1);
            return -2;
        }
    }
}
