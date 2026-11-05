using System;
using System.Collections.Generic;
using NUnit.Framework;
using Debug = UnityEngine.Debug;

namespace POSEIDON.Reckoning.Tests
{
    public class ReckonedRandomTests
    {
        private const ulong Seed = 1442;

        // (e)
        [Test]
        public void Draws_AreIndependentOfCallOrder()
        {
            const int count = 2000;
            var ids = new DrawId[count];
            var forward = new double[count];
            for (int i = 0; i < count; i++)
            {
                ids[i] = new DrawId((uint)(i % 7), (uint)(i % 13), i * 3L, (uint)(i % 5));
                forward[i] = ReckonedRandom.Normal(Seed, ids[i]);
            }

            var backward = new double[count];
            for (int i = count - 1; i >= 0; i--) backward[i] = ReckonedRandom.Normal(Seed, ids[i]);

            // Interleaved with unrelated draws of every kind: nothing should notice them.
            var interleaved = new double[count];
            for (int i = 0; i < count; i++)
            {
                ReckonedRandom.Uniform(Seed, new DrawId(99, (uint)i, 1, 0));
                ReckonedRandom.Range(Seed, new DrawId(98, (uint)i, 2, 0), 0, 10);
                interleaved[i] = ReckonedRandom.Normal(Seed, ids[i]);
            }

            CollectionAssert.AreEqual(forward, backward);
            CollectionAssert.AreEqual(forward, interleaved);
        }

        [Test]
        public void Draws_ChangeWithEveryPartOfTheirName()
        {
            var id = new DrawId(1, 2, 3, 4);
            ulong baseline = ReckonedRandom.Bits(Seed, id);

            Assert.AreNotEqual(baseline, ReckonedRandom.Bits(Seed + 1, id), "seed");
            Assert.AreNotEqual(baseline, ReckonedRandom.Bits(Seed, new DrawId(9, 2, 3, 4)), "rule");
            Assert.AreNotEqual(baseline, ReckonedRandom.Bits(Seed, new DrawId(1, 9, 3, 4)), "entity");
            Assert.AreNotEqual(baseline, ReckonedRandom.Bits(Seed, new DrawId(1, 2, 9, 4)), "tick");
            Assert.AreNotEqual(baseline, ReckonedRandom.Bits(Seed, new DrawId(1, 2, 3, 9)), "index");
            Assert.AreEqual(baseline, ReckonedRandom.Bits(Seed, new DrawId(1, 2, 3, 4)), "same name, same draw");
        }

        [Test]
        public void Draws_SwappingFields_GivesDifferentValues()
        {
            // A sloppy hash (a plain XOR of fields) would make these collide.
            Assert.AreNotEqual(ReckonedRandom.Bits(Seed, new DrawId(1, 2, 0, 0)), ReckonedRandom.Bits(Seed, new DrawId(2, 1, 0, 0)));
            Assert.AreNotEqual(ReckonedRandom.Bits(Seed, new DrawId(0, 0, 1, 2)), ReckonedRandom.Bits(Seed, new DrawId(0, 0, 2, 1)));
        }

        [Test]
        public void Mix64_MatchesPublishedSplitMix64Sequence()
        {
            // SplitMix64 is "state += gamma; output = finalizer(state)". These are the published
            // first outputs for seed 1234567, so this pins the finalizer to the real algorithm.
            const ulong gamma = 0x9E3779B97F4A7C15UL;
            ulong state = 1234567;
            ulong[] expected = { 6457827717110365317UL, 3203168211198807973UL, 9817491932198370423UL, 4593380528125082431UL, 16408922859458223821UL };
            foreach (ulong value in expected)
            {
                state = unchecked(state + gamma);
                Assert.AreEqual(value, ReckonedRandom.Mix64(state));
            }
        }

        [Test]
        public void GoldenDraws_NeverChange()
        {
            // Saved Logs replay against these exact numbers. If this fails, every saved run has been
            // invalidated: change the expected values only when that is what you mean to do.
            var id = new DrawId(7, 3, 900, 0);
            Assert.AreEqual(GoldenBits, ReckonedRandom.Bits(Seed, id));
        }

        // Computed independently in Python from the same formula, not copied from a C# run.
        private const ulong GoldenBits = 0x5275C1AD2D00A447UL;

        [Test]
        public void Uniform_StaysInRange_WithExpectedMoments()
        {
            const int count = 200000;
            double sum = 0, sumSquares = 0, min = 1, max = 0;
            for (int i = 0; i < count; i++)
            {
                double u = ReckonedRandom.Uniform(Seed, new DrawId(1, (uint)i, 0, 0));
                Assert.GreaterOrEqual(u, 0.0);
                Assert.Less(u, 1.0);
                sum += u;
                sumSquares += u * u;
                if (u < min) min = u;
                if (u > max) max = u;
            }
            double mean = sum / count;
            double variance = sumSquares / count - mean * mean;

            // Mean standard error is sqrt(1/12 / n), about 0.00065; 0.003 is over four of them.
            Assert.AreEqual(0.5, mean, 0.003);
            Assert.AreEqual(1.0 / 12.0, variance, 0.002);
            Debug.Log($"[RECKONING] uniform: n={count}, mean {mean:F5}, variance {variance:F5} (ideal 0.08333), min {min:E2}, max {max:F6}");
        }

