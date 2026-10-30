using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Demonstrations;
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
    /// <para>Recording the ace (SENSORS-CONCEPT J13, A22): with <c>-recordDemos &lt;steps&gt;</c> on a player's command line
    /// (and <c>-demoDir &lt;folder&gt;</c>), or Record Demos ticked, every learned jet carries the eyes-and-hands profile's
    /// rule pilot as a teacher riding along, flies what the teacher asks (through the agent's own actions, so the
    /// recording is exactly what the brain will be asked to copy), and records each decision into a demonstration file
    /// per jet at the training speed. The player quits when the first jet has recorded its steps.</para>
    /// </summary>
    [DefaultExecutionOrder(-55)]
    public sealed class FighterTraining : MonoBehaviour
    {
        [SerializeField] AirWingRunner runner;
        [SerializeField] AviatorFight fight;
        [SerializeField] AirWingArena arena;
        [SerializeField] LearnedPilotSO learnedPilot;
        [SerializeField] RewardWeightsSO rewardWeights;
        [Tooltip("What everything costs, in game credits (A23): the scoreboard's ledger. Empty: the training asset in the Editor, else the list's own defaults.")]
        [SerializeField] PriceListSO priceList;
        [Tooltip("The clock T switches to from real time; the trainer's engine_settings.time_scale in Training/config.")]
        [SerializeField] float fastTimeScale = 20f;
        [SerializeField] bool showPanel = true;
        [Tooltip("Record the ace's demonstrations (J13): a rule pilot rides along in every learned jet and the agent records what it asks. -recordDemos <steps> on a player's command line turns it on.")]
        [SerializeField] bool recordDemos;
        [Tooltip("Decisions each jet records before the recording stops (0: until stopped).")]
        [SerializeField] int demoSteps = 6000;
        [Tooltip("Where the demonstrations go; empty: Training/demos beside the project (the player's own folder in a build).")]
        [SerializeField] string demoDirectory = "";
        [SerializeField] Color colour = new Color(0.55f, 0.95f, 0.75f, 0.95f);

        FighterAgent[] _agents = new FighterAgent[0];
        long[] _lifeStart;
        int[] _resets;
        RewardLedger _ledger;
        PriceLedger _prices;
        LearnedPilotSettings _settings;
        bool _ready, _failed;
        int _blueLives, _redLives, _interrupted;
        string _blueBrains = "", _redBrains = "";
        // Who shot whom (METHODOLOGY C58): missiles launched, by who flew the shooter, at enemies and at friends.
        public const int WhoShotWhomEveryTicks = 3000;
        readonly Dictionary<int, int> _shooterKind = new Dictionary<int, int>();   // aircraft id → 0 learning, 1 stand-in or teacher, absent: a rule pilot
        readonly int[,] _shots = new int[3, 3];   // [learning, stand-in, rule pilot] × [at enemies, at friends, radar missiles]
        int _eventsSeen;
        GUIStyle _style;

        public FlightCore Core => runner.Flight;
        public Engagement Fight => fight.Engagement;
        public LearnedPilotSettings Settings => _settings;
        public RewardLedger Ledger => _ledger;
        /// <summary>Every jet's account in game credits (A23): what it used and did, priced. Null until training starts.</summary>
        public PriceLedger Prices => _prices;
        public AirWingRunner Runner => runner;
        public IReadOnlyList<FighterAgent> Agents => _agents;
        /// <summary>True when every learned jet is recording its teacher (J13).</summary>
        public bool Recording => recordDemos;

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

            ReadCommandLine();
            _settings = learnedPilot.Bake();
            PilotProfile pilot = learnedPilot.eyesAndHands.Bake();
            Engagement engagement = fight.Engagement;
            _ledger = new RewardLedger(rewardWeights.Bake(), engagement);
            _prices = new PriceLedger(PriceListOrDefault().Bake(), engagement);
            if (GetComponent<FightStats>() == null) gameObject.AddComponent<FightStats>();   // P: every jet's stats (A23)
            // Every jet's agent, flying or not: a brain changed in play (A23) can hand a rule pilot's jet to its agent.
            _agents = FindObjectsByType<FighterAgent>(FindObjectsInactive.Exclude).OrderBy(a => a.AircraftId).ToArray();
            _lifeStart = new long[_agents.Length];
            _resets = Enumerable.Repeat(-1, _agents.Length).ToArray();   // so the first flying tick starts the first life
            foreach (FighterAgent a in _agents)
            {
                var view = a.GetComponent<AircraftView>();
                if (view.Aircraft == null) { Debug.LogError($"[RH Aviator] {a.name}: the runner built no aircraft for it; it is not seated.", a); continue; }
                int id = view.AircraftId;
                var seat = new LearnedSeat(runner.Flight, view.Aircraft.Profile, pilot);
                if (a.enabled) runner.Roster.SetOwnSeat(id, seat);   // a rule pilot flies the others, seated by itself
                engagement.Join(id, a.Side);
                runner.Flight.SetIffCode(id, (int)a.Side);   // one IFF code per side (SENSORS-CONCEPT J5)
                _ledger.Track(id);
                a.Bind(this, seat, id);
            }
            if (recordDemos) StartRecording(pilot, engagement);
            foreach (FighterAgent a in _agents) Classify(a);
            Academy.Instance.AutomaticSteppingEnabled = false;
            Academy.Instance.OnEnvironmentReset += ResetFight;
            // Training must not stall when the Editor is not the focused window (2026-10-29: Play stood at frame 1 for 28 s
            // in the background). For this play session only: the project's Player Settings are left as they are.
            Application.runInBackground = true;
            _blueBrains = Brains(Side.Blue);
            _redBrains = Brains(Side.Red);
            _ready = true;
            string who = Academy.Instance.IsCommunicatorOn ? "the trainer" : "a model or, without one, the scripted stand-in";
            Debug.Log($"[RH Aviator] training: {_agents.Count(a => a.enabled)} learned jets (blue {_agents.Count(a => a.enabled && a.Side == Side.Blue)}, red {_agents.Count(a => a.enabled && a.Side == Side.Red)}) " +
                      $"flown by {who}; pilot '{pilot.Name}' eyes and hands; a decision every {_settings.DecisionPeriodTicks} ticks; lives cut at {_settings.MaxLifeS:F0} s. " +
                      $"Blue: {_blueBrains}. Red: {_redBrains}.", this);
            return true;
        }

        /// <summary>The price list: the one set, else (in the Editor) the training asset, else the list's own defaults, said in the Console.</summary>
        PriceListSO PriceListOrDefault()
        {
#if UNITY_EDITOR
            if (priceList == null) priceList = UnityEditor.AssetDatabase.LoadAssetAtPath<PriceListSO>("Assets/RH Aviator/ScriptableObjects/Training/PriceList.asset");
#endif
            if (priceList != null) return priceList;
            Debug.Log("[RH Aviator] no price list set: the scoreboard prices with the list's own defaults (A23).", this);
            return ScriptableObject.CreateInstance<PriceListSO>();
        }

        /// <summary>-recordDemos &lt;steps&gt; and -demoDir &lt;folder&gt; from a player's command line.</summary>
        void ReadCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-recordDemos" && int.TryParse(args[i + 1], out int steps)) { recordDemos = true; demoSteps = steps; }
                if (args[i] == "-demoDir") demoDirectory = args[i + 1];
            }
        }

        /// <summary>
        /// A teacher in every learned jet (the eyes-and-hands profile's rule pilot, in this fight), the agent on its
        /// heuristic (the teacher's lesson), and a demonstration recorder on each, at the training speed.
        /// </summary>
        void StartRecording(in PilotProfile pilot, Engagement engagement)
        {
            string dir = string.IsNullOrEmpty(demoDirectory)
                ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Training", "demos"))
                : demoDirectory;
            System.IO.Directory.CreateDirectory(dir);
            foreach (FighterAgent a in _agents)
            {
                if (a.Seat == null || !a.enabled) continue;
                var view = a.GetComponent<AircraftView>();
                var teacher = new AviatorSeat(runner.Flight, view.Aircraft.Profile, pilot, new RuleLog());
                teacher.FightIn(a.AircraftId, engagement);
                a.Seat.RideAlong(teacher);
                var behaviour = a.GetComponent<BehaviorParameters>();
                behaviour.BehaviorType = BehaviorType.HeuristicOnly;
                var recorder = a.gameObject.AddComponent<DemonstrationRecorder>();
                recorder.DemonstrationName = $"Ace{a.AircraftId:00}";
                recorder.DemonstrationDirectory = dir;
                recorder.NumStepsToRecord = demoSteps;
                recorder.Record = true;
            }
            Time.timeScale = Mathf.Max(1f, fastTimeScale);
            Debug.Log($"[RH Aviator] recording the ace (J13): {_agents.Length} jets, a teacher '{pilot.Name}' riding along in each, {demoSteps} decisions a jet, into {dir}; clock ×{Time.timeScale:0.#}.", this);
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
        internal static string Who(FighterBrain brain, BehaviorParameters behaviour)
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

        /// <summary>Who flew a shooter, for who-shot-whom: 0 learning, 1 the stand-in or a teacher, absent a rule pilot.</summary>
        void Classify(FighterAgent a)
        {
            var b = a.GetComponent<BehaviorParameters>();
            if (!a.enabled) _shooterKind.Remove(a.AircraftId);
            else _shooterKind[a.AircraftId] = b != null && b.BehaviorType == BehaviorType.HeuristicOnly ? 1 : 0;
        }

        /// <summary>
        /// After a jet's brain changed in play (A23, the Fighter Brains window): its agent takes its seat back when it flies
        /// now (its rule pilot reseats itself when it does), the tallies learn who flies it, and the panel says so.
        /// </summary>
        public void Reseat(FighterAgent agent)
        {
            if (!_ready || agent == null || agent.Seat == null) return;
            if (agent.enabled) runner.Roster.SetOwnSeat(agent.AircraftId, agent.Seat);
            Classify(agent);
            _blueBrains = Brains(Side.Blue);
            _redBrains = Brains(Side.Red);
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
            _prices.Observe(core);
            long tick = core.Tick;   // the tick it flies next
            double dt = System.Math.Round(core.Clock.TickSeconds, 6);
            long lifeTicks = (long)System.Math.Round(_settings.MaxLifeS / dt);
            for (int i = 0; i < _agents.Length; i++)
            {
                FighterAgent agent = _agents[i];
                if (agent.Seat == null) continue;
                Aircraft jet = core.Find(agent.AircraftId);
                if (!agent.enabled)
                {
                    // A rule pilot flies it now: nothing it earns is the agent's, and its next life starts fresh if handed back.
                    _ledger.Take(jet.Id);
                    _ledger.TakeLoss(jet.Id, out _);
                    _resets[i] = -1;
                    continue;
                }
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
            CountShots(core, tick);
        }

        /// <summary>
        /// Tallies each new launch by who flew the shooter and whether its target was a friend, and says the running totals
        /// in the log every <see cref="WhoShotWhomEveryTicks"/> ticks (C58: a training curve is read with this split).
        /// </summary>
        void CountShots(FlightCore core, long tick)
        {
            var events = core.Missiles.Events;
            var live = core.Missiles.Live;
            Engagement e = fight.Engagement;
            for (; _eventsSeen < events.Count; _eventsSeen++)
            {
                var ev = events[_eventsSeen];
                if (ev.Kind != DeepWater.AirWing.Core.Missiles.MissileEventKind.Launched) continue;
                int who = _shooterKind.TryGetValue(ev.ShooterId, out int k) ? k : 2;
                _shots[who, e.SideOf(ev.ShooterId) == e.SideOf(ev.TargetId) ? 1 : 0]++;
                for (int i = 0; i < live.Count; i++)
                    if (live[i].Id == ev.MissileId && live[i].Radar) { _shots[who, 2]++; break; }
            }
            if (tick > 0 && tick % WhoShotWhomEveryTicks == 0)
                Debug.Log($"[WHO-SHOT-WHOM] tick {tick}, step {Academy.Instance.StepCount}: missiles at enemies / at friends (radar of them): " +
                          $"learning {_shots[0, 0]} / {_shots[0, 1]} ({_shots[0, 2]}); stand-in or teacher {_shots[1, 0]} / {_shots[1, 1]} ({_shots[1, 2]}); " +
                          $"rule pilots {_shots[2, 0]} / {_shots[2, 1]} ({_shots[2, 2]})", this);
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
            GUI.Label(new Rect(w - 820f * s, 12f * s, 800f * s, 140f * s),
                      $"TRAINING · {who} · STEP {Academy.Instance.StepCount:N0} · ×{Time.timeScale:0.#} (T)\n" +
                      $"BLUE  {_blueBrains}  ·  LIVES {_blueLives}  KILLS {e.SideKills(Side.Blue)}  LOST {blueLoss}  ·  {FightStats.Credits(_prices.SideNet(Side.Blue))}\n" +
                      $"RED   {_redBrains}  ·  LIVES {_redLives}  KILLS {e.SideKills(Side.Red)}  LOST {redLoss}  ·  {FightStats.Credits(_prices.SideNet(Side.Red))}   CUT {_interrupted}\n" +
                      "P: EVERY JET'S STATS", _style);
        }
    }
}
