using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Gates;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Tests
{
    /// <summary>Slice 1e: the Dogfight gates (W14). A gate counts once, the right way, inside its ring, in order.</summary>
    public class GateCourseTests
    {
        const string CoursePath = "Assets/Air Wing/Examples/Dogfight/Dogfight_Gates.asset";

        static Gate North(float radius = 100f, Band? speed = null, Band? altitude = null) =>
            new Gate("North", new Vector3(0f, 1000f, 0f), 0f, radius, speed ?? Band.Any, altitude ?? Band.Any);

        static AirframeState At(Vector3 position, Vector3 velocity, FlightStatus status = FlightStatus.Flying) =>
            new AirframeState { Position = position, Velocity = velocity, Attitude = Quaternion.LookRotation(velocity), Status = status };

        [Test]
        public void AGate_CountsOnlyTheRightWayThrough_InsideItsRing()
        {
            Gate g = North();
            Assert.IsTrue(GateCourse.Crossed(g, new Vector3(0f, 1000f, -3f), new Vector3(0f, 1000f, 1f), out float t, out float off));
            Assert.AreEqual(0.75f, t, 1e-6f, "the plane is three quarters of the way along the step");
            Assert.AreEqual(0f, off, 1e-4f);

            Assert.IsFalse(GateCourse.Crossed(g, new Vector3(0f, 1000f, 1f), new Vector3(0f, 1000f, -3f), out _, out _), "the wrong way through");
            Assert.IsFalse(GateCourse.Crossed(g, new Vector3(101f, 1000f, -1f), new Vector3(101f, 1000f, 1f), out _, out off), "outside the ring");
            Assert.AreEqual(101f, off, 1e-3f);
            Assert.IsTrue(GateCourse.Crossed(g, new Vector3(0f, 1100f, -1f), new Vector3(0f, 1100f, 1f), out _, out _), "on the rim counts");
            Assert.IsFalse(GateCourse.Crossed(g, new Vector3(0f, 1000f, -5f), new Vector3(0f, 1000f, -1f), out _, out _), "short of the plane");

            // A step that ends exactly on the plane counts; the next step, starting on it, does not count again.
            Assert.IsTrue(GateCourse.Crossed(g, new Vector3(0f, 1000f, -2f), new Vector3(0f, 1000f, 0f), out t, out _));
            Assert.AreEqual(1f, t);
            Assert.IsFalse(GateCourse.Crossed(g, new Vector3(0f, 1000f, 0f), new Vector3(0f, 1000f, 2f), out _, out _));
        }

        [Test]
        public void AGateFacingEast_IsFlownAlongPlusX()
        {
            var east = new Gate("East", Vector3.zero, 90f, 50f, Band.Any, Band.Any);
            Assert.AreEqual(1f, east.Facing.x, 1e-6f);
            Assert.IsTrue(GateCourse.Crossed(east, new Vector3(-1f, 0f, 10f), new Vector3(1f, 0f, 10f), out _, out float off));
            Assert.AreEqual(10f, off, 1e-4f);
        }

        [Test]
        public void TheCourse_TakesGatesInOrder_AndJudgesTheBandsWhereThePathCrossed()
        {
            var first = new Gate("First", new Vector3(0f, 1000f, 0f), 0f, 100f, new Band(350f, 450f), new Band(3000f, 3500f));
            var second = new Gate("Second", new Vector3(0f, 1000f, 1000f), 0f, 100f, Band.Any, new Band(4000f, 5000f));
            var course = new GateCourse(new[] { first, second });
            var v = new Vector3(0f, 0f, 400f * (float)Units.KnotToMetrePerSecond);

            Assert.IsFalse(course.Observe(1, At(new Vector3(0f, 1000f, 999f), v), At(new Vector3(0f, 1000f, 1001f), v), out _), "the second gate before the first does not count");
            Assert.AreEqual(0, course.Next);

            // Through the first gate, climbing from 990 m to 1,010 m: the plane is at the middle of the step, 1,000 m.
            Assert.IsTrue(course.Observe(2, At(new Vector3(0f, 990f, -1f), v), At(new Vector3(0f, 1010f, 1f), v), out GatePass pass));
            Assert.AreEqual(0, pass.Gate);
            Assert.AreEqual(2, pass.Tick);
            Assert.AreEqual(1000.0 / Units.FootToMetre, pass.AltitudeFt, 0.01, "altitude interpolated to the crossing");
            Assert.AreEqual(400f, pass.SpeedKt, 0.01f);
            Assert.IsTrue(pass.SpeedOk);
            Assert.IsTrue(pass.AltitudeOk, "3,281 ft lies in 3,000 … 3,500");
            Assert.AreEqual(1, course.Next);

            Assert.IsTrue(course.Observe(3, At(new Vector3(0f, 1000f, 999f), v), At(new Vector3(0f, 1000f, 1001f), v), out pass));
            Assert.IsFalse(pass.AltitudeOk, "3,281 ft misses 4,000 … 5,000");
            Assert.IsTrue(pass.SpeedOk, "an 'any' band is always held");
            Assert.IsFalse(pass.Clean);
            Assert.AreEqual(0, course.Next, "after the last gate the lap starts again");
            Assert.AreEqual(1, course.Laps);
            Assert.AreEqual(2, course.Passes.Count);

            course.Observe(4, At(new Vector3(0f, 1000f, -1f), v), At(new Vector3(0f, 1000f, 1f), v), out _);
            Assert.AreEqual(1, course.Next);
            course.Restart();
            Assert.AreEqual(0, course.Next, "a reset starts the course again at gate 1");
            Assert.AreEqual(3, course.Passes.Count, "and keeps the record");
        }

        [Test]
        public void ACrashedJet_FliesNoGates()
        {
            var course = new GateCourse(new[] { North() });
            var v = new Vector3(0f, 0f, 200f);
            Assert.IsFalse(course.Observe(1, At(new Vector3(0f, 1000f, -1f), v, FlightStatus.Crashed), At(new Vector3(0f, 1000f, 1f), v, FlightStatus.Crashed), out _));
        }

        [Test]
        public void Bands_DescribeThemselves_AndRefuseNonsense()
        {
            Assert.AreEqual("350–450 kt", new Band(350f, 450f).Describe("kt"));
            Assert.AreEqual("≥ 300 kt", new Band(300f, float.PositiveInfinity).Describe("kt"));
            Assert.AreEqual("≤ 1,000 ft", new Band(float.NegativeInfinity, 1000f).Describe("ft"));
            Assert.AreEqual("any", Band.Any.Describe("kt"));
            Assert.IsTrue(Band.Any.Contains(1e9f));
            Assert.Throws<ArgumentException>(() => new Band(2f, 1f));
            Assert.Throws<ArgumentException>(() => new Gate("bad", Vector3.zero, 0f, 0f, Band.Any, Band.Any), "a ring needs a radius");
            Assert.Throws<ArgumentException>(() => new GateCourse(new Gate[0]));
        }

        [Test]
        public void TheCourseAsset_BakesItsBandRules()
        {
            var so = ScriptableObject.CreateInstance<GateCourseSO>();
            try
            {
                so.gates = new[]
                {
                    new GateCourseSO.GateEntry { name = "a", centreM = Vector3.zero, radiusM = 50f, speedBandKt = Vector2.zero, altitudeBandFt = new Vector2(300f, 0f) },
                    new GateCourseSO.GateEntry { name = "", centreM = Vector3.one, radiusM = 50f, speedBandKt = new Vector2(250f, 350f) },
                };
                GateCourse c = so.Bake();
                Assert.IsTrue(c.Gates[0].SpeedKt.IsAny, "(0, 0) is any");
                Assert.AreEqual("≥ 300 ft", c.Gates[0].AltitudeFt.Describe("ft"), "(min, 0) is at least min");
                Assert.AreEqual("Gate 2", c.Gates[1].Name, "an unnamed gate is named by its place");

                so.gates[1].speedBandKt = new Vector2(350f, 250f);
                Assert.Throws<ArgumentException>(() => so.Bake());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        /// <summary>
        /// The Dogfight start lines up on gate 1: the mouse instructor, aimed at gate 1's centre from the scene's start,
        /// flies through it within 60 s and the course moves on to gate 2, all through the flight core and the course
        /// asset. A whole lap is left to a pilot. Measured 2026-10-25: aimed at each next gate, the instructor dives
        /// at gate 2 (1,000 ft), comes through it sinking 93 m/s at 641 kt, pushes −2.7 G while rolling toward gate 3
        /// behind it, and meets the sea 2.8 s later; lined up 3 km out it flies gates 1–3 and meets the land short of
        /// gate 4. Its lift-on-aim law banks past 90° toward any aim below the horizon (ROADMAP, slice 1e findings).
        /// </summary>
        [Test]
        public void TheDogfightStart_FliesThroughGate1()
        {
            var so = AssetDatabase.LoadAssetAtPath<GateCourseSO>(CoursePath);
            Assert.IsNotNull(so, CoursePath + " (Deep Water → Air Wing → Build Dogfight seeds it)");
            GateCourse course = so.Bake();
            AircraftProfile p = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
            var sheet = new FidelitySheet(FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, 4, 1442);
            AirframeState start = AircraftFactory.LevelStart(p, new Vector3(0f, 1524f, -2500f), 0f, 400f * (float)Units.KnotToMetrePerSecond, 0.6f * p.Airframe.InternalFuelKg);

            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(8f));   // the land's top, everywhere: the strict case
            core.Add(AircraftFactory.Build(1, p, sheet, start), new CourseSeat(course, 0.7f));
            Core.Aircraft jet = core.Find(1);

            while (core.Tick < 60 * 50 && course.Passes.Count == 0 && jet.State.Status == FlightStatus.Flying)
            {
                AirframeState before = jet.State;
                core.Step();
                course.Observe(core.Tick - 1, before, jet.State, out _);
            }

            Assert.AreEqual(FlightStatus.Flying, jet.State.Status);
            Assert.AreEqual(1, course.Passes.Count, "gate 1 within 60 s");
            GatePass pass = course.Passes[0];
            Debug.Log($"[Gates] {course.Gates[0].Name}: t {pass.Tick * 0.02f:F1} s, {pass.SpeedKt:F0} kt, {pass.AltitudeFt:F0} ft, {pass.OffCentreM:F1} m off centre");
            Assert.AreEqual(0, pass.Gate);
            Assert.IsTrue(pass.AltitudeOk, "straight ahead at 5,000 ft");
            Assert.Less(pass.OffCentreM, 10f, "the instructor flies the path onto the centre");
            Assert.AreEqual(1, course.Next, "the course moved on to gate 2");
        }

        /// <summary>A test seat: the mouse instructor with its aim on the next gate, and a fixed throttle.</summary>
        sealed class CourseSeat : ISeat
        {
            readonly GateCourse _course;
            readonly float _throttle;
            readonly MouseInstructor _instructor = new MouseInstructor(new InstructorSettings { RollGain = 2.5f, PitchGain = 4f, WingsLevelBlendDeg = 5f });

            public CourseSeat(GateCourse course, float throttle)
            {
                _course = course;
                _throttle = throttle;
            }

            public SeatKind Kind => SeatKind.Scenario;

            public PilotInputs Issue(long tick, int aircraftId, in AirframeState state)
            {
                _instructor.Steer(_course.Gates[_course.Next].Centre - state.Position, state, out float pitch, out float roll);
                return new PilotInputs { StickPitch = pitch, StickRoll = roll, Throttle = _throttle };
            }
        }
    }
}
