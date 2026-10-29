using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;
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
    /// <para>It also draws a small panel: who flies (trainer, model or the scripted stand-in), the Academy's step, and
    /// each side's lives, kills and losses.</para>
    /// </summary>
    [DefaultExecutionOrder(-55)]
    public sealed class FighterTraining : MonoBehaviour
    {
        [SerializeField] AirWingRunner runner;
        [SerializeField] AviatorFight fight;
        [SerializeField] AirWingArena arena;
        [SerializeField] LearnedPilotSO learnedPilot;
        [SerializeField] RewardWeightsSO rewardWeights;
        [SerializeField] bool showPanel = true;
        [SerializeField] Color colour = new Color(0.55f, 0.95f, 0.75f, 0.95f);

        FighterAgent[] _agents = new FighterAgent[0];
        long[] _lifeStart;
        int[] _resets;
        RewardLedger _ledger;
        LearnedPilotSettings _settings;
        bool _ready, _failed;
        int _blueLives, _redLives, _interrupted;
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
            _ready = true;
            string who = Academy.Instance.IsCommunicatorOn ? "the trainer" : "a model or, without one, the scripted stand-in";
            Debug.Log($"[RH Aviator] training: {_agents.Length} learned jets (blue {_agents.Count(a => a.Side == Side.Blue)}, red {_agents.Count(a => a.Side == Side.Red)}) " +
                      $"flown by {who}; pilot '{pilot.Name}' eyes and hands; a decision every {_settings.DecisionPeriodTicks} ticks; lives cut at {_settings.MaxLifeS:F0} s.", this);
            return true;
        }

        void OnDestroy()
        {
            if (_ready && Academy.IsInitialized) Academy.Instance.OnEnvironmentReset -= ResetFight;
        }

        /// <summary>The trainer's reset: everyone back at their slots, fresh lives.</summary>
        void ResetFight()
        {
            if (arena != null) arena.RespawnAll();
            Debug.Log($"[RH Aviator] training: the trainer reset the fight at tick {runner.Flight.Tick}; everyone back at their spawn points.", this);
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
                if ((tick - _lifeStart[i]) % _settings.DecisionPeriodTicks == 0) agent.RequestDecision();
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
            string who = Academy.Instance.IsCommunicatorOn ? "TRAINER" : "MODEL OR SCRIPTED";
            int blueLoss = 0, redLoss = 0;
            foreach (FighterAgent a in _agents) { if (a.Side == Side.Red) redLoss += _ledger.LossesOf(a.AircraftId); else blueLoss += _ledger.LossesOf(a.AircraftId); }
            GUI.Label(new Rect(w - 620f * s, 12f * s, 600f * s, 80f * s),
                      $"TRAINING · {who} · STEP {Academy.Instance.StepCount:N0}\n" +
                      $"BLUE  LIVES {_blueLives}  KILLS {e.SideKills(Side.Blue)}  LOST {blueLoss}\n" +
                      $"RED   LIVES {_redLives}  KILLS {e.SideKills(Side.Red)}  LOST {redLoss}   CUT {_interrupted}", _style);
        }
    }
}
