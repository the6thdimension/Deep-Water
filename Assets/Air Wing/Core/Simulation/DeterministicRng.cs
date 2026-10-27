namespace DeepWater.AirWing.Core.Simulation
{
    /// <summary>
    /// Where a random outcome came from: the generator's seed and which draw it was, so a log line can name its
    /// luck ("seed 1442, draw 88") instead of hiding it.
    /// </summary>
    public readonly struct ChanceRoot
    {
        public readonly ulong Seed;
        public readonly long Draw;

        public ChanceRoot(ulong seed, long draw)
        {
            Seed = seed;
            Draw = draw;
        }

        public bool IsNone => Draw == 0;

        public override string ToString() => System.FormattableString.Invariant($"seed {Seed}, draw {Draw}");
    }

    /// <summary>
    /// The flight core's only source of randomness (P7; turbulence at Atmosphere L2): SplitMix64, integer-only, so
    /// the same seed gives the same sequence on every machine and runtime. Injected from the fidelity sheet; never
    /// global. Every draw is counted so an outcome can cite its <see cref="ChanceRoot"/>. The pattern is copied from
    /// General Quarters, not referenced (W31).
    /// </summary>
    public sealed class DeterministicRng : DeepWater.Gunnery.Guns.IUniformSource
    {
        ulong _state;

        public ulong Seed { get; }

        /// <summary>Draws taken so far. The next draw's index is <c>Draws + 1</c>.</summary>
        public long Draws { get; private set; }

        public DeterministicRng(ulong seed)
        {
            Seed = seed;
            _state = seed;
        }

        public ulong NextULong()
        {
            Draws++;
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform in [0, 1), 24 bits of resolution (exactly representable as a float).</summary>
        public float NextFloat01() => (NextULong() >> 40) * (1f / 16777216f);

        /// <summary>True with probability <paramref name="p"/>; <paramref name="root"/> names the draw.</summary>
        public bool Chance(float p, out ChanceRoot root)
        {
            float u = NextFloat01();
            root = new ChanceRoot(Seed, Draws);
            return u < p;
        }
    }
}
