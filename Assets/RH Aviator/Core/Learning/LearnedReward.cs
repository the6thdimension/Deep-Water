using System;
using System.Collections.Generic;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.State;
using RH.Shared.Contracts;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// The learned pilot's reward (M5, M24), signed, all AUTHORED and baked from <c>RewardWeightsSO</c>: outcomes first,
    /// lightly shaped. Kept with every policy (M19), since a policy is only as good as what it was paid for.
    /// </summary>
    public struct RewardWeights
    {
        /// <summary>Shooting down an enemy (+), being shot down (−), a friend (−, the shooter's alone).</summary>
        public float Kill, ShotDown, FriendlyKill;
        /// <summary>Meeting the sea or the ground (−), another jet (−, both), the box's edge (−).</summary>
        public float Crash, Collision, LeftArena;
        /// <summary>Per round: one landed on an enemy (+), one taken (−), one landed on a friend (−).</summary>
        public float HitLanded, HitTaken, FriendlyHit;
        /// <summary>
        /// Per second inside an enemy's rear cone <b>and pointing at it</b> (+): A11's pointed measure, so a head-on pass,
        /// which the position-only cone scores (A9), pays nothing here.
        /// </summary>
        public float PointedConePerS;
        /// <summary>The cone for <see cref="PointedConePerS"/>: within this of the target's tail and of the attacker's nose, degrees, inside the range, metres.</summary>
        public float ConeDeg, ConeRangeM;
        /// <summary>The share of a squadmate's kill or loss each other flying squadmate also gets (0 … 1).</summary>
        public float SquadronShare;

        public void Validate()
        {
            if (!(Kill >= 0f && ShotDown <= 0f && FriendlyKill <= 0f && Crash <= 0f && Collision <= 0f && LeftArena <= 0f))
                throw new ArgumentException("reward: a kill pays and every loss costs");
            if (!(HitLanded >= 0f && HitTaken <= 0f && FriendlyHit <= 0f && PointedConePerS >= 0f))
                throw new ArgumentException("reward: hits landed and pointed cone time pay; hits taken and friendly hits cost");
            if (!(ConeDeg > 0f && ConeDeg <= 180f && ConeRangeM > 0f)) throw new ArgumentException("reward: the cone needs an angle and a range");
            if (!(SquadronShare >= 0f && SquadronShare <= 1f)) throw new ArgumentException("reward: the squadron share is 0 … 1");
        }
    }

    /// <summary>How an aircraft went down.</summary>
    public enum Loss : byte { None = 0, ShotDown = 1, Crashed = 2, Collided = 3, LeftArena = 4 }

    /// <summary>
    /// The referee's ledger of rewards (M24): after each tick it reads what the flight core and its arena logged (hits,
    /// kills, collisions, the box) and who went down, and pays each tracked aircraft its terms, with each flying squadmate's
    /// share of a kill or a loss. It judges on the truth (it is the referee, not a pilot). Pure and deterministic; call
    /// <see cref="Observe"/> once per tick, after the core has stepped.
    /// </summary>
    public sealed class RewardLedger
    {
        readonly RewardWeights _w;
        readonly Engagement _fight;
        readonly HashSet<int> _tracked = new HashSet<int>();
        readonly Dictionary<int, float> _pending = new Dictionary<int, float>();
        readonly Dictionary<int, Loss> _losses = new Dictionary<int, Loss>();
        readonly Dictionary<int, FlightStatus> _status = new Dictionary<int, FlightStatus>();
        readonly Dictionary<int, double> _pointed = new Dictionary<int, double>();
        readonly Dictionary<int, int> _killsBy = new Dictionary<int, int>(), _lossesOf = new Dictionary<int, int>();
        int _hitsSeen, _killsSeen, _arenaSeen;
        long _lastTick = -1;
        readonly float _cosCone;

        public RewardLedger(in RewardWeights weights, Engagement fight)
        {
            weights.Validate();
            _w = weights;
            _fight = fight ?? throw new ArgumentNullException(nameof(fight));
            _cosCone = Mathf.Cos(weights.ConeDeg * Mathf.Deg2Rad);
        }

        public RewardWeights Weights => _w;

        /// <summary>Pay aircraft <paramref name="id"/> its rewards from now on (the learned jets).</summary>
        public void Track(int id) => _tracked.Add(id);

        /// <summary>The reward earned since the last take, and clears it.</summary>
        public float Take(int id)
        {
            if (!_pending.TryGetValue(id, out float r)) return 0f;
            _pending[id] = 0f;
            return r;
        }

        /// <summary>True once per time <paramref name="id"/> went down, with how.</summary>
        public bool TakeLoss(int id, out Loss loss)
        {
            if (!_losses.TryGetValue(id, out loss) || loss == Loss.None) { loss = Loss.None; return false; }
            _losses[id] = Loss.None;
            return true;
        }

        /// <summary>Seconds aircraft <paramref name="id"/> has spent pointed in an enemy's rear cone (the A11 measure).</summary>
        public double PointedSeconds(int id) => _pointed.TryGetValue(id, out double s) ? s : 0.0;
        public int KillsBy(int id) => _killsBy.TryGetValue(id, out int n) ? n : 0;
        public int LossesOf(int id) => _lossesOf.TryGetValue(id, out int n) ? n : 0;

        /// <summary>After a tick: everything that happened in it, paid.</summary>
        public void Observe(FlightCore core)
        {
            long tick = core.Tick - 1;
            if (tick <= _lastTick) return;   // nothing stepped since the last call
            _lastTick = tick;

            // Hits, per round.
            for (; _hitsSeen < core.Hits.Count; _hitsSeen++)
            {
                HitReport h = core.Hits[_hitsSeen];
                bool friendly = Friends(h.ShooterId, h.TargetId);
                Pay(h.ShooterId, h.Rounds * (friendly ? _w.FriendlyHit : _w.HitLanded));
                Pay(h.TargetId, h.Rounds * _w.HitTaken);
            }

            // Who went down this tick, and how: a kill record, else an arena event, else the surface. Every status is
            // brought up to date first, so a squadmate lost on this same tick gets no share of anyone's kill or loss.
            IReadOnlyList<Aircraft> all = core.Aircraft;
            _down.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft a = all[i];
                bool was = !_status.TryGetValue(a.Id, out FlightStatus before) || before == FlightStatus.Flying;
                _status[a.Id] = a.State.Status;
                if (was && a.State.Status != FlightStatus.Flying) _down.Add(a.Id);
            }
            foreach (int id in _down)
            {
                Loss how = HowLost(core, id, out int shooter);
                _losses[id] = how;
                _lossesOf[id] = LossesOf(id) + 1;
                float cost = how == Loss.ShotDown ? _w.ShotDown : how == Loss.Collided ? _w.Collision : how == Loss.LeftArena ? _w.LeftArena : _w.Crash;
                PaySquadron(id, cost);
                if (how != Loss.ShotDown || shooter == 0) continue;
                if (Friends(shooter, id)) Pay(shooter, _w.FriendlyKill);
                else
                {
                    _killsBy[shooter] = KillsBy(shooter) + 1;
                    PaySquadron(shooter, _w.Kill);
                }
            }
            _killsSeen = core.Kills.Count;
            _arenaSeen = core.Arena != null ? core.Arena.Events.Count : 0;

            // Pointed rear-cone time, for the tracked jets.
            if (_w.PointedConePerS <= 0f) return;
            float dt = (float)Math.Round(core.Clock.TickSeconds, 6);
            foreach (int id in _tracked)
            {
                Aircraft me = core.Find(id);
                if (me.State.Status != FlightStatus.Flying) continue;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].Id == id || !Enemies(id, all[i].Id) || !Pointed(me.State, all[i].State)) continue;
                    Pay(id, _w.PointedConePerS * dt);
                    _pointed[id] = PointedSeconds(id) + dt;
                    break;
                }
            }
        }

        readonly List<int> _down = new List<int>();

        /// <summary>Inside the target's rear cone by position (as <see cref="RearCone"/>) and pointing at it within the same angle.</summary>
        public bool Pointed(in AirframeState attacker, in AirframeState target)
        {
            if (!RearCone.Inside(attacker, target, _w.ConeDeg, _w.ConeRangeM)) return false;
            Vector3 to = target.Position - attacker.Position;
            return Vector3.Dot(attacker.Attitude * Vector3.forward, to.normalized) >= _cosCone;
        }

        Loss HowLost(FlightCore core, int id, out int shooter)
        {
            shooter = 0;
            for (int k = _killsSeen; k < core.Kills.Count; k++)
                if (core.Kills[k].VictimId == id) { shooter = core.Kills[k].ShooterId; return Loss.ShotDown; }
            if (core.Arena != null)
                for (int k = _arenaSeen; k < core.Arena.Events.Count; k++)
                {
                    ArenaEvent e = core.Arena.Events[k];
                    if (e.AircraftId != id) continue;
                    if (e.Kind == ArenaEventKind.Collided) return Loss.Collided;
                    if (e.Kind == ArenaEventKind.LeftArena) return Loss.LeftArena;
                }
            return Loss.Crashed;
        }

        /// <summary><paramref name="amount"/> to <paramref name="id"/>, and the share of it to each flying squadmate.</summary>
        void PaySquadron(int id, float amount)
        {
            Pay(id, amount);
            if (_w.SquadronShare <= 0f) return;
            Side side = _fight.SideEver(id);
            if (side == Side.None) return;
            foreach (int mate in _tracked)
                if (mate != id && _fight.SideEver(mate) == side && _fight.SideOf(mate) != Side.None && _status.TryGetValue(mate, out FlightStatus st) && st == FlightStatus.Flying)
                    Pay(mate, _w.SquadronShare * amount);
        }

        void Pay(int id, float amount)
        {
            if (amount == 0f || !_tracked.Contains(id)) return;
            _pending[id] = (_pending.TryGetValue(id, out float r) ? r : 0f) + amount;
        }

        bool Friends(int a, int b) { Side sa = _fight.SideEver(a), sb = _fight.SideEver(b); return sa != Side.None && sa == sb; }
        bool Enemies(int a, int b) { Side sa = _fight.SideEver(a), sb = _fight.SideEver(b); return sa != Side.None && sb != Side.None && sa != sb; }
    }
}
