using NUnit.Framework;
using UnityEngine;
using CarrierOps.Core.Recovery;
using CarrierOps.Core.State;

namespace CarrierOps.Tests
{
    /// <summary>
    /// EditMode tests for the arresting gear state machine. Same pattern as
    /// CatapultCycleTests — the state machine is tested directly, the aircraft is a
    /// simple test stub that implements IRecoveringAircraft.
    /// </summary>
    public class ArrestingGearTests
    {
        private const float Dt = 0.02f;

        private static CarrierProfileData Profile()
        {
            var p = CarrierProfileData.FordClass();
            // Short retract so tests finish quickly.
            p.WireRetractDurationS = 0.2f;
            return p;
        }

        /// <summary>Minimal IRecoveringAircraft for tests — tracks a 1-D velocity along +Z.</summary>
        private sealed class StubAircraft : IRecoveringAircraft
        {
            public int RegistrationId { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 HookTipPosition => Position;
            public bool HookDown { get; set; }
            public Vector3 Velocity { get; set; }
            public bool RanOutOfWire = false;

            public void ApplyDeceleration(float decelMagnitudeMps2, float dt)
            {
                Vector3 v = Velocity;
                float speed = v.magnitude;
                if (speed < 1e-3f) return;
                float newSpeed = Mathf.Max(0f, speed - decelMagnitudeMps2 * dt);
                Velocity = v * (newSpeed / speed);
                Position += v.normalized * speed * dt; // advance position naively
            }
        }

        [Test]
        public void Idle_IgnoresStepUntilRequested()
        {
            var p = Profile();
            var wire = WireState.Idle;
            var aircraft = new StubAircraft();

            for (int i = 0; i < 50; i++)
                ArrestingGear.Step(in p, Dt, ref wire, aircraft);

            Assert.AreEqual(WireStage.Idle, wire.Stage);
        }

        [Test]
        public void RequestEngage_AdvancesToDecelerating()
        {
            var p = Profile();
            var wire = WireState.Idle;
            ArrestingGear.RequestEngage(ref wire, aircraftId: 1, aircraftSpeedAtCatch: 65f);
            Assert.AreEqual(WireStage.Engaged, wire.Stage);

            // One Step moves us out of Engaged (single-step latch) into Decelerating.
            ArrestingGear.Step(in p, Dt, ref wire, new StubAircraft());
            Assert.AreEqual(WireStage.Decelerating, wire.Stage);
        }

        [Test]
        public void Deceleration_BringsAircraftToStop()
        {
            // Aircraft enters at 65 m/s (~127 kt) along +Z. With 1.5g decel that takes
            // ~4.4 s and ~143 m to stop. Well within profile.WireStrokeM = 95m? No — 143m > 95m.
            // So we'd actually break stroke before stopping. Test instead with a shorter
            // initial speed.
            var p = Profile();
            var wire = WireState.Idle;
            var aircraft = new StubAircraft
            {
                Position = Vector3.zero,
                Velocity = new Vector3(0f, 0f, 40f),
                HookDown = true,
            };

            ArrestingGear.RequestEngage(ref wire, aircraftId: 1, aircraftSpeedAtCatch: 40f);

            // Step until Retracting.
            for (int i = 0; i < 10000 && wire.Stage != WireStage.Retracting; i++)
                ArrestingGear.Step(in p, Dt, ref wire, aircraft);

            Assert.AreEqual(WireStage.Retracting, wire.Stage);
            Assert.Less(aircraft.Velocity.magnitude, 1.5f, "Aircraft should be (nearly) stopped");
        }

        [Test]
        public void StrokeOverrun_ForcesRetractEvenIfMoving()
        {
            // Excessive entry speed → wire reaches its stroke limit before stopping the
            // aircraft. Real-world equivalent of an unsafe trap; in sim terms we just
            // transition to Retracting and the aircraft is released still moving.
            var p = Profile();
            p.WireStrokeM = 20f;   // short stroke to force overrun
            var wire = WireState.Idle;
            var aircraft = new StubAircraft
            {
                Position = Vector3.zero,
                Velocity = new Vector3(0f, 0f, 80f),
                HookDown = true,
            };

            ArrestingGear.RequestEngage(ref wire, aircraftId: 1, aircraftSpeedAtCatch: 80f);

            for (int i = 0; i < 10000 && wire.Stage != WireStage.Retracting; i++)
                ArrestingGear.Step(in p, Dt, ref wire, aircraft);

            Assert.AreEqual(WireStage.Retracting, wire.Stage);
            Assert.Greater(aircraft.Velocity.magnitude, 1f,
                "Stroke overrun should release the aircraft while still moving");
        }

        [Test]
        public void FullCycle_ReturnsToIdle()
        {
            var p = Profile();
            var wire = WireState.Idle;
            var aircraft = new StubAircraft
            {
                Position = Vector3.zero,
                Velocity = new Vector3(0f, 0f, 30f),
                HookDown = true,
            };

            ArrestingGear.RequestEngage(ref wire, aircraftId: 1, aircraftSpeedAtCatch: 30f);

            for (int i = 0; i < 10000 && wire.Stage != WireStage.Idle; i++)
                ArrestingGear.Step(in p, Dt, ref wire, aircraft);

            Assert.AreEqual(WireStage.Idle, wire.Stage);
            Assert.AreEqual(0, wire.EngagedAircraftId, "Idle should clear the engaged aircraft");
            Assert.AreEqual(0f, wire.RunoutMeters, 1e-3f);
        }

        [Test]
        public void EngageRequestIgnoredWhenNotIdle()
        {
            var p = Profile();
            var wire = WireState.Idle;
            ArrestingGear.RequestEngage(ref wire, 1, 30f);
            Assert.AreEqual(1, wire.EngagedAircraftId);

            // Second request with a different aircraft must NOT replace the current engagement.
            ArrestingGear.RequestEngage(ref wire, 2, 50f);
            Assert.AreEqual(1, wire.EngagedAircraftId, "Active wire should not accept a second engagement request");
        }
    }
}
