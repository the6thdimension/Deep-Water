using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Captain;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.Scenario;
using DeepWater.GeneralQuarters.Core.World;
using DeepWater.GeneralQuarters.Modules.Propulsion;
using DeepWater.GeneralQuarters.ScriptableObjects;
using Debug = UnityEngine.Debug;

namespace DeepWater.GeneralQuarters.Modules.Combat
{
    /// <summary>
    /// Hosts the ship's combat core on the range: once per fixed step it reads the true own-ship state from the
    /// Rigidbody, lets the AI captain look (for the seat no player holds), steps the raid and then the combat system,
    /// applies kills back to the raid, and hands the conn's standing orders to <see cref="ShipDrive"/>.
    /// Every order, the player's or the AI's, goes in through <see cref="Issue"/>, so the order log has them all.
    /// It also times the combat core each step: the Phase 1 budget is about 1 ms (G16).
    /// </summary>
    [DefaultExecutionOrder(-100)]   // before ShipDrive, so the drive answers this step's orders
    [RequireComponent(typeof(ShipDrive))]
    [DisallowMultipleComponent]
    public sealed class ShipCombat : MonoBehaviour
    {
        const float KnToMs = 0.514444f;
        static readonly ProfilerMarker CoreMarker = new ProfilerMarker("GQ.CombatCore");
        static readonly ProfilerMarker HostMarker = new ProfilerMarker("GQ.CombatHost");

        [SerializeField] ShipLoadoutSO loadout;
        [SerializeField] AiCaptainProfileSO aiCaptainProfile;
        [SerializeField] FidelityLevel aiCaptainLevel = FidelityLevel.L0;

        ShipDrive _drive;
        Rigidbody _body;
        CombatLoadout _baked;
        readonly List<TruthTarget> _world = new List<TruthTarget>();
        readonly List<ShipHit> _hits = new List<ShipHit>();
        readonly List<KillReport> _kills = new List<KillReport>();
        readonly List<KillReport> _runKills = new List<KillReport>();
        int _sequenceBase;
        bool _resolvedRaised, _propulsionLostNoted;

        public CombatSystem Combat { get; private set; }
        public CombatLoadout Loadout => _baked;
        public IAiCaptain AiCaptain { get; private set; }
        /// <summary>The seat the AI captain fills: the one the player is not in.</summary>
        public Seat AiSeat { get; set; } = Seat.Tao;
        public ScriptedRaid Raid { get; private set; }
        public RaidScenarioSO RaidScenario { get; private set; }
        public long RaidStartTick { get; private set; }
        /// <summary>The true world this step: the raid's live threats.</summary>
        public IReadOnlyList<TruthTarget> World => _world;
        /// <summary>Every kill this run, in order.</summary>
        public IReadOnlyList<KillReport> Kills => _runKills;
        public OwnShipState Own { get; private set; }
        public CombatCost Cost { get; } = new CombatCost();
        /// <summary>Runs since the range opened: each has its own combat system, magazine and logs.</summary>
        public int Run { get; private set; }
        public ShipDrive Drive => _drive;
        /// <summary>The AI captain's tuning (the range reads its route id to register the patrol route on the Helm).</summary>
        public AiCaptainProfileData AiProfile => aiCaptainProfile != null ? aiCaptainProfile.profile : default;

        /// <summary>Raised once when the raid has launched everything and nothing is still flying.</summary>
        public event Action<ShipCombat> RaidResolved;
        /// <summary>Raised when a raid starts (general quarters is sounded).</summary>
        public event Action<ShipCombat> RaidStarted;
        /// <summary>Raised when a fresh run replaces the combat system (its log starts again from nothing).</summary>
        public event Action<ShipCombat> RunStarted;

        /// <summary>Give an order (from a seat, or the scenario). It takes effect on the next fixed step.</summary>
        public void Issue(in Order order)
        {
            if (Combat == null) { Debug.LogError("[ShipCombat] no combat system yet; order dropped: " + order, this); return; }
            Combat.Issue(order);
        }

        /// <summary>Start a raid around where the ship is now. Replaces a raid already running.</summary>
        public void StartRaid(RaidScenarioSO scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            RaidScenario = scenario;
            RaidStartTick = Combat.Tick;
            Raid = new ScriptedRaid(scenario.StartingAt(RaidStartTick), Own);
            _resolvedRaised = false;
            Cost.Clear();
            Combat.Log.Add(Combat.Tick, EventKind.Note, "Range", $"raid '{scenario.displayName}' started: {scenario.threats.Length} threats",
                root: RootKind.Scenario);
            Debug.Log($"[GQ] Raid '{scenario.displayName}' started at tick {RaidStartTick}: {scenario.threats.Length} threats.", this);
            RaidStarted?.Invoke(this);
        }

