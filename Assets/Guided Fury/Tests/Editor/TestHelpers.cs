using UnityEngine;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Tests
{
    /// <summary>
    /// Shared test scaffolding. Construct missiles directly from the pure-C# core — no scene,
    /// no MonoBehaviour, no PlayMode runner. This is the payoff of P6 (plain-C# core +
    /// MonoBehaviour adapter): integrator and guidance math are testable in isolation.
    /// </summary>
    internal static class TestHelpers
    {
        /// <summary>
        /// Step a missile entity forward by `totalTime` seconds in fixed `dt` increments.
        /// Mirrors what MissileBehaviour.FixedUpdate would do at runtime.
        /// </summary>
        public static void StepFor(MissileEntity entity, float totalTime, float dt = 0.02f)
        {
            int steps = Mathf.CeilToInt(totalTime / dt);
            for (int i = 0; i < steps; i++)
                entity.Step(dt);
        }

        /// <summary>
        /// Build an entity with a given LOD integrator and profile. Tests build the profile
        /// themselves (start from <see cref="MissileProfileData.TestStub"/> and mutate) and
        /// pass it in. This avoids the by-value-struct gotcha with delegate-based overrides.
        /// </summary>
        public static MissileEntity MakeEntity(
            in MissileProfileData profile,
            MissileLod lod = MissileLod.L1_PointMass3Dof,
            ITargetSource target = null)
        {
            IPhysicsIntegrator integrator;
            switch (lod)
            {
                case MissileLod.L0_Kinematic:
                    integrator = new KinematicL0Integrator();
                    break;
                case MissileLod.L1_PointMass3Dof:
                    integrator = new PointMass3DofL1Integrator();
                    break;
                case MissileLod.L2_RateLimited3Dof:
                    integrator = new RateLimited3DofL2Integrator();
                    break;
                case MissileLod.L3_PseudoRb6Dof:
                    integrator = new PseudoRb6DofL3Integrator();
                    break;
                default:
                    throw new System.NotImplementedException($"Tests don't support LOD {lod} yet");
            }

            return new MissileEntity(
                in profile,
                integrator,
                GuidanceFactory.Create(profile.GuidanceLaw),
                StandardAtmosphere.Instance,
                target);
        }

        /// <summary>
        /// A fixed-position, optionally-moving target source for tests. Reports the exact
        /// position/velocity we tell it to — no Unity Transform involved. Stateless;
        /// Update is a no-op.
        /// </summary>
        public sealed class FakeTarget : ITargetSource
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public bool HasTrack = true;

            public void Update(in GuidedFury.Core.State.MissileState missileState, float dt) { /* stateless */ }

            public TargetTrack Sample()
            {
                if (!HasTrack) return TargetTrack.None;
                return TargetTrack.Omniscient(Position, Velocity);
            }
        }
    }
}
