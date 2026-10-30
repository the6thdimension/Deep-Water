using System;
using System.Collections.Generic;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.State;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// What everything costs, in game credits (A23; SIX 2026-10-30: "a price tag on every bullet and missile, friendly
    /// kill, enemy kill, death, fuel"). All AUTHORED, baked from <c>PriceListSO</c>: game numbers, not real-world prices
    /// (BLUEPRINT standing constraint 8). Radar time and IFF questions are free; what they cost is what they lead to.
    /// </summary>
    public struct PriceList
    {
        /// <summary>A jet: what an enemy kill earns its shooter, and what losing one costs its pilot, however it went down.</summary>
        public float Jet;
        /// <summary>On top of the jet a side lost: what shooting down a friend costs the shooter.</summary>
        public float FriendlyKillPenalty;
        /// <summary>Per gun round fired, per heat missile, per radar missile, per chaff bundle, per flare.</summary>
        public float Round, HeatMissile, RadarMissile, Chaff, Flare;
        /// <summary>Per kilogram of fuel burned.</summary>
        public float FuelPerKg;

        public void Validate()
        {
            if (!(Jet >= 0f && FriendlyKillPenalty >= 0f && Round >= 0f && HeatMissile >= 0f && RadarMissile >= 0f && Chaff >= 0f && Flare >= 0f && FuelPerKg >= 0f))
                throw new ArgumentException("prices: every price is zero or more (a cost is subtracted, a kill is added)");
        }
    }

    /// <summary>One jet's account (A23): what it used, what it did, and the credits each came to.</summary>
    public sealed class JetAccount
    {
        public int Rounds, HeatMissiles, RadarMissiles, Chaff, Flares;
        public float FuelKg;
        public int Kills, FriendlyKills, Deaths;
        /// <summary>Credits spent on rounds, missiles, decoys and fuel.</summary>
        public float Spent;
        /// <summary>Credits earned by enemy kills.</summary>
        public float Earned;
        /// <summary>Credits lost: its own jets, and the friends it shot down with their penalty.</summary>
        public float Lost;
        /// <summary>Earned − spent − lost.</summary>
        public float Net => Earned - Spent - Lost;
    }

    /// <summary>
    /// The price ledger (A23): after each tick it reads every aircraft's stores and what the core logged, and charges
    /// each jet what it used and credits what it did. It counts what left the jet (a store that went down by one is one
    /// fired), so it needs no hook in Air Wing; a respawn (the aircraft's reset count moves on) refills the stores and is
    /// not a refund. Judged on the truth, like the reward ledger. Pure and deterministic; call <see cref="Observe"/> once
    /// per tick, after the core has stepped. Shown on the scoreboard first; the reward later, on SIX's go.
    /// </summary>
    public sealed class PriceLedger
    {
        struct Stores { public int Resets, Rounds, Missiles, RadarMissiles, Chaff, Flares; public float FuelKg; public FlightStatus Status; }

        readonly PriceList _p;
        readonly Engagement _fight;
        readonly Dictionary<int, JetAccount> _accounts = new Dictionary<int, JetAccount>();
        readonly Dictionary<int, Stores> _last = new Dictionary<int, Stores>();
        int _killsSeen;
        long _lastTick = -1;

        public PriceLedger(in PriceList prices, Engagement fight)
        {
            prices.Validate();
            _p = prices;
            _fight = fight ?? throw new ArgumentNullException(nameof(fight));
        }

        public PriceList Prices => _p;

        /// <summary>A jet's account; an empty one for a jet not seen yet.</summary>
        public JetAccount Of(int id)
        {
            if (!_accounts.TryGetValue(id, out JetAccount a)) _accounts[id] = a = new JetAccount();
            return a;
        }

        /// <summary>A side's net credits: the sum of its jets' accounts (by the side each jet last fought on).</summary>
        public float SideNet(Side side)
        {
            float n = 0f;
            foreach (KeyValuePair<int, JetAccount> kv in _accounts)
                if (_fight.SideEver(kv.Key) == side) n += kv.Value.Net;
            return n;
        }

        /// <summary>After a tick: everything used and done in it, priced.</summary>
        public void Observe(FlightCore core)
        {
            long tick = core.Tick - 1;
            if (tick <= _lastTick) return;   // nothing stepped since the last call
            _lastTick = tick;

            IReadOnlyList<Aircraft> all = core.Aircraft;
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft jet = all[i];
                AirframeState s = jet.State;
                var now = new Stores
                {
                    Resets = jet.Resets, Rounds = s.Rounds, Missiles = s.Missiles, RadarMissiles = s.RadarMissiles,
                    Chaff = s.Chaff, Flares = s.Flares, FuelKg = s.FuelKg, Status = s.Status,
                };
                if (_last.TryGetValue(jet.Id, out Stores was) && was.Resets == now.Resets)
                {
                    JetAccount a = Of(jet.Id);
                    int rounds = Math.Max(0, was.Rounds - now.Rounds), heat = Math.Max(0, was.Missiles - now.Missiles);
                    int radar = Math.Max(0, was.RadarMissiles - now.RadarMissiles), chaff = Math.Max(0, was.Chaff - now.Chaff);
                    int flares = Math.Max(0, was.Flares - now.Flares);
                    float fuel = Math.Max(0f, was.FuelKg - now.FuelKg);
                    a.Rounds += rounds; a.HeatMissiles += heat; a.RadarMissiles += radar; a.Chaff += chaff; a.Flares += flares; a.FuelKg += fuel;
                    a.Spent += rounds * _p.Round + heat * _p.HeatMissile + radar * _p.RadarMissile + chaff * _p.Chaff + flares * _p.Flare + fuel * _p.FuelPerKg;
                    if (was.Status == FlightStatus.Flying && now.Status != FlightStatus.Flying)
                    {
                        a.Deaths++;
                        a.Lost += _p.Jet;   // shot down, crashed, collided or left the box: a jet is a jet
                    }
                }
                _last[jet.Id] = now;
            }

            // Who shot whom down: an enemy earns the jet, a friend costs the jet the side lost and the penalty.
            for (; _killsSeen < core.Kills.Count; _killsSeen++)
            {
                int shooter = core.Kills[_killsSeen].ShooterId, victim = core.Kills[_killsSeen].VictimId;
                if (shooter == 0 || shooter == victim) continue;
                JetAccount a = Of(shooter);
                Side ss = _fight.SideEver(shooter), vs = _fight.SideEver(victim);
                if (ss != Side.None && ss == vs)
                {
                    a.FriendlyKills++;
                    a.Lost += _p.Jet + _p.FriendlyKillPenalty;
                }
                else
                {
                    a.Kills++;
                    a.Earned += _p.Jet;
                }
            }
        }
    }
}
