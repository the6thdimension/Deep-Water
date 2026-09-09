using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GuidedFury.Core.Aero;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Autopilot;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Core.Propulsion;
using GuidedFury.Core.Seekers;
using GuidedFury.Core.State;
using GuidedFury.ScriptableObjects.Profiles;

namespace GuidedFury.Tests
{
    /// <summary>
    /// Regression tests for the 2026-09-08 terminal-guidance investigation.
    ///
    /// The bug: the RIM-162 ESSM overflew a stationary ground target at ~40 m altitude and
    /// detonated ~150 m past it. Two physical defects stacked:
    /// 1. The authored ClAlpha curve was body-alone slender-body theory (~8 → 3.5 /rad),
    ///    capping the airframe at ~9 g at Mach 1.5 — a 50 g-class SAM could not make its
    ///    rated maneuvers, so ProNav saturated and missed.
    /// 2. The cone seeker dropped lock instantly at the terminal LOS swing (a body-fixed
    ///    cone ALWAYS breaks lock in the last tens of meters) and guidance went blind.
    ///
    /// Fixes: effective whole-airframe ClAlpha values on the profile, and seeker track
    /// memory (coast) that dead-reckons through break-lock.
    ///
    /// NOTE: below-horizon ground targets are OUT OF the ESSM's design envelope (it's a
    /// surface-to-AIR missile, and ProNav's collision course passes through terrain). The
    /// ground-target test below asserts a bounded miss, not a hit — the tracked follow-up
    /// for true surface attack is gravity-bias PN + trajectory shaping (see ROADMAP).
    /// </summary>
    public class TerminalGuidanceRegressionTests
    {
        private const string EssmAssetPath = "Assets/Guided Fury/Examples/Profiles/RIM-162_ESSM.asset";

        // ---- Seeker coast unit behavior ----------------------------------------

        [Test]
        public void Seeker_CoastZero_DropsLockImmediately_LegacyBehavior()
        {
            var seeker = new SimpleConeSeeker(new SeekerProfile
            {
                FovDeg = 30f, MaxRangeM = 5000f, AcquisitionTimeS = 0.1f, CoastTimeS = 0f,
            });

            var state = MissileState.AtRest(Vector3.zero, Quaternion.identity);
            var inCone = TargetTrack.Omniscient(new Vector3(0f, 0f, 100f), Vector3.zero);
            var outOfCone = TargetTrack.Omniscient(new Vector3(100f, 0f, -100f), Vector3.zero);

            for (int i = 0; i < 10; i++) seeker.Update(in state, in inCone, 0.02f);
            Assert.IsTrue(seeker.HasLock, "Seeker should lock after dwell.");

            seeker.Update(in state, in outOfCone, 0.02f);
            Assert.IsFalse(seeker.HasLock, "CoastTimeS = 0 must preserve the legacy instant break-lock.");
        }

        [Test]
        public void Seeker_Coast_HoldsDeadReckonedTrack_ThenExpires()
        {
            var seeker = new SimpleConeSeeker(new SeekerProfile
            {
                FovDeg = 30f, MaxRangeM = 5000f, AcquisitionTimeS = 0.1f, CoastTimeS = 0.5f,
            });

            var state = MissileState.AtRest(Vector3.zero, Quaternion.identity);
            var targetVel = new Vector3(50f, 0f, 0f);
            var inCone = TargetTrack.Omniscient(new Vector3(0f, 0f, 100f), targetVel);
            var outOfCone = TargetTrack.Omniscient(new Vector3(100f, 0f, -100f), targetVel);

            for (int i = 0; i < 10; i++) seeker.Update(in state, in inCone, 0.02f);
            Assert.IsTrue(seeker.HasLock);
            Vector3 lastRealPos = seeker.GetObservation().Position;

            // Break geometric lock: seeker must coast, dead-reckoning the observation.
            seeker.Update(in state, in outOfCone, 0.02f);
            Assert.IsTrue(seeker.HasLock, "Seeker must coast through break-lock.");
            Vector3 coasted = seeker.GetObservation().Position;
            Assert.AreEqual(lastRealPos.x + targetVel.x * 0.02f, coasted.x, 1e-4f,
                "Coasted track must dead-reckon with the last observed velocity.");

            // Exhaust the coast budget: lock must drop.
            for (int i = 0; i < 30; i++) seeker.Update(in state, in outOfCone, 0.02f);
            Assert.IsFalse(seeker.HasLock, "Coast must expire after CoastTimeS.");
        }

