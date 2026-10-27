using System;
using System.Collections.Generic;
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
    /// Slice 3d (Pilot L1: W38, W43 at eight, W45, W46, W52, W53): the fight, headless, against the pass criteria
    /// fixed in ROADMAP before building (W50). Every part L1, over a flat sea. No weapons: rear-cone seconds only, so
    /// every pilot here flies with its guns safe (<see cref="PilotProfile.TriggerMils"/> 0); the armed fight is
    /// <c>GunFightTests</c>.
    /// </summary>
    public class FightTests
    {
        const float Kt = (float)Units.KnotToMetrePerSecond, Ft = 0.3048f;
        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);

        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        static PilotProfile Pilot(string path)
        {
            var so = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(path);
            Assert.IsNotNull(so, path + " (Deep Water → RH Aviator → Apply Pilot Profiles)");
            PilotProfile p = so.Bake();
            p.TriggerMils = 0f;   // guns safe: these tests are the position fight (W46)
            return p;
        }

        static AirframeState Start(in AircraftProfile e, Vector3 at, float headingDeg, float kt) =>
            AircraftFactory.LevelStart(e, L1, at, headingDeg, kt * Kt, 0.6f * e.Airframe.InternalFuelKg, 0.7f);

        sealed class Merge
        {
            public int Seed;
            public double Ace, Rookie;
            public string Crash;
            public float LowestFt = float.MaxValue;
        }

        /// <summary>A neutral merge: head on, 6 km apart, offset to one side; the ace blue, the rookie red; 180 s.</summary>
        static Merge Fly(int seed, in AircraftProfile e, in PilotProfile ace, in PilotProfile rookie, Action<FlightCore, AviatorSeat, AviatorSeat, Engagement> everyTick = null)
        {
            var rng = new DeterministicRng((ulong)(5000 + seed));
            float height = (9000f + 2000f * rng.NextFloat01()) * Ft;
            float offset = (rng.NextFloat01() < 0.5f ? -1f : 1f) * (200f + 300f * rng.NextFloat01());
            float kt = 380f + 40f * rng.NextFloat01();
            bool aceSouth = rng.NextFloat01() < 0.5f;
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            var fight = new Engagement();
            var aceSeat = new AviatorSeat(core, e, ace, new RuleLog());
            var rookieSeat = new AviatorSeat(core, e, rookie, new RuleLog());
            Vector3 south = new Vector3(0f, height, -3000f), north = new Vector3(offset, height, 3000f);
            core.Add(AircraftFactory.Build(1, e, L1, Start(e, aceSouth ? south : north, aceSouth ? 0f : 180f, kt)), aceSeat);
            core.Add(AircraftFactory.Build(2, e, L1, Start(e, aceSouth ? north : south, aceSouth ? 180f : 0f, kt)), rookieSeat);
            fight.Join(1, Side.Blue);
            fight.Join(2, Side.Red);
            aceSeat.FightIn(1, fight);
            rookieSeat.FightIn(2, fight);
            var m = new Merge { Seed = seed };
            while (core.Tick < 180 * 50)
            {
                core.Step();
                fight.Score(core);
                foreach (Aircraft a in core.Aircraft)
                {
                    if (a.State.Status == FlightStatus.Crashed) m.Crash = $"aircraft {a.Id}: {a.CrashReason}";
                    m.LowestFt = Mathf.Min(m.LowestFt, a.State.Position.y / Ft);
                }
                if (m.Crash != null) break;
                everyTick?.Invoke(core, aceSeat, rookieSeat, fight);
            }
            m.Ace = fight.SecondsInCone(1);
            m.Rookie = fight.SecondsInCone(2);
            return m;
        }

        /// <summary>Tuning, not a pass criterion: one merge, a row a second, to <paramref name="path"/> (call it from a RunCommand).</summary>
        public static string TraceMerge(int seed, string path)
        {
            AircraftProfile e = E();
            var csv = new System.Text.StringBuilder("t,range,ace_mode,ace_pursuit,ace_target,ace_kt,ace_ft,ace_bank,ace_g,rookie_mode,rookie_pursuit,rookie_kt,rookie_ft,rookie_bank,rookie_g,ace_off_tail,rookie_off_tail,ace_s,rookie_s" + "\n");
            Merge m = Fly(seed, e, Pilot(AviatorTools.AcePilotPath), Pilot(AviatorTools.RookiePilotPath), (core, a, r, f) =>
            {
                if (core.Tick % 50 != 0) return;
                AirframeState sa = core.Find(1).State, sr = core.Find(2).State;
                float offA = Vector3.Angle(sa.Position - sr.Position, -sr.Velocity), offR = Vector3.Angle(sr.Position - sa.Position, -sa.Velocity);
                csv.AppendLine(FormattableString.Invariant($"{core.Tick / 50f:F0},{(sa.Position - sr.Position).magnitude:F0},{a.Mode},{a.Pursuit},{a.TargetId},{sa.Velocity.magnitude / Kt:F0},{sa.Position.y / Ft:F0},{FlightPathHands.BankDeg(sa):F0},{sa.LoadFactor:F1},{r.Mode},{r.Pursuit},{sr.Velocity.magnitude / Kt:F0},{sr.Position.y / Ft:F0},{FlightPathHands.BankDeg(sr):F0},{sr.LoadFactor:F1},{offA:F0},{offR:F0},{f.SecondsInCone(1):F1},{f.SecondsInCone(2):F1}"));
            });
            System.IO.File.WriteAllText(path, csv.ToString());
            return FormattableString.Invariant($"merge {seed}: ace {m.Ace:F1} s, rookie {m.Rookie:F1} s{(m.Crash != null ? "; CRASH " + m.Crash : "")}");
        }

        [Test]
        public void TheAce_BeatsTheRookie_InAtLeast15Of20NeutralMerges_AndNobodyCrashes()
        {
            AircraftProfile e = E();
            PilotProfile ace = Pilot(AviatorTools.AcePilotPath), rookie = Pilot(AviatorTools.RookiePilotPath);
            var all = Enumerable.Range(0, 20).Select(i => Fly(i, e, ace, rookie)).ToList();
            foreach (Merge m in all)
                Debug.Log(FormattableString.Invariant($"[Aviator 3d] merge {m.Seed}: ace {m.Ace:F1} s, rookie {m.Rookie:F1} s in the rear cone; lowest {m.LowestFt:F0} ft{(m.Crash != null ? "; CRASH " + m.Crash : "")}"));
            int wins = all.Count(m => m.Ace > m.Rookie);
            Debug.Log($"[Aviator 3d] the ace won {wins} of 20 (ace {all.Sum(m => m.Ace):F0} s, rookie {all.Sum(m => m.Rookie):F0} s in all)");
            foreach (Merge m in all) Assert.IsNull(m.Crash, $"merge {m.Seed}");
            Assert.GreaterOrEqual(wins, 15, "the ace beats the rookie on rear-cone time in at least 15 of 20");
        }

        /// <summary>One fight of any size: who flies (a pilot, or null for a hands-off jet), which side, where; scored for <paramref name="seconds"/>.</summary>
        sealed class Brawl
        {
            public Engagement Fight;
            public string Crash;
            public float LowestFt = float.MaxValue, ClosestM = float.MaxValue;
            public int ConeEvents;
        }

        static Brawl Fly(in AircraftProfile e, (PilotProfile? pilot, Side side, Vector3 at, float heading)[] jets, float seconds, int hunted)
        {
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            var b = new Brawl { Fight = new Engagement { Hunted = () => hunted } };
            var seats = new List<(int id, AviatorSeat seat)>();
            for (int i = 0; i < jets.Length; i++)
            {
                int id = i + 1;
                ISeat seat;
                if (jets[i].pilot.HasValue) { var a = new AviatorSeat(core, e, jets[i].pilot.Value, new RuleLog()); seats.Add((id, a)); seat = a; }
                else seat = new HoldSeat(new DeepWater.AirWing.Core.Inputs.PilotInputs { Throttle = 0.8f });
                core.Add(AircraftFactory.Build(id, e, L1, Start(e, jets[i].at, jets[i].heading, 400f)), seat);
                b.Fight.Join(id, jets[i].side);
            }
            foreach (var (id, seat) in seats) seat.FightIn(id, b.Fight);
            while (core.Tick < seconds * 50 && b.Crash == null)
            {
                core.Step();
                b.Fight.Score(core);
                var all = core.Aircraft;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].State.Status == FlightStatus.Crashed) b.Crash = $"aircraft {all[i].Id}: {all[i].CrashReason}";
                    b.LowestFt = Mathf.Min(b.LowestFt, all[i].State.Position.y / Ft);
                    for (int j = i + 1; j < all.Count; j++) b.ClosestM = Mathf.Min(b.ClosestM, (all[i].State.Position - all[j].State.Position).magnitude);
                }
            }
            b.ConeEvents = b.Fight.Log.Count;
            return b;
        }

        /// <summary>
        /// The dogfight survey (SIX 2026-10-27: "extensively test dogfighting with multiple air entities"), not a pass
        /// criterion: merges of every pairing, 1 v 2, 2 v 2, the 4 v 4 with the player fighting and hands off, all
        /// logged under [Aviator survey]: rear-cone seconds per side, crashes, the lowest height, the closest pass.
        /// </summary>
        [Test, Explicit("the dogfight survey")]
        public void Survey()
        {
            AircraftProfile e = E();
            PilotProfile rookie = Pilot(AviatorTools.RookiePilotPath), line = Pilot(AviatorTools.LinePilotPath), ace = Pilot(AviatorTools.AcePilotPath);
            void Merges(string name, PilotProfile blue, PilotProfile red, int n)
            {
                int blueAhead = 0, redAhead = 0, crashes = 0;
                double bs = 0, rs = 0;
                float lowest = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    var rng = new DeterministicRng((ulong)(7000 + i));
                    float h = (9000f + 2000f * rng.NextFloat01()) * Ft, off = (rng.NextFloat01() < 0.5f ? -1f : 1f) * (200f + 300f * rng.NextFloat01());
                    Brawl b = Fly(e, new (PilotProfile?, Side, Vector3, float)[] { (blue, Side.Blue, new Vector3(0f, h, -3000f), 0f), (red, Side.Red, new Vector3(off, h, 3000f), 180f) }, 180f, 0);
                    double x = b.Fight.SecondsInCone(1), y = b.Fight.SecondsInCone(2);
                    bs += x; rs += y;
                    if (x > y) blueAhead++; else if (y > x) redAhead++;
                    if (b.Crash != null) crashes++;
                    lowest = Mathf.Min(lowest, b.LowestFt);
                }
                Debug.Log(FormattableString.Invariant($"[Aviator survey] {name}, {n} merges of 180 s: blue ahead {blueAhead}, red ahead {redAhead}, even {n - blueAhead - redAhead}; cone blue {bs:F0} s, red {rs:F0} s ({bs / n:F1} / {rs / n:F1} a fight); crashes {crashes}; lowest {lowest:F0} ft"));
            }
            Merges("ace (blue) v rookie (red)", ace, rookie, 20);
            Merges("line v line", line, line, 20);
            Merges("ace v line", ace, line, 20);
            Merges("rookie v ace", rookie, ace, 20);

            void Report(string name, Brawl b) => Debug.Log(FormattableString.Invariant(
                $"[Aviator survey] {name}: cone blue {b.Fight.SideSeconds(Side.Blue):F1} s, red {b.Fight.SideSeconds(Side.Red):F1} s; {b.ConeEvents} cone events; lowest {b.LowestFt:F0} ft; closest {b.ClosestM:F0} m{(b.Crash != null ? "; CRASH " + b.Crash : "")}"));
            Report("1 v 2 (ace blue v two line red)", Fly(e, new (PilotProfile?, Side, Vector3, float)[] {
                (ace, Side.Blue, new Vector3(0f, 3048f, -3000f), 0f), (line, Side.Red, new Vector3(-200f, 3048f, 3000f), 180f), (line, Side.Red, new Vector3(200f, 3048f, 3200f), 180f) }, 300f, 0));
            Report("2 v 2 (line pair v line pair)", Fly(e, new (PilotProfile?, Side, Vector3, float)[] {
                (line, Side.Blue, new Vector3(0f, 3048f, -3000f), 0f), (line, Side.Blue, new Vector3(60f, 3048f, -3060f), 0f),
                (line, Side.Red, new Vector3(-200f, 3048f, 3000f), 180f), (line, Side.Red, new Vector3(-140f, 3048f, 3060f), 180f) }, 300f, 0));
            var fourVFour = new List<(PilotProfile?, Side, Vector3, float)>();
            PilotProfile[] reds = { rookie, line, line, ace };
            for (int i = 0; i < 4; i++) fourVFour.Add((line, Side.Blue, new Vector3(60f * i, 3048f, -60f * i), 0f));
            for (int i = 0; i < 4; i++) fourVFour.Add((reds[i], Side.Red, new Vector3(-300f + 200f * i, 3500f, 20000f), 180f));
            Report("4 v 4, the player's jet fighting (line)", Fly(e, fourVFour.ToArray(), 300f, 1));
            fourVFour[0] = (null, Side.Blue, fourVFour[0].Item3, 0f);
            Report("4 v 4, the player's jet hands off", Fly(e, fourVFour.ToArray(), 300f, 1));
        }

        [Test]
        public void APilot_DoesNotTurnTowardWhatItCannotSee_UntilItsDelayHasPassed()
        {
            AircraftProfile e = E();
            PilotProfile line = Pilot(AviatorTools.LinePilotPath);
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            var fight = new Engagement();
            var log = new RuleLog();
            var red = new AviatorSeat(core, e, line, log);
            var player = new AviatorSeat(core, e, line, new RuleLog());   // holds course: the "player", overtaking
            core.Add(AircraftFactory.Build(1, e, L1, Start(e, new Vector3(1500f, 3048f, -4000f), 0f, 500f)), player);
            core.Add(AircraftFactory.Build(2, e, L1, Start(e, new Vector3(0f, 3048f, 0f), 0f, 350f)), red);
            fight.Join(1, Side.Blue);
            fight.Join(2, Side.Red);
            red.FightIn(2, fight);
            long inView = -1, tally = -1;
            float bankBefore = 0f;
            float cosHalf = Mathf.Cos(line.FieldOfViewDeg * Mathf.Deg2Rad);
            while (core.Tick < 120 * 50)
            {
                AirframeState me = core.Find(2).State, them = core.Find(1).State;
                if (inView < 0 && Vector3.Dot(me.Attitude * Vector3.forward, (them.Position - me.Position).normalized) >= cosHalf) inView = core.Tick;
                core.Step();
                RuleFiring t = log.Firings.FirstOrDefault(f => f.Rule == "tally");
                if (t.Rule != null) { tally = t.Tick; break; }
                bankBefore = Mathf.Max(bankBefore, Mathf.Abs(FlightPathHands.BankDeg(core.Find(2).State)));
            }
            int delay = red.Eyes.DelayTicks;
            Debug.Log($"[Aviator 3d] into the field of view at tick {inView}; tally at tick {tally} (delay {delay} ticks); the most bank before the tally {bankBefore:F1}°");
            Assert.Greater(inView, 0, "the player came into view");
            Assert.GreaterOrEqual(tally, inView + delay, "not seen before the delay has passed");
            Assert.LessOrEqual(tally, inView + delay + 13, "seen on the first watch after it (4 Hz)");
            Assert.Less(bankBefore, 5f, "no turn toward the player before the tally");
        }

        [Test]
        public void FourVersusFour_RedHuntsThePlayer_BlueFights_ThenRejoins_AndNobodyCrashes()
        {
            AircraftProfile e = E();
            PilotProfile rookie = Pilot(AviatorTools.RookiePilotPath), line = Pilot(AviatorTools.LinePilotPath), ace = Pilot(AviatorTools.AcePilotPath);
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            var fight = new Engagement { Hunted = () => 1 };
            var seats = new Dictionary<int, AviatorSeat>();
            var logs = new Dictionary<int, RuleLog>();
            PilotProfile[] reds = { rookie, line, line, ace };
            for (int id = 1; id <= 8; id++)
            {
                bool blue = id <= 4;
                PilotProfile p = blue ? line : reds[id - 5];
                logs[id] = new RuleLog();
                seats[id] = new AviatorSeat(core, e, p, logs[id]);
                Vector3 at = blue ? new Vector3(60f * (id - 1), 3048f, -60f * (id - 1)) : new Vector3(-300f + 200f * (id - 5), 3500f, 20000f);
                core.Add(AircraftFactory.Build(id, e, L1, Start(e, at, blue ? 0f : 180f, 400f)), seats[id]);
                fight.Join(id, blue ? Side.Blue : Side.Red);
            }
            for (int id = 2; id <= 4; id++) seats[id].JoinOn(id, 1, id - 1);   // the flight's slots: left, right, outside right
            for (int id = 1; id <= 8; id++) seats[id].FightIn(id, fight);
            string crash = null;
            void Run(long untilTick)
            {
                while (core.Tick < untilTick && crash == null)
                {
                    core.Step();
                    fight.Score(core);
                    foreach (Aircraft a in core.Aircraft) if (a.State.Status == FlightStatus.Crashed) crash = $"aircraft {a.Id}: {a.CrashReason}";
                }
            }
            Run(300 * 50);
            // Red leaves the fight: blue's wingmen must be clear and back on the player's wing.
            for (int id = 5; id <= 8; id++) fight.Leave(id);
            // Up to five minutes to be back on the player's wing, from wherever the fight left them.
            for (long until = core.Tick + 300 * 50; core.Tick < until && crash == null && !Enumerable.Range(2, 3).All(i => seats[i].Mode == PilotMode.Fingertip);) Run(core.Tick + 50);
            for (int i = 1; i <= 4; i++)
            {
                AirframeState w = core.Find(i).State, lead = core.Find(1).State;
                Debug.Log(($"[Aviator 3d] 4v4 at the end: aircraft {i} {seats[i].Mode} {seats[i].Pursuit}, {(w.Position - lead.Position).magnitude:F0} m from 1, {w.Velocity.magnitude / Kt:F0} kt, {w.Position.y / Ft:F0} ft; throttle {core.Find(i).LastInputs.Throttle:F2} ab {core.Find(i).LastInputs.Afterburner} stick {core.Find(i).LastInputs.StickPitch:F2}/{core.Find(i).LastInputs.StickRoll:F2} aoa {w.AngleOfAttack * Mathf.Rad2Deg:F1} g {w.LoadFactor:F2} bank {FlightPathHands.BankDeg(w):F0}; asks {seats[i].Request.SpeedMps / Kt:F0} kt at {seats[i].Request.AltitudeM / Ft:F0} ft; rules after 300 s: " +
                          string.Join(" | ", logs[i].Firings.Where(f => f.Tick > 300 * 50).Select(f => $"{f.Tick / 50f:F0}s {f.Rule} ({f.Reason})").Take(6))));
            }
            Debug.Log($"[Aviator 3d] 4v4: red left at 300 s; blue's wingmen {(Enumerable.Range(2, 3).All(i => seats[i].Mode == PilotMode.Fingertip) ? "all back in fingertip" : "not all back")} at {core.Tick / 50f:F0} s");

            foreach (int id in seats.Keys)
                Debug.Log($"[Aviator 3d] 4v4 aircraft {id} ({fight.SideOf(id)}): {fight.SecondsInCone(id):F1} s in an enemy's cone; ends {seats[id].Mode}; rules: " +
                          string.Join(" | ", logs[id].Firings.Select(f => $"{f.Tick / 50f:F0}s {f.Rule}").Take(14)));
            Debug.Log($"[Aviator 3d] 4v4: blue {fight.SideSeconds(Side.Blue):F1} s, red {fight.SideSeconds(Side.Red):F1} s in the rear cone; {fight.Log.Count} cone events");
            Assert.IsNull(crash, "nobody crashed");
            for (int id = 5; id <= 8; id++)
                Assert.IsTrue(logs[id].Firings.Any(f => (f.Rule == "engage" && f.Reason.Contains("aircraft 1")) || (f.Rule == "vector" && f.Reason.Contains("aircraft 1"))), $"red {id} went for the player's jet");
            for (int id = 2; id <= 4; id++)
            {
                Assert.IsTrue(logs[id].Firings.Any(f => f.Rule == "engage" && f.Reason.Contains("aircraft") && !f.Reason.Contains("aircraft 1")), $"blue {id} engaged a red jet");
                Assert.IsTrue(logs[id].Firings.Any(f => f.Rule == "clear"), $"blue {id} went clear when red left");
                Assert.AreEqual(PilotMode.Fingertip, seats[id].Mode, $"blue {id} is back in fingertip on the player");
            }
            for (int a = 2; a <= 4; a++)
                for (int b = a + 1; b <= 4; b++)
                    Assert.Greater((core.Find(a).State.Position - core.Find(b).State.Position).magnitude, 30f, $"blue {a} and {b} fly their own slots");
            Assert.Greater(fight.SideSeconds(Side.Blue) + fight.SideSeconds(Side.Red), 0.0, "somebody got into somebody's rear cone");
        }

        [Test]
        public void TheRearCone_Is30DegreesOfTheTail_Inside1km()
        {
            AircraftProfile e = E();
            AirframeState target = Start(e, Vector3.zero, 0f, 300f);
            Assert.IsTrue(RearCone.Inside(Start(e, new Vector3(0f, 0f, -800f), 0f, 300f), target, 30f, 1000f), "dead astern at 800 m");
            Assert.IsTrue(RearCone.Inside(Start(e, new Vector3(-400f, 0f, -800f), 0f, 300f), target, 30f, 1000f), "26.6° off the tail");
            Assert.IsFalse(RearCone.Inside(Start(e, new Vector3(-600f, 0f, -800f), 0f, 300f), target, 30f, 1000f), "36.9° off the tail");
            Assert.IsFalse(RearCone.Inside(Start(e, new Vector3(0f, 0f, -1200f), 0f, 300f), target, 30f, 1000f), "too far");
            Assert.IsFalse(RearCone.Inside(Start(e, new Vector3(0f, 0f, 800f), 180f, 300f), target, 30f, 1000f), "head on");
            Assert.IsTrue(RearCone.Inside(Start(e, new Vector3(0f, 0f, -800f), 180f, 300f), target, 30f, 1000f), "astern but flying away still scores (A9 declined: position alone)");
        }

        [Test]
        public void TheEyes_SeeOnlyInsideTheFieldOfView_AsTheyWereTheDelayAgo()
        {
            AircraftProfile e = E();
            PilotProfile p = Pilot(AviatorTools.LinePilotPath);
            var eyes = new Perception(1, p, 0.02, truth: false);
            AirframeState me = Start(e, Vector3.zero, 0f, 300f);
            AirframeState ahead = Start(e, new Vector3(0f, 0f, 2000f), 0f, 300f), behind = Start(e, new Vector3(0f, 0f, -2000f), 0f, 300f);
            for (long t = 0; t < eyes.DelayTicks + 5; t++)
            {
                ahead.Position.z = 2000f + t;   // so the tick it belongs to can be read back
                eyes.Look(t, new[] { new AircraftRecord(1, me), new AircraftRecord(2, ahead), new AircraftRecord(3, behind) });
                if (t < eyes.DelayTicks) Assert.IsFalse(eyes.TryGet(2, out _), $"tick {t}: nothing has arrived yet");
            }
            long last = eyes.DelayTicks + 4;
            Assert.IsTrue(eyes.TryGet(2, out Contact c), "ahead: seen");
            Assert.AreEqual(last - eyes.DelayTicks, c.SeenTick, "as it was the delay ago");
            Assert.AreEqual(2000f + (last - eyes.DelayTicks), c.Seen.Position.z, 1e-3f);
            Assert.IsFalse(eyes.TryGet(3, out _), "dead astern: outside the field of view");
            Assert.AreEqual(35, eyes.DelayTicks, "the line pilot's 0.7 s at 50 Hz");
        }
    }
}
