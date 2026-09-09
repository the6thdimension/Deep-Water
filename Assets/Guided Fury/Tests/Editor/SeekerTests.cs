using NUnit.Framework;
using UnityEngine;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Core.Seekers;
using GuidedFury.Core.State;

namespace GuidedFury.Tests
{
    /// <summary>
    /// EditMode tests for the seeker subsystem: SimpleConeSeeker acquisition logic, and
    /// SeekerTargetSource gating of guidance observation.
    /// </summary>
    public class SeekerTests
    {
        private const float Dt = 0.02f;

        private static MissileState MakeMissileState(Vector3 position, Vector3 forward)
        {
            return new MissileState
            {
                Position = position,
                Velocity = forward * 100f,
                Orientation = Quaternion.LookRotation(forward, Vector3.up),
                Mass = 100f,
                Phase = MissilePhase.Cruise,
            };
        }

        private static SeekerProfile MakeProfile(float fovDeg = 20f, float maxRange = 5000f, float acqTime = 0.1f)
        {
            return new SeekerProfile
            {
                FovDeg = fovDeg,
                MaxRangeM = maxRange,
                AcquisitionTimeS = acqTime,
            };
        }

        [Test]
        public void ConeSeeker_TargetInFovInRangeAfterDwell_Locks()
        {
            var seeker = new SimpleConeSeeker(MakeProfile(fovDeg: 30f, maxRange: 5000f, acqTime: 0.1f));
            var missile = MakeMissileState(Vector3.zero, Vector3.forward);
            var truth = TargetTrack.Omniscient(new Vector3(0f, 0f, 1000f), Vector3.zero);

            // Step enough times to exceed the dwell time.
            int steps = Mathf.CeilToInt(0.2f / Dt);
            for (int i = 0; i < steps; i++)
                seeker.Update(in missile, in truth, Dt);

            Assert.IsTrue(seeker.HasLock, "Seeker should lock when target is in FOV, in range, after dwell");
            Assert.IsTrue(seeker.GetObservation().HasTrack);
        }

        [Test]
        public void ConeSeeker_TargetOutsideFov_DoesNotLock()
        {
            var seeker = new SimpleConeSeeker(MakeProfile(fovDeg: 20f, maxRange: 5000f, acqTime: 0.05f));
            var missile = MakeMissileState(Vector3.zero, Vector3.forward);

            // Target is 90° to the side — way outside any reasonable FOV.
            var truth = TargetTrack.Omniscient(new Vector3(1000f, 0f, 0f), Vector3.zero);

            for (int i = 0; i < 50; i++)
                seeker.Update(in missile, in truth, Dt);

            Assert.IsFalse(seeker.HasLock, "Seeker should not lock when target is outside FOV");
            Assert.IsFalse(seeker.GetObservation().HasTrack);
        }

        [Test]
        public void ConeSeeker_TargetOutOfRange_DoesNotLock()
        {
            var seeker = new SimpleConeSeeker(MakeProfile(fovDeg: 30f, maxRange: 1000f, acqTime: 0.05f));
            var missile = MakeMissileState(Vector3.zero, Vector3.forward);

            // Target straight ahead at 2 km — twice the max range.
            var truth = TargetTrack.Omniscient(new Vector3(0f, 0f, 2000f), Vector3.zero);

            for (int i = 0; i < 50; i++)
                seeker.Update(in missile, in truth, Dt);

            Assert.IsFalse(seeker.HasLock, "Seeker should not lock when target is out of range");
        }

        [Test]
        public void ConeSeeker_BeforeAcquisitionTime_DoesNotLock()
        {
            // Long acquisition time. After a short dwell, should not yet be locked.
            var seeker = new SimpleConeSeeker(MakeProfile(fovDeg: 30f, maxRange: 5000f, acqTime: 1.0f));
            var missile = MakeMissileState(Vector3.zero, Vector3.forward);
            var truth = TargetTrack.Omniscient(new Vector3(0f, 0f, 1000f), Vector3.zero);

            // Dwell for only 0.1 s — well under the 1 s acquisition time.
            int steps = Mathf.CeilToInt(0.1f / Dt);
            for (int i = 0; i < steps; i++)
                seeker.Update(in missile, in truth, Dt);

            Assert.IsFalse(seeker.HasLock, "Seeker should not lock before acquisition dwell completes");
        }

