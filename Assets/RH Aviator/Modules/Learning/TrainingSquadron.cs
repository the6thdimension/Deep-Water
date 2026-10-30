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

        /// <summary>
        /// In play (A23; SIX 2026-10-30, "a button that allows me to load a particular brain for a particular airplane"): the
        /// squadron's brain changes now, and each of its jets that flies differently for it changes pilot in flight. Null
        /// when done, else why not (nothing changed).
        /// </summary>
        public string SetBrainLive(FighterBrain squadronBrain, ModelAsset saved)
        {
            foreach (AircraftView jet in Jets)
            {
                if (jet == null) continue;
                JetBrain own = jet.GetComponent<JetBrain>();
                if (own != null && own.OwnBrain) continue;   // its own brain stays
                if (CannotFly(jet, squadronBrain, saved) is string why) return why;
            }
            brain = squadronBrain;
            savedBrain = saved;
            foreach (AircraftView jet in Jets) if (jet != null && ApplyJet(jet)) Reseat(jet);
            return null;
        }

        /// <summary>
        /// In play (A23): one jet's own brain (<paramref name="own"/> false: back to the squadron's) changes now, and the jet
        /// changes pilot in flight. Null when done, else why not (nothing changed).
        /// </summary>
        public string SetJetBrainLive(AircraftView jet, bool own, FighterBrain jetBrain, ModelAsset saved)
        {
            FighterBrain flies = own ? jetBrain : brain;
            if (CannotFly(jet, flies, own ? saved : savedBrain) is string why) return why;
            JetBrain mine = jet.GetComponent<JetBrain>();
            if (mine == null) mine = jet.gameObject.AddComponent<JetBrain>();
            mine.Set(own, jetBrain, own ? saved : null);
            if (ApplyJet(jet)) Reseat(jet);
            return null;
        }

        /// <summary>
        /// Why a brain cannot fly this jet in play, or null. A saved brain needs a model whose senses are the ones the jet's
        /// agent was shaped for when it woke (a version 2 brain onto a version 3 jet changes the agent's shape: edit mode only).
        /// </summary>
        static string CannotFly(AircraftView jet, FighterBrain jetBrain, ModelAsset saved)
        {
            if (jetBrain != FighterBrain.Saved) return null;
            if (saved == null) return "no brain chosen";
            if (!BrainLibrary.Fits(saved, out string why)) return $"{saved.name} does not fit: {why}";
            var agent = jet.GetComponent<FighterAgent>();
            int senses = BrainLibrary.SensesOf(saved);
            if (agent != null && senses != agent.SensesSize)
                return $"{saved.name} reads {senses} senses and {jet.name} was shaped for {agent.SensesSize} when Play started: change it in edit mode";
            return null;
        }

        /// <summary>A jet whose brain changed in play takes its new pilot: the agent its seat back, or the rule pilot a new seat.</summary>
        void Reseat(AircraftView jet)
        {
            var agent = jet.GetComponent<FighterAgent>();
            var rule = jet.GetComponent<AviatorPilot>();
            if (rule != null && rule.enabled) rule.Reseat();   // before its Start, Start seats it
            FighterTraining training = FindAnyObjectByType<FighterTraining>();
            if (training != null) training.Reseat(agent);
            Debug.Log($"[RH Aviator] {jet.name}: now flown by {BrainOf(jet, out ModelAsset m)}{(m != null && BrainOf(jet, out _) == FighterBrain.Saved ? $" ({m.name})" : "")}, changed in play.", jet);
        }

        /// <summary>Every jet of the squadron to its brain: which pilot flies, the agent's behaviour type and model.</summary>
        public void Apply()
        {
            foreach (AircraftView jet in Jets) if (jet != null) ApplyJet(jet);
        }

        /// <summary>One jet to its brain. True when anything about who flies it changed.</summary>
        bool ApplyJet(AircraftView jet)
        {
            bool playing = Application.isPlaying;
            {
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
                    // A brain flying without the trainer takes its best guess instead of sampling its noise: a brain whose
                    // noise grew in training otherwise flies at random (sensors-bc, 2026-10-30). The trainer, when connected,
                    // samples regardless. This setter does not rebuild a started agent's policy, so a change in play is
                    // followed by one that does (the inference device, set to itself) unless the brain changes below anyway.
                    bool guess = !behaviour.DeterministicInference;
                    if (guess) { behaviour.DeterministicInference = true; changed = true; }
                    // Each setter rebuilds a started agent's policy, and InferenceOnly with no model throws: the model goes
                    // in before the type when the type needs it, after it otherwise.
                    bool modelFirst = type == BehaviorType.InferenceOnly;
                    bool rebuilt = false;
                    if (modelFirst && behaviour.Model != model) { behaviour.Model = model; changed = rebuilt = true; }
                    if (behaviour.BehaviorType != type) { behaviour.BehaviorType = type; changed = rebuilt = true; }
                    if (!modelFirst && behaviour.Model != model) { behaviour.Model = model; changed = rebuilt = true; }
                    if (guess && playing && !rebuilt) behaviour.InferenceDevice = behaviour.InferenceDevice;
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
                return changed;
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
