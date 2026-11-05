using System;
using System.Collections.Generic;

namespace POSEIDON.Reckoning
{
    /// <summary>
    /// Puts the core together: the Chronometer, the scheduler and the Log, with a Fix after every
    /// tick. One tick T runs in a fixed order:
    /// <list type="number">
    /// <item>Orders stamped T, in key order.</item>
    /// <item>Events due on T, in key order.</item>
    /// <item>Systems due on T, in the written order.</item>
    /// <item>The Fix for T.</item>
    /// </list>
    /// The world is built the same way every time: construct it, register the systems in the written
    /// order, then step. A restore needs that same construction first.
    /// </summary>
    public sealed class ReckonedWorld
    {
        /// <summary>The scheduler's slot in Fix traces. Registered systems use ids above this.</summary>
        public const uint SchedulerSystemId = 0;

        private const string SchedulerName = "EventScheduler";
        private const uint SnapshotMagic = 0x4E535250; // "PRSN"
        private const ushort SnapshotVersion = 1;

        private readonly Chronometer _chronometer = new Chronometer();
        private readonly EventScheduler _scheduler = new EventScheduler();
        private readonly List<CauseRecord> _causes = new List<CauseRecord>();
        private readonly TickContext _context;

        private bool _sealed;
        private int _orderCursor;
        private ulong[] _fingerprints;
        private FixTrace _trace;

        public ReckonedWorld(OrderLog log)
        {
            Log = log ?? throw new ArgumentNullException(nameof(log));
            _context = new TickContext(log.ScenarioSeed, _scheduler, _causes);
        }

        public OrderLog Log { get; }
        public long Now => _chronometer.Now;

        /// <summary>Ticks that actually ran, as opposed to being skipped over.</summary>
        public long TicksExecuted { get; private set; }

        /// <summary>
        /// Reasons recorded so far. Derived from the run and outside the Fix, so a restored world
        /// holds only those since its restore; replay from tick 0 for the whole chain.
        /// </summary>
        public IReadOnlyList<CauseRecord> Causes => _causes;

        /// <summary>The recording in progress, or null. Start one with <see cref="StartTrace"/>.</summary>
        public FixTrace Trace => _trace;

        /// <summary>Adds a system to the end of the written order.</summary>
        public void Register(IReckonedSystem system, Watch watch) => _chronometer.Register(system, watch);

        /// <summary>Begins recording a Fix for each tick from <see cref="Now"/> on.</summary>
        public FixTrace StartTrace()
        {
            SealIfNeeded();
            int count = _chronometer.SystemCount + 1;
            var ids = new uint[count];
            var names = new string[count];
            for (int i = 0; i < _chronometer.SystemCount; i++)
            {
                ids[i] = _chronometer.SystemAt(i).SystemId;
                names[i] = _chronometer.SystemAt(i).Name;
            }
            ids[count - 1] = SchedulerSystemId;
            names[count - 1] = SchedulerName;
            _trace = new FixTrace(ids, names);
            return _trace;
        }

        /// <summary>
        /// Gives an Order: the only way to change the world from outside. It is recorded in the Log,
        /// and it must apply on the current tick or later, because the past is already reckoned.
        /// </summary>
        public Order Issue(long tick, uint issuer, uint targetSystem, ushort kind, byte[] payload = null)
        {
            if (tick < Now) throw new ArgumentOutOfRangeException(nameof(tick), tick, $"Tick {tick} has already run; the world is at {Now}.");
            FindSystem(targetSystem);
            return Log.Append(tick, issuer, targetSystem, kind, payload);
        }

        /// <summary>Runs one tick.</summary>
        public void Step()
        {
            SealIfNeeded();
            long tick = Now;
            _context.Tick = tick;

            // An Order behind the clock can only mean the Log was edited under a running world.
            // Skipping it would quietly lose an input, so stop here instead.
            if (_orderCursor < Log.Count && Log[_orderCursor].Tick < tick)
                throw new InvalidOperationException($"The Log holds an Order for tick {Log[_orderCursor].Tick}, but the world is at {tick}. Use Issue to give Orders to a running world.");

            while (_orderCursor < Log.Count && Log[_orderCursor].Tick == tick)
            {
                var order = Log[_orderCursor++];
                FindSystem(order.TargetSystem).ApplyOrder(order, _context);
            }

            while (_scheduler.TryPopDue(tick, out var due))
                FindSystem(due.TargetSystem).OnEvent(due, _context);

            _chronometer.Step(_context);
            TicksExecuted++;

            ComputeFingerprints();
            _trace?.Add(tick, CombineFix(tick), _fingerprints);
        }

