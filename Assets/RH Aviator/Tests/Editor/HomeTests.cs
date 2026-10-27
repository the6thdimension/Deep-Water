using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Editor;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Tests
{
    /// <summary>
    /// Slice 3c (W41, W47): the AI goes home and flies the overhead break onto Dogfight's runway 09/27, headless,
    /// against the pass criteria and boxes fixed in ROADMAP before tuning (W50). Every part L1; the ground everywhere
    /// at the runway's height (the strict case for the ground guard and the pattern's heights).
    /// </summary>
    public class HomeTests
    {
        const float Kt = (float)Units.KnotToMetrePerSecond, Ft = 0.3048f, Nm = 1852f;
        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);
        /// <summary>Dogfight's runway 09/27 (its builder): centre, the heading of its length, its size.</summary>
        static readonly Vector3 RunwayCentre = new Vector3(0f, 8.05f, -5500f);
        const float RunwayHeadingDeg = 90f, RunwayLengthM = 2438f, RunwayWidthM = 61f;

        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        static PilotProfile Line()
        {
            var so = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(AviatorTools.LinePilotPath);
            Assert.IsNotNull(so, AviatorTools.LinePilotPath + " (Deep Water → RH Aviator → Apply Line Pilot Profile)");
            return so.Bake();
        }

        /// <summary>What one landing did, and every box it left.</summary>
        sealed class Landing
        {
            public int Seed;
            public string Runway, Crash;
            public float SinkFtS = float.NaN, TouchAlongM = float.NaN, TouchLateralM = float.NaN, StopAlongM = float.NaN, StopLateralM = float.NaN, Seconds;
            public bool Stopped;
            public readonly List<string> OutOfBox = new List<string>();
            public RuleLog Log;
            public void Out(string what) { if (OutOfBox.Count < 12 && !OutOfBox.Contains(what)) OutOfBox.Add(what); }
        }

        /// <summary>One AI from a seeded start 10 nm from the runway, ordered home at once, flown until it stops, crashes, or 20 minutes.</summary>
        static Landing Fly(int seed, in AircraftProfile e, in PilotProfile p, Action<FlightCore, AviatorSeat, AirframeState, Runway> everyTick = null)
        {
            var rng = new DeterministicRng((ulong)(1000 + seed));
            float bearing = rng.NextFloat01() * 360f, heading = rng.NextFloat01() * 360f;
            float heightFt = 3000f + 7000f * rng.NextFloat01(), kt = 300f + 150f * rng.NextFloat01();
            Vector3 start = RunwayCentre + Quaternion.Euler(0f, bearing, 0f) * Vector3.forward * (10f * Nm);
            start.y = heightFt * Ft;

            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(RunwayCentre.y));
            var log = new RuleLog();
            var seat = new AviatorSeat(core, e, p, log);
            core.Add(AircraftFactory.Build(1, e, L1, AircraftFactory.LevelStart(e, L1, start, heading, kt * Kt, 0.6f * e.Airframe.InternalFuelKg, 0.7f)), seat);
            Runway home = Runway.PickEnd("09/27", RunwayCentre, RunwayHeadingDeg, RunwayLengthM, RunwayWidthM, start, p);
            seat.GoHome(1, home);

            var l = new Landing { Seed = seed, Runway = home.Name, Log = log };
            Aircraft jet = core.Find(1);
            AirframeState before = jet.State;
            while (core.Tick < 20 * 60 * 50)
            {
                before = jet.State;
                core.Step();
                AirframeState s = jet.State;
                if (s.Status == FlightStatus.Crashed) { l.Crash = jet.CrashReason; break; }
                home.Frame(s.Position, out float along, out float lateral);
                if (float.IsNaN(l.SinkFtS) && s.InContact)
                {
                    l.SinkFtS = Mathf.Max(0f, -before.Velocity.y) / Ft;
                    l.TouchAlongM = along;
                    l.TouchLateralM = lateral;
                }
                if (seat.Mode == PilotMode.Landed) { l.Stopped = true; l.StopAlongM = along; l.StopLateralM = lateral; break; }
                Box(l, seat, s, home, p, core.Tick);
                everyTick?.Invoke(core, seat, s, home);
            }
            l.Seconds = core.Tick / 50f;
            return l;
        }

        /// <summary>
        /// Tuning, not a pass criterion: one seeded flight home, a row every <paramref name="everyTicks"/> ticks, to
        /// <paramref name="path"/> (call it from a RunCommand).
        /// </summary>
        public static string Trace(int seed, string path, int everyTicks = 50)
        {
            AircraftProfile e = E();
            PilotProfile p = Line();
            var csv = new System.Text.StringBuilder("t,leg,along,lateral,ft,kt,thr,ab,gear,flaps,sb,aoa,g,bank,path,req_ft,req_kt,req_path,contact\n");
            Landing l = Fly(seed, e, p, (core, seat, s, r) =>
            {
                if (core.Tick % everyTicks != 0) return;
                r.Frame(s.Position, out float along, out float lateral);
                var i = core.Find(1).LastInputs;
                FlightPathRequest q = seat.Request;
                csv.AppendLine(FormattableString.Invariant($"{core.Tick / 50f:F1},{seat.Leg},{along:F0},{lateral:F0},{(s.Position.y - r.HeightM) / Ft:F0},{s.Velocity.magnitude / Kt:F0},{i.Throttle:F2},{(i.Afterburner ? 1 : 0)},{s.GearDeployed:F2},{((i.Switches & DeepWater.AirWing.Core.Inputs.PilotSwitches.FlapsDown) != 0 ? 1 : 0)},{((i.Switches & DeepWater.AirWing.Core.Inputs.PilotSwitches.SpeedBrake) != 0 ? 1 : 0)},{s.AngleOfAttack * Mathf.Rad2Deg:F1},{s.LoadFactor:F2},{FlightPathHands.BankDeg(s):F0},{FlightPathHands.PathDeg(s):F1},{(q.AltitudeM - r.HeightM) / Ft:F0},{q.SpeedMps / Kt:F0},{q.PathDeg:F1},{(s.InContact ? 1 : 0)}"));
            });
            foreach (RuleFiring f in l.Log.Firings) csv.AppendLine("# " + f);
            csv.AppendLine("# " + Describe(l));
            System.IO.File.WriteAllText(path, csv.ToString());
            return Describe(l);
        }

        /// <summary>The boxes, as fixed in ROADMAP before tuning. A leg's first 10 s are its entry and are not boxed.</summary>
        static void Box(Landing l, AviatorSeat seat, in AirframeState s, in Runway r, in PilotProfile p, long tick)
        {
            if (seat.Mode != PilotMode.Home || tick - seat.LegTick < 10 * 50) return;
            float ft = (s.Position.y - r.HeightM) / Ft, kt = s.Velocity.magnitude / Kt;
            Vector3 track = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
            float offRunway = Vector3.Angle(track, r.Along), offReciprocal = Vector3.Angle(track, -r.Along);
            r.Frame(s.Position, out float along, out _);
            switch (seat.Leg)
            {
                case PatternLeg.Initial:
                    if (ft < 650f || ft > 950f) l.Out($"initial height {ft:F0} ft");
                    if (kt < 320f || kt > 380f) l.Out($"initial speed {kt:F0} kt");
                    if (offRunway > 10f) l.Out($"initial track {offRunway:F0}° off");
                    break;
                case PatternLeg.Break:
                    if (ft < 600f || ft > 1000f) l.Out($"break height {ft:F0} ft");
                    if (s.LoadFactor > p.Hands.MaxG + 0.05f) l.Out($"break G {s.LoadFactor:F2}");
                    break;
                case PatternLeg.Downwind:
                    if (ft < 450f || ft > 950f) l.Out($"downwind height {ft:F0} ft");
                    if (offReciprocal > 15f) l.Out($"downwind track {offReciprocal:F0}° off");
                    break;
                case PatternLeg.Base:
                    if (ft < 150f || ft > 950f) l.Out($"turn to final height {ft:F0} ft");
                    if (Mathf.Abs(FlightPathHands.BankDeg(s)) > p.PatternBankDeg + 10f) l.Out($"turn to final bank {FlightPathHands.BankDeg(s):F0}°");
                    break;
                case PatternLeg.Final:
                    if (ft > 100f)
                    {
                        float pathFt = Pattern.GlidePathHeight(r, along, p) / Ft;
                        if (Mathf.Abs(ft - pathFt) > 75f) l.Out($"final {ft - pathFt:+0;-0} ft off the path");
                    }
                    if (kt < 135f || kt > 160f) l.Out($"final speed {kt:F0} kt");
                    if (offRunway > 10f) l.Out($"final track {offRunway:F0}° off");
                    break;
            }
        }

        static string Describe(Landing l) => FormattableString.Invariant(
            $"seed {l.Seed} {l.Runway}: {(l.Crash != null ? "CRASH " + l.Crash : l.Stopped ? "stopped" : "not stopped")} in {l.Seconds:F0} s; touchdown {l.SinkFtS:F1} ft/s at {l.TouchAlongM:F0} m, {l.TouchLateralM:F1} m off; ") + FormattableString.Invariant(
            $"stopped at {l.StopAlongM:F0} m, {l.StopLateralM:F1} m off; boxes: {(l.OutOfBox.Count == 0 ? "all kept" : string.Join(", ", l.OutOfBox))}");

        [Test]
        public void TheAI_GoesHome_AndLands_From20ScatteredStarts()
        {
            AircraftProfile e = E();
            PilotProfile p = Line();
            var all = Enumerable.Range(0, 20).Select(i => Fly(i, e, p)).ToList();
            foreach (Landing l in all) Debug.Log("[Aviator 3c] " + Describe(l));
            foreach (Landing l in all)
            {
                string who = Describe(l);
                Assert.IsNull(l.Crash, who);
                Assert.IsTrue(l.Stopped, "stopped: " + who);
                Assert.Less(l.SinkFtS, 12f, "touchdown sink under 12 ft/s: " + who);
                Assert.LessOrEqual(Mathf.Abs(l.TouchLateralM), 5f, "on the centreline at touchdown: " + who);
                Assert.That(l.TouchAlongM, Is.InRange(0f, 0.5f * RunwayLengthM), "touchdown in the first half: " + who);
                Assert.That(l.StopAlongM, Is.InRange(0f, RunwayLengthM), "stopped on the runway: " + who);
                Assert.LessOrEqual(Mathf.Abs(l.StopLateralM), 0.5f * RunwayWidthM, "stopped on the runway: " + who);
                Assert.IsEmpty(l.OutOfBox, "every leg inside its box: " + who);
            }
        }

        [Test]
        public void TheRuleLog_NamesEveryLeg_InOrder()
        {
            Landing l = Fly(0, E(), Line());
            string[] rules = l.Log.Firings.Select(f => f.Rule).ToArray();
            Debug.Log("[Aviator 3c] rules: " + string.Join(" | ", l.Log.Firings.Select(f => $"{f.Tick / 50f:F0}s {f.Rule}")));
            string[] legs = { "ordered: go home", "initial", "break", "downwind", "the 180", "final", "touchdown", "stopped" };
            int at = -1;
            foreach (string leg in legs)
            {
                int i = Array.IndexOf(rules, leg, at + 1);
                Assert.Greater(i, at, $"'{leg}' after the leg before it; rules: {string.Join(", ", rules)}");
                at = i;
            }
        }

        [Test]
        public void Bingo_IsAFlightTimeStandIn_ThatSendsAPilotWithAHomeThere()
        {
            AircraftProfile e = E();
            PilotProfile p = Line();
            p.BingoFlightTimeS = 30f;
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(RunwayCentre.y));
            var log = new RuleLog();
            var seat = new AviatorSeat(core, e, p, log);
            core.Add(AircraftFactory.Build(1, e, L1, AircraftFactory.LevelStart(e, L1, new Vector3(0f, 1524f, 10000f), 0f, 350f * Kt, 0.6f * e.Airframe.InternalFuelKg, 0.7f)), seat);
            seat.SetHome(Runway.PickEnd("09/27", RunwayCentre, RunwayHeadingDeg, RunwayLengthM, RunwayWidthM, Vector3.zero, p));
            while (core.Tick < 40 * 50) core.Step();
            RuleFiring bingo = log.Firings.First(f => f.Rule == "bingo");
            Assert.AreEqual(30 * 50, bingo.Tick, "at 30 s, on the tick count");
            StringAssert.Contains("stand-in", bingo.Reason, "labelled a stand-in wherever it shows (W47)");
            Assert.AreEqual(PilotMode.Home, seat.Mode);
        }
    }
}