        // ---- Whole-engagement regressions (deterministic pure-core flights) ----

        [Test]
        public void Essm_HeadOnAirTarget_InterceptsWithinFuzeRadius()
        {
            float miss = FlyEssmAgainst(
                targetPos: new Vector3(0f, 150f, 2000f),
                targetVel: new Vector3(0f, 0f, -200f));
            Assert.LessOrEqual(miss, 10f,
                "Head-on air intercept is a core design case and must close inside the fuze radius.");
        }

        [Test]
        public void Essm_TailChaseAirTarget_InterceptsWithinFuzeRadius()
        {
            float miss = FlyEssmAgainst(
                targetPos: new Vector3(0f, 100f, 800f),
                targetVel: new Vector3(0f, 0f, 150f));
            Assert.LessOrEqual(miss, 10f,
                "Tail-chase air intercept is a core design case and must close inside the fuze radius.");
        }

        [Test]
        public void Essm_CrossingAirTarget_ClosesToNearMissBand()
        {
            float miss = FlyEssmAgainst(
                targetPos: new Vector3(-300f, 60f, 1500f),
                targetVel: new Vector3(80f, 0f, 0f));
            // Crossing shots are the hardest geometry for pure PN + body-fixed seeker;
            // pre-fix this was ~48 m. Blast radius (25 m) covers the current band.
            Assert.LessOrEqual(miss, 20f,
                "Crossing air intercept regressed past the near-miss band the 2026-09-08 fixes established.");
        }

