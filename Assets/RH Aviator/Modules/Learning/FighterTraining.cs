using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.Modules.World;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Modules;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Learning
{
    /// <summary>
    /// Runs the learned pilots' training in a scene (TRAINING-CONCEPT T3; A13–A15). It steps ML-Agents' Academy by hand
    /// from the flight core's tick (§4.9), never on its own clock: each FixedUpdate, before Air Wing's runner steps the
    /// tick (order −55, the runner −50), it pays every learned jet what the last tick earned (<see cref="RewardLedger"/>),
    /// ends the episode of each jet that went down, cuts a life that ran its length, asks each flying jet for a decision
    /// every decision period, and then steps the Academy, which gathers the observations and hands the actions to the
    /// seats. The arena brings the jets back (W59); a new life is a new episode. When the trainer resets the
    /// environment, everyone goes back to their slots.
    /// <para>It also draws a small panel: who flies each side (the trainer, the latest or a saved brain, the scripted
    /// stand-in, the rule pilots), the Academy's step, the clock, and each side's lives, kills and losses. T switches the
    /// clock between real time and the training speed (M31): the trainer sets 20×, and learning slows while you watch.</para>
    /// </summary>
    [DefaultExecutionOrder(-55)]
    public sealed class FighterTraining : MonoBehaviour
    {
        [SerializeField] AirWingRunner runner;
        [SerializeField] AviatorFight fight;
        [SerializeField] AirWingArena arena;
        [SerializeField] LearnedPilotSO learnedPilot;
        [SerializeField] RewardWeightsSO rewardWeights;
        [Tooltip("The clock T switches to from real time; the trainer's engine_settings.time_scale in Training/config.")]
        [SerializeField] float fastTimeScale = 20f;
        [SerializeField] bool showPanel = true;
        [SerializeField] Color colour = new Color(0.55f, 0.95f, 0.75f, 0.95f);

        FighterAgent[] _agents = new FighterAgent[0];
        long[] _lifeStart;
        int[] _resets;
        RewardLedger _ledger;
        LearnedPilotSettings _settings;
        bool _ready, _failed;
        int _blueLives, _redLives, _interrupted;
        string _blueBrains = "", _redBrains = "";
        GUIStyle _style;

        public FlightCore Core => runner.Flight;
        public Engagement Fight => fight.Engagement;
        public LearnedPilotSettings Settings => _settings;
        public RewardLedger Ledger => _ledger;
        public IReadOnlyList<FighterAgent> Agents => _agents;

        public bool IsProtected(int id) => runner.Flight.Arena != null && runner.Flight.Arena.Protected(id, runner.Flight.Tick);

        /// <summary>
        /// Once everything it needs has started (the runner, the fight and the arena start before the first FixedUpdate):
        /// seat each enabled learned jet, put it in the fight, track its pay, and take the Academy's stepping in hand.
        /// </summary>
        bool TryStart()
        {
            if (_failed) return false;
            if (runner == null || runner.Flight == null || fight == null || fight.Engagement == null || learnedPilot == null || learnedPilot.eyesAndHands == null || rewardWeights == null)
            {
                Debug.LogError("[RH Aviator] training needs a running Air Wing runner, the fight, a Learned Pilot asset with its pilot profile, and Reward Weights; nothing trains.", this);
                _failed = true;
                return false;
            }
            if (arena == null || runner.Flight.Arena == null)
                Debug.LogWarning("[RH Aviator] training without an arena: a jet that goes down stays down.", this);

            _settings = learnedPilot.Bake();
            PilotProfile pilot = learnedPilot.eyesAndHands.Bake();
            Engagement engagement = fight.Engagement;
            _ledger = new RewardLedger(rewardWeights.Bake(), engagement);
            _agents = FindObjectsByType<FighterAgent>(FindObjectsInactive.Exclude).Where(a => a.enabled).OrderBy(a => a.AircraftId).ToArray();
            _lifeStart = new long[_agents.Length];
            _resets = Enumerable.Repeat(-1, _agents.Length).ToArray();   // so the first flying tick starts the first life
            foreach (FighterAgent a in _agents)
            {
                var view = a.GetComponent<AircraftView>();
                if (view.Aircraft == null) { Debug.LogError($"[RH Aviator] {a.name}: the runner built no aircraft for it; it is not seated.", a); continue; }
                int id = view.AircraftId;
                var seat = new LearnedSeat(runner.Flight, view.Aircraft.Profile, pilot);
                runner.Roster.SetOwnSeat(id, seat);
                engagement.Join(id, a.Side);
                _ledger.Track(id);
                a.Bind(this, seat, id);
            }
            Academy.Instance.AutomaticSteppingEnabled = false;
            Academy.Instance.OnEnvironmentReset += ResetFight;
            // Training must not stall when the Editor is not the focused window (2026-10-29: Play stood at frame 1 for 28 s
            // in the background). For this play session only: the project's Player Settings are left as they are.
            Application.runInBackground = true;
            _blueBrains = Brains(Side.Blue);
            _redBrains = Brains(Side.Red);
            _ready = true;
            string who = Academy.Instance.IsCommunicatorOn ? "the trainer" : "a model or, without one, the scripted stand-in";
            Debug.Log($"[RH Aviator] training: {_agents.Length} learned jets (blue {_agents.Count(a => a.Side == Side.Blue)}, red {_agents.Count(a => a.Side == Side.Red)}) " +
                      $"flown by {who}; pilot '{pilot.Name}' eyes and hands; a decision every {_settings.DecisionPeriodTicks} ticks; lives cut at {_settings.MaxLifeS:F0} s. " +
                      $"Blue: {_blueBrains}. Red: {_redBrains}.", this);
            return true;
        }

        /// <summary>Who flies a side, counted: "8 TRAINING", "4 LATEST, 4 LINE", "1 SAVED Ace-2M"…</summary>
        static string Brains(Side side)
        {
            var counts = new SortedDictionary<string, int>();
            foreach (TrainingSquadron squadron in FindObjectsByType<TrainingSquadron>(FindObjectsInactive.Exclude))
            {
                if (squadron.Side != side) continue;
                foreach (AircraftView jet in squadron.Jets)
                {
                    if (jet == null) continue;
                    string label = Who(squadron.BrainOf(jet, out _), jet.GetComponent<BehaviorParameters>());
                    counts[label] = counts.TryGetValue(label, out int n) ? n + 1 : 1;
                }
            }
            return counts.Count == 0 ? "none" : string.Join(", ", counts.Select(kv => $"{kv.Value} {kv.Key}"));
        }

        /// <summary>What actually flies a jet once the squadron has applied its brain: the policy ML-Agents picked.</summary>
        static string Who(FighterBrain brain, BehaviorParameters behaviour)
        {
            switch (brain)
            {
                case FighterBrain.Rookie: return "ROOKIE";
                case FighterBrain.Line: return "LINE";
                case FighterBrain.Ace: return "ACE";
            }
            if (behaviour == null || behaviour.BehaviorType == BehaviorType.HeuristicOnly) return "STAND-IN";
            if (behaviour.BehaviorType == BehaviorType.InferenceOnly) return $"SAVED {behaviour.Model.name}";
            if (Academy.Instance.IsCommunicatorOn) return "TRAINING";
            return behaviour.Model != null ? "LATEST" : "STAND-IN";
        }

        void Update()
        {
            // T: real time to watch, or back to the training speed (M31). The trainer sets the speed once, at connection.
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.tKey.wasPressedThisFrame) return;
            Time.timeScale = Time.timeScale > 1.01f ? 1f : Mathf.Max(1f, fastTimeScale);
            Debug.Log($"[RH Aviator] training clock ×{Time.timeScale:0.#} (T switches).", this);
        }

        void OnDestroy()
        {
            if (_ready && Academy.IsInitialized) Academy.Instance.OnEnvironmentReset -= ResetFight;
        }

        /// <summary>The trainer's reset: everyone back at their slots, fresh lives.</summary>
        void ResetFight()
        {
            if (arena != null) arena.RespawnAll();
            Debug.Log($"[RH Aviator] training: the fight reset at tick {runner.Flight.Tick} (the Academy starting, or the trainer); everyone back at their spawn points.", this);
        }

        void FixedUpdate()
        {
            if (!_ready && !TryStart()) return;
            FlightCore core = runner.Flight;
            _ledger.Observe(core);   // the tick the runner flew last (nothing before the first)
            long tick = core.Tick;   // the tick it flies next
            double dt = System.Math.Round(core.Clock.TickSeconds, 6);
            long lifeTicks = (long)System.Math.Round(_settings.MaxLifeS / dt);
            for (int i = 0; i < _agents.Length; i++)
            {
                FighterAgent agent = _agents[i];
                if (agent.Seat == null) continue;
                Aircraft jet = core.Find(agent.AircraftId);
                float r = _ledger.Take(jet.Id);
                if (r != 0f) agent.AddReward(r);
                if (_ledger.TakeLoss(jet.Id, out _))
                {
                    agent.EndEpisode();   // it went down: the episode ends on the loss it was just paid
                    continue;
                }
                if (jet.State.Status != FlightStatus.Flying) continue;   // down: no decisions until it is back
                if (jet.Resets != _resets[i])
                {
                    _resets[i] = jet.Resets;
                    _lifeStart[i] = tick;
                    if (agent.Side == Side.Red) _redLives++; else _blueLives++;
                }
                if (tick - _lifeStart[i] >= lifeTicks)
                {
                    agent.EpisodeInterrupted();   // a life that ran its length: cut, not lost
                    _lifeStart[i] = tick;
                    _interrupted++;
                }
                // Every jet decides on the same ticks, by the core's clock, not its own life's: each tick with a decision is a
                // round trip to the trainer, and lives that start at different ticks made nearly every tick one (2026-10-29: the
                // trainer pinned a core while four headless players idled at 3–7 %). A new life waits at most 4 ticks for its
                // first decision, its seat holding course (LearnedSeat.Hold), inside its 5 s of protection.
                if (tick % _settings.DecisionPeriodTicks == 0) agent.RequestDecision();
            }
            Academy.Instance.EnvironmentStep();
        }

        void OnGUI()
        {
            if (!showPanel || !_ready) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16f * Mathf.Max(1f, Screen.height / 1080f)), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
                _style.normal.textColor = colour;
            }
            float s = Mathf.Max(1f, Screen.height / 1080f), w = Screen.width;
            Engagement e = fight.Engagement;
            string who = Academy.Instance.IsCommunicatorOn ? "TRAINER CONNECTED" : "NO TRAINER";
            int blueLoss = 0, redLoss = 0;
            foreach (FighterAgent a in _agents) { if (a.Side == Side.Red) redLoss += _ledger.LossesOf(a.AircraftId); else blueLoss += _ledger.LossesOf(a.AircraftId); }
            GUI.Label(new Rect(w - 820f * s, 12f * s, 800f * s, 110f * s),
                      $"TRAINING · {who} · STEP {Academy.Instance.StepCount:N0} · ×{Time.timeScale:0.#} (T)\n" +
                      $"BLUE  {_blueBrains}  ·  LIVES {_blueLives}  KILLS {e.SideKills(Side.Blue)}  LOST {blueLoss}\n" +
                      $"RED   {_redBrains}  ·  LIVES {_redLives}  KILLS {e.SideKills(Side.Red)}  LOST {redLoss}   CUT {_interrupted}", _style);
        }
    }
}
