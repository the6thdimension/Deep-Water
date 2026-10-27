using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Gates;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.AirWing.Tests;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Editor;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Tests
{
    /// <summary>
    /// Slice 3a (W36, W37, W39, W44, W51): the AI pilot flies Dogfight's four gates on the L1 jet, headless, against
    /// the pass criteria fixed in ROADMAP before tuning (W50).
    /// </summary>
    public class AviatorSeatTests
    {
        const double Kt = Units.KnotToMetrePerSecond, Ft = Units.FootToMetre;
        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);
        /// <summary>Dogfight's start (its builder's JetStart): 5,000 ft, 2.5 km south of gate 1, heading 000, 400 kt.</summary>
        static readonly Vector3 DogfightStart = new Vector3(0f, 1524f, -2500f);

        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        static PilotProfile Line()
        {
            var so = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(AviatorTools.LinePilotPath);
            Assert.IsNotNull(so, AviatorTools.LinePilotPath + " (Deep Water → RH Aviator → Apply Line Pilot Profile)");
            return so.Bake();
        }
        static GateCourse Course() => AssetDatabase.LoadAssetAtPath<GateCourseSO>(AviatorTools.DogfightRoutePath).Bake();

        sealed class Flight
        {
            public FlightCore Core;
            public AviatorSeat Seat;
            public RuleLog Log;
            public DeepWater.AirWing.Core.Aircraft Jet;
            public float LowestFtAwayFromGate4 = float.MaxValue, LowestFt = float.MaxValue;
            public long Ticks;
        }

        /// <summary>The AI from Dogfight's start, until the course is flown and 10 s more, a crash, or 15 minutes.</summary>
        static Flight Fly()
        {
            AircraftProfile e = E();
            var f = new Flight { Core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(8f)), Log = new RuleLog() };   // the land's top everywhere: the strict case
            GateCourse course = Course();
            f.Seat = new AviatorSeat(f.Core, e, Line(), course, f.Log);
            var start = AircraftFactory.LevelStart(e, L1, DogfightStart, 0f, (float)(400 * Kt), 0.6f * e.Airframe.InternalFuelKg, 0.7f);
            f.Core.Add(AircraftFactory.Build(1, e, L1, start), f.Seat);
            f.Jet = f.Core.Find(1);
            Vector3 gate4 = course.Gates[3].Centre;
            long doneAt = -1;
            while (f.Core.Tick < 15 * 60 * 50 && f.Jet.State.Status == FlightStatus.Flying)
            {
                f.Core.Step();
                AirframeState s = f.Jet.State;
                float ft = s.Position.y / (float)Ft;
                f.LowestFt = Mathf.Min(f.LowestFt, ft);
                Vector2 toGate4 = new Vector2(s.Position.x - gate4.x, s.Position.z - gate4.z);
                if (toGate4.magnitude > 3000f) f.LowestFtAwayFromGate4 = Mathf.Min(f.LowestFtAwayFromGate4, ft);
                if (doneAt < 0 && f.Seat.Mode == PilotMode.HoldCourse) doneAt = f.Core.Tick;
                if (doneAt >= 0 && f.Core.Tick > doneAt + 500) break;
            }
            f.Ticks = f.Core.Tick;
            return f;
        }

        [Test]
        public void TheAI_FliesDogfightsFourGates_InOrder_InsideTheirBands()
        {
            Flight f = Fly();
            foreach (GatePass p in f.Seat.Route.Passes)
                Debug.Log($"[AVIATOR] {f.Seat.Route.Gates[p.Gate].Name}: t {p.Tick * 0.02f:F1} s, {p.SpeedKt:F0} kt, {p.AltitudeFt:F0} ft, {p.OffCentreM:F0} m off centre{(p.Clean ? "" : " — OUTSIDE ITS BANDS")}");
            Debug.Log($"[AVIATOR] course: {f.Ticks * 0.02f:F0} s, lowest {f.LowestFt:F0} ft (away from gate 4: {f.LowestFtAwayFromGate4:F0} ft), {f.Jet.State.Status} {f.Jet.CrashReason}");
            Assert.AreEqual(FlightStatus.Flying, f.Jet.State.Status, f.Jet.CrashReason);
            Assert.AreEqual(4, f.Seat.Route.Passes.Count, "all four gates");
            for (int i = 0; i < 4; i++)
            {
                GatePass p = f.Seat.Route.Passes[i];
                Assert.AreEqual(i, p.Gate, "in order");
                Assert.IsTrue(p.SpeedOk, $"{f.Seat.Route.Gates[i].Name}: {p.SpeedKt:F0} kt is outside its band");
                Assert.IsTrue(p.AltitudeOk, $"{f.Seat.Route.Gates[i].Name}: {p.AltitudeFt:F0} ft is outside its band");
            }
            Assert.GreaterOrEqual(f.LowestFtAwayFromGate4, 300f, "never below 300 ft except on the runway gate's run");
            Assert.AreEqual(PilotMode.HoldCourse, f.Seat.Mode);
        }

        [Test]
        public void TheRuleLog_NamesEveryModeChange_WithItsTickAndReason()
        {
            Flight f = Fly();
            foreach (RuleFiring r in f.Log.Firings) Debug.Log("[AVIATOR] " + r);
            Assert.AreEqual(3, f.Log.Firings.Count(r => r.Rule == "next gate"), "three retargets: gates 2, 3 and 4");
            Assert.AreEqual(1, f.Log.Firings.Count(r => r.Rule == "course flown" && r.From == PilotMode.Navigate && r.To == PilotMode.HoldCourse), "one mode change");
            long last = -1;
            foreach (RuleFiring r in f.Log.Firings)
            {
                Assert.IsFalse(string.IsNullOrEmpty(r.Reason), r.Rule + " gave no reason");
                Assert.Greater(r.Tick, last, "ticks rise");
                Assert.IsTrue(Watch.IsWatch(r.Tick, 0.02, AviatorSeat.WatchHz), "rules fire on a watch");
                last = r.Tick;
            }
        }

        [Test]
        public void TwoRuns_GiveTheSameFixOnEveryTick_AndTheSameRuleLog()
        {
            Flight a = Fly(), b = Fly();
            Assert.AreEqual(a.Ticks, b.Ticks);
            Assert.AreEqual(-1, a.Core.Fixes.FirstDivergence(b.Core.Fixes), "the same Fix every tick");
            Assert.AreEqual(-1, a.Log.FirstDivergence(b.Log), "the same rule log");
        }

        [Test]
        public void TheWatch_Is4Hz_OnTheTickCount()
        {
            Assert.AreEqual(4, Enumerable.Range(0, 50).Count(t => Watch.IsWatch(t, 0.02, 4)), "four watches in the first second");
            Assert.AreEqual(400, Enumerable.Range(0, 5000).Count(t => Watch.IsWatch(t, 0.02, 4)), "400 in 100 s: no drift");
        }

        [Test]
        public void TheAviatorCore_UsesNoBannedApis()
        {
            string core = Path.Combine(Application.dataPath, "RH Aviator", "Core");
            string report = CoreBannedApiTests.Scan(core, out int files);
            Assert.Greater(files, 0, "no core files found");
            Assert.IsEmpty(report, "banned APIs in RH Aviator's core:\n" + report);
        }

        [Test]
        public void TheLineProfile_NamesTheSourceOfEveryNumber()
        {
            var so = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(AviatorTools.LinePilotPath);
            Assert.IsNotNull(so);
            var missing = typeof(PilotProfileSO).GetFields().Where(x => x.FieldType == typeof(float) && !so.provenance.Contains(x.Name)).Select(x => x.Name).ToList();
            Assert.IsEmpty(missing, "fields with no provenance: " + string.Join(", ", missing));
            StringAssert.Contains("AUTHORED", so.provenance);
        }
    }
}
