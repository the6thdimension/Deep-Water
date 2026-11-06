using System;

namespace POSEIDON.Reckoning
{
    /// <summary>
    /// The full name of one random draw. The value comes from this name and the scenario seed
    /// alone, so a draw can be quoted in an Inquiry ("gust, chance: rule 7, entity 3, tick 900,
    /// draw 0") and recomputed later without replaying anything.
    /// </summary>
    public readonly struct DrawId : IEquatable<DrawId>
    {
        public readonly uint Rule;
        public readonly uint Entity;
        public readonly long Tick;
        public readonly uint Index;

        public DrawId(uint rule, uint entity, long tick, uint index)
        {
            Rule = rule;
            Entity = entity;
            Tick = tick;
            Index = index;
        }

        public bool Equals(DrawId other) => Rule == other.Rule && Entity == other.Entity && Tick == other.Tick && Index == other.Index;
        public override bool Equals(object obj) => obj is DrawId other && Equals(other);
        public override int GetHashCode() => unchecked((int)Rule * 31 + (int)Entity * 17 + Tick.GetHashCode() * 13 + (int)Index);
        public override string ToString() => $"rule {Rule}, entity {Entity}, tick {Tick}, draw {Index}";
    }

    /// <summary>
    /// Counter-based randomness: there is no generator object and no state to advance. Each draw
    /// hashes (seed, rule, entity, tick, index). Old way vs this way: with a stateful generator, one
    /// extra call anywhere shifts every later number in the run; here a draw cannot notice who else
    /// drew, or in what order. Mixing is SplitMix64's finalizer, applied once per field.
    /// </summary>
    public static class ReckonedRandom
    {
        private const ulong Gamma = 0x9E3779B97F4A7C15UL; // SplitMix64's increment
        private const double TwoToMinus53 = 1.0 / (1UL << 53);

        /// <summary>SplitMix64's finalizer: a bijection on 64 bits that scatters nearby inputs.</summary>
        public static ulong Mix64(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>The 64 raw bits behind a draw.</summary>
        public static ulong Bits(ulong seed, in DrawId id)
        {
            unchecked
            {
                ulong h = Mix64(seed + Gamma);
                h = Mix64((h ^ id.Rule) + Gamma);
                h = Mix64((h ^ id.Entity) + Gamma);
                h = Mix64((h ^ (ulong)id.Tick) + Gamma);
                h = Mix64((h ^ id.Index) + Gamma);
                return h;
            }
        }

        /// <summary>Uniform in [0, 1).</summary>
        public static double Uniform(ulong seed, in DrawId id) => (Bits(seed, id) >> 11) * TwoToMinus53;

        /// <summary>
        /// Uniform integer in [min, maxExclusive), without modulo bias. A value in the biased top
        /// sliver re-mixes its own bits and tries again; that stays stateless, because the retry is
        /// a function of the same DrawId.
        /// </summary>
        public static int Range(ulong seed, in DrawId id, int min, int maxExclusive)
        {
            if (maxExclusive <= min) throw new ArgumentOutOfRangeException(nameof(maxExclusive), $"Empty range [{min}, {maxExclusive}).");

            ulong span = (ulong)((long)maxExclusive - min);
            ulong highestAccepted = ulong.MaxValue - (ulong.MaxValue % span + 1) % span;
            ulong bits = Bits(seed, id);
            while (bits > highestAccepted) bits = unchecked(Mix64(bits + Gamma));
            return (int)((long)min + (long)(bits % span));
        }

        /// <summary>
        /// Standard normal by inverse CDF. Takes exactly one hash per draw (Box-Muller wants two
        /// numbers and trig; Marsaglia's polar method rejects a varying number of times), so every
        /// normal is one named DrawId. Accuracy is that of <see cref="ReckonedMath.InverseNormalCdf"/>.
        /// </summary>
        public static double Normal(ulong seed, in DrawId id)
        {
            // Bucket midpoints, so p is never exactly 0 or 1.
            double p = ((Bits(seed, id) >> 11) + 0.5) * TwoToMinus53;
            return ReckonedMath.InverseNormalCdf(p);
        }
    }
}
