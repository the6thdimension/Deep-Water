using Unity.MLAgents.Policies;

namespace DeepWater.Aviator.Learning
{
    /// <summary>
    /// What flies a training jet (A15, A17; TRAINING-CONCEPT M2, M4, M31): the learning brain, a saved brain, the scripted
    /// stand-in, or one of the three rule pilots. The numbers are the old SquadronPilot's (Learned 0, Rookie 1, Line 2,
    /// Ace 3), so a scene saved before the rename reads the same.
    /// </summary>
    public enum FighterBrain
    {
        /// <summary>Trains while the trainer runs; without one it flies the latest trained brain, or the stand-in until one exists (M33).</summary>
        Learning = 0,
        Rookie = 1,
        Line = 2,
        Ace = 3,
        /// <summary>A kept brain (an .onnx in Policies/Kept): flies it, never trains it.</summary>
        Saved = 4,
        /// <summary>The scripted stand-in (<see cref="FighterAgent.Heuristic"/>): lead pursuit, guns inside the trigger discipline, one missile at a time.</summary>
        Scripted = 5,
    }

    /// <summary>What one brain setting means for a jet's components: which pilot flies, and how ML-Agents picks the policy.</summary>
    public readonly struct BrainPlan
    {
        /// <summary>The <see cref="FighterAgent"/> flies (the learning, saved and scripted brains); otherwise the rule pilot does.</summary>
        public readonly bool Agent;
        /// <summary>ML-Agents' behaviour type when the agent flies: Default (trainer, else a model, else the stand-in), InferenceOnly, HeuristicOnly.</summary>
        public readonly BehaviorType Behaviour;
        /// <summary>The rule pilot's skill when it flies (Rookie, Line or Ace); Learning when the agent flies.</summary>
        public readonly FighterBrain RuleSkill;

        BrainPlan(bool agent, BehaviorType behaviour, FighterBrain ruleSkill)
        {
            Agent = agent;
            Behaviour = behaviour;
            RuleSkill = ruleSkill;
        }

        public static BrainPlan For(FighterBrain brain)
        {
            switch (brain)
            {
                case FighterBrain.Saved: return new BrainPlan(true, BehaviorType.InferenceOnly, FighterBrain.Learning);
                case FighterBrain.Scripted: return new BrainPlan(true, BehaviorType.HeuristicOnly, FighterBrain.Learning);
                case FighterBrain.Rookie:
                case FighterBrain.Line:
                case FighterBrain.Ace: return new BrainPlan(false, BehaviorType.Default, brain);
                default: return new BrainPlan(true, BehaviorType.Default, FighterBrain.Learning);
            }
        }
    }
}