        /// <summary>Runs tick by tick until <see cref="Now"/> reaches this tick.</summary>
        public void StepTo(long tick)
        {
            while (Now < tick) Step();
        }

        /// <summary>
        /// The next tick with anything to do: a system's Watch, a pending event or a logged Order.
        /// Null if nothing will ever be due.
        /// </summary>
        public long? NextDueTick()
        {
            SealIfNeeded();
            long? next = _chronometer.NextDue();
            if (_scheduler.NextTick is long eventTick && (next == null || eventTick < next)) next = eventTick;
            if (_orderCursor < Log.Count)
            {
                long orderTick = Log[_orderCursor].Tick;
                if (next == null || orderTick < next) next = orderTick;
            }
            return next;
        }

        /// <summary>
        /// Jumps over idle ticks to the next due tick, or to <paramref name="limit"/> if that is
        /// sooner, and returns where it landed. The tick landed on has not run yet.
        ///
        /// Why this gives the same Fix as stepping: on an idle tick nothing runs, so no state can
        /// change, and the Fix for that tick is just the same fingerprints under a new tick number.
        /// That also makes the jump cheap with no trace; with one, the idle ticks are filled in so
        /// two traces still compare tick for tick.
        /// </summary>
        public long AdvanceToNextDue(long limit)
        {
            if (limit < Now) throw new ArgumentOutOfRangeException(nameof(limit), limit, $"The world is at {Now}.");

            long? due = NextDueTick();
            long target = due == null || due > limit ? limit : due.Value;

            if (_trace != null)
                for (long idle = Now; idle < target; idle++)
                    _trace.Add(idle, CombineFix(idle), _fingerprints);

            _chronometer.SkipTo(target);
            return target;
        }

        /// <summary>The Fix of the state right now, labelled with the last tick processed (-1 before any).</summary>
        public ulong CurrentFix()
        {
            SealIfNeeded();
            return CombineFix(Now - 1);
        }

        /// <summary>
        /// Saves system and scheduler state with the current Fix. It does not hold the Log;
        /// <see cref="Save"/> pairs the two.
        /// </summary>
        public byte[] SaveSnapshot()
        {
            SealIfNeeded();
            var w = new StateWriter(1024);
            w.WriteUInt(SnapshotMagic);
            w.WriteUShort(SnapshotVersion);
            w.WriteULong(Log.ScenarioHash);
            w.WriteString(Log.BuildId);
            w.WriteLong(Now);
            w.WriteULong(CurrentFix());

            w.WriteInt(_chronometer.SystemCount);
            for (int i = 0; i < _chronometer.SystemCount; i++)
            {
                var system = _chronometer.SystemAt(i);
                var state = new StateWriter();
                system.SaveState(state);
                w.WriteUInt(system.SystemId);
                w.WriteBytes(state.ToArray());
            }

            var schedulerState = new StateWriter();
            _scheduler.SaveState(schedulerState);
            w.WriteBytes(schedulerState.ToArray());

            w.WriteChecksum();
            return w.ToArray();
        }

