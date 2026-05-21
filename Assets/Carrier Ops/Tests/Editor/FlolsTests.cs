using NUnit.Framework;
using UnityEngine;
using CarrierOps.Core.Recovery;
using CarrierOps.Core.State;

namespace CarrierOps.Tests
{
    /// <summary>
    /// EditMode tests for the FLOLS geometry model. Pure-function tests — no carrier entity
    /// or scene needed.
    /// </summary>
    public class FlolsTests
    {
        private static CarrierProfileData Profile()
        {
            var p = CarrierProfileData.FordClass();
            p.FlolsGlideslopeDeg = 3.5f;
            p.FlolsWindowHalfAngleDeg = 0.7f;
            p.FlolsWaveOffThresholdDeg = 1.5f;
            return p;
        }

        /// <summary>
        /// Helper: place an aircraft at a known distance ahead of the FLOLS reference, at a
        /// known glideslope angle, and return the resulting sample. Ship forward is +Z.
        /// </summary>
        private static FlolsState SampleAt(float horizontalRangeM, float angleAboveHorizontalDeg)
        {
            Vector3 flolsRef = Vector3.zero;
            Vector3 shipFwd = Vector3.forward;

            float rad = angleAboveHorizontalDeg * Mathf.Deg2Rad;
            // x = horizontal (forward), y = vertical. Aircraft is at distance × (cos angle, sin angle).
            Vector3 aircraft = new Vector3(0f, horizontalRangeM * Mathf.Sin(rad), horizontalRangeM * Mathf.Cos(rad));
            var p = Profile();
            return FlolsModel.Sample(in p, flolsRef, shipFwd, aircraft);
        }

        [Test]
        public void OnGlideslope_ProducesZeroBallOffset()
        {
            var f = SampleAt(1000f, 3.5f); // perfectly on glideslope
            Assert.IsTrue(f.HasTrack);
            Assert.AreEqual(0f, f.BallOffsetNormalized, 1e-3f, "On glideslope → ball centered");
            Assert.AreEqual(0f, f.GlideslopeDeviationDeg, 1e-3f);
            Assert.IsFalse(f.IsWaveOff);
        }

        [Test]
        public void High_ProducesPositiveBallOffset()
        {
            var f = SampleAt(1000f, 4.0f); // 0.5° high
            Assert.IsTrue(f.HasTrack);
            Assert.Greater(f.BallOffsetNormalized, 0f, "High → ball above datum");
            Assert.AreEqual(0.5f / 0.7f, f.BallOffsetNormalized, 0.05f,
                "Normalized = deviation/halfAngle = 0.5/0.7");
        }

        [Test]
        public void Low_ProducesNegativeBallOffset()
        {
            var f = SampleAt(1000f, 3.0f); // 0.5° low
            Assert.IsTrue(f.HasTrack);
            Assert.Less(f.BallOffsetNormalized, 0f, "Low → ball below datum");
        }

        [Test]
        public void SaturatesAtWindowEdge()
        {
            // Way high — should clamp to +1.
            var f = SampleAt(1000f, 10f);
            Assert.AreEqual(1f, f.BallOffsetNormalized, 1e-3f, "Saturates at +1");
            Assert.IsTrue(f.IsWaveOff, "Way off glideslope → wave-off");
        }

        [Test]
        public void BehindFlols_ProducesNoTrack()
        {
            Vector3 flolsRef = Vector3.zero;
            Vector3 shipFwd = Vector3.forward;
            // Aircraft behind: -50 m on Z.
            Vector3 aircraft = new Vector3(0f, 50f, -50f);
            var p = Profile();
            var f = FlolsModel.Sample(in p, flolsRef, shipFwd, aircraft);
            Assert.IsFalse(f.HasTrack);
        }

        [Test]
        public void LateralOffset_IgnoredByDeviation()
        {
            // Aircraft on glideslope but shifted laterally → still on the ball.
            Vector3 flolsRef = Vector3.zero;
            Vector3 shipFwd = Vector3.forward;
            float range = 1000f;
            float rad = 3.5f * Mathf.Deg2Rad;
            Vector3 onSlope = new Vector3(100f, range * Mathf.Sin(rad), range * Mathf.Cos(rad));

            var p = Profile();
            var f = FlolsModel.Sample(in p, flolsRef, shipFwd, onSlope);

            Assert.IsTrue(f.HasTrack);
            Assert.AreEqual(0f, f.BallOffsetNormalized, 1e-3f,
                "Lateral offset should not affect the ball (FLOLS reads vertical deviation only)");
        }
    }
}