        [Test]
        public void Uniform_NeighbouringNames_AreUncorrelated()
        {
            // Hash quality check: consecutive ticks, entities and indices are the inputs real rules
            // use, so they must not produce related outputs.
            const int count = 100000;
            double byTick = 0, byEntity = 0, byIndex = 0;
            for (int i = 0; i < count; i++)
            {
                double u = ReckonedRandom.Uniform(Seed, new DrawId(1, 1, i, 0)) - 0.5;
                byTick += u * (ReckonedRandom.Uniform(Seed, new DrawId(1, 1, i + 1L, 0)) - 0.5);
                byEntity += u * (ReckonedRandom.Uniform(Seed, new DrawId(1, 2, i, 0)) - 0.5);
                byIndex += u * (ReckonedRandom.Uniform(Seed, new DrawId(1, 1, i, 1)) - 0.5);
            }

            // Correlation = covariance / (1/12). Its standard error is about 1/sqrt(n) = 0.0032.
            Assert.AreEqual(0.0, byTick / count * 12.0, 0.015, "tick");
            Assert.AreEqual(0.0, byEntity / count * 12.0, 0.015, "entity");
            Assert.AreEqual(0.0, byIndex / count * 12.0, 0.015, "index");
        }

        [Test]
        public void Range_HitsEveryValue_AndOnlyThose_Evenly()
        {
            const int count = 120000;
            var hits = new int[6];
            for (int i = 0; i < count; i++)
            {
                int value = ReckonedRandom.Range(Seed, new DrawId(2, (uint)i, 0, 0), -2, 4);
                Assert.GreaterOrEqual(value, -2);
                Assert.Less(value, 4);
                hits[value + 2]++;
            }
            foreach (int hit in hits) Assert.AreEqual(count / 6, hit, 450); // about 3.5 standard errors
        }

        [Test]
        public void Range_HandlesItsExtremes()
        {
            Assert.AreEqual(5, ReckonedRandom.Range(Seed, new DrawId(1, 1, 1, 1), 5, 6), "one value");
            int wide = ReckonedRandom.Range(Seed, new DrawId(1, 1, 1, 1), int.MinValue, int.MaxValue);
            Assert.Less(wide, int.MaxValue);
            Assert.Throws<ArgumentOutOfRangeException>(() => ReckonedRandom.Range(Seed, new DrawId(1, 1, 1, 1), 3, 3));
        }

        [Test]
        public void Normal_HasExpectedMoments_AndTails()
        {
            const int count = 200000;
            double sum = 0, sumSquares = 0;
            int beyond2 = 0;
            for (int i = 0; i < count; i++)
            {
                double z = ReckonedRandom.Normal(Seed, new DrawId(3, (uint)i, 0, 0));
                sum += z;
                sumSquares += z * z;
                if (Math.Abs(z) > 2.0) beyond2++;
            }
            double mean = sum / count;
            double variance = sumSquares / count - mean * mean;
            double tail = (double)beyond2 / count;

            Assert.AreEqual(0.0, mean, 0.01);          // standard error 0.0022
            Assert.AreEqual(1.0, variance, 0.015);     // standard error 0.0032
            Assert.AreEqual(0.0455, tail, 0.0025);     // true P(|z|>2) is 0.0455; standard error 0.00047
            Debug.Log($"[RECKONING] normal: n={count}, mean {mean:F5}, variance {variance:F5}, P(|z|>2) {tail:F5} (ideal 0.04550)");
        }

        [TestCase(0.975, 1.959963985)]
        [TestCase(0.5, 0.0)]
        [TestCase(0.84134474606854, 1.0)]
        [TestCase(0.001, -3.090232306)]
        [TestCase(1e-10, -6.361340902)]
        public void InverseNormalCdf_MatchesKnownQuantiles(double p, double expected)
        {
            Assert.AreEqual(expected, ReckonedMath.InverseNormalCdf(p), 1e-7);
        }

        [Test]
        public void Log_MatchesTheRuntimeLog_AcrossItsRange()
        {
            // ReckonedMath.Log exists because Math.Log is banned from reckoned code. The test is free
            // to use it as a yardstick.
            double worst = 0;
            double[] specials = { 1.0, 2.0, Math.E, 0.5, 1e-300, 1e300, double.Epsilon, double.MaxValue, 1.0000000001, 0.9999999999 };
            var values = new List<double>(specials);
            for (int i = 1; i <= 20000; i++) values.Add(i * 0.00137);

            foreach (double x in values)
            {
                double ours = ReckonedMath.Log(x);
                double theirs = Math.Log(x);
                double error = Math.Abs(ours - theirs) / Math.Max(1.0, Math.Abs(theirs));
                if (error > worst) worst = error;
            }

            Assert.Less(worst, 4e-16);
            Assert.Throws<ArgumentOutOfRangeException>(() => ReckonedMath.Log(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ReckonedMath.Log(-1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ReckonedMath.Log(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => ReckonedMath.Log(double.PositiveInfinity));
            Debug.Log($"[RECKONING] log: worst scaled error against Math.Log {worst:E2} over {values.Count} values");
        }
    }
}
