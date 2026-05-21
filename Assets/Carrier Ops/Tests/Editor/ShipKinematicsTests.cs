using NUnit.Framework;
using UnityEngine;
using CarrierOps.Core.Movement;
using CarrierOps.Core.State;

namespace CarrierOps.Tests
{
    /// <summary>
    /// EditMode tests for the ship kinematic model. Validates speed accel/decel,
    /// rudder-driven heading change, and that rudder authority scales with speed.
    /// </summary>
    public class ShipKinematicsTests
    {
        private const float Dt = 0.02f;

        private static CarrierState NewState() => new CarrierState(1, 1, 1);

        [Test]
        public void FullThrottle_ApproachesMaxSpeedOverTime()
        {
            var profile = CarrierProfileData.FordClass();
            // Use a brisk accel so the test doesn't take a sim hour.
            profile.AccelKnotsPerSec = 5f;

            var state = NewState();
            var command = new ShipCommand { ThrottleNormalized = 1f, RudderNormalized = 0f };

            // Step 60 simulated seconds — at 5 kt/s accel, we'd reach max in ~7s.
            for (int i = 0; i < 60 / Dt; i++)
                ShipKinematics.Step(in profile, in command, Dt, state);

            Assert.AreEqual(profile.MaxSpeedKnots, state.SpeedKnots, 0.01f,
                "Should reach max speed under full throttle given enough time");
        }

        [Test]
        public void AllStop_DecelsToZero()
        {
            var profile = CarrierProfileData.FordClass();
            profile.AccelKnotsPerSec = 5f;
            var state = NewState();
            state.SpeedKnots = 20f;

            var command = new ShipCommand { ThrottleNormalized = 0f, RudderNormalized = 0f };

            for (int i = 0; i < 60 / Dt; i++)
                ShipKinematics.Step(in profile, in command, Dt, state);

            Assert.AreEqual(0f, state.SpeedKnots, 0.01f, "Should decel to zero with throttle 0");
        }

        [Test]
        public void Rudder_ChangesHeadingOverTime()
        {
            var profile = CarrierProfileData.FordClass();
            profile.AccelKnotsPerSec = 50f;            // get to speed quickly
            profile.TurnRateAccelDegPerSec2 = 5f;       // and turn quickly for the test
            var state = NewState();
            state.SpeedKnots = profile.MaxSpeedKnots;   // start at max speed for full rudder authority

            var command = new ShipCommand { ThrottleNormalized = 1f, RudderNormalized = 1f };

            float h0 = state.HeadingDeg;
            for (int i = 0; i < 30 / Dt; i++)
                ShipKinematics.Step(in profile, in command, Dt, state);

            // After 30 s of right rudder at full speed → heading should have changed significantly.
            float dh = Mathf.DeltaAngle(h0, state.HeadingDeg);
            Assert.Greater(dh, 1f, "Right rudder at full speed should produce visible heading change");
        }

        [Test]
        public void Rudder_AtZeroSpeed_HasNoTurnAuthority()
        {
            var profile = CarrierProfileData.FordClass();
            var state = NewState();
            // Force zero speed.
            state.SpeedKnots = 0f;

            var command = new ShipCommand { ThrottleNormalized = 0f, RudderNormalized = 1f };

            float h0 = state.HeadingDeg;
            for (int i = 0; i < 60 / Dt; i++)
                ShipKinematics.Step(in profile, in command, Dt, state);

            Assert.AreEqual(h0, state.HeadingDeg, 0.01f,
                "Rudder must produce no heading change at zero speed");
        }

        [Test]
        public void Position_AdvancesForwardAtSpeed()
        {
            var profile = CarrierProfileData.FordClass();
            profile.AccelKnotsPerSec = 50f;
            var state = NewState();
            state.HeadingDeg = 0f;                       // facing +Z
            state.SpeedKnots = profile.MaxSpeedKnots;

            var command = new ShipCommand { ThrottleNormalized = 1f, RudderNormalized = 0f };

            Vector3 p0 = state.Position;
            for (int i = 0; i < 10 / Dt; i++)
                ShipKinematics.Step(in profile, in command, Dt, state);

            // At 33 kt × 0.514 m/s/kt × 10 s ≈ 170 m forward.
            Assert.Greater(state.Position.z - p0.z, 100f, "Position should move forward at speed");
            Assert.Less(Mathf.Abs(state.Position.x - p0.x), 1f, "No lateral drift expected with rudder 0");
        }
    }
}
