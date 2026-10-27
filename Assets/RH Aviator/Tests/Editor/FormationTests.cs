using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
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
    /// Slice 3b (W40, W42): the AI joins and holds fingertip on a leader, and the player swaps seats with it, headless,
    /// against the pass criteria fixed in ROADMAP before tuning (W50). Every part L1, 5,000 ft, over a flat sea.
    /// </summary>
    public class FormationTests
    {
        const float Kt = (float)Units.KnotToMetrePerSecond;
        const int TicksPerSecond = 50;
        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);
        const float Height = 1524f, LeaderKt = 350f;
        /// <summary>The leader's script: straight until 120 s, a 30° bank turn through 90°, straight, a 2 G pull at 200 s for 3 s, then the climb it reached.</summary>
        const float TurnAtS = 120f, PullAtS = 200f, PullS = 3f, EndS = 260f;

        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        static PilotProfile Line()
        {
            var so = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(AviatorTools.LinePilotPath);
            Assert.IsNotNull(so, AviatorTools.LinePilotPath + " (Deep Water → RH Aviator → Apply Line Pilot Profile)");
            return so.Bake();
        }

        /// <summary>
        /// The leader, scripted (a scenario seat): the line pilot's hands hold 5,000 ft and 350 kt north; at 120 s a
        /// copy held to 30° of bank turns it through 90°; at 200 s the stick asks for 2 G for 3 s; then hands with a
        /// gentle path gain hold the climb it reached (a first script pushed straight back to level at 0.2 G).
        /// </summary>
        sealed class LeaderSeat : ISeat
        {
            readonly FlightPathHands _hands, _turn, _levelOff;
            float _climbDeg = float.NaN, _throttle;
            int _phase;

            public LeaderSeat(in AircraftProfile e, in PilotProfile p)
            {
                HandsSettings turn = p.Hands, level = p.Hands;
                turn.MaxBankDeg = 30f;
                level.GPerPathDeg = 0.05f;
                _hands = new FlightPathHands(p.Hands, e.Controls);
                _turn = new FlightPathHands(turn, e.Controls);
                _levelOff = new FlightPathHands(level, e.Controls);
            }

            public SeatKind Kind => SeatKind.Scenario;

            public PilotInputs Issue(long tick, int aircraftId, in AirframeState s)
            {
                float t = tick / (float)TicksPerSecond;
                var north = new FlightPathRequest { AimWorld = s.Position + Vector3.forward * 1e6f, AltitudeM = Height, SpeedMps = LeaderKt * Kt };
                var east = new FlightPathRequest { AimWorld = new Vector3(1e7f, Height, s.Position.z), AltitudeM = Height, SpeedMps = LeaderKt * Kt };
                int phase = t < TurnAtS ? 0 : t < PullAtS ? 1 : t < PullAtS + PullS ? 2 : 3;
                FlightPathHands hands = phase == 0 ? _hands : phase == 1 ? _turn : phase == 2 ? _hands : _levelOff;
                // Each phase's hands pick up the throttle the last left, so the leader does not lurch in speed.
                if (phase != _phase) hands.Resume(_throttle);
                _phase = phase;
                PilotInputs inputs;
                if (phase == 0) inputs = hands.Fly(north, s, 0f);
                else if (phase == 1) inputs = hands.Fly(east, s, 0f);
                else if (phase == 2)
                {
                    inputs = hands.Fly(east, s, 0f);
                    inputs.StickPitch = hands.StickForG(2f);
                }
                else
                {
                    if (float.IsNaN(_climbDeg)) _climbDeg = FlightPathHands.PathDeg(s);
                    east.AltitudeM = s.Position.y;
                    east.PathDeg = _climbDeg;
                    inputs = hands.Fly(east, s, 0f);
                }
                _throttle = inputs.Throttle;
                return inputs;
            }
        }

        sealed class Run
        {
            public FlightCore Core;
            public AviatorSeat Wing;
            public RuleLog Log;
            public float JoinedS = float.NaN, MostAheadOfLeaderM = float.MinValue, LeaderMaxG, LeaderMaxBankDeg;
            public float WorstAlong, WorstAcross, WorstUp;
        }

        /// <summary>Leader (1) and wingman (2) at 350 kt, the wingman 2 nm behind on the same line, ordered to join.</summary>
        static Run Fly(float untilS, System.Action<Run, long> everyTick = null, PilotProfile? pilot = null)
        {
            AircraftProfile e = E();
            PilotProfile p = pilot ?? Line();
            var run = new Run { Core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f)), Log = new RuleLog() };
            float fuel = 0.6f * e.Airframe.InternalFuelKg;
            run.Core.Add(AircraftFactory.Build(1, e, L1, AircraftFactory.LevelStart(e, L1, new Vector3(0f, Height, 0f), 0f, LeaderKt * Kt, fuel, 0.7f)), new LeaderSeat(e, p));
            run.Wing = new AviatorSeat(run.Core, e, p, run.Log);
            run.Core.Add(AircraftFactory.Build(2, e, L1, AircraftFactory.LevelStart(e, L1, new Vector3(0f, Height, -2f * 1852f), 0f, LeaderKt * Kt, fuel, 0.7f)), run.Wing);
            run.Wing.JoinOn(2, 1);

            Aircraft lead = run.Core.Find(1), wing = run.Core.Find(2);
            while (run.Core.Tick < untilS * TicksPerSecond && wing.State.Status == FlightStatus.Flying && lead.State.Status == FlightStatus.Flying)
            {
                run.Core.Step();
                long tick = run.Core.Tick;
                float t = tick / (float)TicksPerSecond;
                var slot = new Slot(lead.State, p);
                slot.ErrorOf(wing.State.Position, out float along, out float across, out float up);
                run.MostAheadOfLeaderM = Mathf.Max(run.MostAheadOfLeaderM, slot.AheadOfLeader(wing.State.Position));
                bool inBox = Mathf.Abs(along) <= 10f && Mathf.Abs(across) <= 5f && Mathf.Abs(up) <= 5f;
                if (float.IsNaN(run.JoinedS) && inBox) run.JoinedS = t;
                // The hold is judged from the leader's turn to the end of its level-off.
                if (t >= TurnAtS)
                {
                    run.WorstAlong = Mathf.Max(run.WorstAlong, Mathf.Abs(along));
                    run.WorstAcross = Mathf.Max(run.WorstAcross, Mathf.Abs(across));
                    run.WorstUp = Mathf.Max(run.WorstUp, Mathf.Abs(up));
                    run.LeaderMaxG = Mathf.Max(run.LeaderMaxG, lead.State.LoadFactor);
                    run.LeaderMaxBankDeg = Mathf.Max(run.LeaderMaxBankDeg, Mathf.Abs(FlightPathHands.BankDeg(lead.State)));
                }
                everyTick?.Invoke(run, tick);
            }
            return run;
        }

        [Test]
        public void TheAI_JoinsFrom2nmBehind_Within90s_WithoutPassingTheLeader()
        {
            Run run = Fly(TurnAtS);
            Debug.Log($"[Aviator 3b] join: in the box at {run.JoinedS:F1} s; most ahead of the leader {run.MostAheadOfLeaderM:F1} m; rules: {string.Join(" | ", run.Log.Firings.Select(f => $"{f.Tick / 50f:F1}s {f.Rule}"))}");
            Assert.AreEqual(FlightStatus.Flying, run.Core.Find(2).State.Status, "the wingman did not crash");
            Assert.IsFalse(float.IsNaN(run.JoinedS), "the wingman reached the slot (±10 m along, ±5 m across and up)");
            Assert.LessOrEqual(run.JoinedS, 90f, "joined from 2 nm behind in 90 s or less");
            Assert.LessOrEqual(run.MostAheadOfLeaderM, 0f, "never past the leader");
            Assert.IsTrue(run.Log.Firings.Any(f => f.Rule == "in the slot" && f.To == PilotMode.Fingertip), "the rule log says when it was in the slot");
        }

        /// <summary>Also writes a trace, once every half second, to <c>Temp/aviator_3b_trace.csv</c> (the slice's evidence).</summary>
        [Test]
        public void TheAI_HoldsTheSlot_ThroughA30DegreeTurnAndA2GPull()
        {
            var csv = new System.Text.StringBuilder("t,mode,along,across,up,ahead_of_lead,wing_kt,slot_kt,cmd_kt,throttle,ab,brake,lead_g,lead_bank,wing_g,wing_bank" + "\n");
            PilotProfile p = Line();
            Run run = Fly(EndS, (r, tick) =>
            {
                if (tick % 25 != 0) return;
                Aircraft lead = r.Core.Find(1), wing = r.Core.Find(2);
                var slot = new Slot(lead.State, p);
                slot.ErrorOf(wing.State.Position, out float along, out float across, out float up);
                PilotInputs i = wing.LastInputs;
                csv.AppendLine(FormattableString.Invariant($"{tick / 50f:F1},{r.Wing.Mode},{along:F1},{across:F1},{up:F1},{slot.AheadOfLeader(wing.State.Position):F1},{wing.State.Velocity.magnitude / Kt:F1},{slot.Velocity.magnitude / Kt:F1},{r.Wing.Request.SpeedMps / Kt:F1},{i.Throttle:F3},{(i.Afterburner ? 1 : 0)},{((i.Switches & PilotSwitches.SpeedBrake) != 0 ? 1 : 0)},{lead.State.LoadFactor:F2},{FlightPathHands.BankDeg(lead.State):F1},{wing.State.LoadFactor:F2},{FlightPathHands.BankDeg(wing.State):F1}"));
            });
            System.IO.File.WriteAllText("Temp/aviator_3b_trace.csv", csv.ToString());
            Debug.Log($"[Aviator 3b] hold from {TurnAtS:F0} s to {EndS:F0} s: worst {run.WorstAlong:F1} m along, {run.WorstAcross:F1} m across, {run.WorstUp:F1} m up; " +
                      $"leader up to {run.LeaderMaxBankDeg:F1}° bank, {run.LeaderMaxG:F2} G; rules: {string.Join(" | ", run.Log.Firings.Select(f => $"{f.Tick / 50f:F1}s {f.Rule}"))}");
            Assert.AreEqual(FlightStatus.Flying, run.Core.Find(2).State.Status, "the wingman did not crash");
            Assert.GreaterOrEqual(run.LeaderMaxBankDeg, 29f, "the leader did bank 30°");
            Assert.GreaterOrEqual(run.LeaderMaxG, 1.95f, "the leader did pull 2 G");
            Assert.LessOrEqual(run.WorstAlong, 10f, "within ±10 m along");
            Assert.LessOrEqual(run.WorstAcross, 5f, "within ±5 m across");
            Assert.LessOrEqual(run.WorstUp, 5f, "within ±5 m up");
            Assert.AreEqual(PilotMode.Fingertip, run.Wing.Mode, "still in fingertip at the end");
        }

        [Test]
        public void ThePlayer_TakesTheWingmansJet_AndHandsItBack_TheLogKeepsItsShape_AndTheFixIsContinuous()
        {
            const float TakeS = 100f, GiveBackS = 110f;
            var player = new PlayerSeat(new PlayerSeatSettings { LeverRatePerSecond = 0.5f, AfterburnerDetent = 0.9f, Instructor = new InstructorSettings { RollGain = 2.5f, PitchGain = 4f, WingsLevelBlendDeg = 5f } }, 0.02);
            SeatRoster roster = null;
            float worstStepErrorM = 0f;
            Vector3 lastPos = default, lastVel = default;
            Run run = Fly(GiveBackS + 30f, (r, tick) =>
            {
                Aircraft wing = r.Core.Find(2);
                if (roster == null)
                {
                    roster = new SeatRoster(r.Core, player);
                    roster.SetOwnSeat(2, r.Wing);
                }
                // A tick's move must match the velocities either side of it (no jump at either handover).
                if (tick > 1)
                {
                    Vector3 predicted = lastPos + 0.5f * (lastVel + wing.State.Velocity) * 0.02f;
                    worstStepErrorM = Mathf.Max(worstStepErrorM, (wing.State.Position - predicted).magnitude);
                }
                lastPos = wing.State.Position;
                lastVel = wing.State.Velocity;
                if (tick == (long)(TakeS * TicksPerSecond)) roster.TakeSeat(2);
                if (tick == (long)(GiveBackS * TicksPerSecond)) roster.TakeSeat(0);
            });

            var log = run.Core.Inputs.All;
            long ticks = run.Core.Tick;
            Assert.AreEqual(2 * ticks, log.Count, "one entry per aircraft per tick, through both handovers");
            for (long t = 0; t < ticks; t++)
            {
                Assert.AreEqual(1, log[(int)(2 * t)].AircraftId, $"tick {t}: aircraft 1 first");
                Assert.AreEqual(2, log[(int)(2 * t + 1)].AircraftId, $"tick {t}: aircraft 2 second");
            }
            // The hook runs after a step, when the core's Tick is already the next tick: the first one flown from the new seat.
            long take = (long)(TakeS * TicksPerSecond), back = (long)(GiveBackS * TicksPerSecond);
            Assert.IsTrue(run.Core.Inputs.TryGet(take - 1, 2, out InputEntry lastAi) && lastAi.Seat == SeatKind.Ai, "the AI flew it up to the handover");
            Assert.IsTrue(run.Core.Inputs.TryGet(take, 2, out InputEntry firstPlayer) && firstPlayer.Seat == SeatKind.Player, "the player flies it from the next tick");
            Assert.IsTrue(run.Core.Inputs.TryGet(back - 1, 2, out InputEntry lastPlayer) && lastPlayer.Seat == SeatKind.Player, "the player flew it up to handing back");
            Assert.IsTrue(run.Core.Inputs.TryGet(back, 2, out InputEntry firstAi) && firstAi.Seat == SeatKind.Ai, "the AI has it again at once");
            Debug.Log($"[Aviator 3b] handover: throttle AI {lastAi.Inputs.Throttle:F3} → player {firstPlayer.Inputs.Throttle:F3}; player {lastPlayer.Inputs.Throttle:F3} → AI {firstAi.Inputs.Throttle:F3}; " +
                      $"worst step error {worstStepErrorM * 1000f:F1} mm; mode at the end {run.Wing.Mode}; rules: {string.Join(" | ", run.Log.Firings.Select(f => $"{f.Tick / 50f:F1}s {f.Rule}"))}");
            Assert.AreEqual(lastAi.Inputs.Throttle, firstPlayer.Inputs.Throttle, 1e-3f, "the player's lever starts where the AI left the throttle");
            Assert.AreEqual(lastPlayer.Inputs.Throttle, firstAi.Inputs.Throttle, 0.02f, "the AI's throttle starts where the player left it");
            Assert.Less(worstStepErrorM, 0.05f, "the Fix is continuous: no tick moves the jet more than its velocities say");
            Assert.AreEqual(FlightStatus.Flying, run.Core.Find(2).State.Status, "the wingman did not crash");
        }

        /// <summary>Tuning, not a pass criterion: the hold's worst errors over a grid of the formation gains (Editor.log, [Aviator 3b sweep]).</summary>
        [Test, Explicit("a tuning sweep")]
        public void Sweep()
        {
            foreach (float share in new[] { 0.75f, 0.9f, 1f })
            foreach (float climb in new[] { 0.03f, 0.04f, 0.06f })
            foreach (float gPath in new[] { 0.4f, 0.5f, 0.6f, 0.8f })
            {
                PilotProfile p = Line();
                p.SlotRotationShare = share;
                p.SlotClimbDegPerM = climb;
                p.SlotGPerPathDeg = gPath;
                Run run = Fly(EndS, null, p);
                Debug.Log(FormattableString.Invariant($"[Aviator 3b sweep] share {share:F2} climb {climb:F2} gPath {gPath:F2}: joined {run.JoinedS:F1} s, worst {run.WorstAlong:F1} along {run.WorstAcross:F1} across {run.WorstUp:F1} up, end {run.Wing.Mode}"));
            }
        }

        /// <summary>Starts for three wingmen (offsets from the leader: x east, y up, z north), slots 1, 2, 3 in that order.</summary>
        static readonly (string name, Vector3[] at)[] JoinStarts =
        {
            ("echelon behind (the 4 v 4 start)", new[] { new Vector3(60f, 0f, -60f), new Vector3(120f, 0f, -120f), new Vector3(180f, 0f, -180f) }),
            ("crossed: right-hand jets to left slots", new[] { new Vector3(1500f, 0f, -2000f), new Vector3(-1500f, 0f, -2000f), new Vector3(800f, 0f, -1000f) }),
            ("stacked in trail", new[] { new Vector3(0f, 0f, -1000f), new Vector3(0f, 0f, -2000f), new Vector3(0f, 0f, -3000f) }),
            ("abeam both sides, crossed", new[] { new Vector3(2000f, 0f, -300f), new Vector3(-2000f, 0f, -300f), new Vector3(-1000f, 150f, -600f) }),
        };

        /// <summary>
        /// Three wingmen join one leader with no fight, from <paramref name="at"/>: the closest any two jets come after the
        /// first 5 s, when all three were first in fingertip (NaN: never), and how many crashed, over 115 s.
        /// </summary>
        static float FourShipJoin(Vector3[] at, out string pair, out float closestAtS, out float allInS, out int crashed)
        {
            AircraftProfile e = E();
            PilotProfile p = Line();
            float fuel = 0.6f * e.Airframe.InternalFuelKg;
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            core.Add(AircraftFactory.Build(1, e, L1, AircraftFactory.LevelStart(e, L1, new Vector3(0f, Height, 0f), 0f, LeaderKt * Kt, fuel, 0.7f)), new LeaderSeat(e, p));
            var seats = new AviatorSeat[3];
            for (int i = 0; i < 3; i++)
            {
                seats[i] = new AviatorSeat(core, e, p, new RuleLog());
                core.Add(AircraftFactory.Build(i + 2, e, L1, AircraftFactory.LevelStart(e, L1, new Vector3(0f, Height, 0f) + at[i], 0f, LeaderKt * Kt, fuel, 0.7f)), seats[i]);
                seats[i].JoinOn(i + 2, 1, i + 1);
            }
            float closest = float.MaxValue;
            closestAtS = 0f; allInS = float.NaN; pair = "";
            while (core.Tick < 115 * TicksPerSecond)
            {
                core.Step();
                float t = core.Tick / (float)TicksPerSecond;
                if (float.IsNaN(allInS) && seats.All(s => s.Mode == PilotMode.Fingertip)) allInS = t;
                if (t < 5f) continue;
                for (int a = 1; a <= 4; a++)
                    for (int b = a + 1; b <= 4; b++)
                    {
                        float d = (core.Find(a).State.Position - core.Find(b).State.Position).magnitude;
                        if (d < closest) { closest = d; closestAtS = t; pair = $"{a}-{b}"; }
                    }
            }
            crashed = core.Aircraft.Count(a => a.State.Status != FlightStatus.Flying);
            return closest;
        }

        /// <summary>
        /// A join with no fight avoids too (PilotProfile.JoinAvoidM): before it, crossed joins met at 0 m (a survey of these starts,
        /// 2026-10-27). From each start, no two jets closer than 20 m (an F/A-18E spans 13.6 m), all three in fingertip
        /// within 90 s, nobody down.
        /// </summary>
        [Test]
        public void FourShipJoins_KeepClearOfEachOther_AndAllReachTheirSlots()
        {
            foreach (var (name, at) in JoinStarts)
            {
                float closest = FourShipJoin(at, out string pair, out float closestAtS, out float allInS, out int crashed);
                Debug.Log(FormattableString.Invariant($"[Aviator joins] {name}: closest {closest:F0} m ({pair} at {closestAtS:F0} s); all in fingertip {(float.IsNaN(allInS) ? "never" : allInS.ToString("F0") + " s")}; crashed {crashed}"));
                Assert.Greater(closest, 20f, $"{name}: closest pass {closest:F0} m ({pair} at {closestAtS:F0} s)");
                Assert.Less(allInS, 90f, $"{name}: all three in fingertip within 90 s");
                Assert.AreEqual(0, crashed, name);
            }
        }

        [Test]
        public void TheFingertipLog_IsTheSameOnTwoRuns()
        {
            Run a = Fly(100f), b = Fly(100f);
            Assert.AreEqual(-1, a.Log.FirstDivergence(b.Log), "the same rule log");
            Assert.AreEqual(a.Core.Fixes.All.Last().Hash, b.Core.Fixes.All.Last().Hash, "the same Fix at the end");
        }
    }
}
