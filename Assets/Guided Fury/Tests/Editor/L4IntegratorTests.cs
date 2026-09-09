using NUnit.Framework;
using UnityEngine;
using GuidedFury.Core.Aero;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Autopilot;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Core.Propulsion;
using GuidedFury.Core.State;

namespace GuidedFury.Tests
{
    /// <summary>
    /// EditMode tests for L4 — focused on the new pieces vs L3: aero coefficient sampling,
    /// Mach computation, control surface deflection rate limit, thrust model selection.
    /// </summary>
    public class L4IntegratorTests
    {
        private const float Dt = 0.02f;

        [Test]
        public void L4_BoostStillAccelerates_RegressionVsL3()
        {
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw    = GuidanceLawKind.None;
            profile.CruiseSpeedMps = 100f;
            profile.BoostThrustN   = 30000f;
            profile.BoostDurationS = 2f;
            profile.DragCoefficient = 0f;
            profile.LiftSlopePerRad = 0f;
            profile.WeatherVaneCoefficient = 0f;
            profile.AeroModel = AeroModelKind.Simple;
            profile.ThrustModel = ThrustModelKind.ConstantBoost;
            profile.Autopilot = AutopilotKind.SimpleRate;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L4_FullAero6Dof);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            float v0 = entity.State.Velocity.magnitude;
            TestHelpers.StepFor(entity, 1.0f, Dt);
            float v1 = entity.State.Velocity.magnitude;

            Assert.Greater(v1, v0, "L4 boost should accelerate the missile");
        }

        [Test]
        public void BoostSustainThrustModel_HoldsSpeedAfterBoost()
        {
            // A pure boost should DECELERATE after burnout (drag > 0, thrust = 0). A
            // boost-sustain motor with sustain thrust > drag should HOLD speed (or accelerate).
            // We test that sustain thrust keeps the missile faster than no-sustain would.
            var boostOnly = MissileProfileData.TestStub();
            boostOnly.GuidanceLaw = GuidanceLawKind.None;
            boostOnly.BoostThrustN = 30000f;
            boostOnly.BoostDurationS = 0.5f;
            boostOnly.SustainThrustN = 0f;
            boostOnly.SustainDurationS = 0f;
            boostOnly.ThrustModel = ThrustModelKind.ConstantBoost;
            boostOnly.AeroModel = AeroModelKind.Simple;
            boostOnly.Autopilot = AutopilotKind.SimpleRate;
            boostOnly.DragCoefficient = 0.5f;     // heavy drag so the difference shows
            boostOnly.ReferenceAreaM2 = 0.05f;
            boostOnly.LiftSlopePerRad = 0f;        // isolate translational effect
            boostOnly.WeatherVaneCoefficient = 0f;

            var bs = boostOnly;
            bs.ThrustModel = ThrustModelKind.BoostSustain;
            bs.SustainThrustN = 8000f;             // less than peak boost, enough to fight drag
            bs.SustainDurationS = 3f;

            var eBoost = TestHelpers.MakeEntity(in boostOnly, MissileLod.L4_FullAero6Dof);
            var eSust  = TestHelpers.MakeEntity(in bs,        MissileLod.L4_FullAero6Dof);
            eBoost.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));
            eSust.Launch(Vector3.zero,  Quaternion.LookRotation(Vector3.forward, Vector3.up));

            TestHelpers.StepFor(eBoost, 2.5f, Dt);
            TestHelpers.StepFor(eSust,  2.5f, Dt);

            Assert.Greater(
                eSust.State.Velocity.magnitude,
                eBoost.State.Velocity.magnitude,
                "Boost-sustain should hold speed better than boost-only against drag");
        }

        [Test]
        public void TabulatedAeroModel_RespectsMachInLookup()
        {
            // Build a Cd curve that's clearly different at M=0.5 vs M=2.0, sample at both,
            // confirm we get the right values.
            var cdCurve = new AnimationCurve(
                new Keyframe(0f,  0.10f),
                new Keyframe(0.5f, 0.20f),
                new Keyframe(1.05f, 0.60f),  // transonic spike
                new Keyframe(2.0f, 0.30f),
                new Keyframe(5.0f, 0.25f));
            var model = new TabulatedAeroModel(cdCurve, null, null, null);

            var profile = MissileProfileData.TestStub();
            var atmo = AtmosphereSample.SeaLevelIsa; // a = 340.3 m/s
            // M=0.5 → v ≈ 170 m/s; M=1.05 → v ≈ 357 m/s; M=2.0 → v ≈ 680 m/s.
            var stateSlow  = MissileState.AtRest(Vector3.zero, Quaternion.identity);
            stateSlow.Velocity = new Vector3(0, 0, 170f);

            var stateTransonic = stateSlow;
            stateTransonic.Velocity = new Vector3(0, 0, 357f);

            var stateFast = stateSlow;
            stateFast.Velocity = new Vector3(0, 0, 680f);

            var slow      = model.Sample(in stateSlow,      in atmo, in profile);
            var transonic = model.Sample(in stateTransonic, in atmo, in profile);
            var fast      = model.Sample(in stateFast,      in atmo, in profile);

            Assert.Greater(transonic.Cd, slow.Cd, "Transonic Cd should be higher than subsonic");
            Assert.Greater(transonic.Cd, fast.Cd, "Transonic Cd should be higher than supersonic");
        }

        [Test]
        public void DeflectionRateLimit_ClampsSurfaceTravel()
        {
            // Verify the integrator rate-limits the surface deflection rather than snapping
            // to the commanded value. Drive a huge command and check that one Dt of motion
            // can't exceed MaxControlRateDegPerSec × Dt.
            var profile = MissileProfileData.TestStub();
            profile.GuidanceLaw = GuidanceLawKind.None;
            profile.MaxControlDeflectionDeg = 90f;
            profile.MaxControlRateDegPerSec = 100f;       // moderate rate so one Dt is small
            profile.Autopilot = AutopilotKind.SurfaceDeflection;
            profile.ThrustModel = ThrustModelKind.ConstantBoost;
            profile.AeroModel = AeroModelKind.Simple;
            profile.AutopilotGain = 1000f;                 // huge gain → autopilot saturates
            profile.BoostDurationS = 0f;
            profile.BoostThrustN = 0f;

            var entity = TestHelpers.MakeEntity(in profile, MissileLod.L4_FullAero6Dof);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));
            entity.State.Velocity = new Vector3(0, 0, 300f);

            // Manually feed a strong command via a fake target source. Easier path: use a target
            // straight up so guidance produces a large vertical accel demand.
            var target = new TestHelpers.FakeTarget
            {
                Position = new Vector3(1000f, 200f, 1000f),
                Velocity = Vector3.zero,
                HasTrack = true,
            };
            profile.GuidanceLaw = GuidanceLawKind.ProportionalNavigation;
            profile.NavigationGain = 5f;
            entity = TestHelpers.MakeEntity(in profile, MissileLod.L4_FullAero6Dof, target);
            entity.Launch(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));
            entity.State.Velocity = new Vector3(0, 0, 300f);

            // One Dt → max possible deflection change = MaxControlRate × Dt = 2°.
            float prev = entity.State.PitchDeflectionRad;
            entity.Step(Dt);
            float delta = Mathf.Abs(entity.State.PitchDeflectionRad - prev);
            float allowed = (profile.MaxControlRateDegPerSec * Mathf.Deg2Rad) * Dt + 1e-3f;

            Assert.LessOrEqual(delta, allowed,
                "Surface deflection rate must be bounded by MaxControlRateDegPerSec × Dt");
        }
    }
}