        [Test]
        public void ConeSeeker_LockBroken_WhenTargetExitsFov()
        {
            var seeker = new SimpleConeSeeker(MakeProfile(fovDeg: 20f, maxRange: 5000f, acqTime: 0.05f));
            var missile = MakeMissileState(Vector3.zero, Vector3.forward);

            // Phase 1: target in FOV — let seeker lock.
            var truthIn = TargetTrack.Omniscient(new Vector3(0f, 0f, 1000f), Vector3.zero);
            for (int i = 0; i < 20; i++)
                seeker.Update(in missile, in truthIn, Dt);

            Assert.IsTrue(seeker.HasLock, "Precondition: seeker should be locked after dwell");

            // Phase 2: target jumps outside FOV.
            var truthOut = TargetTrack.Omniscient(new Vector3(1500f, 0f, 0f), Vector3.zero);
            seeker.Update(in missile, in truthOut, Dt);

            Assert.IsFalse(seeker.HasLock, "Seeker should drop lock when target exits FOV");
        }

        [Test]
        public void SeekerTargetSource_NotLocked_ReturnsNone()
        {
            // Build a seeker with a truth source that points to a target out of range, so the
            // seeker never locks.
            var truth = new TestHelpers.FakeTarget
            {
                Position = new Vector3(0f, 0f, 10000f), // out of range
                Velocity = Vector3.zero,
                HasTrack = true,
            };
            var seeker = new SimpleConeSeeker(MakeProfile(fovDeg: 30f, maxRange: 1000f, acqTime: 0.05f));
            var source = new SeekerTargetSource(seeker, truth);

            var missile = MakeMissileState(Vector3.zero, Vector3.forward);

            for (int i = 0; i < 20; i++)
                source.Update(in missile, Dt);

            Assert.IsFalse(source.HasLock);
            Assert.IsFalse(source.Sample().HasTrack, "Without lock, the source must report no track");
        }

        [Test]
        public void SeekerTargetSource_LockedReturnsTruth()
        {
            var truth = new TestHelpers.FakeTarget
            {
                Position = new Vector3(0f, 0f, 1000f),
                Velocity = new Vector3(50f, 0f, 0f),
                HasTrack = true,
            };
            var seeker = new SimpleConeSeeker(MakeProfile(fovDeg: 30f, maxRange: 5000f, acqTime: 0.05f));
            var source = new SeekerTargetSource(seeker, truth);

            var missile = MakeMissileState(Vector3.zero, Vector3.forward);

            // Step until lock acquired.
            for (int i = 0; i < 20; i++)
                source.Update(in missile, Dt);

            Assert.IsTrue(source.HasLock);
            TargetTrack obs = source.Sample();
            Assert.IsTrue(obs.HasTrack);
            // Phase 3: seeker passes truth through verbatim (no noise model yet).
            Assert.AreEqual(truth.Position, obs.Position);
            Assert.AreEqual(truth.Velocity, obs.Velocity);
        }

        [Test]
        public void EndToEnd_L2WithConeSeekerAndProNav_AcquiresAndCloses()
        {
            // The integration test: missile flying L2 physics, with a cone seeker filtering
            // truth, using ProNav. Place the target straight ahead inside FOV; the missile
            // should acquire, lock, and close on it.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw    = GuidanceLawKind.ProportionalNavigation;
            profile.NavigationGain = 3f;
            profile.CruiseSpeedMps = 300f;
            profile.BoostThrustN   = 20000f;
            profile.BoostDurationS = 5f;
            profile.MaxLifetimeS   = 20f;
            profile.MaxLoadFactorG = 40f;
            profile.MaxTurnRateDegPerSec = 90f;
            profile.SeekerKind = SeekerKind.ConeSeeker;
            profile.SeekerFovDeg = 30f;
            profile.SeekerMaxRangeM = 5000f;
            profile.SeekerAcquisitionTimeS = 0.1f;

            var truth = new TestHelpers.FakeTarget
            {
                Position = new Vector3(150f, 0f, 2000f),
                Velocity = Vector3.zero,
                HasTrack = true,
            };
            // Build the chain manually so the test exercises the same wiring as the runtime.
            var seekerProfile = new SeekerProfile
            {
                FovDeg = profile.SeekerFovDeg,
                MaxRangeM = profile.SeekerMaxRangeM,
                AcquisitionTimeS = profile.SeekerAcquisitionTimeS,
            };
            var seeker = SeekerFactory.Create(profile.SeekerKind, seekerProfile);
            var source = new SeekerTargetSource(seeker, truth);

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L2_RateLimited3Dof, source);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            float initialRange = Vector3.Distance(entity.State.Position, truth.Position);
            TestHelpers.StepFor(entity, 5.0f, Dt);
            float finalRange = Vector3.Distance(entity.State.Position, truth.Position);

            Assert.Less(finalRange, initialRange * 0.4f,
                "L2 + ConeSeeker + ProNav should close substantially on an in-FOV target");
        }
    }
}
