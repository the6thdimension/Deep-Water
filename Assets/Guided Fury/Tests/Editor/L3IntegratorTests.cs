using NUnit.Framework;
using UnityEngine;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Tests
{
    /// <summary>
    /// EditMode tests for the L3 pseudo-6DOF integrator. Focus is on the L3-specific
    /// behavior: body axis evolving independently of velocity, angle of attack producing
    /// lift, body-axis thrust, weather-vane stability, autopilot rate-tracking.
    /// </summary>
    public class L3IntegratorTests
    {
        private const float Dt = 0.02f;

        [Test]
        public void L3_BoostStillAccelerates_RegressionVsL1()
        {
            // Sanity: same setup as the L1/L2 boost test. L3 should also accelerate.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw     = GuidanceLawKind.None;
            profile.CruiseSpeedMps  = 100f;
            profile.BoostThrustN    = 30000f;
            profile.BoostDurationS  = 2f;
            profile.DragCoefficient = 0f;
            // Quiet aero so we isolate boost.
            profile.LiftSlopePerRad = 0f;
            profile.WeatherVaneCoefficient = 0f;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L3_PseudoRb6Dof);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            float speedBefore = entity.State.Velocity.magnitude;
            TestHelpers.StepFor(entity, 1.0f, Dt);
            float speedAfter = entity.State.Velocity.magnitude;

            Assert.Greater(speedAfter, speedBefore, "L3 boost should accelerate the missile");
        }

        [Test]
        public void L3_PitchedBodyUnderThrust_AcceleratesAlongBodyAxis()
        {
            // The defining L3 behavior: thrust acts along the body axis, not velocity.
            // We start the missile flying horizontally (+Z) but oriented pitched up (45°),
            // so body forward = (0, 0.707, 0.707). Under boost, the missile should gain
            // significant +Y velocity (upward) — proof that thrust follows the body.
            //
            // Disable lift / drag / weather-vane / gravity-rotation effects so we can read
            // the result cleanly.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw     = GuidanceLawKind.None;
            profile.CruiseSpeedMps  = 100f;
            profile.BoostThrustN    = 30000f;
            profile.BoostDurationS  = 2f;
            profile.DragCoefficient = 0f;
            profile.LiftSlopePerRad = 0f;
            profile.WeatherVaneCoefficient = 0f;
            profile.AutopilotGain   = 0f; // no autopilot torque

            // 45° pitch up from level flight.
            Quaternion pitchedOrientation = Quaternion.LookRotation(
                new Vector3(0f, 0.7071f, 0.7071f).normalized, Vector3.up);

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L3_PseudoRb6Dof);
            entity.Launch(Vector3.zero, pitchedOrientation);

            // Force the velocity to be horizontal so body axis genuinely diverges from velocity.
            // (Initialize sets velocity along body forward, which would be parallel — not useful here.)
            entity.State.Velocity = new Vector3(0f, 0f, 100f);

            float vyBefore = entity.State.Velocity.y;
            // Short step: weather-vane is off, but quickly the missile's body might still
            // rotate due to numerical reasons. We measure over a short window.
            TestHelpers.StepFor(entity, 0.2f, Dt);
            float vyAfter = entity.State.Velocity.y;

            // Expected: +Y velocity grows due to thrust component on Y axis. Gravity contributes
            // negative Y, but boost thrust at 30 kN / 150 kg ≈ 200 m/s² far outweighs 9.81.
            Assert.Greater(vyAfter, vyBefore + 5f, "Body-axis thrust should produce upward acceleration");
        }

        [Test]
        public void L3_WeatherVane_ReducesAoaOverTime()
        {
            // With weather-vane enabled and no other torque, an off-axis body should rotate
            // back toward the velocity vector. We measure the AoA before vs after.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw     = GuidanceLawKind.None;
            profile.BoostThrustN    = 0f; // no thrust
            profile.BoostDurationS  = 0f;
            profile.DragCoefficient = 0f; // no drag
            profile.LiftSlopePerRad = 0f; // no lift (which would also rotate things)
            profile.WeatherVaneCoefficient = 50f; // strong
            profile.AutopilotGain = 0f;

            // Body pitched 30° up; velocity is horizontal.
            Quaternion pitched = Quaternion.LookRotation(
                new Vector3(0f, 0.5f, Mathf.Sqrt(0.75f)).normalized, Vector3.up);

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L3_PseudoRb6Dof);
            entity.Launch(Vector3.zero, pitched);
            entity.State.Velocity = new Vector3(0f, 0f, 300f); // high speed → high dyn pressure

            float aoaBefore = AngleOfAttackDeg(entity.State);
            TestHelpers.StepFor(entity, 1.0f, Dt);
            float aoaAfter = AngleOfAttackDeg(entity.State);

            Assert.Less(aoaAfter, aoaBefore - 1f,
                $"Weather-vane should reduce AoA over time (before={aoaBefore:F1}°, after={aoaAfter:F1}°)");
        }

        [Test]
        public void L3_NoTorque_NoRotation()
        {
            // With autopilot off, weather-vane off, no AoA, no aero: angular velocity
            // shouldn't spontaneously change.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw     = GuidanceLawKind.None;
            profile.BoostThrustN    = 0f;
            profile.BoostDurationS  = 0f;
            profile.DragCoefficient = 0f;
            profile.LiftSlopePerRad = 0f;
            profile.WeatherVaneCoefficient = 0f;
            profile.AutopilotGain   = 0f;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L3_PseudoRb6Dof);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));
            // Velocity along body forward — zero AoA.

            Vector3 omegaBefore = entity.State.AngularVelocity;
            TestHelpers.StepFor(entity, 1.0f, Dt);
            Vector3 omegaAfter = entity.State.AngularVelocity;

            Assert.Less((omegaAfter - omegaBefore).magnitude, 0.05f,
                "With no torques, angular velocity should be stable");
        }

        [Test]
        public void L3_AoaProducesLift_PitchedBodyPullsToward()
        {
            // Body pitched up relative to velocity. Lift should produce upward force, pulling
            // velocity in the body-forward direction over time. Disable thrust / weather-vane
            // / autopilot to isolate the lift contribution.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw     = GuidanceLawKind.None;
            profile.BoostThrustN    = 0f;
            profile.BoostDurationS  = 0f;
            profile.DragCoefficient = 0f;
            profile.LiftSlopePerRad = 8f;
            profile.WeatherVaneCoefficient = 0f;
            profile.AutopilotGain = 0f;
            profile.ReferenceAreaM2 = 0.1f; // exaggerated for visibility

            Quaternion pitched = Quaternion.LookRotation(
                new Vector3(0f, 0.34f, 0.94f).normalized, Vector3.up); // ~20° pitch

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L3_PseudoRb6Dof);
            entity.Launch(Vector3.zero, pitched);
            entity.State.Velocity = new Vector3(0f, 0f, 300f);

            float vyBefore = entity.State.Velocity.y;
            TestHelpers.StepFor(entity, 0.5f, Dt);
            float vyAfter = entity.State.Velocity.y;

            // Gravity is also acting (about -4.9 m/s of dV over 0.5s). Lift must exceed
            // gravity for vy to rise. With dynamic pressure ≈ 0.5×1.225×90000 = 55125 Pa,
            // Cl × area = 8 × (20°×π/180) × 0.1 = 0.28, so lift ≈ 15400 N → ~103 m/s² accel,
            // far exceeding gravity. Net dV_y over 0.5 s should be substantial positive.
            Assert.Greater(vyAfter, vyBefore,
                $"AoA-induced lift should pull velocity toward body axis (vy {vyBefore:F2} → {vyAfter:F2})");
        }

        [Test]
        public void L3_Autopilot_AppliesTorqueTowardCommand()
        {
            // Feed the L3 integrator a non-zero lateral-accel command and check that the
            // autopilot starts spinning up angular velocity in the right direction.
            // Test directly with the integrator (not via entity) for full control over command.
            var profile = MissileProfileData.TestStub();
            profile.BoostThrustN    = 0f;
            profile.BoostDurationS  = 0f;
            profile.DragCoefficient = 0f;
            profile.LiftSlopePerRad = 0f;
            profile.WeatherVaneCoefficient = 0f;
            profile.AutopilotGain   = 4f;

            var state = MissileState.AtRest(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));
            var integrator = new PseudoRb6DofL3Integrator();
            integrator.Initialize(in profile, ref state);
            // Force a stable velocity along +Z so commands have meaning.
            state.Velocity = new Vector3(0f, 0f, 300f);

            // Command +X lateral accel — autopilot should try to pitch the missile to produce that.
            var command = new MissileCommand
            {
                CommandedAccelerationWorld = new Vector3(100f, 0f, 0f), // 100 m/s² toward +X
            };
            var atmoSl = Core.Atmosphere.AtmosphereSample.SeaLevelIsa;

            // Step a handful of frames; angular velocity should grow.
            for (int i = 0; i < 5; i++)
                integrator.Step(in profile, in command, in atmoSl, Dt, ref state);

            Assert.Greater(state.AngularVelocity.sqrMagnitude, 1e-4f,
                "Autopilot should be spinning the body up to track the commanded accel");
        }

        [Test]
        public void EndToEnd_L3WithProNav_ClosesOnTarget()
        {
            // Full integration: L3 physics + ProNav guidance + an off-axis stationary target.
            // Missile should close range.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw     = GuidanceLawKind.ProportionalNavigation;
            profile.NavigationGain  = 3f;
            profile.CruiseSpeedMps  = 300f;
            profile.BoostThrustN    = 25000f;
            profile.BoostDurationS  = 4f;
            profile.MaxLifetimeS    = 15f;
            // Tunable: keep weather-vane reasonable so autopilot can dominate.
            profile.WeatherVaneCoefficient = 3f;
            profile.AutopilotGain          = 6f;

            var target = new TestHelpers.FakeTarget
            {
                Position = new Vector3(200f, 0f, 2000f),
                Velocity = Vector3.zero,
            };

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L3_PseudoRb6Dof, target);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            float initialRange = Vector3.Distance(entity.State.Position, target.Position);
            TestHelpers.StepFor(entity, 5f, Dt);
            float finalRange = Vector3.Distance(entity.State.Position, target.Position);

            Assert.Less(finalRange, initialRange * 0.5f,
                "L3 + ProNav should close at least halfway on a stationary off-axis target in 5 s");
        }

        // -- helpers --------------------------------------------------------------
        private static float AngleOfAttackDeg(MissileState state)
        {
            Vector3 bodyForward = state.Orientation * Vector3.forward;
            Vector3 velocityDir = state.Velocity.sqrMagnitude > 1e-6f
                ? state.Velocity.normalized
                : bodyForward;
            float cosAoa = Mathf.Clamp(Vector3.Dot(bodyForward, velocityDir), -1f, 1f);
            return Mathf.Acos(cosAoa) * Mathf.Rad2Deg;
        }
    }
}
