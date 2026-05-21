using NUnit.Framework;
using UnityEngine;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Tests
{
    /// <summary>
    /// EditMode tests for the L2 rate-limited 3DOF integrator. Focus is on the maneuver-
    /// limit behavior — base force physics (drag, gravity, thrust, mass burn) are inherited
    /// from L1 and tested there.
    /// </summary>
    public class L2IntegratorTests
    {
        private const float Dt = 0.02f;

        /// <summary>
        /// We step the integrator manually here (not via MissileEntity) so we can supply an
        /// arbitrary command and observe the integrator's response. This is the cleanest
        /// way to test maneuver limits in isolation.
        /// </summary>
        private static void StepDirect(
            IPhysicsIntegrator integrator,
            in MissileProfileData profile,
            in MissileCommand command,
            in AtmosphereSample atmo,
            ref MissileState state,
            float dt)
        {
            integrator.Step(in profile, in command, in atmo, dt, ref state);
        }

        [Test]
        public void L2_GLimitClipsExcessiveCommand()
        {
            // Profile with low g-limit and high turn-rate cap so g is the binding constraint.
            var profile = MissileProfileData.TestStub();
            profile.MaxLoadFactorG       = 5f;      // 5 g cap = ~49 m/s²
            profile.MaxTurnRateDegPerSec = 10000f;  // effectively unbounded
            profile.BoostDurationS       = 0f;       // no thrust noise
            profile.BoostThrustN         = 0f;
            profile.DragCoefficient      = 0f;       // no drag noise

            var state = MissileState.AtRest(Vector3.zero, Quaternion.LookRotation(Vector3.forward));
            var integrator = new RateLimited3DofL2Integrator();
            integrator.Initialize(in profile, ref state);

            // Force the missile to a stable forward velocity (Initialize sets cruise speed).
            state.Velocity = new Vector3(0f, 0f, 250f);

            // Ask for a huge lateral accel (1000 m/s² along +X). With a 5g cap, only ~49 m/s²
            // should actually apply.
            var atmo = AtmosphereSample.SeaLevelIsa;
            var command = new MissileCommand
            {
                CommandedAccelerationWorld = new Vector3(1000f, 0f, 0f),
            };

            // Capture velocity before the step so we can measure the delta-V due to the
            // (clipped) command alone. We've zeroed out thrust/drag; gravity contributes to
            // V_y but not V_x, so V_x delta is purely from the lateral command.
            Vector3 vBefore = state.Velocity;
            StepDirect(integrator, in profile, in command, in atmo, ref state, Dt);
            float deltaVx = state.Velocity.x - vBefore.x;

            // Expected: |a_lateral| ≤ 5g = 49.03 m/s²; deltaVx ≤ 49.03 × Dt = ~0.98 m/s.
            float maxDeltaVx = 5f * 9.80665f * Dt;
            Assert.LessOrEqual(deltaVx, maxDeltaVx + 1e-3f,
                "Lateral command should be clipped to the g-limit");
            // And the clip should be tight — within ~1% of the cap (no underflow).
            Assert.GreaterOrEqual(deltaVx, maxDeltaVx * 0.95f,
                "When commanded above the cap, the integrator should apply exactly the cap");
        }

        [Test]
        public void L2_TurnRateLimitClipsAtLowSpeed()
        {
            // Make the turn-rate limit binding by choosing low speed × low turn rate.
            // a_turnLimit = ω · v. With ω = 30 deg/s = 0.524 rad/s and v = 10 m/s,
            // a_turnLimit ≈ 5.24 m/s². Make g-limit huge so it doesn't bind.
            var profile = MissileProfileData.TestStub();
            profile.MaxLoadFactorG       = 1000f;   // effectively unbounded
            profile.MaxTurnRateDegPerSec = 30f;
            profile.BoostDurationS       = 0f;
            profile.BoostThrustN         = 0f;
            profile.DragCoefficient      = 0f;

            var state = MissileState.AtRest(Vector3.zero, Quaternion.LookRotation(Vector3.forward));
            var integrator = new RateLimited3DofL2Integrator();
            integrator.Initialize(in profile, ref state);
            state.Velocity = new Vector3(0f, 0f, 10f);  // low speed

            var command = new MissileCommand
            {
                CommandedAccelerationWorld = new Vector3(1000f, 0f, 0f), // huge ask
            };

            Vector3 vBefore = state.Velocity;
            var atmoSl = AtmosphereSample.SeaLevelIsa;
            StepDirect(integrator, in profile, in command, in atmoSl, ref state, Dt);
            float deltaVx = state.Velocity.x - vBefore.x;

            // Expected cap: ω·v = 0.5236 × 10 = ~5.236 m/s²; deltaVx ≤ 5.236 × Dt ≈ 0.105.
            float expectedCapAccel = (30f * Mathf.Deg2Rad) * 10f;
            float maxDeltaVx = expectedCapAccel * Dt;
            Assert.LessOrEqual(deltaVx, maxDeltaVx + 1e-3f,
                "Turn-rate limit should be the binding cap at low speed");
            Assert.GreaterOrEqual(deltaVx, maxDeltaVx * 0.95f,
                "Turn-rate-bound clip should be tight");
        }

        [Test]
        public void L2_SmallCommand_PassesThroughUnclipped()
        {
            // A command well within both limits should pass through unchanged.
            var profile = MissileProfileData.TestStub();
            profile.MaxLoadFactorG       = 30f;
            profile.MaxTurnRateDegPerSec = 60f;
            profile.BoostDurationS       = 0f;
            profile.BoostThrustN         = 0f;
            profile.DragCoefficient      = 0f;

            var state = MissileState.AtRest(Vector3.zero, Quaternion.LookRotation(Vector3.forward));
            var integrator = new RateLimited3DofL2Integrator();
            integrator.Initialize(in profile, ref state);
            state.Velocity = new Vector3(0f, 0f, 300f);

            float smallAccel = 5f; // m/s², well under any plausible limit
            var command = new MissileCommand
            {
                CommandedAccelerationWorld = new Vector3(smallAccel, 0f, 0f),
            };

            Vector3 vBefore = state.Velocity;
            var atmoSl = AtmosphereSample.SeaLevelIsa;
            StepDirect(integrator, in profile, in command, in atmoSl, ref state, Dt);
            float deltaVx = state.Velocity.x - vBefore.x;

            // Expected deltaVx = smallAccel × Dt = 0.1; within fp tolerance.
            Assert.AreEqual(smallAccel * Dt, deltaVx, 1e-3f,
                "A command within limits should pass through unchanged");
        }

        [Test]
        public void L2_BasePhysicsUnchanged_BoostAndGravityStillWork()
        {
            // Regression check: L2's additional clipping logic shouldn't break the base
            // L1-equivalent physics. Same setup as L1's boost-acceleration test.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw     = GuidanceLawKind.None;
            profile.CruiseSpeedMps  = 100f;
            profile.BoostThrustN    = 30000f;
            profile.BoostDurationS  = 2f;
            profile.DragCoefficient = 0f;
            profile.MaxLoadFactorG  = 30f;
            profile.MaxTurnRateDegPerSec = 60f;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L2_RateLimited3Dof);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            float speedBefore = entity.State.Velocity.magnitude;
            TestHelpers.StepFor(entity, 1.0f, Dt);
            float speedAfter = entity.State.Velocity.magnitude;

            Assert.Greater(speedAfter, speedBefore, "Boost should still accelerate the missile at L2");
        }
    }
}
