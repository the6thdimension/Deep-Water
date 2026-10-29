using System;
using System.Collections.Generic;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using UnityEngine;

namespace DeepWater.AirWing.Core
{
    /// <summary>
    /// The arena's rules (W59–W61), all AUTHORED; a zero turns its rule off, so a core with no arena, or an arena of
    /// zeros, flies exactly as before.
    /// </summary>
    public struct ArenaSettings
    {
        /// <summary>
        /// Seconds after an aircraft leaves the fight (shot down, crashed, collided or out of the box) before it comes
        /// back at its spawn slot (W59). Zero: at once, on the next tick.
        /// </summary>
        public float RespawnDelayS;
        /// <summary>Seconds after a respawn in which the aircraft can't be hit and can't fire (W59).</summary>
        public float ProtectionS;
        /// <summary>
        /// A respawn comes back at its slot's height ± this, metres, and on its slot's heading ± <see cref="JitterHeadingDeg"/>,
        /// both drawn from the arena's own seed, so a learner never sees one start and a run still repeats.
        /// </summary>
        public float JitterHeightM, JitterHeadingDeg;
        /// <summary>Two flying aircraft whose paths over one tick pass within this, metres, both go down (W60). Zero: none.</summary>
        public float CollisionM;
        /// <summary>The box's centre, metres. Only x and z count: the box is a column from the sea up.</summary>
        public Vector3 CentreM;
        /// <summary>
        /// The box's half-widths, east-west (x) and north-south (z), metres. A flying aircraft outside it goes down (W61).
        /// Zero: no box.
        /// </summary>
        public float HalfWidthXM, HalfWidthZM;

        public void Validate()
        {
            Check(RespawnDelayS, nameof(RespawnDelayS));
            Check(ProtectionS, nameof(ProtectionS));
            Check(JitterHeightM, nameof(JitterHeightM));
            Check(JitterHeadingDeg, nameof(JitterHeadingDeg));
            Check(CollisionM, nameof(CollisionM));
            Check(HalfWidthXM, nameof(HalfWidthXM));
            Check(HalfWidthZM, nameof(HalfWidthZM));
            if (JitterHeadingDeg > 180f) throw new ArgumentException($"arena: {nameof(JitterHeadingDeg)} {JitterHeadingDeg} is more than half a turn");
            if ((HalfWidthXM > 0f) != (HalfWidthZM > 0f)) throw new ArgumentException("arena: the box needs both half-widths, or neither");
        }

        static void Check(float v, string name)
        {
            if (float.IsNaN(v) || float.IsInfinity(v) || v < 0f) throw new ArgumentException($"arena: {name} is {v}; it must be zero or more");
        }
    }

    public enum ArenaEventKind : byte
    {
        /// <summary>Back at its spawn slot, protected.</summary>
        Respawned = 0,
        /// <summary>Its path passed within the collision distance of another's; both went down (<see cref="ArenaEvent.OtherId"/>).</summary>
        Collided = 1,
        /// <summary>Flew out of the box and went down.</summary>
        LeftArena = 2,
    }

    /// <summary>One thing the arena did, on a tick, to an aircraft.</summary>
    public readonly struct ArenaEvent
    {
        public readonly long Tick;
        public readonly int AircraftId, OtherId;
        public readonly ArenaEventKind Kind;

        public ArenaEvent(long tick, int aircraftId, ArenaEventKind kind, int otherId)
        {
            Tick = tick;
            AircraftId = aircraftId;
            Kind = kind;
            OtherId = otherId;
        }

        public override string ToString() => Kind switch
        {
            ArenaEventKind.Respawned => $"tick {Tick} · {AircraftId} respawned",
            ArenaEventKind.Collided => $"tick {Tick} · {AircraftId} collided with {OtherId}",
            _ => $"tick {Tick} · {AircraftId} left the arena",
        };
    }

