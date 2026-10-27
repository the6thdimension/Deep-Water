using System;
using System.Collections.Generic;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.Gunnery.Guns;
using RH.Shared.Contracts;
using UnityEngine;

namespace DeepWater.AirWing.Core
{
    /// <summary>One aircraft shot down (Gunnery K5): when, by whom, with what.</summary>
    public readonly struct KillRecord
    {
        public readonly long Tick;
        public readonly int VictimId, ShooterId;
        public readonly string Weapon;

        public KillRecord(long tick, int victimId, int shooterId, string weapon)
        {
            Tick = tick;
            VictimId = victimId;
            ShooterId = shooterId;
            Weapon = weapon;
        }

        public override string ToString() => $"tick {Tick} · {ShooterId} shot down {VictimId} ({Weapon})";
    }

    /// <summary>
    /// The flight core's tick (BLUEPRINT §3.2): for each aircraft, in ascending id order, its seat issues this tick's
    /// Pilot Inputs, they go into the input log, and the aircraft steps its substeps; then the guns (Gunnery K4): the
    /// rounds already in flight fly the tick and are tested against every aircraft's path over it, hits take hit
    /// points and may kill, and each aircraft whose trigger is held fires this tick's rounds; then the tick's Fix.
    /// A Unity adapter calls <see cref="Step"/> from FixedUpdate (P2). Same aircraft, same seats issuing the same
    /// inputs, same Fix sequence (W7, W28).
    /// </summary>
    public sealed class FlightCore
    {
        readonly List<Aircraft> _aircraft = new List<Aircraft>();
        readonly List<ISeat> _seats = new List<ISeat>();
        readonly List<AircraftRecord> _records = new List<AircraftRecord>();
        readonly List<AircraftRecord> _picture = new List<AircraftRecord>();
        readonly ISurfaceTruth _surface;
        readonly IContactProbe _probe;
        readonly List<PilotInputs> _issued = new List<PilotInputs>();
        readonly List<GunTarget> _targets = new List<GunTarget>();
        readonly List<HitReport> _tickHits = new List<HitReport>();
        readonly List<HitReport> _hits = new List<HitReport>();
        readonly List<KillRecord> _kills = new List<KillRecord>();
        readonly DeterministicRng _gunDraws;

        /// <summary>Every round in flight (Gunnery). Presentation draws the tracers from it.</summary>
        public readonly RoundsInFlight Rounds = new RoundsInFlight();

        /// <summary>Every hit so far, in tick order.</summary>
        public IReadOnlyList<HitReport> Hits => _hits;

        /// <summary>Every aircraft shot down so far, in tick order.</summary>
        public IReadOnlyList<KillRecord> Kills => _kills;

        public readonly SubstepClock Clock;
        public readonly InputLog Inputs = new InputLog();
        public readonly FixLog Fixes = new FixLog();

        /// <summary>The next tick to run. Ticks count from 0.</summary>
        public long Tick { get; private set; }

        public IReadOnlyList<Aircraft> Aircraft => _aircraft;

        /// <summary>
        /// Every aircraft as the previous tick ended, the same for every seat this tick (RH Aviator A1). Seats are asked
        /// in id order, so reading <see cref="Aircraft"/> mid-tick would show some aircraft a tick ahead of others; a
        /// pilot reads this instead. Empty before the first tick.
        /// </summary>
        public IReadOnlyList<AircraftRecord> Picture => _picture;

        /// <summary>The truth feed's surface (sea or ground height) under any point.</summary>
        public ISurfaceTruth Surface => _surface;

        /// <summary>
        /// The flight core over <paramref name="surface"/>. The landing gear meets the ground through
        /// <paramref name="probe"/> (the scenes pass PhysX's); without one it uses the truth feed's level ground.
        /// </summary>
        public FlightCore(in SubstepClock clock, ISurfaceTruth surface, IContactProbe probe = null, ulong gunnerySeed = 1)
        {
            Clock = clock;
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _probe = probe ?? new SurfaceProbe(surface);
            _gunDraws = new DeterministicRng(gunnerySeed);
        }

        /// <summary>Add an aircraft and the seat that flies it. Ids must rise, so the tick order and the Fix are stable.</summary>
        public void Add(Aircraft aircraft, ISeat seat)
        {
            if (aircraft == null) throw new ArgumentNullException(nameof(aircraft));
            if (_aircraft.Count > 0 && aircraft.Id <= _aircraft[_aircraft.Count - 1].Id)
                throw new ArgumentException($"Aircraft ids must rise: {aircraft.Id} after {_aircraft[_aircraft.Count - 1].Id}.", nameof(aircraft));
            _aircraft.Add(aircraft);
            _seats.Add(seat ?? throw new ArgumentNullException(nameof(seat)));
        }

