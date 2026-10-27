using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.LandingGear;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.Modules.World;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// Slice 2d (W4, W29): Landing Gear L1 on the F/A-18E: spring-damper struts on probed contacts, brakes, nosewheel
    /// steering, the gear's transit and its weight-on-wheels lock; a ground start, a take-off and a landing on the
    /// truth feed's flat ground; a hard landing and a gear-up landing end the flight; the ground replays bit for bit.
    /// </summary>
    public class LandingGearL1Tests
    {
        const double Kt = Units.KnotToMetrePerSecond;
        const float R2D = Mathf.Rad2Deg;

        static readonly FidelitySheet Sheet =
            new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1);

        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();

        static float Fuel(in AircraftProfile e) => 0.6f * e.Airframe.InternalFuelKg;

        static FlightCore Core(in AircraftProfile e, in AirframeState start, ISeat seat)
        {
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            core.Add(AircraftFactory.Build(1, e, Sheet, start), seat);
            return core;
        }

        static float PitchDeg(in AirframeState s) => Mathf.Asin(Mathf.Clamp((s.Attitude * Vector3.forward).y, -1f, 1f)) * R2D;

        /// <summary>Each strut's load from its compression alone (the spring; at rest the damper adds nothing).</summary>
        static float[] StrutLoads(in AircraftProfile e, in AirframeState s)
        {
            WheelProfile[] w = e.LandingGear.Wheels;
            var loads = new float[w.Length];
            for (int i = 0; i < w.Length; i++)
                loads[i] = w[i].SpringNPerM * Mathf.Max(0f, -(s.Position.y + (s.Attitude * w[i].ContactPointM).y));
            return loads;
        }

        /// <summary>
        /// A 3° approach at <paramref name="kt"/> knots from <paramref name="heightM"/> metres, trimmed, the gear in the
        /// given position, flaps down. The stick held centred (a 1 g command holds the path); the throttle holds the speed.
        /// </summary>
        static AirframeState Approach(in AircraftProfile e, float kt, float heightM, float pathDeg, bool gearDown)
        {
            float v = (float)(kt * Kt);
            float back = heightM / Mathf.Tan(pathDeg * Mathf.Deg2Rad);
            AirframeState s = AircraftFactory.LevelStart(e, Sheet, new Vector3(0f, heightM, -back), 0f, v, Fuel(e), 0.5f);
            Quaternion down = Quaternion.Euler(pathDeg, 0f, 0f);
            s.Attitude = down * s.Attitude;
            s.Velocity = down * s.Velocity;
            s.GearDeployed = gearDown ? 1f : 0f;
            return s;
        }

        /// <summary>The stick that asks Flight Controls L1 for load factor <paramref name="n"/> (the inverse of its map).</summary>
        static float StickFor(in AircraftProfile e, float n) => n >= 1f
            ? Mathf.Clamp01((n - 1f) / (e.Controls.LoadFactorMax - 1f))
            : -Mathf.Clamp01((1f - n) / (1f - e.Controls.LoadFactorMin));

        static float PathDeg(in AirframeState s) => Mathf.Asin(Mathf.Clamp(s.Velocity.y / Mathf.Max(s.Velocity.magnitude, 1f), -1f, 1f)) * R2D;

        /// <summary>
        /// A scripted pilot on a <paramref name="pathDeg"/> glide path to the ground at z = 0: the flight path angle
        /// steered onto the path (G through the stick), the speed held with the throttle. On the wheels: idle, the stick
        /// centred, and the brakes if asked.
        /// </summary>
        static PilotInputs ApproachInputs(in AircraftProfile e, in AirframeState s, float kt, float pathDeg, bool gearDown, bool brakeOnTheGround,
                                          bool handsOffOnTheWheels = false)
        {
            var i = new PilotInputs { Switches = PilotSwitches.FlapsDown | (gearDown ? PilotSwitches.GearDown : PilotSwitches.None) };
            if (s.InContact && handsOffOnTheWheels)
            {
                i.Brakes = brakeOnTheGround && PitchDeg(s) < 0.5f;   // stick centred, idle: the nose is left to fall
                return i;
            }
            if (s.InContact)
            {
                // On the wheels: idle, and fly the nose down (aft stick against the slap-down the mains give, easing off
                // as the pitch comes down); brakes once the nosewheel is on.
                float pitch = PitchDeg(s);
                i.Throttle = 0f;
                i.StickPitch = Mathf.Clamp01(0.12f * pitch + 0.03f * s.BodyRates.x * R2D);   // BodyRates.x > 0: the nose falling
                i.Brakes = brakeOnTheGround && pitch < 0.5f;
                return i;
            }
            float onPath = -s.Position.z * Mathf.Tan(pathDeg * Mathf.Deg2Rad);
            float gammaWanted = Mathf.Clamp(-pathDeg + 0.3f * (onPath - s.Position.y), -pathDeg - 3f, -pathDeg + 3f);
            float n = Mathf.Cos(PathDeg(s) * Mathf.Deg2Rad) + 0.12f * (gammaWanted - PathDeg(s));
            i.StickPitch = StickFor(e, Mathf.Clamp(n, 0.3f, 2f));
            i.Throttle = Mathf.Clamp01(0.55f + 0.15f * ((float)(kt * Kt) - s.Velocity.magnitude));
            return i;
        }

        [Test]
        public void TheF18EProfile_CarriesTheGear()
        {
            AircraftProfile e = E();
            Assert.IsTrue(e.LandingGear.IsSet, "Deep Water → Air Wing → Apply HARV L1 Data to F-A-18E");
            Assert.AreEqual(3, e.LandingGear.Wheels.Length);
            Assert.AreEqual(24f * 0.3048f, e.LandingGear.MaxSinkRateMps, 1e-4f);
            Aircraft a = AircraftFactory.Build(1, e, Sheet, AircraftFactory.GroundStart(e, Sheet, Vector3.zero, 0f, Fuel(e)));
            Assert.IsInstanceOf<LandingGearL1>(a.Gear);
            var noGear = new FidelitySheet(FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L1, FidelityLevel.L0, 4, 1);
            Assert.Throws<InvalidOperationException>(() => AircraftFactory.GroundStart(e, noGear, Vector3.zero, 0f, Fuel(e)),
                "gear L1 needs airframe L1: a ground start on an L0 airframe is refused");
        }

        [Test]
        public void AGroundStart_SettlesOnItsWheels()
        {
            AircraftProfile e = E();
            AirframeState start = AircraftFactory.GroundStart(e, Sheet, Vector3.zero, 0f, Fuel(e));
            var core = Core(e, start, new HoldSeat(new PilotInputs { Switches = PilotSwitches.GearDown, Brakes = true }));
            for (int t = 0; t < 500; t++) core.Step();
            AirframeState s = core.Find(1).State;
            float[] loads = StrutLoads(e, s);
            float total = loads[0] + loads[1] + loads[2];
            float noseShare = loads[0] / total;
            Debug.Log($"[AIRWING GEAR] at rest after 10 s: height {s.Position.y:F3} m (start {start.Position.y:F3}), pitch {PitchDeg(s):F2}°, " +
                      $"speed {s.Velocity.magnitude:F3} m/s, nose share {noseShare:P1}, struts carry {total / (s.MassKg * 9.80665f):P1} of the weight");
            Assert.AreEqual(FlightStatus.Flying, s.Status);
            Assert.IsTrue(s.InContact, "weight on wheels");
            Assert.Less(s.Velocity.magnitude, 0.05f, "at rest, brakes on");
            Assert.AreEqual(start.Position.y, s.Position.y, 0.05f, "the start sits near its static compression");
            Assert.AreEqual(0f, PitchDeg(s), 1.5f, "sits level");
            Assert.That(noseShare, Is.InRange(0.09f, 0.16f), "the nose takes its moment-arm share (~12 %)");
            Assert.AreEqual(1f, total / (s.MassKg * 9.80665f), 0.03f, "the struts carry the weight");
        }

        [Test]
        public void ItTakesOff_InUnder2000m_WithoutATailStrike()
        {
            AircraftProfile e = E();
            AirframeState start = AircraftFactory.GroundStart(e, Sheet, Vector3.zero, 0f, Fuel(e));
            float rotateMps = (float)(150 * Kt);
            float maxPitch = 0f, liftOffZ = float.NaN, liftOffKt = float.NaN;
            bool airborne = false;
            var seat = new StateSeat((tick, s) =>
            {
                var i = new PilotInputs { Throttle = 1f, Afterburner = true, Switches = s.Position.y > 60f ? PilotSwitches.None : PilotSwitches.GearDown };
                if (s.InContact) i.StickPitch = s.Velocity.magnitude > rotateMps ? Mathf.Clamp01(0.1f * (10f - PitchDeg(s))) : 0f;
                else i.StickPitch = StickFor(e, Mathf.Clamp(Mathf.Cos(PathDeg(s) * Mathf.Deg2Rad) + 0.1f * (8f - PathDeg(s)), 0.5f, 2f));
                return i;
            });
            var core = Core(e, start, seat);
            int airborneTicks = 0;
            for (int t = 0; t < 2000 && core.Find(1).State.Position.y < 300f; t++)
            {
                core.Step();
                AirframeState s = core.Find(1).State;
                if (s.Status != FlightStatus.Flying) Assert.Fail($"tick {t}: {core.Find(1).CrashReason}");
                if (s.InContact) maxPitch = Mathf.Max(maxPitch, PitchDeg(s));
                airborneTicks = s.InContact ? 0 : airborneTicks + 1;
                if (!airborne && airborneTicks >= 50) { airborne = true; }
                if (airborneTicks == 1) { liftOffZ = s.Position.z; liftOffKt = Units.Knots(s.Velocity.magnitude); }
            }
            AirframeState end = core.Find(1).State;
            Debug.Log($"[AIRWING GEAR] take-off (afterburner, rotate at 150 kt): lift-off after {liftOffZ:F0} m at {liftOffKt:F0} kt, " +
                      $"max pitch on the wheels {maxPitch:F1}° (tail strike ~15°), at 300 m the gear is {end.GearDeployed:P0} down");
            Assert.IsTrue(airborne, "it flew");
            Assert.Less(liftOffZ, 2000f, "lift-off within 2,000 m");
            Assert.Less(maxPitch, 14f, "no tail strike on the rotation");
            Assert.Greater(end.Position.y, 299f, "climbed out");
        }

        [Test]
        public void ItLands_OnA3DegreePath_AndTheBrakesStopIt()
        {
            AircraftProfile e = E();
            const float kt = 145f;
            float touchdownSink = float.NaN, touchdownZ = float.NaN, maxSide = 0f;
            var core = Core(e, Approach(e, kt, 30f, 3f, true), new StateSeat((tick, s) => ApproachInputs(e, s, kt, 3f, true, true)));
            int t = 0;
            for (; t < 6000; t++)
            {
                AirframeState before = core.Find(1).State;
                core.Step();
                AirframeState s = core.Find(1).State;
                if (s.Status != FlightStatus.Flying) Assert.Fail($"tick {t}: {core.Find(1).CrashReason}");
                if (s.InContact && float.IsNaN(touchdownSink)) { touchdownSink = -before.Velocity.y; touchdownZ = s.Position.z; }
                if (s.InContact) maxSide = Mathf.Max(maxSide, Mathf.Abs(s.Position.x));
                if (s.InContact && s.Velocity.magnitude < 0.5f) break;
            }
            AirframeState end = core.Find(1).State;
            float rollout = end.Position.z - touchdownZ;
            Debug.Log($"[AIRWING GEAR] landing at {kt} kt on 3°: touchdown sink {touchdownSink:F2} m/s ({touchdownSink / 0.3048f:F1} ft/s, limit 24), " +
                      $"rollout {rollout:F0} m with brakes, {t * 0.02f:F1} s, off the centreline at most {maxSide:F2} m");
            Assert.Less(touchdownSink, e.LandingGear.MaxSinkRateMps, "under the gear's sink limit");
            Assert.Less(end.Velocity.magnitude, 0.5f, "the brakes stop it");
            Assert.Less(rollout, 2500f);
            Assert.Less(maxSide, 2f, "stays on the centreline");
        }

        [Test]
        public void AHandsOffTouchdown_TheStrutsEndStopTakesTheNoseSlap()
        {
            AircraftProfile e = E();
            var core = Core(e, Approach(e, 145f, 30f, 3f, true), new StateSeat((tick, s) => ApproachInputs(e, s, 145f, 3f, true, true, handsOffOnTheWheels: true)));
            var gear = (LandingGearL1)core.Find(1).Gear;
            WheelProfile nose = e.LandingGear.Wheels[0];
            float maxNose = 0f, maxNoseRate = 0f;
            for (int t = 0; t < 6000; t++)
            {
                core.Step();
                AirframeState s = core.Find(1).State;
                if (s.Status != FlightStatus.Flying) Assert.Fail($"tick {t}: {core.Find(1).CrashReason}");
                maxNose = Mathf.Max(maxNose, -(s.Position.y + (s.Attitude * nose.ContactPointM).y));
                if (s.InContact) maxNoseRate = Mathf.Max(maxNoseRate, s.BodyRates.x * R2D);
                if (s.InContact && s.Velocity.magnitude < 0.5f) break;
            }
            float peakLoad = gear.Spring(nose, maxNose) / (e.LandingGear.Wheels[0].SpringNPerM * nose.TravelM);
            Debug.Log($"[AIRWING GEAR] hands-off touchdown: nose slap {maxNoseRate:F1}°/s, the nose strut took {maxNose / nose.TravelM:P0} of its travel " +
                      $"(spring at {peakLoad:F2}× a linear strut's full-travel force)");
            Assert.Less(maxNose, nose.TravelM, "the end stop held: the nose strut did not bottom");
            Assert.Greater(maxNose, 0.7f * nose.TravelM, "and it took the slap in the stiffening part of its travel");
            Assert.Less(core.Find(1).State.Velocity.magnitude, 0.5f, "stopped on the brakes");
        }

        [Test]
        public void TheEndStop_IsLinearBelowItsStart_AndStiffensPastIt()
        {
            AircraftProfile e = E();
            var gear = new LandingGearL1(e.LandingGear);
            WheelProfile w = e.LandingGear.Wheels[1];
            float k = w.SpringNPerM, L = w.TravelM, x0 = e.LandingGear.EndStopFraction * L;
            Assert.AreEqual(k * 0.5f * L, gear.Spring(w, 0.5f * L), 1e-2f, "static compression is on the linear part");
            Assert.AreEqual(k * x0, gear.Spring(w, x0), 1e-2f, "continuous where it starts");
            float full = gear.Spring(w, L);
            Assert.AreEqual(k * L + e.LandingGear.EndStopStiffening * k * (L - x0), full, 1f, "the formula at full travel");
            Debug.Log($"[AIRWING GEAR] end stop: at full travel the main strut gives {full / (k * L):F1}× a linear strut's force");
        }

        [Test]
        public void AHardLanding_BreaksTheGear()
        {
            AircraftProfile e = E();
            var core = Core(e, Approach(e, 145f, 20f, 9f, true), new StateSeat((tick, s) => ApproachInputs(e, s, 145f, 9f, true, false)));
            for (int t = 0; t < 500 && core.Find(1).State.Status == FlightStatus.Flying; t++) core.Step();
            Aircraft a = core.Find(1);
            Debug.Log($"[AIRWING GEAR] 9° dive onto the runway: {a.State.Status}, {a.CrashReason}");
            Assert.AreEqual(FlightStatus.Crashed, a.State.Status);
            StringAssert.Contains("met the ground at", a.CrashReason);
        }

        [Test]
        public void AGearUpLanding_IsACrash()
        {
            AircraftProfile e = E();
            var core = Core(e, Approach(e, 145f, 20f, 3f, false), new StateSeat((tick, s) => ApproachInputs(e, s, 145f, 3f, false, false)));
            for (int t = 0; t < 1000 && core.Find(1).State.Status == FlightStatus.Flying; t++) core.Step();
            Aircraft a = core.Find(1);
            Debug.Log($"[AIRWING GEAR] gear-up landing: {a.State.Status}, {a.CrashReason}");
            Assert.AreEqual(FlightStatus.Crashed, a.State.Status);
            StringAssert.Contains("hard point", a.CrashReason);
        }

        [Test]
        public void TheGear_TakesItsTransitTime_AndWillNotComeUpOnTheGround()
        {
            AircraftProfile e = E();
            // In the air: down over 4 s, then up over 4 s.
            AirframeState air = AircraftFactory.LevelStart(e, Sheet, new Vector3(0f, 3000f, 0f), 0f, (float)(250 * Kt), Fuel(e), 0.6f);
            bool down = true;
            var flying = Core(e, air, new StateSeat((tick, s) => new PilotInputs { Throttle = 0.6f, Switches = down ? PilotSwitches.GearDown : PilotSwitches.None }));
            for (int t = 0; t < 100; t++) flying.Step();
            Assert.AreEqual(0.5f, flying.Find(1).State.GearDeployed, 0.01f, "half down after 2 s");
            for (int t = 0; t < 110; t++) flying.Step();
            Assert.AreEqual(1f, flying.Find(1).State.GearDeployed, "down and locked");
            down = false;
            for (int t = 0; t < 210; t++) flying.Step();
            Assert.AreEqual(0f, flying.Find(1).State.GearDeployed, "up");

            // On the ground the switch up does nothing: weight on wheels.
            AirframeState ground = AircraftFactory.GroundStart(e, Sheet, Vector3.zero, 0f, Fuel(e));
            var parked = Core(e, ground, new HoldSeat(new PilotInputs { Brakes = true }));
            for (int t = 0; t < 300; t++) parked.Step();
            Assert.AreEqual(1f, parked.Find(1).State.GearDeployed, "weight on wheels: the gear stays down");
            Assert.IsTrue(parked.Find(1).State.InContact);
            Assert.AreEqual(FlightStatus.Flying, parked.Find(1).State.Status);
        }

        [Test]
        public void ThePedals_SteerTheNosewheel_AtTaxiSpeed()
        {
            AircraftProfile e = E();
            AirframeState start = AircraftFactory.GroundStart(e, Sheet, Vector3.zero, 0f, Fuel(e));
            float taxi = (float)(15 * Kt);
            var core = Core(e, start, new StateSeat((tick, s) => new PilotInputs
            {
                Switches = PilotSwitches.GearDown,
                Throttle = s.Velocity.magnitude < taxi ? 0.35f : 0f,
                Pedals = tick > 500 ? 1f : 0f,
            }));
            for (int t = 0; t < 500; t++) core.Step();
            float turned = 0f, firstSecond = 0f, last = core.Find(1).State.Attitude.eulerAngles.y;
            for (int t = 0; t < 400; t++)
            {
                core.Step();
                float now = core.Find(1).State.Attitude.eulerAngles.y;
                turned += Mathf.DeltaAngle(last, now);
                last = now;
                if (t == 49) firstSecond = turned;
            }
            AirframeState s = core.Find(1).State;
            Debug.Log($"[AIRWING GEAR] taxi at {Units.Knots(s.Velocity.magnitude):F0} kt, full right pedal 8 s: turned {turned:F0}° ({firstSecond:F1}° in the first second)");
            Assert.AreEqual(FlightStatus.Flying, s.Status);
            Assert.Greater(firstSecond, 0f, "right pedal turns it right");
            Assert.Greater(turned, 20f, "and keeps turning");
        }

        [Test]
        public void OnTheGround_TheCoreIsBitIdentical_AndReplays()
        {
            AircraftProfile e = E();
            AirframeState start = AircraftFactory.GroundStart(e, Sheet, Vector3.zero, 0f, Fuel(e));
            PilotInputs Script(long t) => new PilotInputs
            {
                Switches = PilotSwitches.GearDown,
                Throttle = t < 300 ? 1f : 0f,
                Afterburner = t < 300,
                Pedals = t > 100 && t < 150 ? 0.5f : 0f,
                Brakes = t > 320,
            };
            FlightCore Run(ISeat seat)
            {
                FlightCore c = Core(e, start, seat);
                for (int t = 0; t < 600; t++) c.Step();
                return c;
            }
            FlightCore a = Run(new StateSeat((t, s) => Script(t))), b = Run(new StateSeat((t, s) => Script(t)));
            Assert.AreEqual(-1, a.Fixes.FirstDivergence(b.Fixes), "two runs, the same Fix on every tick");
            FlightCore r = Run(new ReplaySeat(a.Inputs));
            Assert.AreEqual(-1, a.Fixes.FirstDivergence(r.Fixes), "the input log reproduces it");
            Assert.AreEqual(FlightStatus.Flying, a.Find(1).State.Status, a.Find(1).CrashReason);
            Assert.Greater(a.Find(1).State.Position.z, 50f, "it rolled");
        }

        [Test]
        public void ThePhysXProbe_FindsABoxCollider()
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                box.transform.position = new Vector3(0f, -0.5f, 0f);   // top face at y = 0
                box.transform.localScale = new Vector3(20f, 1f, 20f);
                Physics.SyncTransforms();
                var probe = new PhysXContactProbe();
                Assert.IsTrue(probe.Cast(new Vector3(1f, 0.3f, 2f), Vector3.down, 0.5f, out float d, out Vector3 n), "a ray from 0.3 m above meets the top");
                Assert.AreEqual(0.3f, d, 1e-4f);
                Assert.AreEqual(1f, Vector3.Dot(n, Vector3.up), 1e-4f, "the top face's normal");
                Assert.IsFalse(probe.Cast(new Vector3(1f, 0.8f, 2f), Vector3.down, 0.5f, out _, out _), "out of reach: the wheel is in the air");
                Assert.IsFalse(probe.Cast(new Vector3(30f, 0.3f, 2f), Vector3.down, 0.5f, out _, out _), "off the edge: nothing");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(box);
            }
        }

        /// <summary>A seat that reads the aircraft's state (a scripted pilot: throttle on speed, gear on height).</summary>
        sealed class StateSeat : ISeat
        {
            readonly Func<long, AirframeState, PilotInputs> _f;
            public StateSeat(Func<long, AirframeState, PilotInputs> f) => _f = f;
            public SeatKind Kind => SeatKind.Scenario;
            public PilotInputs Issue(long tick, int aircraftId, in AirframeState state) => _f(tick, state);
        }
    }
}