    /// <summary>
    /// The arena (W59–W61): the rules a fight that never stops needs. An aircraft with a spawn slot that leaves the fight
    /// comes back at its slot after the delay, jittered, with full rounds and hit points, and can't be hit or fire while
    /// it is protected. Two aircraft that meet both go down, and so does one that leaves the box. The flight core runs it
    /// inside the tick (<see cref="FlightCore.UseArena"/>): respawns before the picture, collisions and the box after the
    /// guns. Like a hit, it changes an aircraft outside the Pilot Inputs (W6's exception for game events); like a hit, it
    /// is pure and on the tick count, so the same seats, seeds and arena give the same Fix on every tick.
    /// </summary>
    public sealed class Arena
    {
        readonly ArenaSettings _s;
        readonly long _delayTicks, _protectTicks;
        readonly DeterministicRng _rng;
        readonly Dictionary<int, AirframeState> _slots = new Dictionary<int, AirframeState>();
        readonly Dictionary<int, long> _outSince = new Dictionary<int, long>();
        readonly Dictionary<int, long> _protectedUntil = new Dictionary<int, long>();
        readonly Dictionary<int, int> _respawns = new Dictionary<int, int>();
        readonly List<ArenaEvent> _events = new List<ArenaEvent>();
        readonly List<(int a, int b)> _met = new List<(int, int)>();

        public Arena(in ArenaSettings settings, double tickSeconds, ulong seed)
        {
            settings.Validate();
            if (!(tickSeconds > 0.0)) throw new ArgumentOutOfRangeException(nameof(tickSeconds));
            _s = settings;
            double dt = Math.Round(tickSeconds, 6);   // AP26: whole ticks from a rounded step, never a float clock
            _delayTicks = (long)Math.Round(settings.RespawnDelayS / dt);
            _protectTicks = (long)Math.Round(settings.ProtectionS / dt);
            _rng = new DeterministicRng(seed);
        }

        public ArenaSettings Settings => _s;
        public long DelayTicks => _delayTicks;
        public long ProtectionTicks => _protectTicks;
        /// <summary>Everything the arena did, in tick order.</summary>
        public IReadOnlyList<ArenaEvent> Events => _events;

        /// <summary>
        /// Give aircraft <paramref name="aircraftId"/> its spawn slot: the state it comes back in (before the jitter),
        /// usually the state it was built with. Without a slot it is never respawned; the collision and box rules still
        /// apply to it.
        /// </summary>
        public void Assign(int aircraftId, in AirframeState slot)
        {
            if (slot.Status != FlightStatus.Flying) throw new ArgumentException($"aircraft {aircraftId}: a spawn slot must be a flying state", nameof(slot));
            _slots[aircraftId] = slot;
        }

        public bool HasSlot(int aircraftId) => _slots.ContainsKey(aircraftId);

        /// <summary>True while <paramref name="aircraftId"/> is protected on <paramref name="tick"/>: it can't be hit and can't fire.</summary>
        public bool Protected(int aircraftId, long tick) => _protectedUntil.TryGetValue(aircraftId, out long until) && tick < until;

        /// <summary>How many times <paramref name="aircraftId"/> has respawned.</summary>
        public int RespawnsOf(int aircraftId) => _respawns.TryGetValue(aircraftId, out int n) ? n : 0;

        /// <summary>
        /// Put <paramref name="aircraft"/> back at its slot now, jittered and protected, whatever it is doing (a reset of
        /// the whole fight calls it for everyone). Logged as a respawn on <paramref name="tick"/>, the tick it flies from.
        /// </summary>
        public void Respawn(Aircraft aircraft, long tick)
        {
            if (aircraft == null) throw new ArgumentNullException(nameof(aircraft));
            if (!_slots.TryGetValue(aircraft.Id, out AirframeState s))
                throw new InvalidOperationException($"aircraft {aircraft.Id} has no spawn slot");
            // Two draws every time, so one aircraft's jitter never shifts the next one's sequence by a rule turned off.
            float dh = 2f * _rng.NextFloat01() - 1f, dpsi = 2f * _rng.NextFloat01() - 1f;
            s.Position.y += dh * _s.JitterHeightM;
            // Turning the whole level state about the vertical keeps it trimmed: attitude and velocity turn together.
            Quaternion turn = Quaternion.AngleAxis(dpsi * _s.JitterHeadingDeg, Vector3.up);
            s.Attitude = turn * s.Attitude;
            s.Velocity = turn * s.Velocity;
            aircraft.Respawn(s);
            _outSince.Remove(aircraft.Id);
            _protectedUntil[aircraft.Id] = tick + _protectTicks;
            _respawns[aircraft.Id] = RespawnsOf(aircraft.Id) + 1;
            _events.Add(new ArenaEvent(tick, aircraft.Id, ArenaEventKind.Respawned, 0));
        }

