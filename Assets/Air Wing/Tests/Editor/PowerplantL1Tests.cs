using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Gates;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Powerplant;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Tests
{
    /// <summary>Slice 2c: Powerplant L1's spool, and L1 as the scenes fly it (W35).</summary>
    public class PowerplantL1Tests
    {
        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();

        static float Spool(PowerplantL1 pp, ref AirframeState s, PilotInputs i, float seconds, out float thrust)
        {
            thrust = 0f;
            AirData air = Isa.At(0.0);
            int n = Mathf.RoundToInt(seconds / 0.005f);
            for (int k = 0; k < n; k++) thrust = pp.Step(i, air, ref s, 0.005f);
            return s.EngineSpool;
        }

        [Test]
        public void TheCoreSpools_Up_AndDown_WithItsLags()
        {
            PowerplantProfile p = E().Powerplant;
            var pp = new PowerplantL1(p);
            var s = new AirframeState();
            float spool = Spool(pp, ref s, new PilotInputs { Throttle = 1f }, p.SpoolUpTauS, out _);
            Assert.AreEqual(1f - Mathf.Exp(-1f), spool, 2e-3f, "one time constant: 63 %");
            Spool(pp, ref s, new PilotInputs { Throttle = 1f }, 3f * p.SpoolUpTauS, out float mil);
            float milStatic = p.EngineCount * p.MilitaryThrustPerEngineN;
            float expected = milStatic * (p.IdleThrustFraction + (1f - p.IdleThrustFraction) * (1f - Mathf.Exp(-4f)));
            Assert.AreEqual(expected, mil, 1e-3f * milStatic, "four time constants: the spool at 98.2 %, the thrust to match");
            float down = Spool(pp, ref s, new PilotInputs { Throttle = 0f }, p.SpoolDownTauS, out _);
            Assert.AreEqual(Mathf.Exp(-1f), down, 0.01f, "down with its own lag");
        }

        [Test]
        public void TheAfterburner_LightsOnlyOnceTheCoreIsUp()
        {
            PowerplantProfile p = E().Powerplant;
            var pp = new PowerplantL1(p);
            var s = new AirframeState();
            var ab = new PilotInputs { Throttle = 1f, Afterburner = true };
            float tLight = -1f;
            AirData air = Isa.At(0.0);
            for (int k = 0; k < 2000 && tLight < 0f; k++)
            {
                pp.Step(ab, air, ref s, 0.005f);
                if (s.AfterburnerSpool > 0f) tLight = (k + 1) * 0.005f;
            }
            float expected = -p.SpoolUpTauS * Mathf.Log(1f - p.AfterburnerMinSpool);
            Debug.Log($"[AIRWING PP] afterburner from idle lights at {tLight:F2} s (core at {p.AfterburnerMinSpool:P0}: {expected:F2} s)");
            Assert.AreEqual(expected, tLight, 0.01f);
            Spool(pp, ref s, ab, 3f, out float full);
            float maxStatic = p.EngineCount * p.MaxThrustPerEngineN;
            Assert.AreEqual(maxStatic, full, 0.01f * maxStatic, "full afterburner");
            Spool(pp, ref s, new PilotInputs { Throttle = 1f }, 1.5f, out _);
            Assert.Less(s.AfterburnerSpool, 0.01f, "out when deselected");
        }

        [Test]
        public void WithoutSpoolData_PowerplantL1_FallsBack_AndSaysWhy()
        {
            AircraftProfile e = E();
            e.Powerplant.SpoolUpTauS = 0f;
            var sheet = new FidelitySheet(FidelityLevel.L0, FidelityLevel.L1, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, 4, 1);
            Aircraft a = AircraftFactory.Build(1, e, sheet, AircraftFactory.LevelStart(e, Vector3.up * 3000f, 0f, 200f, 1000f));
            Assert.IsInstanceOf<PowerplantL0>(a.Powerplant);
            StringAssert.Contains("no spool data", string.Join("; ", a.Levels));
        }

        [Test]
        public void TheScenesSheet_AsksForL1_AndGetsIt()
        {
            FidelitySheet sheet = AssetDatabase.LoadAssetAtPath<FidelitySheetSO>("Assets/Air Wing/ScriptableObjects/FA18E_FidelitySheet.asset").Bake();
            AircraftProfile e = E();
            Aircraft a = AircraftFactory.Build(1, e, sheet, AircraftFactory.LevelStart(e, sheet, new Vector3(0f, 1524f, 0f), 0f, 205.8f, 3000f, 0.6f));
            Debug.Log("[AIRWING PP] the scenes' sheet: " + string.Join("; ", a.Levels));
            foreach (FidelityResolution r in a.Levels)
                if (r.Part == PartKind.Airframe || r.Part == PartKind.FlightControls || r.Part == PartKind.Powerplant)
                    Assert.AreEqual(FidelityLevel.L1, r.Resolved, r.ToString());
        }

        /// <summary>
        /// The Dogfight start at L1 (W35): the mouse instructor, aimed at gate 1, flies through it through the
        /// fly-by-wire, as it does at L0 (GateCourseTests). The start is trimmed; the throttle is held at 0.7.
        /// </summary>
        [Test]
        public void AtL1_TheDogfightStart_FliesThroughGate1()
        {
            GateCourse course = AssetDatabase.LoadAssetAtPath<GateCourseSO>("Assets/Air Wing/Examples/Dogfight/Dogfight_Gates.asset").Bake();
            AircraftProfile e = E();
            FidelitySheet sheet = AssetDatabase.LoadAssetAtPath<FidelitySheetSO>("Assets/Air Wing/ScriptableObjects/FA18E_FidelitySheet.asset").Bake();
            AirframeState start = AircraftFactory.LevelStart(e, sheet, new Vector3(0f, 1524f, -2500f), 0f,
                                                             400f * (float)Units.KnotToMetrePerSecond, 0.6f * e.Airframe.InternalFuelKg, 0.7f);
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(8f));
            core.Add(AircraftFactory.Build(1, e, sheet, start), new AimAt(course, 0.7f));
            Aircraft jet = core.Find(1);
            while (core.Tick < 60 * 50 && course.Passes.Count == 0 && jet.State.Status == FlightStatus.Flying)
            {
                AirframeState before = jet.State;
                core.Step();
                course.Observe(core.Tick - 1, before, jet.State, out _);
            }
            Assert.AreEqual(FlightStatus.Flying, jet.State.Status);
            Assert.AreEqual(1, course.Passes.Count, "gate 1 within 60 s");
            GatePass pass = course.Passes[0];
            Debug.Log($"[AIRWING PP] L1 gate 1: t {pass.Tick * 0.02f:F1} s, {pass.SpeedKt:F0} kt, {pass.AltitudeFt:F0} ft, {pass.OffCentreM:F1} m off centre");
            Assert.IsTrue(pass.AltitudeOk);
            Assert.Less(pass.OffCentreM, 25f);
        }

        sealed class AimAt : ISeat
        {
            readonly GateCourse _course;
            readonly float _throttle;
            readonly MouseInstructor _i = new MouseInstructor(new InstructorSettings { RollGain = 2.5f, PitchGain = 4f, WingsLevelBlendDeg = 5f });
            public AimAt(GateCourse course, float throttle) { _course = course; _throttle = throttle; }
            public SeatKind Kind => SeatKind.Scenario;
            public PilotInputs Issue(long tick, int aircraftId, in AirframeState state)
            {
                _i.Steer(_course.Gates[_course.Next].Centre - state.Position, state, out float pitch, out float roll);
                return new PilotInputs { StickPitch = pitch, StickRoll = roll, Throttle = _throttle };
            }
        }
    }
}
