using System;

namespace POSEIDON.Reckoning
{
    public enum CauseLinkKind : byte
    {
        Order,
        Event,
        Draw,
        Scenario,
    }

    /// <summary>
    /// One link in a cause chain. Every chain ends at an Order (who gave it), the scenario (how the
    /// world started) or chance (which draw). What the fields mean depends on the kind:
    /// Order: tick, issuer, sequence. Event: tick, source system, entity, sequence.
    /// Draw: tick, rule, entity, draw index.
    /// </summary>
    public readonly struct CauseLink
    {
        public readonly CauseLinkKind Kind;
        public readonly long Tick;
        public readonly uint Source;
        public readonly uint Entity;
        public readonly long Sequence;

        private CauseLink(CauseLinkKind kind, long tick, uint source, uint entity, long sequence)
        {
            Kind = kind;
            Tick = tick;
            Source = source;
            Entity = entity;
            Sequence = sequence;
        }

        public static CauseLink ToOrder(in Order order) => new CauseLink(CauseLinkKind.Order, order.Tick, order.Issuer, 0, order.Sequence);
        public static CauseLink ToEvent(in ScheduledEvent e) => new CauseLink(CauseLinkKind.Event, e.Tick, e.SourceSystem, e.Entity, e.Sequence);
        public static CauseLink ToDraw(in DrawId draw) => new CauseLink(CauseLinkKind.Draw, draw.Tick, draw.Rule, draw.Entity, draw.Index);
        public static CauseLink ToScenario() => new CauseLink(CauseLinkKind.Scenario, 0, 0, 0, 0);
    }

    /// <summary>A number a rule used, by name: "wake contrast", 0.42.</summary>
    public readonly struct CauseNumber
    {
        public readonly string Name;
        public readonly double Value;

        public CauseNumber(string name, double value)
        {
            Name = name;
            Value = value;
        }
    }

    /// <summary>
    /// Why a notable thing happened: on which tick, which rule, the numbers it used, and the Orders,
    /// events and draws behind them. A stub on purpose. It exists so rule packs record their reasons
    /// from day one; the cause graph, retention and the reading surface are Inquiry's job later.
    /// Records are for notable events (thresholds crossed, discrete happenings), not per-tick state.
    /// </summary>
    public sealed class CauseRecord
    {
        public CauseRecord(long tick, uint ruleId, uint entity, CauseNumber[] numbers, CauseLink[] links)
        {
            Tick = tick;
            RuleId = ruleId;
            Entity = entity;
            Numbers = numbers ?? Array.Empty<CauseNumber>();
            Links = links ?? Array.Empty<CauseLink>();
        }

        public long Tick { get; }
        public uint RuleId { get; }
        public uint Entity { get; }
        public CauseNumber[] Numbers { get; }
        public CauseLink[] Links { get; }
    }
}
