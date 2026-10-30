using System;
using System.Collections.Generic;
using UnityEngine;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;

namespace DeepWater.Aviator.Core
{
    /// <summary>What a pilot believes about one other aircraft (W38): where it was when last seen, and how long ago.</summary>
    public struct Contact
    {
        public int Id;
        /// <summary>The state as the pilot last saw it (already the reaction delay old when it arrived).</summary>
        public AirframeState Seen;
        /// <summary>The tick the seen state belongs to (not the tick it arrived).</summary>
        public long SeenTick;
        /// <summary>In view on the last look (as of the delay ago).</summary>
        public bool InView;
        /// <summary>Ever seen.</summary>
        public bool Known;

        /// <summary>Where the pilot guesses it is now: the last sight carried on at its velocity.</summary>
        public Vector3 GuessPosition(long now, double tickSeconds) => Seen.Position + Seen.Velocity * (float)((now - SeenTick) * tickSeconds);
    }

    /// <summary>
    /// A pilot's eyes (W38). <b>Pilot L0</b> sees every aircraft as the last tick left it (the picture, A1).
    /// <b>Pilot L1</b> sees an aircraft only when it lay inside the pilot's field of view and visual range, as things
    /// stood the reaction delay ago, and remembers the last sight when it is lost (ageing; forgotten after the memory
    /// time). The delay is a ring of past pictures, so what the pilot "sees" now is exactly what was true then.
    /// Spotting by size (SENSORS-CONCEPT J8): a jet in view that the pilot does not know yet is first seen by a seeded
    /// draw each tick, at a rate that falls with distance and with how small it looks (nose-on is hardest; see
    /// <see cref="SpotRatePerS"/>); one it knows (seen, not yet forgotten) is seen again the moment it is in view.
    /// Pure and deterministic; no allocation after the first ticks.
    /// </summary>
    public sealed class Perception
    {
        readonly int _self;
        readonly int _delayTicks;
        readonly float _cosHalfFov, _rangeSq;
        readonly long _memoryTicks;
        readonly bool _truth;
        readonly PilotProfile _pilot;
        readonly float _tickS;
        readonly DeterministicRng _draws;
        readonly AircraftRecord[][] _ring;
        readonly int[] _ringCount;
        int _head = -1;
        long _lastTick = long.MinValue;
        readonly Dictionary<int, Contact> _contacts = new Dictionary<int, Contact>();
        readonly List<int> _ids = new List<int>();

        /// <param name="truth">Pilot L0: no field of view, no delay, no forgetting, no spotting chance.</param>
        /// <param name="seed">The fight's seed (<c>FlightCore.Seed</c>): each pilot's eyes draw from a stream of their own.</param>
        public Perception(int selfId, in PilotProfile p, double tickSeconds, bool truth, ulong seed = 1)
        {
            _self = selfId;
            _truth = truth;
            _pilot = p;
            _tickS = (float)Math.Round(tickSeconds, 6);
            _draws = new DeterministicRng(seed ^ 0x73706F7474696E67UL ^ ((ulong)(uint)selfId * 0x9E3779B97F4A7C15UL));
            _delayTicks = truth ? 0 : Math.Max(0, (int)Math.Round(p.ReactionDelayS / Math.Round(tickSeconds, 6)));
            _cosHalfFov = Mathf.Cos(Mathf.Clamp(p.FieldOfViewDeg, 0f, 180f) * Mathf.Deg2Rad);
            _rangeSq = p.VisualRangeM * p.VisualRangeM;
            _memoryTicks = (long)Math.Round(p.MemoryS / Math.Round(tickSeconds, 6));
            _ring = new AircraftRecord[_delayTicks + 1][];
            _ringCount = new int[_delayTicks + 1];
        }

        public int DelayTicks => _delayTicks;
        /// <summary>Every aircraft the pilot has ever seen (not itself), by id order.</summary>
        public IReadOnlyList<int> Known => _ids;

        public bool TryGet(int id, out Contact c) => _contacts.TryGetValue(id, out c) && c.Known;

