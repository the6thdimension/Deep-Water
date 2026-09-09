using NUnit.Framework;
using UnityEngine;
using CarrierOps.Core.Catapult;
using CarrierOps.Core.State;

namespace CarrierOps.Tests
{
    /// <summary>
    /// EditMode tests for the catapult state machine. Exercises stage transitions, the
    /// Firing physics, and JBD slaving.
    /// </summary>
    public class CatapultCycleTests
    {
        private const float Dt = 0.02f;

        private static CarrierProfileData TestProfile()
        {
            var p = CarrierProfileData.FordClass();
            // Compress timings so tests finish quickly.
            p.CatapultSpottedDurationS  = 0.2f;
            p.CatapultTensionedDurationS = 0.2f;
            p.CatapultReadyDurationS    = 0.1f;
            p.CatapultRetractDurationS  = 0.5f;
            return p;
        }

        private static void StepFor(in CarrierProfileData profile, ref CatapultState cat, float totalS)
        {
            int steps = Mathf.CeilToInt(totalS / Dt);
            for (int i = 0; i < steps; i++)
                CatapultCycle.Step(in profile, Dt, ref cat);
        }

        [Test]
        public void Idle_IgnoresStepUntilRequested()
        {
            var profile = TestProfile();
            var cat = CatapultState.Idle;
            StepFor(in profile, ref cat, 1.0f);
            Assert.AreEqual(CatapultStage.Idle, cat.Stage, "Idle catapult must not advance on its own");
        }

        [Test]
        public void RequestLaunch_AdvancesThroughAllStagesAndReturnsToIdle()
        {
            var profile = TestProfile();
            var cat = CatapultState.Idle;
            CatapultCycle.RequestLaunch(ref cat);
            Assert.AreEqual(CatapultStage.Spotted, cat.Stage);

            // Step long enough to cover all stages: 0.2 + 0.2 + 0.1 + firing + 0.5 retract.
            // Firing should be on the order of a few hundred ms.
            StepFor(in profile, ref cat, 5.0f);

            Assert.AreEqual(CatapultStage.Idle, cat.Stage, "Full cycle must terminate back at Idle");
            Assert.AreEqual(0f, cat.ShuttleDistanceM, 0.1f, "Shuttle should be back at start");
            Assert.IsFalse(cat.JbdRaised, "JBD must be lowered at the end of the cycle");
        }

        [Test]
        public void Firing_ProducesExpectedEndSpeed()
        {
            var profile = TestProfile();
            var cat = CatapultState.Idle;
            CatapultCycle.RequestLaunch(ref cat);

            // Step until we leave Firing.
            float lastVelocity = 0f;
            for (int i = 0; i < 1000; i++)
            {
                CatapultCycle.Step(in profile, Dt, ref cat);
                if (cat.Stage == CatapultStage.Firing) lastVelocity = cat.ShuttleVelocityMps;
                if (cat.Stage == CatapultStage.Retracting) break;
            }

            // The shuttle should have reached approximately the profile's end speed by the
            // time it leaves Firing — within 10% (Euler integration accumulates error).
            Assert.AreEqual(profile.CatapultEndSpeedMps, lastVelocity, profile.CatapultEndSpeedMps * 0.10f,
                "Catapult should end Firing at approximately the requested end speed");
        }

        [Test]
        public void JBD_RaisedDuringTensionedThroughFiring_LoweredDuringRetracting()
        {
            var profile = TestProfile();
            var cat = CatapultState.Idle;
            CatapultCycle.RequestLaunch(ref cat);

            // Phase 1: through Spotted — JBD still down.
            StepFor(in profile, ref cat, profile.CatapultSpottedDurationS + Dt);
            Assert.AreEqual(CatapultStage.Tensioned, cat.Stage);
            // After at least one Step in Tensioned, JBD must be up.
            Assert.IsTrue(cat.JbdRaised, "JBD should be raised during Tensioned");

            // Step into Retracting.
            for (int i = 0; i < 1000 && cat.Stage != CatapultStage.Retracting; i++)
                CatapultCycle.Step(in profile, Dt, ref cat);

            Assert.AreEqual(CatapultStage.Retracting, cat.Stage);
            // First Step in Retracting clears JbdRaised.
            CatapultCycle.Step(in profile, Dt, ref cat);
            Assert.IsFalse(cat.JbdRaised, "JBD should be lowered during Retracting");
        }

        [Test]
        public void PeakGClipsExcessiveDemand()
        {
            // Build a profile that demands a launch unachievable within PeakG: ridiculous
            // end speed, normal stroke. The integrator should clip at PeakG and end at less
            // than the requested speed.
            var profile = TestProfile();
            profile.CatapultStrokeM = 50f;       // short
            profile.CatapultEndSpeedMps = 200f;  // huge
            profile.CatapultPeakG = 3f;          // realistic clip

            var cat = CatapultState.Idle;
            CatapultCycle.RequestLaunch(ref cat);

            float endOfFiringSpeed = 0f;
            for (int i = 0; i < 5000; i++)
            {
                CatapultCycle.Step(in profile, Dt, ref cat);
                if (cat.Stage == CatapultStage.Firing) endOfFiringSpeed = cat.ShuttleVelocityMps;
                if (cat.Stage == CatapultStage.Retracting) break;
            }

            Assert.Less(endOfFiringSpeed, profile.CatapultEndSpeedMps,
                "When PeakG clips, end speed should be less than requested");
        }
    }
}
