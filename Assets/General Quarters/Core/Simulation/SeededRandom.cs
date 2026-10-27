using System;

namespace DeepWater.GeneralQuarters.Core.Simulation
{
    /// <summary>
    /// The run's <see cref="DeterministicRng"/> in the shape of a <see cref="Random"/>, for pure libraries that take one
    /// (RH Radar Suite's core, G66). <see cref="NextDouble"/>, the <c>Next</c> overloads and <c>NextBytes(byte[])</c> are
    /// overridden to draw from the run's generator, so each draw is counted, goes into the Fix, and replays with the
    /// seed; <see cref="Random"/>'s own algorithm, which is not guaranteed the same across runtimes
    /// (CoreBannedApiTests), is not used by them. The radar core calls only <see cref="NextDouble"/>.
    /// </summary>
    public sealed class SeededRandom : Random
    {
        readonly DeterministicRng _rng;

        public SeededRandom(DeterministicRng rng) : base(0) => _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        public DeterministicRng Rng => _rng;

        protected override double Sample() => _rng.NextDouble01();
        public override double NextDouble() => _rng.NextDouble01();
        public override int Next() => (int)(_rng.NextULong() >> 33);
        public override int Next(int maxValue)
        {
            if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue), maxValue, "must not be negative");
            return (int)(_rng.NextDouble01() * maxValue);
        }

        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue), minValue, "min must not exceed max");
            return minValue + (int)(_rng.NextDouble01() * ((long)maxValue - minValue));
        }

        public override void NextBytes(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            for (int i = 0; i < buffer.Length; i++) buffer[i] = (byte)(_rng.NextULong() >> 56);
        }
    }
}
