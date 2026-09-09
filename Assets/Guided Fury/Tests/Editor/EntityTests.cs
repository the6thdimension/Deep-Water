using NUnit.Framework;
using UnityEngine;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Tests
{
    /// <summary>
    /// EditMode tests for MissileEntity-level behavior: launch lifecycle, terminal-state
    /// gating, determinism, target-source wiring.
    /// </summary>
    public class EntityTests
    {
        private const float Dt = 0.02f;

        [Test]
        public void Entity_AfterDetonate_DoesNotAdvance()
        {
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw = GuidanceLawKind.None;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L0_Kinematic);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            Vector3 posBefore = entity.State.Position;
            entity.Detonate();

            // Try to step several frames — state should not change.
            TestHelpers.StepFor(entity, 0.5f, Dt);

            Assert.AreEqual(posBefore, entity.State.Position, "Detonated missile must not move");
            Assert.AreEqual(MissilePhase.Detonated, entity.State.Phase);
        }

        [Test]
        public void Entity_DeterministicTrajectory_SameInputsProduceSameOutputs()
        {
            // Run the same scenario twice. Final state must match exactly. This is the basis
            // for replays and regression testing (P7 / methodology candidate C2).
            var entityA = BuildHomingScenario(out var targetA);
            var entityB = BuildHomingScenario(out var targetB);

            int steps = 100;
            for (int i = 0; i < steps; i++)
            {
                // Move both targets identically each step (constant velocity).
                targetA.Position += targetA.Velocity * Dt;
                targetB.Position += targetB.Velocity * Dt;
                entityA.Step(Dt);
                entityB.Step(Dt);
            }

            Assert.AreEqual(entityA.State.Position, entityB.State.Position,
                "Deterministic runs must produce identical positions");
            Assert.AreEqual(entityA.State.Velocity, entityB.State.Velocity,
                "Deterministic runs must produce identical velocities");
            Assert.AreEqual(entityA.State.TimeOfFlight, entityB.State.TimeOfFlight, 1e-5f);
        }

        [Test]
        public void Entity_HomingMissile_ClosesOnTarget()
        {
            // L1 + ProNav vs a stationary target straight ahead but offset. The missile should
            // close range over time.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw    = GuidanceLawKind.ProportionalNavigation;
            profile.NavigationGain = 3f;
            profile.CruiseSpeedMps = 300f;
            profile.BoostThrustN   = 20000f;
            profile.BoostDurationS = 5f;
            profile.MaxLifetimeS   = 20f;

            var target = new TestHelpers.FakeTarget
            {
                Position = new Vector3(200f, 0f, 2000f),
                Velocity = Vector3.zero,
            };

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L1_PointMass3Dof, target);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            float initialRange = Vector3.Distance(entity.State.Position, target.Position);

            // Step 4 seconds of flight.
            TestHelpers.StepFor(entity, 4.0f, Dt);

            float finalRange = Vector3.Distance(entity.State.Position, target.Position);

            Assert.Less(finalRange, initialRange,
                "Homing missile should close range on a stationary off-axis target");
            // A reasonable sanity check: closed by at least half.
            Assert.Less(finalRange, initialRange * 0.5f,
                "Homing missile should close at least halfway in 4 s");
        }

        [Test]
        public void Entity_NoTarget_FliesBallistic()
        {
            // No target source: guidance returns zero command; missile coasts under thrust+gravity+drag.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw = GuidanceLawKind.ProportionalNavigation; // even with a guidance law,
                                                                         // no target = no command

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L1_PointMass3Dof, target: null);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            TestHelpers.StepFor(entity, 1.0f, Dt);

            // Missile should still have flown forward; X should remain close to zero (no
            // lateral command was issued because there's no target).
            Assert.Greater(entity.State.Position.z, 0f, "Missile should have moved forward under thrust");
            Assert.AreEqual(0f, entity.State.Position.x, 0.1f, "No target means no lateral command, X stays small");
        }

        // -- helper -----------------------------------------------------------
        private MissileEntity BuildHomingScenario(out TestHelpers.FakeTarget target)
        {
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw = GuidanceLawKind.ProportionalNavigation;
            profile.NavigationGain = 3f;

            target = new TestHelpers.FakeTarget
            {
                Position = new Vector3(100f, 0f, 1500f),
                Velocity = new Vector3(50f, 0f, 0f),
            };

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L1_PointMass3Dof, target);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));
            return entity;
        }
    }
}
