using System.Collections.Generic;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.DamageControl;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.Simulation;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core
{
    /// <summary>
    /// The ship's combat core: CIC, Magazine and Damage Control, stepped once per fixed tick in the
    /// written order of BLUEPRINT §3.2. Pure C# (P6): the host feeds it the true world and receives kills;
    /// the only way to command it is <see cref="Issue"/>. Same seed + same Orders + same truth feed
    /// gives the same <see cref="Fix"/> on every tick.
    /// </summary>
    public sealed class CombatSystem
    {
        const string DoctrineRule = "Doctrine / ROE";

        readonly CombatLoadout _loadout;
        readonly CombatClock _clock = new CombatClock();
        readonly DeterministicRng _rng;
        readonly EngagementContext _ctx = new EngagementContext();
        readonly List<Order> _due = new List<Order>();
        readonly List<Detection> _detections = new List<Detection>();
        readonly List<Assignment> _assignments = new List<Assignment>();
        readonly List<DirectedEngagement> _directed = new List<DirectedEngagement>();
        readonly List<ShipSystem> _offline = new List<ShipSystem>();
        readonly List<FidelityResolution> _fidelity = new List<FidelityResolution>();
        readonly ISensorModel _radar;
        readonly IGunMount[] _guns;

        public OrderLog Orders { get; } = new OrderLog();
        public EngagementLog Log { get; } = new EngagementLog();
        public DoctrineState Doctrine { get; } = new DoctrineState();
        public ITrackManager Tracks { get; }
        public ITewa Tewa { get; }
        public IVls Vls { get; }
        public IReadOnlyList<IGunMount> Guns => _guns;
        public IDamageControl DamageControl { get; }
        public ISensorModel Radar => _radar;

        /// <summary>The next tick to be stepped. Orders issued now take effect on it.</summary>
        public long Tick { get; private set; }
        public float TickSeconds => _loadout.TickSeconds;
        /// <summary>Running fingerprint over every tick so far.</summary>
        public ulong Fix { get; private set; } = CombatFix.Offset;
        /// <summary>What each element asked for and what it got.</summary>
        public IReadOnlyList<FidelityResolution> Fidelity => _fidelity;
        /// <summary>What the conn has ordered, as standing state; the Helm element steers by it.</summary>
        public HelmCommand Helm { get; private set; } = HelmCommand.Stopped(0f);

        public CombatSystem(CombatLoadout loadout)
        {
            _loadout = loadout;
            _clock.TickSeconds = loadout.TickSeconds;
            _rng = new DeterministicRng(loadout.Seed);

            _radar = SensorFactory.CreateRadar(loadout.RadarLevel, loadout.Radar, out FidelityResolution r); Note(r);
            Tracks = TrackFactory.Create(loadout.TracksLevel, loadout.Tracks, out r); Note(r);
            Tewa = TewaFactory.Create(loadout.TewaLevel, loadout.Tewa, out r); Note(r);
            Vls = VlsFactory.Create(loadout.VlsLevel, loadout.Vls, _clock, out r); Note(r);
            _guns = new IGunMount[loadout.Guns.Length];
            for (int i = 0; i < _guns.Length; i++) { _guns[i] = GunFactory.Create(loadout.GunsLevel, loadout.Guns[i], out r); if (i == 0) Note(r); }
            DamageControl = DamageControlFactory.Create(loadout.DamageLevel, loadout.Damage, out r); Note(r);

            // The scenario's starting doctrine is a root like any Order: Inquiry can cite it.
            int roe = Log.Add(0, EventKind.OrderApplied, DoctrineRule, $"scenario default ROE {loadout.DefaultRoe}", root: RootKind.Scenario);
            Doctrine.SetRoe(loadout.DefaultRoe, roe);
            foreach (DoctrineDefault d in loadout.DefaultDoctrine)
            {
                int ev = Log.Add(0, EventKind.OrderApplied, DoctrineRule, $"scenario default {d.Weapon} doctrine {d.Mode}", root: RootKind.Scenario);
                Doctrine.SetMode(d.Weapon, d.Mode, ev);
            }
        }

        /// <summary>Give an Order. It takes effect on the next tick stepped.</summary>
        public Order Issue(Order order) => Orders.Issue(order, Tick);

        /// <summary>
        /// Advance one tick. <paramref name="world"/> is the true world at this tick; <paramref name="hits"/>
        /// are threats that reached the ship since the last tick. Kills are appended to <paramref name="kills"/>
        /// for the host to apply to the world.
        /// </summary>
        public void Step(in OwnShipState own, IReadOnlyList<TruthTarget> world, IReadOnlyList<ShipHit> hits, List<KillReport> kills)
        {
            long tick = Tick;
            _clock.Tick = tick;
            _ctx.Tick = tick;
            _ctx.TickSeconds = _loadout.TickSeconds;
            _ctx.Own = own;
            _ctx.IndexWorld(world);
            _ctx.Tracks = Tracks;
            _ctx.Doctrine = Doctrine;
            _ctx.Rng = _rng;
            _ctx.Log = Log;
            _ctx.Kills = kills;

            // 1. Orders
            _due.Clear();
            Orders.Drain(tick, _due);
            for (int i = 0; i < _due.Count; i++) Apply(_due[i]);

            // 2. Sensors
            _detections.Clear();
            if (_radar.Online && !DamageControl.OutOfAction && _radar.Watch.IsDue(tick)) _radar.Sweep(own, world, _detections);

            // 3. Tracks
            Tracks.Update(tick, _detections, Log);

            // 4. TEWA
            if (!DamageControl.OutOfAction && tick % Tewa.EvaluatePeriodTicks == 0)
                Tewa.Evaluate(tick, _loadout.TickSeconds, own, Tracks, Doctrine, Vls, Log, _assignments);

            // 5. Fire control: missile assignments in order; one that can't launch now is dropped and
            //    TEWA re-assigns at its next evaluation.
            if (!DamageControl.OutOfAction)
                for (int i = 0; i < _assignments.Count; i++)
                {
                    Assignment a = _assignments[i];
                    for (int n = 0; n < a.Rounds; n++)
                        if (!Vls.TryLaunch(_ctx, a, out _)) break;
                }
            _assignments.Clear();

            // 6. Weapons in flight: missiles resolve first, then each gun mount (in loadout order) fires
            //    and resolves its bursts.
            Vls.StepInFlight(_ctx);
            for (int i = 0; i < _guns.Length; i++)
                if (!DamageControl.OutOfAction || _guns[i].Profile.SelfDefence) _guns[i].Step(_ctx, _directed);
            PruneDirected();

            // 7. Damage control
            for (int i = 0; i < hits.Count; i++)
            {
                ShipHit hit = hits[i];
                int cause = Tracks.TryGetByTruth(hit.ThreatId, out Track t) ? t.CreatedEvent : -1;
                _offline.Clear();
                DamageControl.ApplyHit(tick, hit, own, cause, Log, _offline);
                foreach (ShipSystem s in _offline) TakeOffline(s);
                if (Tracks.TryGetByTruth(hit.ThreatId, out t)) Tracks.MarkDestroyed(t.Id);
            }

            // 8. Fix
            Fix = Fingerprint(Fix, tick);
            Tick = tick + 1;
        }

        void Apply(in Order o)
        {
            int ev = Log.Add(o.Tick, EventKind.OrderApplied, "Orders", o.ToString(), o.TrackId,
                root: RootKind.Order, orderSequence: o.Sequence);
            switch (o.Kind)
            {
                case OrderKind.Roe: Doctrine.SetRoe(o.Roe, ev); break;
                case OrderKind.Doctrine: Doctrine.SetMode(o.Weapon, o.Mode, ev); break;
                case OrderKind.Helm:
                case OrderKind.Bell:
                case OrderKind.Rudder:
                case OrderKind.Route:
                case OrderKind.Course:
                    Helm = HelmCommand.Apply(Helm, o);
                    break;
                case OrderKind.CeaseFire:
                    int ceaseTrack = o.TrackId;
                    _directed.RemoveAll(d => ceaseTrack < 0 || d.TrackId == ceaseTrack);
                    break;
                case OrderKind.Engage:
                    if (IsMissile(o.Weapon))
                        _assignments.Add(new Assignment { TrackId = o.TrackId, Weapon = o.Weapon, Rounds = o.Rounds > 0 ? o.Rounds : 1, CauseEvent = ev });
                    else
                        _directed.Add(new DirectedEngagement { TrackId = o.TrackId, Weapon = o.Weapon, BurstsRemaining = o.Rounds > 0 ? o.Rounds : -1, CauseEvent = ev });
                    break;
            }
        }

        static bool IsMissile(WeaponClass w) =>
            w == WeaponClass.Essm || w == WeaponClass.Sm2 || w == WeaponClass.Harpoon || w == WeaponClass.Tomahawk;

        void PruneDirected()
        {
            for (int i = _directed.Count - 1; i >= 0; i--)
                if (!Tracks.TryGet(_directed[i].TrackId, out Track t) || t.Destroyed || !t.Active) _directed.RemoveAt(i);
        }

        void TakeOffline(ShipSystem system)
        {
            if (system == ShipSystem.Radar) _radar.Online = false;
            Vls.SetGroupOnline(system, false);
            for (int i = 0; i < _guns.Length; i++)
                if (_guns[i].Profile.System == system) _guns[i].Online = false;
        }

        void Note(in FidelityResolution r)
        {
            _fidelity.Add(r);
            if (r.FellBack) Log.Add(0, EventKind.FidelityFallback, "Fidelity ladder", r.ToString(), root: RootKind.Scenario);
        }

        ulong Fingerprint(ulong h, long tick)
        {
            h = CombatFix.Mix(h, tick);
            h = CombatFix.Mix(h, _rng.Draws);
            h = CombatFix.Mix(CombatFix.Mix(h, (int)Doctrine.Roe), _radar.Online);
            IReadOnlyList<Track> tracks = Tracks.Tracks;
            for (int i = 0; i < tracks.Count; i++)
            {
                Track t = tracks[i];
                h = CombatFix.Mix(CombatFix.Mix(CombatFix.Mix(h, t.Id), t.Active), t.Destroyed);
                h = CombatFix.Mix(CombatFix.Mix(h, t.Position), t.Velocity);
            }
            h = Vls.Fingerprint(h);
            for (int i = 0; i < _guns.Length; i++) h = _guns[i].Fingerprint(h);
            return DamageControl.Fingerprint(h);
        }
    }
}
