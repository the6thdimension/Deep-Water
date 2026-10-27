using System;
using NUnit.Framework;
using RHRadarSuite.Tracking;

namespace RHRadarSuite.Tests
{
    /// <summary>
    /// ROADMAP Phase 3, the α-β tracker: its step response against the closed form, zero lag on a constant-velocity
    /// target, the gain rules (Benedict–Bordner, critically damped, Kalata), and its noise against the textbook
    /// variance-reduction ratio.
    /// </summary>
    public class AlphaBetaFilterTests
    {
        [Test]
        public void StepResponse_MatchesTheClosedForm()
        {
            // Critically damped, both poles at θ: the error to a unit step after k updates is θ^k (1 − k(1 − θ)),
            // from e' = (1 − α)(e − u), u' = βe + (1 − β)u with u = vT (worked by hand).
            const double theta = 0.6, dt = 2.0;
            AlphaBetaGains g = AlphaBetaGains.CriticallyDamped(theta);
            var f = new AlphaBetaFilter(0.0, 0.0);
            for (int k = 1; k <= 30; k++)
            {
                f.Update(g, 1.0, dt);
                double expected = Math.Pow(theta, k) * (1.0 - k * (1.0 - theta));
                Assert.AreEqual(expected, 1.0 - f.Position, 1e-12, $"update {k}");
            }
            Assert.AreEqual(0.0, f.Velocity, 1e-5, "settles at rest");
        }

        [Test]
        public void ConstantVelocity_IsTrackedWithNoLag()
        {
            AlphaBetaGains g = AlphaBetaGains.BenedictBordner(0.5);
            var f = new AlphaBetaFilter(0.0, 0.0);
            const double v = 250.0, dt = 1.0;
            for (int k = 1; k <= 200; k++) f.Update(g, v * k * dt, dt);
            Assert.AreEqual(v * 200 * dt, f.Position, 1e-6, "position");
            Assert.AreEqual(v, f.Velocity, 1e-6, "velocity");
        }

        [Test]
        public void TheGainRules_GiveTheirTextbookPairs()
        {
            AlphaBetaGains bb = AlphaBetaGains.BenedictBordner(0.5);
            Assert.AreEqual(0.25 / 1.5, bb.Beta, 1e-15, "Benedict–Bordner: β = α²/(2 − α)");

            AlphaBetaGains cd = AlphaBetaGains.CriticallyDamped(0.6);
            Assert.AreEqual(0.64, cd.Alpha, 1e-15);
            Assert.AreEqual(0.16, cd.Beta, 1e-15);

            AlphaBetaGains k1 = AlphaBetaGains.Kalata(1.0);
            Assert.AreEqual(0.75, k1.Alpha, 1e-12, "λ = 1: α = 0.75, worked by hand");
            Assert.AreEqual(0.5, k1.Beta, 1e-12, "λ = 1: β = 0.5");
            foreach (double l in new[] { 0.01, 0.1, 1.0, 10.0 })
            {
                AlphaBetaGains k = AlphaBetaGains.Kalata(l);
                Assert.AreEqual(l * l, k.Beta * k.Beta / (1.0 - k.Alpha), 1e-9 * Math.Max(1.0, l * l), $"Kalata's identity λ² = β²/(1 − α) at λ = {l}");
            }
            Assert.Less(AlphaBetaGains.Kalata(0.01).Alpha, AlphaBetaGains.Kalata(10.0).Alpha, "a quieter sensor (larger λ) is trusted more");
        }

        [Test]
        public void Noise_FollowsTheVarianceReductionRatio()
        {
            // A still target, unit white measurement noise from a seeded generator (P7): the filtered position's
            // variance over the measurement's should equal (2α² + β(2 − 3α)) / (α(4 − 2α − β)).
            AlphaBetaGains g = AlphaBetaGains.BenedictBordner(0.5);
            Assert.AreEqual(0.583333333 / 1.416666667, g.PositionVarianceRatio, 1e-8, "0.4118, worked by hand");

            var rng = new Random(20261027);
            var f = new AlphaBetaFilter(0.0, 0.0);
            const int warmUp = 200, n = 200000;
            double sum = 0.0, sumSq = 0.0;
            for (int k = 0; k < warmUp + n; k++)
            {
                f.Update(g, Gaussian(rng), 1.0);
                if (k < warmUp) continue;
                sum += f.Position;
                sumSq += f.Position * f.Position;
            }
            double mean = sum / n, variance = sumSq / n - mean * mean;
            Assert.AreEqual(g.PositionVarianceRatio, variance, 0.03 * g.PositionVarianceRatio, "within 3 % over 200,000 updates");
        }

        [Test]
        public void Coast_FliesThePredictionAlone()
        {
            var f = new AlphaBetaFilter(1000.0, -20.0);
            f.Update(AlphaBetaGains.BenedictBordner(0.5), 985.0, 1.0);
            double p = f.Position, v = f.Velocity;
            f.Coast(3.0);
            Assert.AreEqual(0.0, f.Residual);
            Assert.AreEqual(v, f.Velocity, 0.0, "a coast keeps the velocity");
            Assert.AreEqual(p + 3.0 * v, f.Position, 1e-12, "and moves the position on it");
        }

        [Test]
        public void UnstableGainsAndPeriodsAreRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AlphaBetaGains(0.0, 0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AlphaBetaGains(1.5, 1.2), "β ≥ 4 − 2α is unstable");
            Assert.Throws<ArgumentOutOfRangeException>(() => AlphaBetaGains.CriticallyDamped(1.0));
            var f = new AlphaBetaFilter(0.0, 0.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Update(AlphaBetaGains.BenedictBordner(0.5), 1.0, 0.0));
        }

        /// <summary>Box–Muller, one draw: a unit normal from two uniforms.</summary>
        static double Gaussian(Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
