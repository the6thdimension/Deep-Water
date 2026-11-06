using System;
using System.Collections.Generic;

namespace POSEIDON.Reckoning
{
    /// <summary>
    /// Something a system asked to have happen on a later tick. Ties break on the key alone:
    /// (tick, phase, source system, entity, sequence). Sequence is the scheduler's own counter, so
    /// the key is unique and the order never depends on a sort's whim.
    /// </summary>
    public readonly struct ScheduledEvent
    {
        public readonly long Tick;
        public readonly int Phase;
        public readonly uint SourceSystem;
        public readonly uint Entity;
        public readonly long Sequence;
        public readonly uint TargetSystem;
        public readonly ushort Kind;
        public readonly long Data;

        internal ScheduledEvent(long tick, int phase, uint sourceSystem, uint entity, long sequence, uint targetSystem, ushort kind, long data)
        {
            Tick = tick;
            Phase = phase;
            SourceSystem = sourceSystem;
            Entity = entity;
            Sequence = sequence;
            TargetSystem = targetSystem;
            Kind = kind;
            Data = data;
        }

        internal static int CompareKeys(in ScheduledEvent a, in ScheduledEvent b)
        {
            int c = a.Tick.CompareTo(b.Tick);
            if (c != 0) return c;
            c = a.Phase.CompareTo(b.Phase);
            if (c != 0) return c;
            c = a.SourceSystem.CompareTo(b.SourceSystem);
            if (c != 0) return c;
            c = a.Entity.CompareTo(b.Entity);
            return c != 0 ? c : a.Sequence.CompareTo(b.Sequence);
        }
    }

    /// <summary>
    /// The pending events, always sorted. A sorted list instead of a heap on purpose: its layout is
    /// canonical, so the Fix and a snapshot can walk it directly, where a heap's internal shape would
    /// first need sorting. Insert is a memory move, and the earliest event sits at the list's end so
    /// popping is cheap. Worth revisiting only if a real scenario shows a long queue in the Profiler.
    /// </summary>
    public sealed class EventScheduler
    {
        private readonly List<ScheduledEvent> _latestFirst = new List<ScheduledEvent>();
        private long _nextSequence;

        public int PendingCount => _latestFirst.Count;

        /// <summary>The tick of the earliest pending event, or null with none pending.</summary>
        public long? NextTick => _latestFirst.Count == 0 ? (long?)null : _latestFirst[_latestFirst.Count - 1].Tick;

        /// <summary>Schedules an event. It must lie strictly in the future, which keeps firing finite.</summary>
        public ScheduledEvent Schedule(long now, long tick, int phase, uint sourceSystem, uint entity, uint targetSystem, ushort kind, long data = 0)
        {
            if (tick <= now) throw new ArgumentOutOfRangeException(nameof(tick), tick, $"Events must be scheduled after the current tick {now}.");

            var scheduled = new ScheduledEvent(tick, phase, sourceSystem, entity, _nextSequence++, targetSystem, kind, data);
            _latestFirst.Insert(InsertionIndex(scheduled), scheduled);
            return scheduled;
        }

        /// <summary>Removes and returns the earliest event due at or before this tick, if any.</summary>
        public bool TryPopDue(long tick, out ScheduledEvent due)
        {
            int last = _latestFirst.Count - 1;
            if (last < 0 || _latestFirst[last].Tick > tick)
            {
                due = default;
                return false;
            }
            due = _latestFirst[last];
            _latestFirst.RemoveAt(last);
            return true;
        }

        internal void WriteFix(ref FixHasher hasher)
        {
            hasher.AddLong(_nextSequence);
            hasher.AddInt(_latestFirst.Count);
            for (int i = 0; i < _latestFirst.Count; i++)
            {
                var e = _latestFirst[i];
                hasher.AddLong(e.Tick);
                hasher.AddInt(e.Phase);
                hasher.AddUInt(e.SourceSystem);
                hasher.AddUInt(e.Entity);
                hasher.AddLong(e.Sequence);
                hasher.AddUInt(e.TargetSystem);
                hasher.AddUInt(e.Kind);
                hasher.AddLong(e.Data);
            }
        }

        internal void SaveState(StateWriter w)
        {
            w.WriteLong(_nextSequence);
            w.WriteInt(_latestFirst.Count);
            foreach (var e in _latestFirst)
            {
                w.WriteLong(e.Tick);
                w.WriteInt(e.Phase);
                w.WriteUInt(e.SourceSystem);
                w.WriteUInt(e.Entity);
                w.WriteLong(e.Sequence);
                w.WriteUInt(e.TargetSystem);
                w.WriteUShort(e.Kind);
                w.WriteLong(e.Data);
            }
        }

        internal void RestoreState(StateReader r)
        {
            _nextSequence = r.ReadLong();
            int count = r.ReadInt();
            _latestFirst.Clear();
            for (int i = 0; i < count; i++)
                _latestFirst.Add(new ScheduledEvent(r.ReadLong(), r.ReadInt(), r.ReadUInt(), r.ReadUInt(), r.ReadLong(), r.ReadUInt(), r.ReadUShort(), r.ReadLong()));
        }

        private int InsertionIndex(in ScheduledEvent e)
        {
            // Binary search in a list sorted latest key first.
            int lo = 0, hi = _latestFirst.Count;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (ScheduledEvent.CompareKeys(_latestFirst[mid], e) > 0) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }
    }
}