        /// <summary>
        /// A fresh run: a new combat system from the loadout (full magazines, no damage, empty logs, the seed again).
        /// The ship keeps her way: the new run starts by steadying on her present course and speed.
        /// </summary>
        public void NewRun()
        {
            if (Combat != null) _sequenceBase += Combat.Orders.All.Count + 1;   // keep the Helm seeing new sequence numbers
            _baked = loadout.Bake();
            Combat = new CombatSystem(_baked);
            foreach (FidelityResolution f in Combat.Fidelity) if (f.FellBack) Debug.Log($"[GQ] {f}", this);
            AiCaptain = AiCaptainFactory.Create(aiCaptainLevel, aiCaptainProfile.Bake(), out FidelityResolution ai);
            if (ai.FellBack) Debug.Log($"[GQ] {ai}", this);
            Raid = null;
            RaidScenario = null;
            _world.Clear();
            _runKills.Clear();
            _resolvedRaised = false;
            _propulsionLostNoted = false;
            Cost.Clear();
            Run++;

            ShipMotion m = _drive.Motion;
            float knots = Run == 1 ? _drive.StartSpeedKn : Mathf.Round(m.SurgeMs / KnToMs);
            float course = Mathf.Round(ReadOwn().HeadingDeg) % 360f;
            Combat.Issue(Order.Helm(OrderIssuer.Scenario, course, knots));
            RunStarted?.Invoke(this);
        }

        void Awake()
        {
            _drive = GetComponent<ShipDrive>();
            _body = GetComponent<Rigidbody>();
            if (loadout == null || aiCaptainProfile == null)
            {
                Debug.LogError($"[ShipCombat] '{name}' needs a loadout and an AI captain profile; the combat system is off.", this);
                enabled = false;
            }
        }

        void Start()
        {
            Own = ReadOwn();
            NewRun();
        }

        void FixedUpdate()
        {
            if (Combat == null) return;
            long started = Stopwatch.GetTimestamp();
            using (HostMarker.Auto())
            {
                OwnShipState own = ReadOwn();
                Own = own;
                AiCaptain.Step(Combat, own, AiSeat);

                _hits.Clear();
                if (Raid != null) Raid.Step(Combat.Tick, Combat.TickSeconds, own, _world, _hits);
                else _world.Clear();

                _kills.Clear();
                long coreStarted = Stopwatch.GetTimestamp();
                using (CoreMarker.Auto()) Combat.Step(own, _world, _hits, _kills);
                long coreEnded = Stopwatch.GetTimestamp();

                if (Raid != null) Raid.ApplyKills(_kills);
                _runKills.AddRange(_kills);
                if (Raid != null && !_resolvedRaised) Cost.Add(Ms(coreStarted, coreEnded), Ms(started, Stopwatch.GetTimestamp()), _world.Count);
            }

            AnswerHelm();

            if (Raid != null && Raid.Resolved && !_resolvedRaised)
            {
                _resolvedRaised = true;
                RaidResolved?.Invoke(this);
            }
        }

        void AnswerHelm()
        {
            HelmCommand command = Combat.Helm;
            command.OrderSequence += _sequenceBase;
            // Damage Control L0 has no partial casualties (Engine Room L2): propulsion offline stops the shafts.
            if (!Combat.DamageControl.IsOnline(ShipSystem.Propulsion))
            {
                command.Speed = SpeedMode.Bell;
                command.Bell = EngineBell.Stop;
                if (!_propulsionLostNoted)
                {
                    _propulsionLostNoted = true;
                    Combat.Log.Add(Combat.Tick, EventKind.Note, "Range (Damage Control L0 → Engine Room)",
                        "propulsion offline: shafts stopped; she coasts", root: RootKind.Scenario);
                }
            }
            _drive.Answer(command);
        }

        OwnShipState ReadOwn()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            float heading = forward.sqrMagnitude > 1e-8f ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg : 0f;
            return new OwnShipState
            {
                Position = transform.position,
                Velocity = _body != null ? _body.linearVelocity : Vector3.zero,
                HeadingDeg = CompassMath.Normalize(heading),
            };
        }

        static float Ms(long from, long to) => (float)((to - from) * 1000.0 / Stopwatch.Frequency);
    }

    /// <summary>
    /// What the combat core costs per fixed step during a raid, in milliseconds of wall-clock time on this machine:
    /// the core alone (<see cref="CombatSystem.Step"/>) and the whole host step (AI captain + raid + core).
    /// Editor numbers run slower than a player build; the gate is read from them anyway, and says so.
    /// </summary>
    public sealed class CombatCost
    {
        readonly List<float> _core = new List<float>(20000);
        float _coreSum, _hostSum, _coreMax, _hostMax;
        int _maxThreats;

        public int Steps => _core.Count;
        public float CoreMeanMs => Steps > 0 ? _coreSum / Steps : 0f;
        public float CoreMaxMs => _coreMax;
        public float HostMeanMs => Steps > 0 ? _hostSum / Steps : 0f;
        public float HostMaxMs => _hostMax;
        /// <summary>The most live threats seen in one step.</summary>
        public int MaxThreats => _maxThreats;

        public void Clear()
        {
            _core.Clear();
            _coreSum = _hostSum = _coreMax = _hostMax = 0f;
            _maxThreats = 0;
        }

        public void Add(float coreMs, float hostMs, int threats)
        {
            _core.Add(coreMs);
            _coreSum += coreMs;
            _hostSum += hostMs;
            if (coreMs > _coreMax) _coreMax = coreMs;
            if (hostMs > _hostMax) _hostMax = hostMs;
            if (threats > _maxThreats) _maxThreats = threats;
        }

        /// <summary>The core's cost at percentile <paramref name="p"/> (0–1). Sorts a copy: for the debrief, not per frame.</summary>
        public float CorePercentileMs(float p)
        {
            if (Steps == 0) return 0f;
            var sorted = new List<float>(_core);
            sorted.Sort();
            return sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1)];
        }
    }
}