        /// <summary>
        /// Loads a snapshot into a world built with the same systems. Afterwards it recomputes the
        /// Fix and compares it with the one saved: a system that forgets some state in its save
        /// fails here, instead of quietly replaying into a different world. A world that throws here
        /// may be half-restored and should be thrown away.
        /// </summary>
        public void RestoreSnapshot(byte[] snapshot)
        {
            SealIfNeeded();
            var r = StateReader.ForChecked(snapshot, "Snapshot");
            uint magic = r.ReadUInt();
            if (magic != SnapshotMagic) throw new ReckoningFormatException($"Snapshot: bad magic {magic:X8}.");
            ushort version = r.ReadUShort();
            if (version != SnapshotVersion) throw new ReckoningFormatException($"Snapshot: format {version}, this build reads {SnapshotVersion}.");

            ulong scenarioHash = r.ReadULong();
            if (scenarioHash != Log.ScenarioHash) throw new ReckoningFormatException($"Snapshot is of scenario {scenarioHash:X16}, this world's Log is of {Log.ScenarioHash:X16}.");
            string buildId = r.ReadString();
            if (buildId != Log.BuildId) throw new ReckoningFormatException($"Snapshot is from build '{buildId}', this world's Log is from '{Log.BuildId}'.");

            long tick = r.ReadLong();
            ulong savedFix = r.ReadULong();

            int count = r.ReadInt();
            if (count != _chronometer.SystemCount) throw new ReckoningFormatException($"Snapshot holds {count} systems, this world has {_chronometer.SystemCount}.");
            for (int i = 0; i < count; i++)
            {
                var system = _chronometer.SystemAt(i);
                uint id = r.ReadUInt();
                if (id != system.SystemId) throw new ReckoningFormatException($"Snapshot slot {i} is system {id}; the written order has {system.Name} ({system.SystemId}) there.");
                var state = new StateReader(r.ReadBytes());
                system.RestoreState(state);
                if (!state.AtEnd) throw new ReckoningFormatException($"{system.Name} left {state.Remaining} bytes of its saved state unread.");
            }

            var schedulerState = new StateReader(r.ReadBytes());
            _scheduler.RestoreState(schedulerState);
            if (!schedulerState.AtEnd) throw new ReckoningFormatException($"The scheduler left {schedulerState.Remaining} bytes of its saved state unread.");
            if (!r.AtEnd) throw new ReckoningFormatException($"Snapshot: {r.Remaining} bytes left over.");

            _chronometer.RestoreTo(tick);
            _orderCursor = Log.FirstIndexAtOrAfter(tick);
            ComputeFingerprints();

            ulong restoredFix = CurrentFix();
            if (restoredFix != savedFix)
                throw new ReckoningFormatException($"Restored Fix {restoredFix:X16} differs from the saved {savedFix:X16} at tick {tick}: a system's save omits state its Fix covers, or its restore is wrong.");

            _trace = null;
            _causes.Clear();
        }

        /// <summary>Save = snapshot + Log.</summary>
        public ReckoningSave Save() => new ReckoningSave(SaveSnapshot(), Log.ToBytes());

        private void SealIfNeeded()
        {
            if (_sealed) return;
            _sealed = true;
            _chronometer.Seal();
            _fingerprints = new ulong[_chronometer.SystemCount + 1];
            ComputeFingerprints();
        }

        private IReckonedSystem FindSystem(uint systemId)
        {
            // A plain walk: systems number in the tens, and this runs per Order and per event.
            for (int i = 0; i < _chronometer.SystemCount; i++)
            {
                var system = _chronometer.SystemAt(i);
                if (system.SystemId == systemId) return system;
            }
            throw new InvalidOperationException($"No system {systemId} is registered in this world.");
        }

        private void ComputeFingerprints()
        {
            for (int i = 0; i < _chronometer.SystemCount; i++)
            {
                var hasher = FixHasher.Start();
                _chronometer.SystemAt(i).WriteFix(ref hasher);
                _fingerprints[i] = hasher.Value;
            }

            var schedulerHasher = FixHasher.Start();
            _scheduler.WriteFix(ref schedulerHasher);
            _fingerprints[_chronometer.SystemCount] = schedulerHasher.Value;
        }

        private ulong CombineFix(long tick)
        {
            var hasher = FixHasher.Start();
            hasher.AddLong(tick);
            for (int i = 0; i < _fingerprints.Length; i++) hasher.AddULong(_fingerprints[i]);
            return hasher.Value;
        }
    }

    /// <summary>A whole save: a snapshot and the Log that carries on from it.</summary>
    public sealed class ReckoningSave
    {
        private const uint Magic = 0x56535250; // "PRSV"

        public ReckoningSave(byte[] snapshot, byte[] logBytes)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            LogBytes = logBytes ?? throw new ArgumentNullException(nameof(logBytes));
        }

        public byte[] Snapshot { get; }
        public byte[] LogBytes { get; }

        public byte[] ToBytes()
        {
            var w = new StateWriter(Snapshot.Length + LogBytes.Length + 32);
            w.WriteUInt(Magic);
            w.WriteBytes(Snapshot);
            w.WriteBytes(LogBytes);
            w.WriteChecksum();
            return w.ToArray();
        }

        public static ReckoningSave FromBytes(byte[] bytes)
        {
            var r = StateReader.ForChecked(bytes, "Save");
            uint magic = r.ReadUInt();
            if (magic != Magic) throw new ReckoningFormatException($"Save: bad magic {magic:X8}.");
            var save = new ReckoningSave(r.ReadBytes(), r.ReadBytes());
            if (!r.AtEnd) throw new ReckoningFormatException($"Save: {r.Remaining} bytes left over.");
            return save;
        }
    }
}
