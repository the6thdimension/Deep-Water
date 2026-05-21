using NUnit.Framework;
using UnityEngine;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Tests
{
    /// <summary>
    /// EditMode tests for guidance laws. Each law is a pure function of (context, profile);
    /// we don't need to step a full entity to exercise it.
    /// </summary>
    public class GuidanceTests
    {
        private static GuidanceContext MakeContext(
            Vector3 missilePos, Vector3 missileVel,
            Vector3 targetPos,  Vector3 targetVel,
            bool hasTrack = true)
        {
            return new GuidanceContext
            {
                State = new MissileState
                {
                    Position    = missilePos,
                    Velocity    = missileVel,
                    Orientation = Quaternion.LookRotation(
                                      missileVel.sqrMagnitude > 1e-6f ? missileVel.normalized : Vector3.forward,
                                      Vector3.up),
                    Mass        = 100f,
                    Fuel        = 0f,
                    TimeOfFlight = 0f,
                    Phase       = MissilePhase.Cruise,
                },
                Target = hasTrack
                    ? TargetTrack.Omniscient(targetPos, targetVel)
                    : TargetTrack.None,
                Atmo = AtmosphereSample.SeaLevelIsa,
                Dt   = 0.02f,
            };
        }

        // ============================================================
        // Pursuit
        // ============================================================

        [Test]
        public void Pursuit_NoTrack_ReturnsZeroCommand()
        {
            var profile = MissileProfileData.TestStub();
            var ctx = MakeContext(Vector3.zero, Vector3.forward * 100f, Vector3.up, Vector3.zero, hasTrack: false);

            var cmd = PursuitGuidance.Instance.ComputeCommand(in ctx, in profile);

            Assert.AreEqual(Vector3.zero, cmd.CommandedAccelerationWorld);
            Assert.AreEqual(0f, cmd.ThrustThrottle);
        }

        [Test]
        public void Pursuit_StaticTargetOffAxis_CommandsLateralAccelTowardTarget()
        {
            var profile = MissileProfileData.TestStub();
            profile.NavigationGain = 1f;
            // Missile flying along +Z at 100 m/s. Target 100m to the right (+X).
            var ctx = MakeContext(Vector3.zero, new Vector3(0, 0, 100f),
                                  new Vector3(100f, 0, 0), Vector3.zero);

            var cmd = PursuitGuidance.Instance.ComputeCommand(in ctx, in profile);

            // Expected: command has positive X component (toward target), no significant
            // forward-axis component (lateral, perpendicular to velocity).
            Assert.Greater(cmd.CommandedAccelerationWorld.x, 0f,
                "Pursuit should command toward the target (+X)");
            // Forward axis of velocity is +Z; commanded accel should be perpendicular.
            Assert.AreEqual(0f, cmd.CommandedAccelerationWorld.z, 1e-3f,
                "Pursuit command should be perpendicular to velocity");
        }

        // ============================================================
        // Proportional Navigation
        // ============================================================

        [Test]
        public void ProNav_NoTrack_ReturnsZeroCommand()
        {
            var profile = MissileProfileData.TestStub();
            var ctx = MakeContext(Vector3.zero, Vector3.forward * 100f, Vector3.up, Vector3.zero, hasTrack: false);

            var cmd = ProportionalNavigation.Instance.ComputeCommand(in ctx, in profile);

            Assert.AreEqual(Vector3.zero, cmd.CommandedAccelerationWorld);
        }

        [Test]
        public void ProNav_HeadOnStaticTarget_ProducesZeroLateralAccel()
        {
            // Missile at origin moving +Z at 100 m/s. Target straight ahead at (0,0,1000), stationary.
            // LOS is parallel to velocity; LOS rate is zero → ProNav command is zero.
            var profile = MissileProfileData.TestStub();
            profile.NavigationGain = 3f;

            var ctx = MakeContext(
                Vector3.zero, new Vector3(0, 0, 100f),
                new Vector3(0, 0, 1000f), Vector3.zero);

            var cmd = ProportionalNavigation.Instance.ComputeCommand(in ctx, in profile);

            Assert.Less(cmd.CommandedAccelerationWorld.magnitude, 1e-2f,
                "Head-on intercept geometry should yield ~zero ProNav command");
        }

        [Test]
        public void ProNav_StaticTargetOffAxis_CommandsLateralAccelInPlane()
        {
            // Missile heading +Z, target offset to +X. LOS is rotating because we keep moving
            // forward but the target is to the side — ProNav should command lateral accel.
            var profile = MissileProfileData.TestStub();
            profile.NavigationGain = 3f;

            var ctx = MakeContext(
                Vector3.zero, new Vector3(0, 0, 100f),
                new Vector3(500f, 0, 1000f), Vector3.zero);

            var cmd = ProportionalNavigation.Instance.ComputeCommand(in ctx, in profile);

            // Acceleration must be in the XZ plane (no Y component for this engagement geometry),
            // and have a positive X component (toward the target).
            Assert.AreEqual(0f, cmd.CommandedAccelerationWorld.y, 1e-3f,
                "ProNav command should be in the engagement plane (no Y component here)");
            Assert.Greater(cmd.CommandedAccelerationWorld.x, 0f,
                "ProNav should command toward +X (target offset direction)");
        }

        [Test]
        public void ProNav_MovingTarget_CommandsLeadAcceleration()
        {
            // Missile heading +Z. Target crossing in +X. ProNav should lead, producing more
            // lateral command than the same geometry with a stationary target.
            var profile = MissileProfileData.TestStub();
            profile.NavigationGain = 3f;

            var ctxMoving = MakeContext(
                Vector3.zero, new Vector3(0, 0, 100f),
                new Vector3(500f, 0, 1000f), new Vector3(100f, 0, 0));

            var ctxStatic = MakeContext(
                Vector3.zero, new Vector3(0, 0, 100f),
                new Vector3(500f, 0, 1000f), Vector3.zero);

            var cmdMoving = ProportionalNavigation.Instance.ComputeCommand(in ctxMoving, in profile);
            var cmdStatic = ProportionalNavigation.Instance.ComputeCommand(in ctxStatic, in profile);

            Assert.AreNotEqual(cmdMoving.CommandedAccelerationWorld, cmdStatic.CommandedAccelerationWorld,
                "Moving target should produce a different command than a static one");
        }
    }
}
