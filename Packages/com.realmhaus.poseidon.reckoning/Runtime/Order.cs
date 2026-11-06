using System;
using System.Collections.Generic;

namespace POSEIDON.Reckoning
{
    /// <summary>
    /// A command, stamped with the tick it applies on and who gave it. Captains, players and the god
    /// all act this way; the world never knows who is human. The payload stays private so nothing
    /// holding an Order can edit the Log: read it with <see cref="OpenPayload"/>.
    /// </summary>
    public readonly struct Order
    {
        public readonly long Tick;
        public readonly uint Issuer;
        public readonly long Sequence;
        public readonly uint TargetSystem;
        public readonly ushort Kind;
        private readonly byte[] _payload;

        internal Order(long tick, uint issuer, long sequence, uint targetSystem, ushort kind, byte[] payload)
        {
            Tick = tick;
            Issuer = issuer;
            Sequence = sequence;
            TargetSystem = targetSystem;
            Kind = kind;
            _payload = payload;
        }

        public int PayloadLength => _payload.Length;
        public StateReader OpenPayload() => new StateReader(_payload);
        internal byte[] PayloadBytes => _payload;

        /// <summary>Apply order within a tick: by issuer, then by the sequence the Log assigned.</summary>
        internal static int CompareKeys(in Order a, in Order b)
        {
            int c = a.Tick.CompareTo(b.Tick);
            if (c != 0) return c;
            c = a.Issuer.CompareTo(b.Issuer);
            return c != 0 ? c : a.Sequence.CompareTo(b.Sequence);
        }
    }

    /// <summary>
    /// The recording of every Order, kept sorted by (tick, issuer, sequence), plus what it takes to
    /// know which world it belongs to. Replay is: same scenario, same build, this Log.
    /// </summary>
    public sealed class OrderLog
    {
        private const uint Magic = 0x474C5250; // "PRLG"
        private const ushort FormatVersion = 1;

        private readonly List<Order> _orders = new List<Order>();
        private long _nextSequence;

        /// <param name="scenarioHash">Fingerprint of how the world started.</param>
        /// <param name="scenarioSeed">Root of every random draw. Replay needs it, so it travels with the Log.</param>
        /// <param name="buildId">Which build wrote this. A replay is only promised on the same build.</param>
        public OrderLog(ulong scenarioHash, ulong scenarioSeed, string buildId)
        {
            ScenarioHash = scenarioHash;
            ScenarioSeed = scenarioSeed;
            BuildId = buildId ?? throw new ArgumentNullException(nameof(buildId));
        }

        public ulong ScenarioHash { get; }
        public ulong ScenarioSeed { get; }
        public string BuildId { get; }
        public int Count => _orders.Count;
        public Order this[int index] => _orders[index];

        /// <summary>
        /// Adds an Order and returns it with its sequence assigned. The payload is copied, so the
        /// caller cannot change a recorded Order afterwards. Normally reached through
        /// <see cref="ReckonedWorld.Issue"/>, which also refuses ticks already past.
        /// </summary>
        public Order Append(long tick, uint issuer, uint targetSystem, ushort kind, byte[] payload = null)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "An Order cannot apply before tick 0.");

            byte[] copy = payload == null ? Array.Empty<byte>() : (byte[])payload.Clone();
            var order = new Order(tick, issuer, _nextSequence++, targetSystem, kind, copy);
            _orders.Insert(UpperBound(order), order);
            return order;
        }

        /// <summary>Index of the first Order applying at or after this tick.</summary>
        public int FirstIndexAtOrAfter(long tick)
        {
            int lo = 0, hi = _orders.Count;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (_orders[mid].Tick < tick) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        public byte[] ToBytes()
        {
            var w = new StateWriter(64 + _orders.Count * 32);
            w.WriteUInt(Magic);
            w.WriteUShort(FormatVersion);
            w.WriteULong(ScenarioHash);
            w.WriteULong(ScenarioSeed);
            w.WriteString(BuildId);
            w.WriteLong(_nextSequence);
            w.WriteInt(_orders.Count);
            foreach (var order in _orders)
            {
                w.WriteLong(order.Tick);
                w.WriteUInt(order.Issuer);
                w.WriteLong(order.Sequence);
                w.WriteUInt(order.TargetSystem);
                w.WriteUShort(order.Kind);
                w.WriteBytes(order.PayloadBytes);
            }
            w.WriteChecksum();
            return w.ToArray();
        }

        public static OrderLog FromBytes(byte[] bytes)
        {
            var r = StateReader.ForChecked(bytes, "OrderLog");
            uint magic = r.ReadUInt();
            if (magic != Magic) throw new ReckoningFormatException($"OrderLog: bad magic {magic:X8}.");
            ushort version = r.ReadUShort();
            if (version != FormatVersion) throw new ReckoningFormatException($"OrderLog: format {version}, this build reads {FormatVersion}.");

            var log = new OrderLog(r.ReadULong(), r.ReadULong(), r.ReadString());
            log._nextSequence = r.ReadLong();
            int count = r.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var order = new Order(r.ReadLong(), r.ReadUInt(), r.ReadLong(), r.ReadUInt(), r.ReadUShort(), r.ReadBytes());
                if (i > 0 && Order.CompareKeys(log._orders[i - 1], order) >= 0)
                    throw new ReckoningFormatException($"OrderLog: Order {i} is out of key order.");
                log._orders.Add(order);
            }
            if (!r.AtEnd) throw new ReckoningFormatException($"OrderLog: {r.Remaining} bytes left after the last Order.");
            return log;
        }

        private int UpperBound(in Order order)
        {
            int lo = 0, hi = _orders.Count;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (Order.CompareKeys(_orders[mid], order) <= 0) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }
    }
}
