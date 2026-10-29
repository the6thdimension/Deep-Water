using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.Modules.World;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Modules;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Learning
{
    /// <summary>
    /// A training squadron (A15, A17): the jets of one <see cref="SpawnPoint"/>, its side, and the brain that flies them
    /// (<see cref="FighterBrain"/>). A jet with its own <see cref="JetBrain"/> overrides the squadron's. Each jet carries
    /// both a <see cref="FighterAgent"/> and an <see cref="AviatorPilot"/>; the brain enables one and disables the other,
    /// and sets how ML-Agents picks the agent's policy, so M4's curriculum against the rule pilots, or a kept brain on one
    /// jet, is a setting, not a rebuild. Applied when a setting changes in the Inspector and again in Awake, before
    /// anything seats itself. In play, a learning jet gets the latest trained brain (M33) and a saved brain is checked
    /// against the senses; one that does not fit flies the scripted stand-in, said in the Console.
    /// </summary>
    [RequireComponent(typeof(SpawnPoint))]
    [DefaultExecutionOrder(-200)]
    public sealed class TrainingSquadron : MonoBehaviour
    {
        [SerializeField] Side side = Side.Blue;
        [Tooltip("What flies the squadron's jets. A jet with its own Jet Brain overrides it.")]
        [FormerlySerializedAs("pilot")]
        [SerializeField] FighterBrain brain = FighterBrain.Learning;
        [Tooltip("The kept brain (an .onnx in Policies/Kept) when the brain is Saved.")]
        [SerializeField] ModelAsset savedBrain;
        [SerializeField] PilotProfileSO rookie, line, ace;

        public Side Side => side;
        public FighterBrain Brain => brain;
        public ModelAsset SavedBrain => savedBrain;
        public IReadOnlyList<AircraftView> Jets => GetComponent<SpawnPoint>().Jets;

        /// <summary>Sets the squadron up from code (the scene builder), then applies it.</summary>
        public void Configure(Side squadronSide, FighterBrain squadronBrain, PilotProfileSO rookieProfile, PilotProfileSO lineProfile, PilotProfileSO aceProfile)
        {
            side = squadronSide;
            brain = squadronBrain;
            rookie = rookieProfile;
            line = lineProfile;
            ace = aceProfile;
            Apply();
        }

        /// <summary>The squadron's brain from code (the Fighter Brains window), then applied.</summary>
        public void SetBrain(FighterBrain squadronBrain, ModelAsset saved)
        {
            brain = squadronBrain;
            savedBrain = saved;
            Apply();
        }

        /// <summary>A jet's brain: its own when its <see cref="JetBrain"/> says so, else the squadron's.</summary>
        public FighterBrain BrainOf(AircraftView jet, out ModelAsset saved)
        {
            JetBrain own = jet != null ? jet.GetComponent<JetBrain>() : null;
            if (own != null && own.OwnBrain)
            {
                saved = own.SavedBrain;
                return own.Brain;
            }
            saved = savedBrain;
            return brain;
        }

        /// <summary>The squadron that holds a jet, or null.</summary>
        public static TrainingSquadron Of(AircraftView jet)
        {
            if (jet == null) return null;
            foreach (TrainingSquadron s in FindObjectsByType<TrainingSquadron>(FindObjectsInactive.Include))
                foreach (AircraftView j in s.Jets)
                    if (j == jet) return s;
            return null;
        }

        void Awake() => Apply();

#if UNITY_EDITOR
        void OnValidate()
        {
            // Enabling components from inside OnValidate is not allowed: do it just after.
            if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
        }
#endif

        /// <summary>Every jet of the squadron to its brain: which pilot flies, the agent's behaviour type and model.</summary>
        public void Apply()
        {
            bool playing = Application.isPlaying;
            foreach (AircraftView jet in Jets)
            {
                if (jet == null) continue;
                FighterBrain jetBrain = BrainOf(jet, out ModelAsset saved);
                BrainPlan plan = BrainPlan.For(jetBrain);
                BehaviorType type = plan.Behaviour;
                ModelAsset model = jetBrain == FighterBrain.Saved ? saved : null;
                if (jetBrain == FighterBrain.Saved && model == null)
                {
                    // InferenceOnly without a model throws when the agent starts: say so and fly the stand-in.
                    type = BehaviorType.HeuristicOnly;
                    if (playing) Debug.LogWarning($"[RH Aviator] {jet.name}: brain Saved but no brain chosen; it flies the scripted stand-in.", jet);
                }
                else if (playing && jetBrain == FighterBrain.Saved && !BrainLibrary.Fits(model, out string why))
                {
                    type = BehaviorType.HeuristicOnly;
                    model = null;
                    Debug.LogError($"[RH Aviator] {jet.name}: the saved brain '{saved.name}' does not fit: {why}. It flies the scripted stand-in.", jet);
                }
                else if (playing && jetBrain == FighterBrain.Learning)
                {
                    model = LatestForPlay();   // the trainer, when it is connected, flies it instead (Behavior Type Default)
                }

                var agent = jet.GetComponent<FighterAgent>();
                var rule = jet.GetComponent<AviatorPilot>();
                var behaviour = jet.GetComponent<BehaviorParameters>();
                // Change only what differs, and say so: opening the scene must not leave it dirty (a smoke found it did).
                bool changed = false;
                int team = side == Side.Red ? 1 : 0;
                if (agent != null)
                {
                    changed |= agent.SetSide(side);
                    if (agent.enabled != plan.Agent) { agent.enabled = plan.Agent; changed = true; }
                }
                if (behaviour != null)
                {
                    if (behaviour.TeamId != team) { behaviour.TeamId = team; changed = true; }
                    // Each setter rebuilds a started agent's policy, and InferenceOnly with no model throws: the model goes
                    // in before the type when the type needs it, after it otherwise.
                    bool modelFirst = type == BehaviorType.InferenceOnly;
                    if (modelFirst && behaviour.Model != model) { behaviour.Model = model; changed = true; }
                    if (behaviour.BehaviorType != type) { behaviour.BehaviorType = type; changed = true; }
                    if (!modelFirst && behaviour.Model != model) { behaviour.Model = model; changed = true; }
                }
                if (rule != null)
                {
                    PilotProfileSO rules = plan.RuleSkill == FighterBrain.Rookie ? rookie : plan.RuleSkill == FighterBrain.Ace ? ace : line;
                    changed |= rule.ConfigureFight(rules, side);
                    if (rule.enabled == plan.Agent) { rule.enabled = !plan.Agent; changed = true; }
                }
#if UNITY_EDITOR
                if (changed && !playing)
                {
                    if (agent != null) UnityEditor.EditorUtility.SetDirty(agent);
                    if (rule != null) UnityEditor.EditorUtility.SetDirty(rule);
                    if (behaviour != null) UnityEditor.EditorUtility.SetDirty(behaviour);
                }
#endif
            }
        }

        static int _latestFrame = -1;
        static ModelAsset _latest;

        /// <summary>
        /// The latest trained brain if it fits the senses, looked up once a frame (both squadrons wake in the same one) and
        /// said once in the Console; null when there is none or it does not fit, so the stand-in flies.
        /// </summary>
        static ModelAsset LatestForPlay()
        {
            if (_latestFrame == Time.frameCount) return _latest;
            _latestFrame = Time.frameCount;
            _latest = BrainLibrary.Latest();
            if (_latest == null)
                Debug.Log($"[RH Aviator] no trained brain yet ({BrainLibrary.LatestPath}): without the trainer, the learning jets fly the scripted stand-in.");
            else if (!BrainLibrary.Fits(_latest, out string why))
            {
                Debug.LogWarning($"[RH Aviator] the latest brain does not fit: {why}. Without the trainer, the learning jets fly the scripted stand-in; train a fresh run.");
                _latest = null;
            }
            return _latest;
        }
    }
}
