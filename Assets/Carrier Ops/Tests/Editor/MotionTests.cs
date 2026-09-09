using NUnit.Framework;
using UnityEngine;
using CarrierOps.Core.Motion;
using CarrierOps.Core.State;

namespace CarrierOps.Tests
{
    /// <summary>
    /// EditMode tests for the sea-state motion model. The contract is determinism +
    /// bounded amplitude — same seed produces same outcome, and amplitudes stay within
    /// the authored profile values.
    /// </summary>
    public class MotionTests
    {
        [Test]
        public void Motion_SameSeed_ProducesSameOutput()
        {
            var sea = SeaStateData.FreshBreeze;
            sea.Seed = 42;
            var motion = SumOfSinesMotion.Instance;

            motion.Sample(in sea, 5.0, out float h1, out float r1, out float p1);
            motion.Sample(in sea, 5.0, out float h2, out float r2, out float p2);

            Assert.AreEqual(h1, h2, 1e-6f, "Same seed + same time must produce identical heave");
            Assert.AreEqual(r1, r2, 1e-6f, "Same seed + same time must produce identical roll");
            Assert.AreEqual(p1, p2, 1e-6f, "Same seed + same time must produce identical pitch");
        }

        [Test]
        public void Motion_DifferentSeed_ProducesDifferentOutput()
        {
            var sea1 = SeaStateData.FreshBreeze;
            sea1.Seed = 42;
            var sea2 = SeaStateData.FreshBreeze;
            sea2.Seed = 9999;

            var motion = SumOfSinesMotion.Instance;
            motion.Sample(in sea1, 5.0, out float h1, out _, out _);
            motion.Sample(in sea2, 5.0, out float h2, out _, out _);

            Assert.AreNotEqual(h1, h2, "Different seeds must produce different motion");
        }

        [Test]
        public void Motion_CalmSea_ProducesZeroOutput()
        {
            var sea = SeaStateData.Calm;
            var motion = SumOfSinesMotion.Instance;
            motion.Sample(in sea, 12.34, out float h, out float r, out float p);

            Assert.AreEqual(0f, h, 1e-6f);
            Assert.AreEqual(0f, r, 1e-6f);
            Assert.AreEqual(0f, p, 1e-6f);
        }

        [Test]
        public void Motion_AmplitudeBound_HeaveStaysWithinAuthoredEnvelope()
        {
            // Sample over a long window; max |heave| must not exceed the profile's amplitude.
            var sea = new SeaStateData
            {
                HeaveAmplitudeM = 0.5f,
                HeavePeriodS    = 4f,
                RollAmplitudeDeg  = 0f, RollPeriodS  = 1f,
                PitchAmplitudeDeg = 0f, PitchPeriodS = 1f,
                Seed = 1,
            };

            float maxAbs = 0f;
            for (double t = 0; t < 60.0; t += 0.05)
            {
                SumOfSinesMotion.Instance.Sample(in sea, t, out float h, out _, out _);
                maxAbs = Mathf.Max(maxAbs, Mathf.Abs(h));
            }

            // Sum-of-sines with weights (0.4, 0.4, 0.2) summing to 1.0 has theoretical max ≤
            // the authored amplitude. Allow a tiny float tolerance.
            Assert.LessOrEqual(maxAbs, sea.HeaveAmplitudeM + 1e-4f,
                "Heave should never exceed the authored amplitude");
        }
    }
}
