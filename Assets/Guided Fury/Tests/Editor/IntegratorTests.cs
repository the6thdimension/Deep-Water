using NUnit.Framework;
using UnityEngine;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Tests
{
    /// <summary>
    /// EditMode tests for the L0 and L1 integrators. Exercises pure simulation math without
    /// a scene — the entity is constructed in-memory, stepped programmatically, asserted
    /// against. This is the payoff of methodology principle P6.
    /// </summary>
    public class IntegratorTests
    {
        private const float Dt = 0.02f; // 50 Hz, matches Unity's default fixed step

        // ============================================================
        // L0 — Kinematic
        // ============================================================

        [Test]
        public void L0_NoGravity_FliesStraightAtCruiseSpeed()
        {
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw = GuidanceLawKind.None;
            profile.L0UseGravity = false;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L0_Kinematic);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            TestHelpers.StepFor(entity, 1.0f, Dt);

            // Expected: ≈ cruise_speed × 1 s along +Z, near-zero on X/Y.
            float expectedZ = profile.CruiseSpeedMps * 1.0f;
            Assert.AreEqual(expectedZ, entity.State.Position.z, expectedZ * 0.05f);
            Assert.AreEqual(0f, entity.State.Position.x, 0.01f);
            Assert.AreEqual(0f, entity.State.Position.y, 0.01f);
        }

        [Test]
        public void L0_WithGravity_FollowsBallisticArc()
        {
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw = GuidanceLawKind.None;
            profile.L0UseGravity = true;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L0_Kinematic);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            TestHelpers.StepFor(entity, 0.5f, Dt);

            // After 0.5 s with gravity on:
            //   v_y ≈ -g·t  = -4.90 m/s
            //   p_y ≈ -½g·t² ≈ -1.23 m
            Assert.Less(entity.State.Position.y, 0f, "Missile should have fallen due to gravity");
            Assert.AreEqual(-9.80665f * 0.5f, entity.State.Velocity.y, 0.5f);
        }

        [Test]
        public void L0_ExceedsMaxLifetime_PhaseBecomesFailed()
        {
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw = GuidanceLawKind.None;
            profile.MaxLifetimeS = 0.5f;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L0_Kinematic);
            entity.Launch(Vector3.zero, Quaternion.identity);

            TestHelpers.StepFor(entity, 1.0f, Dt);

            Assert.AreEqual(MissilePhase.Failed, entity.State.Phase);
        }

        // ============================================================
        // L1 — Point-mass 3DOF
        // ============================================================

        [Test]
        public void L1_StationaryMissile_OnlyGravity_FallsAtG()
        {
            // Start at rest, zero thrust, zero drag. Only gravity should act.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw   = GuidanceLawKind.None;
            profile.CruiseSpeedMps = 0f;          // Launch sets v = forward * 0 = zero
            profile.BoostDurationS = 0f;          // no boost (so no thrust applied)
            profile.BoostThrustN   = 0f;
            profile.DragCoefficient = 0f;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L1_PointMass3Dof);
            entity.Launch(Vector3.zero, Quaternion.identity);

            entity.Step(Dt);

            // After one Dt: v ≈ (0, -g·Dt, 0)
            Assert.AreEqual(-9.80665f * Dt, entity.State.Velocity.y, 1e-3f);
            Assert.AreEqual(0f, entity.State.Velocity.x, 1e-3f);
            Assert.AreEqual(0f, entity.State.Velocity.z, 1e-3f);
        }

        [Test]
        public void L1_BoostPhase_AcceleratesForward()
        {
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw = GuidanceLawKind.None;
            profile.CruiseSpeedMps = 100f;
            profile.BoostThrustN   = 30000f;
            profile.BoostDurationS = 2f;
            profile.DragCoefficient = 0f;          // isolate thrust vs gravity

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L1_PointMass3Dof);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            float speedBefore = entity.State.Velocity.magnitude;
            TestHelpers.StepFor(entity, 1.0f, Dt);
            float speedAfter = entity.State.Velocity.magnitude;

            Assert.Greater(speedAfter, speedBefore, "Missile should accelerate during boost");
        }

        [Test]
        public void L1_MassBurnsLinearlyDuringBoost()
        {
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw = GuidanceLawKind.None;
            profile.DryMassKg        = 100f;
            profile.PropellantMassKg = 50f;
            profile.BoostDurationS   = 2f;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L1_PointMass3Dof);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            Assert.AreEqual(150f, entity.State.Mass, 1e-3f, "Start mass = dry + propellant");

            // Halfway through the burn (1 s of 2 s): 25 kg of fuel consumed.
            TestHelpers.StepFor(entity, 1.0f, Dt);

            Assert.AreEqual(125f, entity.State.Mass, 1f, "Mass should drop linearly during burn");
            Assert.AreEqual(25f, entity.State.Fuel, 1f, "Half the fuel should remain");
        }

        [Test]
        public void L1_BoostPhase_TransitionsToCruiseAtBurnout()
        {
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw    = GuidanceLawKind.None;
            profile.BoostDurationS = 0.5f;
            profile.MaxLifetimeS   = 5f;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L1_PointMass3Dof);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            Assert.AreEqual(MissilePhase.Boost, entity.State.Phase, "Should start in Boost");

            TestHelpers.StepFor(entity, 1.0f, Dt);

            Assert.AreEqual(MissilePhase.Cruise, entity.State.Phase, "Should transition to Cruise after burn");
            Assert.AreEqual(0f, entity.State.Fuel, 1e-3f, "Fuel should be zero after burnout");
        }

        [Test]
        public void L1_DragSlowsCoastingMissile()
        {
            // Coast: no thrust. Measure speed loss along the flight direction. Gravity acts
            // perpendicular (downward); we look at the horizontal component for drag effect.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw     = GuidanceLawKind.None;
            profile.BoostThrustN    = 0f;
            profile.BoostDurationS  = 0f;
            profile.DragCoefficient = 1f;          // exaggerated for visibility
            profile.ReferenceAreaM2 = 0.1f;
            profile.CruiseSpeedMps  = 300f;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L1_PointMass3Dof);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            float initialHorizontalSpeed =
                new Vector2(entity.State.Velocity.x, entity.State.Velocity.z).magnitude;

            TestHelpers.StepFor(entity, 1.0f, Dt);

            float finalHorizontalSpeed =
                new Vector2(entity.State.Velocity.x, entity.State.Velocity.z).magnitude;

            Assert.Less(finalHorizontalSpeed, initialHorizontalSpeed, "Drag should reduce horizontal speed");
        }
    }
}