        /// <summary>The start of the tick, before the picture: everyone out for the delay comes back.</summary>
        internal void BeforeTick(long tick, IReadOnlyList<Aircraft> aircraft)
        {
            for (int i = 0; i < aircraft.Count; i++)
            {
                Aircraft a = aircraft[i];
                if (!_slots.ContainsKey(a.Id)) continue;
                if (a.State.Status == FlightStatus.Flying) { _outSince.Remove(a.Id); continue; }   // a reset brought it back
                if (!_outSince.TryGetValue(a.Id, out long since)) _outSince[a.Id] = since = tick;
                if (tick - since >= _delayTicks) Respawn(a, tick);
            }
        }

        /// <summary>
        /// After the guns: collisions over the tick's paths (from the picture to now), then the box. Every pair is found
        /// before anyone goes down, so the order of the aircraft never decides who survives a collision.
        /// </summary>
        internal void AfterTick(long tick, IReadOnlyList<Aircraft> aircraft, IReadOnlyList<AircraftRecord> picture)
        {
            if (_s.CollisionM > 0f)
            {
                _met.Clear();
                float limitSq = _s.CollisionM * _s.CollisionM;
                for (int i = 0; i < aircraft.Count; i++)
                {
                    if (aircraft[i].State.Status != FlightStatus.Flying) continue;
                    for (int j = i + 1; j < aircraft.Count; j++)
                    {
                        if (aircraft[j].State.Status != FlightStatus.Flying) continue;
                        if (ClosestApproachSq(picture[i].State.Position, aircraft[i].State.Position,
                                              picture[j].State.Position, aircraft[j].State.Position) < limitSq)
                            _met.Add((i, j));
                    }
                }
                foreach ((int i, int j) in _met)
                {
                    Aircraft a = aircraft[i], b = aircraft[j];
                    a.Kill(0, $"collided with {b.Id}");
                    b.Kill(0, $"collided with {a.Id}");
                    _events.Add(new ArenaEvent(tick, a.Id, ArenaEventKind.Collided, b.Id));
                    _events.Add(new ArenaEvent(tick, b.Id, ArenaEventKind.Collided, a.Id));
                }
            }

            if (_s.HalfWidthXM > 0f)
                for (int i = 0; i < aircraft.Count; i++)
                {
                    Aircraft a = aircraft[i];
                    if (a.State.Status != FlightStatus.Flying) continue;
                    Vector3 p = a.State.Position;
                    if (Mathf.Abs(p.x - _s.CentreM.x) <= _s.HalfWidthXM && Mathf.Abs(p.z - _s.CentreM.z) <= _s.HalfWidthZM) continue;
                    a.Kill(0, "left the arena");
                    _events.Add(new ArenaEvent(tick, a.Id, ArenaEventKind.LeftArena, 0));
                }

            // The delay counts from the tick an aircraft went out, whatever took it out (a hit, the sea, a collision).
            for (int i = 0; i < aircraft.Count; i++)
            {
                Aircraft a = aircraft[i];
                if (a.State.Status != FlightStatus.Flying && _slots.ContainsKey(a.Id) && !_outSince.ContainsKey(a.Id)) _outSince[a.Id] = tick;
            }
        }

        /// <summary>
        /// The squared closest distance between two aircraft moving in straight lines over a tick, A from <paramref name="a0"/>
        /// to <paramref name="a1"/> and B from <paramref name="b0"/> to <paramref name="b1"/>: |r0 + (r1 − r0)s|² least over
        /// s in [0, 1], r the separation. Two jets closing at 800 m/s cover 16 m a tick, so ends alone would miss a pass.
        /// </summary>
        public static float ClosestApproachSq(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
        {
            Vector3 r0 = b0 - a0, dr = (b1 - a1) - r0;
            float dd = dr.sqrMagnitude;
            float s = dd > 1e-9f ? Mathf.Clamp01(-Vector3.Dot(r0, dr) / dd) : 0f;
            return (r0 + dr * s).sqrMagnitude;
        }
    }
}
