using System;

namespace DeepWater.AirWing.Core.Simulation
{
    /// <summary>
    /// The flight core's clock (W24). The project ticks at 50 Hz and this module never changes that; inside each
    /// tick the core takes <see cref="Substeps"/> equal fixed substeps, so a fast jet's pitch motion stays stable.
    /// Time is computed from the whole tick count, never accumulated, so it cannot drift.
    /// </summary>
    public readonly struct SubstepClock
    {
        public const int MaxSubsteps = 16;

        /// <summary>One tick, seconds (the adapter passes Unity's fixed timestep in).</summary>
        public readonly double TickSeconds;
        public readonly int Substeps;
        /// <summary>One substep, seconds: exactly <see cref="TickSeconds"/> / <see cref="Substeps"/>.</summary>
        public readonly double SubstepSeconds;

        public SubstepClock(double tickSeconds, int substeps)
        {
            if (!(tickSeconds > 0) || double.IsInfinity(tickSeconds))
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "a tick must be a positive, finite time");
            if (substeps < 1 || substeps > MaxSubsteps)
                throw new ArgumentOutOfRangeException(nameof(substeps), substeps, $"substeps must be 1 … {MaxSubsteps}");
            TickSeconds = tickSeconds;
            Substeps = substeps;
            SubstepSeconds = tickSeconds / substeps;
        }

        /// <summary>Simulation time at the start of <paramref name="tick"/>, seconds.</summary>
        public double TimeAt(long tick) => tick * TickSeconds;

        /// <summary>Simulation time at the start of <paramref name="substep"/> within <paramref name="tick"/>, seconds.</summary>
        public double TimeAt(long tick, int substep) => tick * TickSeconds + substep * SubstepSeconds;
    }
}