        /// <summary>
        /// One look, on every tick: store this tick's picture; then judge the picture the delay ago. Call once per
        /// tick with the flight core's picture (A1). A tick skipped (the player flew the jet) clears the delay ring,
        /// since the pilot was not looking; what it remembers stays.
        /// </summary>
        public void Look(long tick, IReadOnlyList<AircraftRecord> picture)
        {
            if (_lastTick != long.MinValue && tick != _lastTick + 1) Array.Clear(_ringCount, 0, _ringCount.Length);
            _lastTick = tick;
            _head = (_head + 1) % _ring.Length;
            if (_ring[_head] == null || _ring[_head].Length < picture.Count) _ring[_head] = new AircraftRecord[Math.Max(8, picture.Count)];
            for (int i = 0; i < picture.Count; i++) _ring[_head][i] = picture[i];
            _ringCount[_head] = picture.Count;

            int then = (_head + 1) % _ring.Length;   // the oldest slot: the delay ago (the newest when there is no delay)
            if (_delayTicks == 0) then = _head;
            int n = _ringCount[then];
            if (n == 0) { Age(tick); return; }        // not looked long enough yet: nothing has arrived
            AircraftRecord[] seen = _ring[then];
            long seenTick = tick - _delayTicks;

            AirframeState me = default;
            bool haveMe = false;
            for (int i = 0; i < n; i++) if (seen[i].Id == _self) { me = seen[i].State; haveMe = true; break; }
            for (int i = 0; i < n; i++)
            {
                int id = seen[i].Id;
                if (id == _self) continue;
                AirframeState other = seen[i].State;
                _contacts.TryGetValue(id, out Contact c);
                bool visible = other.Status == FlightStatus.Flying && (_truth || (haveMe && InView(me, other) && (c.Known || Spots(me, other))));
                // Never seen and not in view: nothing to remember (slice 3d: storing it made an unseen jet "known").
                if (!visible && !c.Known) continue;
                c.Id = id;
                c.InView = visible;
                if (visible)
                {
                    if (!c.Known) Insert(id);
                    c.Known = true;
                    c.Seen = other;
                    c.SeenTick = seenTick;
                }
                _contacts[id] = c;
            }
            Age(tick);
        }

        bool InView(in AirframeState me, in AirframeState other)
        {
            Vector3 to = other.Position - me.Position;
            float d2 = to.sqrMagnitude;
            if (d2 > _rangeSq || d2 < 1e-4f) return d2 < 1e-4f;
            Vector3 nose = me.Attitude * Vector3.forward;
            return Vector3.Dot(nose, to) >= _cosHalfFov * Mathf.Sqrt(d2);
        }

        /// <summary>One draw: is a jet in view but not known spotted this tick (J8)?</summary>
        bool Spots(in AirframeState me, in AirframeState other)
        {
            if (_pilot.SpotRatePerS <= 0f) return true;
            float p = 1f - Mathf.Exp(-SpotRatePerS(_pilot, me.Position, other) * _tickS);
            return _draws.NextFloat01() < p;
        }

        /// <summary>
        /// How big <paramref name="other"/> looks from <paramref name="eye"/> against its beam view (J8): 1 from the
        /// side, the pilot's nose size head-on, its tail size from behind, blended by the square of the aspect's cosine.
        /// </summary>
        public static float ApparentSize(in PilotProfile p, Vector3 eye, in AirframeState other)
        {
            Vector3 to = eye - other.Position;
            float d = to.magnitude;
            if (d < 1e-3f) return 1f;
            float c = Vector3.Dot(other.Attitude * Vector3.forward, to / d);   // 1: it points at the eye, −1: away
            float end = c >= 0f ? p.SpotNoseSize : p.SpotTailSize;
            return 1f + (end - 1f) * c * c;
        }

        /// <summary>
        /// The rate per second at which a jet in view is first spotted (J8):
        /// SpotRatePerS / (1 + (d / (SpotHalfRangeM × size))²), the apparent area falling with the square of the distance.
        /// </summary>
        public static float SpotRatePerS(in PilotProfile p, Vector3 eye, in AirframeState other)
        {
            float d = (other.Position - eye).magnitude / (p.SpotHalfRangeM * ApparentSize(p, eye, other));
            return p.SpotRatePerS / (1f + d * d);
        }

        void Age(long tick)
        {
            if (_truth) return;
            for (int i = _ids.Count - 1; i >= 0; i--)
            {
                Contact c = _contacts[_ids[i]];
                if (!c.InView && tick - c.SeenTick > _memoryTicks)
                {
                    _contacts.Remove(_ids[i]);
                    _ids.RemoveAt(i);
                }
            }
        }

        void Insert(int id)
        {
            int at = _ids.BinarySearch(id);
            if (at < 0) _ids.Insert(~at, id);
        }
    }
}