        /// <summary>Hand aircraft <paramref name="id"/> to another seat. Takes effect on the next tick (W18).</summary>
        public void SetSeat(int id, ISeat seat)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            _seats[IndexOf(id)] = seat;
        }

        public ISeat SeatOf(int id) => _seats[IndexOf(id)];

        public Aircraft Find(int id) => _aircraft[IndexOf(id)];

        int IndexOf(int id)
        {
            for (int i = 0; i < _aircraft.Count; i++)
                if (_aircraft[i].Id == id) return i;
            throw new KeyNotFoundException($"No aircraft {id}.");
        }

        public void Step()
        {
            long tick = Tick;
            // The picture for this tick: everyone where the last tick left them (A1).
            _picture.Clear();
            foreach (Aircraft a in _aircraft) _picture.Add(new AircraftRecord(a.Id, a.State));
            _issued.Clear();
            for (int i = 0; i < _aircraft.Count; i++)
            {
                Aircraft a = _aircraft[i];
                ISeat seat = _seats[i];
                InputEntry entry = Inputs.Append(tick, a.Id, seat.Kind, seat.Issue(tick, a.Id, a.State));
                float surface = _surface.HeightAt(a.State.Position.x, a.State.Position.z);
                a.Step(entry.Inputs, surface, _probe, Clock);
                _issued.Add(entry.Inputs);
            }
            Guns(tick);
            _records.Clear();
            foreach (Aircraft a in _aircraft) _records.Add(new AircraftRecord(a.Id, a.State));
            Fixes.Append(FlightFix.Of(tick, _records));
            Tick = tick + 1;
        }

        /// <summary>
        /// The guns' part of the tick. Rounds already in flight fly first, against every flying aircraft's path from the
        /// picture to now; then this tick's rounds leave the muzzles, to fly from the next tick. A round fired this tick
        /// therefore never hits on the tick it was fired, which keeps the order of firing out of who gets hit.
        /// </summary>
        void Guns(long tick)
        {
            float dt = (float)Clock.TickSeconds;
            _targets.Clear();
            for (int i = 0; i < _aircraft.Count; i++)
            {
                Aircraft a = _aircraft[i];
                if (a.State.Status != FlightStatus.Flying || !a.Profile.Combat.IsSet) continue;
                _targets.Add(new GunTarget { Id = a.Id, From = _picture[i].State.Position, To = a.State.Position, RadiusM = a.Profile.Combat.HitRadiusM });
            }
            _tickHits.Clear();
            if (Rounds.Live.Count > 0) Rounds.Step(tick, dt, _targets, _gunDraws, _tickHits);
            foreach (HitReport h in _tickHits)
            {
                _hits.Add(h);
                Aircraft target = Find(h.TargetId);
                if (target.State.Status != FlightStatus.Flying) continue;
                target.State.HitPoints -= h.Rounds;
                if (target.State.HitPoints <= 0f)
                {
                    target.State.HitPoints = 0f;
                    target.Kill(h.ShooterId, $"shot down by {h.ShooterId} ({h.Weapon})");
                    _kills.Add(new KillRecord(tick, h.TargetId, h.ShooterId, h.Weapon));
                }
            }

            for (int i = 0; i < _aircraft.Count; i++)
            {
                Aircraft a = _aircraft[i];
                ref AirframeState s = ref a.State;
                if (!a.Profile.Gun.IsSet || s.Status != FlightStatus.Flying || !_issued[i].Trigger || s.Rounds <= 0)
                {
                    s.GunCarry = 0f;   // a burst starts from a whole round
                    continue;
                }
                GunMount m = a.Profile.Gun;
                s.GunCarry += m.Gun.RoundsPerMinute / 60f * dt;
                int n = Mathf.Min((int)s.GunCarry, s.Rounds);
                s.GunCarry -= (int)s.GunCarry;
                if (n <= 0) continue;
                s.Rounds -= n;
                Rounds.Fire(a.Id, tick, m.Gun, s.Position + s.Attitude * m.MuzzleM, s.Velocity, s.Attitude * m.BoreBody, n);
            }
        }
    }
}
