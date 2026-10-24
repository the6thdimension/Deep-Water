using System;

namespace DeepWater.GeneralQuarters.Core.Simulation
{
    /// <summary>
    /// The phases of one combat tick, in the order they always run (BLUEPRINT §3.2). The order is part of
    /// the rules: changing it changes outcomes, and the Fix test would catch it.
    /// </summary>
    public enum CombatPhase : byte
    {
        Orders = 0,
        Sensors = 1,
        Tracks = 2,
        Tewa = 3,
        FireControl = 4,
        WeaponsInFlight = 5,
        DamageControl = 6,
        Fix = 7,
    }

    /// <summary>
    /// How often a system runs: every <see cref="PeriodTicks"/> ticks, starting at <see cref="OffsetTicks"/>.
    /// A whole-number multiple of the base tick (Reckoning R15), so rates stay exact and reproducible.
    /// </summary>
    public readonly struct Watch
    {
        public readonly int PeriodTicks;
        public readonly int OffsetTicks;

        public Watch(int periodTicks, int offsetTicks = 0)
        {
            if (periodTicks < 1) throw new ArgumentOutOfRangeException(nameof(periodTicks), periodTicks, "a Watch period is at least one tick");
            if (offsetTicks < 0 || offsetTicks >= periodTicks)
                throw new ArgumentOutOfRangeException(nameof(offsetTicks), offsetTicks, "the offset must lie inside the period");
            PeriodTicks = periodTicks;
            OffsetTicks = offsetTicks;
        }

        public static Watch EveryTick => new Watch(1);

        /// <summary>A Watch running at about <paramref name="hz"/>, rounded to whole ticks of <paramref name="tickSeconds"/>.</summary>
        public static Watch FromRate(float hz, float tickSeconds) =>
            new Watch(Math.Max(1, (int)Math.Round(1.0 / (hz * tickSeconds))));

        public bool IsDue(long tick) => tick >= OffsetTicks && (tick - OffsetTicks) % PeriodTicks == 0;
    }
}
