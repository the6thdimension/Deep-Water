using System.Collections.Generic;
using DeepWater.GeneralQuarters.Core.Simulation;

namespace DeepWater.GeneralQuarters.Core.Log
{
    public enum EventKind : byte
    {
        Note = 0,
        FidelityFallback = 1,
        OrderApplied = 2,
        DetectionGained = 3,
        DetectionLost = 4,
        TrackCreated = 5,
        TrackDropped = 6,
        Proposed = 7,
        Assigned = 8,
        Launched = 9,
        BurstFired = 10,
        Kill = 11,
        Miss = 12,
        ShipHit = 13,
        SystemOffline = 14,
        /// <summary>A seat-holder's decision: the AI captain applying one of its named rules.</summary>
        Decision = 15,
    }

    /// <summary>Where a cause chain ends (Reckoning §4.10): an Order, the scenario, or a seeded draw.</summary>
    public enum RootKind : byte
    {
        None = 0,
        Order = 1,
        Scenario = 2,
        Chance = 3,
    }

    /// <summary>
    /// One notable event: what happened, which rule made it happen, the sentence built from that rule's
    /// own numbers, and links to the events and root that caused it. Only notable events are recorded:
    /// thresholds crossed and discrete happenings, never every tick.
    /// </summary>
    public struct EngagementEvent
    {
        public int Id;
        public long Tick;
        public EventKind Kind;
        /// <summary>The rule that produced it, e.g. "TEWA L0 range bands".</summary>
        public string Rule;
        /// <summary>One plain sentence from the rule's numbers (invariant culture).</summary>
        public string Text;
        /// <summary>The track concerned, or -1.</summary>
        public int TrackId;
        /// <summary>Up to three causing events (-1 when none).</summary>
        public int CauseA, CauseB, CauseC;
        public RootKind Root;
        /// <summary>The Order's sequence number when <see cref="Root"/> is Order.</summary>
        public int OrderSequence;
        public ChanceRoot Chance;

        public override string ToString() => $"[{Id}] t{Tick} {Kind}: {Text}";
    }

    /// <summary>
    /// The combat core's cause records: Reckoning's Inquiry at L0. Append-only; ids are indices.
    /// </summary>
    public sealed class EngagementLog
    {
        readonly List<EngagementEvent> _events = new List<EngagementEvent>();

        public IReadOnlyList<EngagementEvent> Events => _events;

        public int Add(long tick, EventKind kind, string rule, string text, int trackId = -1,
            int causeA = -1, int causeB = -1, RootKind root = RootKind.None, int orderSequence = -1, ChanceRoot chance = default,
            int causeC = -1)
        {
            int id = _events.Count;
            _events.Add(new EngagementEvent
            {
                Id = id, Tick = tick, Kind = kind, Rule = rule, Text = text, TrackId = trackId,
                CauseA = causeA, CauseB = causeB, CauseC = causeC, Root = root, OrderSequence = orderSequence, Chance = chance,
            });
            return id;
        }

        public int Count(EventKind kind)
        {
            int n = 0;
            for (int i = 0; i < _events.Count; i++) if (_events[i].Kind == kind) n++;
            return n;
        }

        /// <summary>
        /// The event and every event behind it, depth first, each listed once. A complete chain ends in
        /// events whose <see cref="EngagementEvent.Root"/> is set; <see cref="IsComplete"/> checks that.
        /// </summary>
        public List<EngagementEvent> Chain(int id)
        {
            var chain = new List<EngagementEvent>();
            var seen = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(id);
            while (stack.Count > 0)
            {
                int e = stack.Pop();
                if (e < 0 || e >= _events.Count || !seen.Add(e)) continue;
                EngagementEvent ev = _events[e];
                chain.Add(ev);
                if (ev.CauseC >= 0) stack.Push(ev.CauseC);
                if (ev.CauseB >= 0) stack.Push(ev.CauseB);
                if (ev.CauseA >= 0) stack.Push(ev.CauseA);
            }
            return chain;
        }

        /// <summary>True when every leaf of the chain (an event with no causes) names a root.</summary>
        public bool IsComplete(int id)
        {
            foreach (EngagementEvent ev in Chain(id))
                if (ev.CauseA < 0 && ev.CauseB < 0 && ev.CauseC < 0 && ev.Root == RootKind.None) return false;
            return true;
        }
    }
}
