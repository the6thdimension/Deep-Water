using NUnit.Framework;
using UnityEngine;
using CarrierOps.Core.Carrier;
using CarrierOps.Core.Movement;
using CarrierOps.Core.State;

namespace CarrierOps.Tests
{
    /// <summary>
    /// EditMode tests for the CarrierEntity-level integration of subsystems. Determinism
    /// across runs, basic state plumbing, and the catapult request API.
    /// </summary>
    public class CarrierEntityTests
    {
        private const float Dt = 0.02f;

        [Test]
        public void Entity_ProducesSameStateAcrossIdenticalRuns()
        {
            // Same seed + same commands + same dt → identical state.
            var profile = CarrierProfileData.FordClass();
            profile.SeaState.Seed = 7777;

            var a = new CarrierEntity(in profile);
            var b = new CarrierEntity(in profile);

            var command = new ShipCommand { ThrottleNormalized = 0.5f, RudderNormalized = 0.2f };

            for (int i = 0; i < 100; i++)
            {
                a.Step(in command, Dt);
                b.Step(in command, Dt);
            }

            Assert.AreEqual(a.State.Position.x, b.State.Position.x, 1e-5f);
            Assert.AreEqual(a.State.Position.y, b.State.Position.y, 1e-5f);
            Assert.AreEqual(a.State.Position.z, b.State.Position.z, 1e-5f);
            Assert.AreEqual(a.State.HeadingDeg, b.State.HeadingDeg, 1e-5f);
            Assert.AreEqual(a.State.SwayRollDeg, b.State.SwayRollDeg, 1e-5f);
        }

        [Test]
        public void RequestCatapultLaunch_BeginsCycle()
        {
            var profile = CarrierProfileData.FordClass();
            var entity = new CarrierEntity(in profile);

            entity.RequestCatapultLaunch(0);
            Assert.AreEqual(CatapultStage.Spotted, entity.State.Catapults[0].Stage,
                "Catapult 0 should leave Idle on launch request");

            // Other catapults untouched.
            for (int i = 1; i < entity.State.Catapults.Length; i++)
                Assert.AreEqual(CatapultStage.Idle, entity.State.Catapults[i].Stage,
                    $"Catapult {i} should remain Idle when not requested");
        }

        [Test]
        public void RequestElevator_FlipsCommand()
        {
            var profile = CarrierProfileData.FordClass();
            var entity = new CarrierEntity(in profile);

            Assert.IsFalse(entity.State.Elevators[0].CommandUp);
            entity.RequestElevator(0, true);
            Assert.IsTrue(entity.State.Elevators[0].CommandUp);
        }

        [Test]
        public void Entity_AccumulatesTimeOfSim()
        {
            var profile = CarrierProfileData.FordClass();
            var entity = new CarrierEntity(in profile);

            var command = new ShipCommand();
            for (int i = 0; i < 1000; i++)
                entity.Step(in command, Dt);

            Assert.AreEqual(1000 * Dt, (float)entity.State.TimeOfSim, 1e-3f);
        }
    }
}
