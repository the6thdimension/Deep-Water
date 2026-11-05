using System;
using System.Collections.Generic;

namespace POSEIDON.Reckoning
{
    /// <summary>A system's rate: it runs on every Nth tick (ticks 0, N, 2N, ...).</summary>
    public readonly struct Watch
    {
        public readonly int Every;

        private Watch(int every) => Every = every;

        public static Watch EveryTicks(int ticks)
        {
            if (ticks < 1) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "A Watch is a whole number of ticks, at least 1.");
            return new Watch(ticks);
        }

        public bool IsDue(long tick) => tick % Every == 0;

        /// <summary>The first due tick at or after this one.</summary>
        public long NextDueAtOrAfter(long tick)
        {
            long remainder = tick % Every;
            return remainder == 0 ? tick : tick + (Every - remainder);
        }
    }

    /// <summary>
    /// The one clock: an integer tick counter, never a float of seconds, so time cannot drift or lose
    /// precision over a long run. Systems run in the order they were registered, and that order is
    /// part of the rules: write it down once, in one place, in the code that builds the world.
    /// </summary>
    public sealed class Chronometer
    {
        /// <summary>
        /// 20 ms, 50 Hz. A law of the core rather than a tuning value: every Watch, Log and Fix is
        /// counted in these ticks, so changing it would change every saved run. That is why it is
        /// here and not in a ScriptableObject.
        /// </summary>
        public const int BaseTickMicroseconds = 20000;
        public const int TicksPerSecond = 1000000 / BaseTickMicroseconds;

        private struct Entry
        {
            public IReckonedSystem System;
            public Watch Watch;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private bool _sealed;

        /// <summary>The next tick to run. Ticks before this have happened.</summary>
        public long Now { get; private set; }

        public int SystemCount => _entries.Count;
        public IReckonedSystem SystemAt(int index) => _entries[index].System;
        public Watch WatchAt(int index) => _entries[index].Watch;

        /// <summary>Adds a system to the end of the written order.</summary>
        public void Register(IReckonedSystem system, Watch watch)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (_sealed) throw new InvalidOperationException($"Cannot register {system.Name}: the world has started, and the written order is fixed.");
            if (watch.Every < 1) throw new ArgumentException("Use Watch.EveryTicks; a default Watch has no rate.", nameof(watch));
            if (system.SystemId == ReckonedWorld.SchedulerSystemId) throw new ArgumentException($"{system.Name}: system id {ReckonedWorld.SchedulerSystemId} is reserved for the event scheduler.", nameof(system));
            foreach (var entry in _entries)
                if (entry.System.SystemId == system.SystemId)
                    throw new ArgumentException($"{system.Name} repeats system id {system.SystemId}, already used by {entry.System.Name}.", nameof(system));

            _entries.Add(new Entry { System = system, Watch = watch });
        }

        /// <summary>Fixes the written order. Registering after this is an error.</summary>
        public void Seal() => _sealed = true;

        /// <summary>Runs every system due on this tick, in the written order, then moves to the next tick.</summary>
        public void Step(TickContext context)
        {
            context.Tick = Now;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Watch.IsDue(Now)) entry.System.Step(context);
            }
            Now++;
        }

        /// <summary>The first tick at or after <see cref="Now"/> on which any system is due; null with no systems.</summary>
        public long? NextDue()
        {
            long? next = null;
            for (int i = 0; i < _entries.Count; i++)
            {
                long due = _entries[i].Watch.NextDueAtOrAfter(Now);
                if (next == null || due < next) next = due;
            }
            return next;
        }

        /// <summary>Jumps forward over idle ticks. Jumping past a due system would lose its step, so that throws.</summary>
        public void SkipTo(long tick)
        {
            if (tick < Now) throw new ArgumentOutOfRangeException(nameof(tick), tick, $"The Chronometer only moves forward; it is at {Now}.");
            if (NextDue() is long due && due < tick) throw new InvalidOperationException($"Cannot skip to tick {tick}: a system is due on tick {due}.");
            Now = tick;
        }

        /// <summary>Puts the clock back to a snapshot's tick.</summary>
        internal void RestoreTo(long tick)
        {
            if (tick < 0) throw new ReckoningFormatException($"Negative tick {tick} in a snapshot.");
            Now = tick;
        }
    }
}