        [Test]
        public void Essm_VerticalLaunch_PitchesOverAndClosesOnAirTarget()
        {
            // VLS: launch straight up at an air target ~90 deg off boresight. Pure ProNav is
            // degenerate here (closing speed ~0 -> no command -> ballistic climb; pre-fix the
            // missile reached a 10 km apogee and missed by 3.1 km). The Pursuit->ProNav blend
            // + midcourse datalink must pitch it over and close inside the blast radius.
            var so = AssetDatabase.LoadAssetAtPath<MissileProfileSO>(EssmAssetPath);
            Assert.IsNotNull(so);
            MissileProfileData data = so.Bake();

            var aero = new TabulatedAeroModel(so.cdVsMach, so.clAlphaVsMach, so.cmAlphaVsMach, so.cmDeltaVsMach);
            var integrator = new FullAero6DofL4Integrator(
                aero, AutopilotFactory.Create(data.Autopilot), ThrustModelFactory.Create(data.ThrustModel));
            var guidance = GuidanceFactory.Create(data.GuidanceLaw);

            var targetGo = new GameObject("VlsTarget");
            try
            {
                Vector3 center = new Vector3(0f, 300f, 3000f);
                targetGo.transform.position = center;
                var seeker = new SimpleConeSeeker(new SeekerProfile
                {
                    FovDeg = data.SeekerFovDeg, MaxRangeM = data.SeekerMaxRangeM,
                    AcquisitionTimeS = data.SeekerAcquisitionTimeS, CoastTimeS = data.SeekerCoastTimeS,
                });
                var entity = new MissileEntity(in data, integrator, guidance, StandardAtmosphere.Instance,
                    new SeekerTargetSource(seeker, new TransformTargetSource(targetGo.transform), data.SeekerMidcourseDatalink));
                entity.Launch(new Vector3(0f, 3.5f, 0f), Quaternion.LookRotation(Vector3.up, Vector3.forward));

                const float dt = 0.02f;
                float minDist = float.MaxValue, apogee = 0f;
                for (int i = 0; i < 3000; i++)
                {
                    float a = (90f * i * dt) / 800f; // orbiting drone at r=800, 90 m/s
                    targetGo.transform.position = center + new Vector3(Mathf.Cos(a) * 800f, 0f, Mathf.Sin(a) * 800f);
                    entity.Step(dt);
                    var st = entity.State;
                    if (st.Position.y > apogee) apogee = st.Position.y;
                    float d = (targetGo.transform.position - st.Position).magnitude;
                    if (d < minDist) minDist = d;
                    if (st.Phase == MissilePhase.Detonated || st.Phase == MissilePhase.Failed || st.Position.y < 0f) break;
                }
                Assert.LessOrEqual(minDist, 25f, "VLS shot must pitch over and close inside the blast radius, not climb ballistically.");
                // Pre-fix (pure ProNav) the VLS round climbed to a ~10.3 km apogee and missed by
                // 3.1 km. The blend keeps apogee well under that even for an energetic pitch-over;
                // 8 km is the guard that separates "pitched over and homed" from "ballistic climb".
                Assert.Less(apogee, 8000f, "VLS shot must not climb ballistically (pre-fix apogee was ~10.3 km).");
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Essm_StationaryGroundTarget_MissIsBounded_OutOfEnvelopeCase()
        {
            float miss = FlyEssmAgainst(
                targetPos: new Vector3(0f, 1.5f, 600f),
                targetVel: Vector3.zero);
            // Below-horizon ground shots are out of envelope. Pre-fix the missile sailed
            // over at ~40 m altitude and detonated ~150 m late (44 m minimum distance and
            // no terminal dive at all). Post-fix it dives to impact beside the target.
            // 30 m keeps that behavior pinned without pretending this is a hit-capable case.
            Assert.LessOrEqual(miss, 30f,
                "Ground-target miss regressed past the bounded band the 2026-09-08 fixes established.");
        }

        // ---- Harness -----------------------------------------------------------

        /// <summary>
        /// Deterministic pure-core flight of the authored RIM-162 asset against a
        /// constant-velocity target. Mirrors MissileBehaviour.Launch's construction path
        /// (tabulated aero + factories + cone seeker wrapping a transform truth source),
        /// stepped at the standard 0.02 s fixed dt. Returns the minimum missile-target
        /// distance over the flight.
        /// </summary>
        private static float FlyEssmAgainst(Vector3 targetPos, Vector3 targetVel)
        {
            var so = AssetDatabase.LoadAssetAtPath<MissileProfileSO>(EssmAssetPath);
            Assert.IsNotNull(so, "RIM-162 profile asset missing — run Guided Fury > Authored Missiles > Build RIM-162 ESSM Profile.");
            MissileProfileData data = so.Bake();

            var aero = new TabulatedAeroModel(so.cdVsMach, so.clAlphaVsMach, so.cmAlphaVsMach, so.cmDeltaVsMach);
            var integrator = new FullAero6DofL4Integrator(
                aero, AutopilotFactory.Create(data.Autopilot), ThrustModelFactory.Create(data.ThrustModel));
            var guidance = GuidanceFactory.Create(data.GuidanceLaw);

            var targetGo = new GameObject("RegressionTarget");
            try
            {
                targetGo.transform.position = targetPos;
                var seeker = new SimpleConeSeeker(new SeekerProfile
                {
                    FovDeg = data.SeekerFovDeg,
                    MaxRangeM = data.SeekerMaxRangeM,
                    AcquisitionTimeS = data.SeekerAcquisitionTimeS,
                    CoastTimeS = data.SeekerCoastTimeS,
                });
                var entity = new MissileEntity(in data, integrator, guidance,
                    StandardAtmosphere.Instance, new SeekerTargetSource(seeker, new TransformTargetSource(targetGo.transform),
                        data.SeekerMidcourseDatalink)); // mirror MissileBehaviour's construction

                // Launcher behavior: yaw toward the target, fixed ~11 deg rail elevation.
                Vector3 launchPos = new Vector3(0f, 4f, 3f);
                Vector3 flat = targetPos - launchPos; flat.y = 0f;
                Vector3 dir = Quaternion.LookRotation(flat.normalized, Vector3.up) * new Vector3(0f, 0.2f, 1f).normalized;
                entity.Launch(launchPos, Quaternion.LookRotation(dir, Vector3.up));

                const float dt = 0.02f;
                float minDist = float.MaxValue;
                for (int i = 0; i < 3000; i++)
                {
                    targetGo.transform.position = targetPos + targetVel * (i * dt);
                    entity.Step(dt);
                    var st = entity.State;
                    float dist = (targetGo.transform.position - st.Position).magnitude;
                    if (dist < minDist) minDist = dist;
                    if (st.Phase == MissilePhase.Detonated || st.Phase == MissilePhase.Failed) break;
                    if (st.Position.y < 0f) break; // terrain (pure core has no colliders)
                }
                return minDist;
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
            }
        }
    }
}
