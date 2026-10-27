using System;
using System.Collections.Generic;

namespace DeepWater.Aviator.Core
{
    /// <summary>What the pilot is doing (W36). Slice 3a has the first two, 3b Join and Fingertip, 3c Home and Landed; Fight follows.</summary>
    public enum PilotMode : byte
    {
        /// <summary>Flying a route, gate by gate.</summary>
        Navigate = 0,
        /// <summary>Route done, or nothing to do: holding a height and a speed on the present heading.</summary>
        HoldCourse = 1,
        /// <summary>Closing on a leader's wing (W40).</summary>
        Join = 2,
        /// <summary>Holding the fingertip slot on a leader (W40).</summary>
        Fingertip = 3,
        /// <summary>Going home: the initial, the break, the pattern and the landing, leg by leg (W41, W47).</summary>
        Home = 4,
        /// <summary>Stopped on the runway.</summary>
        Landed = 5,
    }

    /// <summary>One rule firing (C19): when, whose, which rule, the mode change, and why in words.</summary>
    public readonly struct RuleFiring : IEquatable<RuleFiring>
    {
        public readonly long Tick;
        public readonly int AircraftId;
        public readonly string Rule;
        public readonly PilotMode From, To;
        public readonly string Reason;

        public RuleFiring(long tick, int aircraftId, string rule, PilotMode from, PilotMode to, string reason)
        {
            Tick = tick;
            AircraftId = aircraftId;
            Rule = rule;
            From = from;
            To = to;
            Reason = reason;
        }

        public bool Equals(RuleFiring o) =>
            Tick == o.Tick && AircraftId == o.AircraftId && Rule == o.Rule && From == o.From && To == o.To && Reason == o.Reason;

        public override bool Equals(object obj) => obj is RuleFiring o && Equals(o);
        public override int GetHashCode() => unchecked((int)Tick * 31 + AircraftId);

        public override string ToString() =>
            $"tick {Tick} · aircraft {AircraftId} · {Rule}: {From}{(From != To ? " → " + To : "")} ({Reason})";
    }

    /// <summary>Every rule that fired, in order (W36, C19). Append-only; two runs from one start give the same log.</summary>
    public sealed class RuleLog
    {
        readonly List<RuleFiring> _firings = new List<RuleFiring>();

        public IReadOnlyList<RuleFiring> Firings => _firings;
        public int Count => _firings.Count;

        /// <summary>Raised on each firing, for an adapter that prints it (Observable); the core never needs it.</summary>
        public event Action<RuleFiring> Fired;

        public void Add(in RuleFiring firing)
        {
            _firings.Add(firing);
            Fired?.Invoke(firing);
        }

        /// <summary>The index of the first firing where two logs differ, or −1 when they match.</summary>
        public int FirstDivergence(RuleLog other)
        {
            int n = Math.Min(Count, other.Count);
            for (int i = 0; i < n; i++)
                if (!_firings[i].Equals(other._firings[i])) return i;
            return Count == other.Count ? -1 : n;
        }
    }

    /// <summary>The brain's decision moments (W44): <c>hz</c> times a second, timed by the tick count so a run repeats them.</summary>
    public static class Watch
    {
        /// <summary>
        /// True on tick 0 and on each tick where ⌊tick × hz × tickSeconds⌋ changes. The tick length is rounded to the
        /// microsecond first: the core's clock is built from a float (0.02f is 0.0199999995 s), and without the rounding
        /// a watch due at tick 1200 fell on tick 1201 (slice 3a; METHODOLOGY AP26, a float clock deciding an edge).
        /// </summary>
        public static bool IsWatch(long tick, double tickSeconds, int hz)
        {
            if (tick <= 0) return true;
            double dt = Math.Round(tickSeconds, 6);
            return Math.Floor(tick * hz * dt + 1e-9) != Math.Floor((tick - 1) * hz * dt + 1e-9);
        }
    }
}
