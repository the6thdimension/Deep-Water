using NUnit.Framework;
using UnityEngine;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Orders;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>Helm L0 logic on its own: orders, compass arithmetic, and the controllers against a stand-in ship.</summary>
    public class HelmL0Tests
    {
        /// <summary>A ship that answers with simple fixed numbers: 1 rev/s per 4 m/s, a 500 m turn at any speed.</summary>
        sealed class StandInShip : IShipAnswers
        {
            public float SteadyShaftRps(float speedMs) => speedMs / 4f;
            public float TurningRadiusM(float speedMs, float rudderDeg) => 500f;
            public float MaxRudderDeg => 35f;
            public float MaxShaftRps => 3f;
            public float MinShaftRps => -2f;
        }

        static HelmProfileData Profile() => new HelmProfileData
        {
            AheadBellKn = new[] { 0f, 5f, 10f, 15f, 20f, 31f },
            AsternBellFraction = new[] { 1f / 3f, 2f / 3f, 0.85f, 1f },
            CourseKp = 2f, CourseKd = 10f, CourseKi = 0f, CourseMaxRudderDeg = 20f,
            SpeedKp = 0.1f, SpeedKi = 0f,
            RouteRudderDeg = 15f, LookAheadShipLengths = 2f, ShipLengthM = 140f,
        };

        [Test]
        public void Compass_NormalizesAndTakesTheShortWayRound()
        {
            Assert.AreEqual(350f, CompassMath.Normalize(-10f), 1e-4f);
            Assert.AreEqual(10f, CompassMath.Normalize(370f), 1e-4f);
            Assert.AreEqual(20f, CompassMath.Difference(350f, 10f), 1e-4f, "350 → 10 is 20° to starboard");
            Assert.AreEqual(-20f, CompassMath.Difference(10f, 350f), 1e-4f, "10 → 350 is 20° to port");
            Assert.AreEqual(180f, CompassMath.Difference(0f, 180f), 1e-4f);
        }

        [Test]
        public void HelmCommand_BellsAndRudderChangeOnlyTheirOwnHalf()
        {
            var c = HelmCommand.Stopped(90f);
            c = HelmCommand.Apply(c, Order.Helm(OrderIssuer.Captain, 45f, 18f));
            Assert.AreEqual(CourseMode.Hold, c.Course);
            Assert.AreEqual(SpeedMode.Hold, c.Speed);
            Assert.AreEqual(45f, c.CourseDeg);
            Assert.AreEqual(18f, c.SpeedKn);

            c = HelmCommand.Apply(c, Order.EngineOrder(OrderIssuer.Captain, EngineBell.AheadFlank));
            Assert.AreEqual(SpeedMode.Bell, c.Speed);
            Assert.AreEqual(CourseMode.Hold, c.Course, "a bell leaves the course alone");
            Assert.AreEqual(45f, c.CourseDeg);

            c = HelmCommand.Apply(c, Order.Rudder(OrderIssuer.Captain, -20f));
            Assert.AreEqual(CourseMode.Manual, c.Course);
            Assert.AreEqual(SpeedMode.Bell, c.Speed, "a rudder order leaves the engines alone");

            c = HelmCommand.Apply(c, Order.SetRoe(OrderIssuer.Captain, Roe.WeaponsHold));
            Assert.AreEqual(CourseMode.Manual, c.Course, "non-helm orders change nothing");
        }

        [Test]
        public void CourseHold_TurnsTheShortWay_AndSteadiesOnTheCourse()
        {
            var helm = HelmFactory.Create(FidelityLevel.L0, Profile(), new StandInShip());
            var c = HelmCommand.Apply(HelmCommand.Stopped(0f), Order.Helm(OrderIssuer.Captain, 350f, 15f));
            var m = new ShipMotion { HeadingDeg = 0f, SurgeMs = 7.7f };
            HelmOutput o = helm.Step(c, m, 0.02f);
            Assert.Less(o.RudderOrderDeg, 0f, "350 from 000 is a turn to port");

            m.HeadingDeg = 350f;
            o = helm.Step(c, m, 0.02f);
            Assert.AreEqual(0f, o.RudderOrderDeg, 1e-4f, "on course and not turning: rudder amidships");

            m.YawRateRadS = 0.01f;   // swinging to starboard on course: check the swing
            o = helm.Step(c, m, 0.02f);
            Assert.Less(o.RudderOrderDeg, 0f);
        }

        [Test]
        public void Speed_BellsRingUpRevolutions_HoldAddsTrim()
        {
            var helm = HelmFactory.Create(FidelityLevel.L0, Profile(), new StandInShip());
            var c = HelmCommand.Apply(HelmCommand.Stopped(0f), Order.EngineOrder(OrderIssuer.Captain, EngineBell.AheadStandard));
            HelmOutput o = helm.Step(c, new ShipMotion { SurgeMs = 0f }, 0.02f);
            Assert.AreEqual(15f, o.TargetSpeedKn);
            Assert.AreEqual(15f * 0.514444f / 4f, o.ShaftOrderRps, 1e-4f, "a bell is the steady revolutions, whatever the speed now");

            c = HelmCommand.Apply(c, Order.Helm(OrderIssuer.Captain, 0f, 15f));
            HelmOutput slow = helm.Step(c, new ShipMotion { SurgeMs = 5f * 0.514444f }, 0.02f);
            Assert.Greater(slow.ShaftOrderRps, o.ShaftOrderRps, "below the ordered speed, speed hold rings up more");
            HelmOutput astern = helm.Step(HelmCommand.Apply(c, Order.EngineOrder(OrderIssuer.Captain, EngineBell.BackEmergency)), new ShipMotion(), 0.02f);
            Assert.AreEqual(-2f, astern.ShaftOrderRps, 1e-5f, "back emergency is the full astern revolutions");
            Assert.IsNaN(astern.TargetSpeedKn, "astern bells are revolutions, not a speed");
        }

        [Test]
        public void Route_SteersEachLeg_AndWheelsOverBeforeTheWaypoint()
        {
            var helm = HelmFactory.Create(FidelityLevel.L0, Profile(), new StandInShip());
            // North 3 km, then east: a 90° turn at a 500 m radius wheels over 500·tan 45° = 500 m before the corner.
            helm.AddRoute(7, new Route(new Vector2(0f, 0f), new Vector2(0f, 3000f), new Vector2(3000f, 3000f)));
            var c = HelmCommand.Apply(HelmCommand.Stopped(0f), Order.FollowRoute(OrderIssuer.Captain, 7, 15f));

            HelmOutput o = helm.Step(c, new ShipMotion { Position = new Vector3(0f, 0f, 1000f), SurgeMs = 7.7f }, 0.02f);
            Assert.AreEqual(1, o.Waypoint);
            Assert.AreEqual(0f, CompassMath.Normalize(o.SteeringCourseDeg + 180f) - 180f, 1e-3f, "on the first leg, steering north");

            o = helm.Step(c, new ShipMotion { Position = new Vector3(50f, 0f, 1000f), SurgeMs = 7.7f }, 0.02f);
            Assert.Less(CompassMath.Difference(0f, o.SteeringCourseDeg), 0f, "50 m to starboard of the leg: steer back to port");

            o = helm.Step(c, new ShipMotion { Position = new Vector3(0f, 0f, 2550f), SurgeMs = 7.7f }, 0.02f);
            Assert.AreEqual(2, o.Waypoint, "inside the wheel-over point: on to the next leg");
            // Line of sight onto the eastbound leg from 450 m south of it, looking 280 m ahead: 090 − atan(450/280).
            Assert.AreEqual(90f - Mathf.Atan2(450f, 280f) * Mathf.Rad2Deg, o.SteeringCourseDeg, 0.05f);
        }
    }
}
