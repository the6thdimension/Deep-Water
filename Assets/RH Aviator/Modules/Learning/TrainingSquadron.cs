using UnityEngine;
using Unity.MLAgents.Policies;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.Modules.World;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Modules;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Learning
{
    /// <summary>Who flies a training squadron (A15, TRAINING-CONCEPT M2, M4): the learned pilot, or one of the three rule pilots.</summary>
    public enum SquadronPilot { Learned = 0, Rookie = 1, Line = 2, Ace = 3 }

    /// <summary>
    /// A training squadron (A15): the jets of one <see cref="SpawnPoint"/>, its side, and who flies them. Each jet carries
    /// both a <see cref="FighterAgent"/> and an <see cref="AviatorPilot"/>; the pilot setting enables one and disables the
    /// other, so M4's curriculum against the rule pilots is a setting, not a rebuild. Applied when the setting changes in
    /// the Inspector and again in Awake, before anything seats itself. Self-play wants both squadrons learned; against a
    /// rule-pilot squadron, train with the config that has no self-play (README).
    /// </summary>
    [RequireComponent(typeof(SpawnPoint))]
    [DefaultExecutionOrder(-200)]
    public sealed class TrainingSquadron : MonoBehaviour
    {
        [SerializeField] Side side = Side.Blue;
        [SerializeField] SquadronPilot pilot = SquadronPilot.Learned;
        [SerializeField] PilotProfileSO rookie, line, ace;

        public Side Side => side;
        public SquadronPilot Pilot => pilot;

        /// <summary>Sets the squadron up from code (the scene builder), then applies it.</summary>
        public void Configure(Side squadronSide, SquadronPilot squadronPilot, PilotProfileSO rookieProfile, PilotProfileSO lineProfile, PilotProfileSO aceProfile)
        {
            side = squadronSide;
            pilot = squadronPilot;
            rookie = rookieProfile;
            line = lineProfile;
            ace = aceProfile;
            Apply();
        }

        void Awake() => Apply();

#if UNITY_EDITOR
        void OnValidate()
        {
            // Enabling components from inside OnValidate is not allowed: do it just after.
            if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
        }
#endif

        /// <summary>Every jet of the squadron to the setting: the learned pilot on, or the rule pilot of that skill.</summary>
        public void Apply()
        {
            bool learned = pilot == SquadronPilot.Learned;
            PilotProfileSO rules = pilot == SquadronPilot.Rookie ? rookie : pilot == SquadronPilot.Ace ? ace : line;
            foreach (AircraftView jet in GetComponent<SpawnPoint>().Jets)
            {
                if (jet == null) continue;
                var agent = jet.GetComponent<FighterAgent>();
                var rule = jet.GetComponent<AviatorPilot>();
                var behaviour = jet.GetComponent<BehaviorParameters>();
                // Change only what differs, and say so: opening the scene must not leave it dirty (a smoke found it did).
                bool changed = false;
                int team = side == Side.Red ? 1 : 0;
                if (agent != null)
                {
                    changed |= agent.SetSide(side);
                    if (agent.enabled != learned) { agent.enabled = learned; changed = true; }
                }
                if (behaviour != null && behaviour.TeamId != team) { behaviour.TeamId = team; changed = true; }
                if (rule != null)
                {
                    changed |= rule.ConfigureFight(rules, side);
                    if (rule.enabled == learned) { rule.enabled = !learned; changed = true; }
                }
#if UNITY_EDITOR
                if (changed && !Application.isPlaying)
                {
                    if (agent != null) UnityEditor.EditorUtility.SetDirty(agent);
                    if (rule != null) UnityEditor.EditorUtility.SetDirty(rule);
                    if (behaviour != null) UnityEditor.EditorUtility.SetDirty(behaviour);
                }
#endif
            }
        }
    }
}
