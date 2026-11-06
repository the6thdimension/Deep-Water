using System;
using System.Collections.Generic;

namespace POSEIDON.Reckoning
{
    /// <summary>
    /// FNV-1a 64 over raw bytes: the fingerprint behind every Fix. Doubles go in as their raw bits,
    /// so "same Fix" means identical bits, not merely close values. Start one with
    /// <see cref="Start"/>; a default-constructed hasher has no offset basis.
    /// </summary>
    public struct FixHasher
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private ulong _hash;

        public static FixHasher Start() => new FixHasher { _hash = OffsetBasis };

        public ulong Value => _hash;

        public void AddByte(byte value)
        {
            _hash = unchecked((_hash ^ value) * Prime);
        }

        public void AddBool(bool value) => AddByte(value ? (byte)1 : (byte)0);

        public void AddUInt(uint value)
        {
            for (int shift = 0; shift < 32; shift += 8) AddByte((byte)(value >> shift));
        }

        public void AddInt(int value) => AddUInt(unchecked((uint)value));

        public void AddULong(ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8) AddByte((byte)(value >> shift));
        }

        public void AddLong(long value) => AddULong(unchecked((ulong)value));

        public void AddDouble(double value) => AddLong(BitConverter.DoubleToInt64Bits(value));

        public void AddBytes(byte[] bytes, int offset, int count)
        {
            for (int i = offset; i < offset + count; i++) AddByte(bytes[i]);
        }

        public static ulong Of(byte[] bytes, int offset, int count)
        {
            var hasher = Start();
            hasher.AddBytes(bytes, offset, count);
            return hasher.Value;
        }
    }

    /// <summary>
    /// A recording of Fixes, tick by tick: the world Fix plus one fingerprint per system. Two of
    /// these are what <see cref="FixDiff"/> compares. Flat lists rather than one object per tick,
    /// so a long recording costs one allocation per list doubling, not one per tick.
    /// </summary>
    public sealed class FixTrace
    {
        private readonly uint[] _systemIds;
        private readonly string[] _systemNames;
        private readonly List<long> _ticks = new List<long>();
        private readonly List<ulong> _worldFixes = new List<ulong>();
        private readonly List<ulong> _systemFixes = new List<ulong>();

        internal FixTrace(uint[] systemIds, string[] systemNames)
        {
            _systemIds = systemIds;
            _systemNames = systemNames;
        }

        public int Count => _ticks.Count;
        public int SystemCount => _systemIds.Length;

        public long TickAt(int index) => _ticks[index];
        public ulong WorldFixAt(int index) => _worldFixes[index];
        public ulong SystemFixAt(int index, int system) => _systemFixes[index * _systemIds.Length + system];
        public uint SystemIdAt(int system) => _systemIds[system];
        public string SystemNameAt(int system) => _systemNames[system];

        internal void Add(long tick, ulong worldFix, ulong[] systemFixes)
        {
            _ticks.Add(tick);
            _worldFixes.Add(worldFix);
            for (int i = 0; i < systemFixes.Length; i++) _systemFixes.Add(systemFixes[i]);
        }
    }

    public enum FixDiffOutcome
    {
        /// <summary>Every tick both traces hold matched.</summary>
        Identical,
        /// <summary>A tick held by both traces differs; the result names the tick and system.</summary>
        Diverged,
        /// <summary>The traces share no tick, so nothing was compared.</summary>
        NoOverlap,
        /// <summary>The traces list different systems, so per-system fingerprints do not line up.</summary>
        DifferentSystems,
    }

    /// <summary>The answer from <see cref="FixDiff.Compare"/>.</summary>
    public readonly struct FixDiffResult
    {
        public readonly FixDiffOutcome Outcome;
        public readonly int TicksCompared;
        public readonly long Tick;
        public readonly uint SystemId;
        public readonly string SystemName;
        public readonly ulong FixA;
        public readonly ulong FixB;

        internal FixDiffResult(FixDiffOutcome outcome, int ticksCompared, long tick = -1, uint systemId = 0, string systemName = null, ulong fixA = 0, ulong fixB = 0)
        {
            Outcome = outcome;
            TicksCompared = ticksCompared;
            Tick = tick;
            SystemId = systemId;
            SystemName = systemName;
            FixA = fixA;
            FixB = fixB;
        }

        public override string ToString()
        {
            switch (Outcome)
            {
                case FixDiffOutcome.Identical: return $"Identical over {TicksCompared} ticks.";
                case FixDiffOutcome.NoOverlap: return "No tick in common; nothing compared.";
                case FixDiffOutcome.DifferentSystems: return "The traces list different systems; nothing compared.";
                default: return $"Diverged at tick {Tick} in {SystemName} (system {SystemId}): {FixA:X16} vs {FixB:X16}, after {TicksCompared} matching ticks.";
            }
        }
    }

    /// <summary>
    /// Names the first tick and system where two runs part ways. Within a tick, systems are checked
    /// in the written order, so a value that spreads from one system to the next is blamed on the
    /// earliest one: that is where to start looking.
    /// </summary>
    public static class FixDiff
    {
        public static FixDiffResult Compare(FixTrace a, FixTrace b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (a.SystemCount != b.SystemCount) return new FixDiffResult(FixDiffOutcome.DifferentSystems, 0);
            for (int s = 0; s < a.SystemCount; s++)
                if (a.SystemIdAt(s) != b.SystemIdAt(s))
                    return new FixDiffResult(FixDiffOutcome.DifferentSystems, 0);

            // The traces can start at different ticks (a restored run starts at its snapshot), so
            // walk both and compare only ticks they share.
            int ia = 0, ib = 0, compared = 0;
            while (ia < a.Count && ib < b.Count)
            {
                long tickA = a.TickAt(ia), tickB = b.TickAt(ib);
                if (tickA < tickB) { ia++; continue; }
                if (tickB < tickA) { ib++; continue; }

                for (int s = 0; s < a.SystemCount; s++)
                {
                    ulong fixA = a.SystemFixAt(ia, s), fixB = b.SystemFixAt(ib, s);
                    if (fixA != fixB)
                        return new FixDiffResult(FixDiffOutcome.Diverged, compared, tickA, a.SystemIdAt(s), a.SystemNameAt(s), fixA, fixB);
                }

                // The world Fix is a pure function of the tick and the fingerprints just compared.
                // If it differs anyway, the recording itself is wrong; say that, don't pass it.
                if (a.WorldFixAt(ia) != b.WorldFixAt(ib))
                    throw new InvalidOperationException($"Tick {tickA}: every system fingerprint matches but the world Fix differs. The Fix combiner is broken.");

                compared++;
                ia++;
                ib++;
            }

            return compared == 0
                ? new FixDiffResult(FixDiffOutcome.NoOverlap, 0)
                : new FixDiffResult(FixDiffOutcome.Identical, compared);
        }
    }
}
