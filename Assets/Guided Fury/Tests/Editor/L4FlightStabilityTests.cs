using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GuidedFury.Core.Aero;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Autopilot;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;
using GuidedFury.ScriptableObjects.Profiles;

namespace GuidedFury.Tests
{
    public class L4FlightStabilityTests
    {
        [Test]
        public void SurfaceFeedbackOpposesExistingPitchAndYawRates()
        {
            var profile = MissileProfileData.TestStub();
            profile.AutopilotGain = 6f;
            profile.MaxControlDeflectionDeg = 25f;
            var state = MissileState.AtRest(Vector3.zero, Quaternion.identity);
            state.Velocity = Vector3.forward * 200f;
            state.AngularVelocity = new Vector3(0.1f, -0.1f, 0f);
            var command = default(MissileCommand);
            var output = SurfaceDeflectionAutopilot.Instance.Compute(in state, in command, in profile);
            Assert.Less((-2f * output.PitchDeflectionRad) * state.AngularVelocity.x, 0f);
            Assert.Less((-2f * output.YawDeflectionRad) * state.AngularVelocity.y, 0f);
        }

        [Test]
        public void NoseAboveFlightPathProducesUpwardLift()
        {
            var profile = MissileProfileData.TestStub();
            profile.BoostThrustN = 0f;
            profile.DragCoefficient = 0f;
            profile.LiftSlopePerRad = 8f;
            profile.WeatherVaneCoefficient = 0f;
            var integrator = new FullAero6DofL4Integrator(SimpleAeroModel.Instance);
            var state = MissileState.AtRest(Vector3.zero, Quaternion.Euler(-5f, 0f, 0f));
            integrator.Initialize(in profile, ref state);
            state.Velocity = Vector3.forward * 300f;
            var command = default(MissileCommand);
            var atmo = AtmosphereSample.SeaLevelIsa;
            integrator.Step(in profile, in command, in atmo, 0.001f, ref state);
            Assert.Greater(state.Velocity.y, -9.80665f * 0.001f, "Lift must oppose gravity for a nose-up airframe.");
        }

        [Test]
        public void EssmRemainsAlignedThroughoutBoostAtBothFixedTimesteps()
        {
            var so = AssetDatabase.LoadAssetAtPath<MissileProfileSO>("Assets/Guided Fury/Examples/Profiles/RIM-162_ESSM.asset");
            Assert.IsNotNull(so);
            var profile = so.Bake();
            foreach (float dt in new[] { 0.02f, 0.005f })
            {
                var aero = new TabulatedAeroModel(so.cdVsMach, so.clAlphaVsMach, so.cmAlphaVsMach, so.cmDeltaVsMach);
                var integrator = new FullAero6DofL4Integrator(aero);
                var state = MissileState.AtRest(new Vector3(0, 100, 0), Quaternion.identity);
                integrator.Initialize(in profile, ref state);
                var command = new MissileCommand { CommandedAccelerationWorld = Vector3.up * 20f };
                var atmo = AtmosphereSample.SeaLevelIsa;
                for (int i = 0; i < Mathf.RoundToInt(6f / dt); i++)
                {
                    integrator.Step(in profile, in command, in atmo, dt, ref state);
                    Assert.Less(Vector3.Angle(state.Orientation * Vector3.forward, state.Velocity), 10f, "L4 departed controlled flight at " + state.TimeOfFlight);
                }
                Assert.Greater(state.Velocity.y, 0f, "Upward guidance must turn the trajectory upward.");
            }
        }
    }
}
