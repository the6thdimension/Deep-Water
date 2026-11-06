using System;
using System.Collections.Generic;

namespace POSEIDON.Reckoning
{
    /// <summary>
    /// A simulation system the core can run, fingerprint and save. All of its outcome-changing state
    /// must pass through <see cref="WriteFix"/> and <see cref="SaveState"/>: state left out of the Fix
    /// can drift unseen, and state left out of a snapshot makes restore lie. The core catches the
    /// second case (see <see cref="ReckonedWorld.RestoreSnapshot"/>).
    /// </summary>
    public interface IReckonedSystem
    {
        /// <summary>Stable id, above 0, used in keys, draws and saves. Never reuse one for a different system.</summary>
        uint SystemId { get; }

        string Name { get; }

        /// <summary>An Order addressed to this system, on the tick it applies.</summary>
        void ApplyOrder(in Order order, TickContext context);

        /// <summary>An event addressed to this system has come due.</summary>
        void OnEvent(in ScheduledEvent scheduledEvent, TickContext context);

        /// <summary>One step of the system's rules. Runs only on ticks its Watch makes due.</summary>
        void Step(TickContext context);

        /// <summary>Adds the raw bits of all outcome-changing state.</summary>
        void WriteFix(ref FixHasher hasher);

        void SaveState(StateWriter writer);

        /// <summary>Must read exactly what <see cref="SaveState"/> wrote; leftover bytes are an error.</summary>
        void RestoreState(StateReader reader);
    }

    /// <summary>
    /// What a system may reach while it runs: the tick, named random draws, the scheduler and the
    /// cause record. Passed in rather than found, which keeps every rule testable without a world
    /// and leaves no global clock or generator to reach for by accident.
    /// </summary>
    public sealed class TickContext
    {
        private readonly EventScheduler _scheduler;
        private readonly List<CauseRecord> _causes;

        public TickContext(ulong seed, EventScheduler scheduler, List<CauseRecord> causes)
        {
            Seed = seed;
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _causes = causes ?? throw new ArgumentNullException(nameof(causes));
        }

        public ulong Seed { get; }

        /// <summary>The tick now running.</summary>
        public long Tick { get; internal set; }

        public double DrawUniform(uint rule, uint entity, uint index) => ReckonedRandom.Uniform(Seed, new DrawId(rule, entity, Tick, index));

        public int DrawRange(uint rule, uint entity, uint index, int min, int maxExclusive) => ReckonedRandom.Range(Seed, new DrawId(rule, entity, Tick, index), min, maxExclusive);

        public double DrawNormal(uint rule, uint entity, uint index) => ReckonedRandom.Normal(Seed, new DrawId(rule, entity, Tick, index));

        /// <summary>Schedules an event on a later tick.</summary>
        public ScheduledEvent Schedule(long tick, int phase, uint sourceSystem, uint entity, uint targetSystem, ushort kind, long data = 0)
        {
            return _scheduler.Schedule(Tick, tick, phase, sourceSystem, entity, targetSystem, kind, data);
        }

        public void RecordCause(CauseRecord record)
        {
            _causes.Add(record ?? throw new ArgumentNullException(nameof(record)));
        }
    }
}
