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
    /// The armed fight (Gunnery K4–K6, SIX 2026-10-27): the AI shoots on its trigger discipline, kills score first and
    /// rear-cone seconds beside, a jet shot down leaves the fight. Every part L1, over a flat sea.
    /// </summary>
    public class GunFightTests
    {
        const float Kt = (float)Units.KnotToMetrePerSecond, Ft = 0.3048f;
        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);

        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        /// <summary>The pilot, missiles safe: these are the gun fights (Gunnery K6) and keep their criteria (W65 added missiles later).</summary>
        static PilotProfile Pilot(string path)
        {
            PilotProfile p = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(path).Bake();
            p.MissileRangeM = 0f;
            p.RadarMissileRangeM = 0f;   // radar missiles safe too (A21)
            return p;
        }

        static AirframeState Start(in AircraftProfile e, Vector3 at, float headingDeg, float kt) =>
            AircraftFactory.LevelStart(e, L1, at, headingDeg, kt * Kt, 0.6f * e.Airframe.InternalFuelKg, 0.7f);

        /// <summary>A crash that was not a shoot-down (a jet shot down falls and crashes by design).</summary>
        static string BadCrash(FlightCore core) =>
            core.Aircraft.Where(a => a.State.Status == FlightStatus.Crashed && a.KilledBy == 0).Select(a => $"aircraft {a.Id}: {a.CrashReason}").FirstOrDefault();

        [Test]
        public void EachSkill_ShootsDownAJetFlyingStraightAhead_TheAceOnFewestRounds()
        {
            AircraftProfile e = E();
            var rounds = new Dictionary<string, long>();
            foreach (string path in new[] { AviatorTools.RookiePilotPath, AviatorTools.LinePilotPath, AviatorTools.AcePilotPath })
            {
                var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: 11);
                var fight = new Engagement();
                var seat = new AviatorSeat(core, e, Pilot(path), new RuleLog());
                core.Add(AircraftFactory.Build(1, e, L1, Start(e, new Vector3(0f, 3048f, 0f), 0f, 420f)), seat);
                core.Add(AircraftFactory.Build(2, e, L1, Start(e, new Vector3(0f, 3048f, 1500f), 0f, 330f)), new HoldSeat(new PilotInputs { Throttle = 0.55f }));
                fight.Join(1, Side.Blue);
                fight.Join(2, Side.Red);
                seat.FightIn(1, fight);
                while (core.Kills.Count == 0 && core.Tick < 90 * 50) { core.Step(); fight.Score(core); }
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                Debug.Log($"[Aviator guns] {name} v a straight target: {(core.Kills.Count > 0 ? $"killed at {core.Kills[0].Tick / 50f:F1} s" : "no kill")}; {core.Rounds.RoundsFired} rounds fired, {core.Rounds.RoundsHit} hit");
                Assert.AreEqual(1, core.Kills.Count, $"{name} shoots it down inside 90 s");
                Assert.AreEqual(1, core.Kills[0].ShooterId);
                Assert.AreEqual(1, fight.SideKills(Side.Blue));
                Assert.IsFalse(fight.Fighters.Contains(2), "a jet shot down leaves the fight");
                Assert.IsNull(BadCrash(core));
                rounds[name] = core.Rounds.RoundsFired;
            }
            Assert.LessOrEqual(rounds["Pilot_Ace"], rounds["Pilot_Rookie"], "the ace waits for the shot; the rookie sprays");
        }

        sealed class Merge
        {
            public int BlueKills, RedKills;
            public double BlueCone, RedCone;
            public long BlueRounds, RedRounds;
            public string Crash;
        }

        /// <summary>A neutral merge, guns hot, 180 s or until someone is shot down and the other is clear.</summary>
        static Merge Fly(int seed, in AircraftProfile e, in PilotProfile blue, in PilotProfile red)
        {
            var rng = new DeterministicRng((ulong)(5000 + seed));
            float height = (9000f + 2000f * rng.NextFloat01()) * Ft;
            float offset = (rng.NextFloat01() < 0.5f ? -1f : 1f) * (200f + 300f * rng.NextFloat01());
            float kt = 380f + 40f * rng.NextFloat01();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: (ulong)(900 + seed));
            var fight = new Engagement();
            var b = new AviatorSeat(core, e, blue, new RuleLog());
            var r = new AviatorSeat(core, e, red, new RuleLog());
            core.Add(AircraftFactory.Build(1, e, L1, Start(e, new Vector3(0f, height, -3000f), 0f, kt)), b);
            core.Add(AircraftFactory.Build(2, e, L1, Start(e, new Vector3(offset, height, 3000f), 180f, kt)), r);
            fight.Join(1, Side.Blue);
            fight.Join(2, Side.Red);
            b.FightIn(1, fight);
            r.FightIn(2, fight);
            var m = new Merge();
            while (core.Tick < 180 * 50 && core.Kills.Count == 0)
            {
                core.Step();
                fight.Score(core);
                m.Crash = BadCrash(core);
                if (m.Crash != null) break;
            }
            m.BlueKills = fight.SideKills(Side.Blue);
            m.RedKills = fight.SideKills(Side.Red);
            m.BlueCone = fight.SecondsInCone(1);
            m.RedCone = fight.SecondsInCone(2);
            m.BlueRounds = 412 - core.Find(1).State.Rounds;
            m.RedRounds = 412 - core.Find(2).State.Rounds;
            return m;
        }

        [Test]
        public void GunsHot_TheAce_BeatsTheRookie_ByKillsThenCone_InAtLeast15Of20_AndNobodyCrashes()
        {
            AircraftProfile e = E();
            PilotProfile ace = Pilot(AviatorTools.AcePilotPath), rookie = Pilot(AviatorTools.RookiePilotPath);
            List<Merge> all = Enumerable.Range(0, 20).Select(i => Fly(i, e, ace, rookie)).ToList();
            int wins = all.Count(m => m.BlueKills > m.RedKills || (m.BlueKills == m.RedKills && m.BlueCone > m.RedCone));
            Debug.Log($"[Aviator guns] ace v rookie, 20 merges: ace ahead in {wins}; kills ace {all.Sum(m => m.BlueKills)}, rookie {all.Sum(m => m.RedKills)}; " +
                      $"rounds ace {all.Sum(m => m.BlueRounds)}, rookie {all.Sum(m => m.RedRounds)}; cone ace {all.Sum(m => m.BlueCone):F0} s, rookie {all.Sum(m => m.RedCone):F0} s");
            foreach (Merge m in all) Assert.IsNull(m.Crash);
            Assert.GreaterOrEqual(wins, 15);
        }

        [Test]
        public void FourVersusFour_GunsHot_TheyShoot_ItReplaysKillForKill_AndNoOneCrashesUnshot()
        {
            AircraftProfile e = E();
            PilotProfile rookie = Pilot(AviatorTools.RookiePilotPath), line = Pilot(AviatorTools.LinePilotPath), ace = Pilot(AviatorTools.AcePilotPath);
            (List<KillRecord> kills, FixLog fixes, string crash, int blue, int red, long rounds) Run()
            {
                var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: 4242);
                var fight = new Engagement { Hunted = () => 1 };
                PilotProfile[] reds = { rookie, line, line, ace };
                var seats = new Dictionary<int, AviatorSeat>();
                for (int id = 1; id <= 8; id++)
                {
                    bool isBlue = id <= 4;
                    seats[id] = new AviatorSeat(core, e, isBlue ? line : reds[id - 5], new RuleLog());
                    Vector3 at = isBlue ? new Vector3(60f * (id - 1), 3048f, -60f * (id - 1)) : new Vector3(-300f + 200f * (id - 5), 3500f, 20000f);
                    core.Add(AircraftFactory.Build(id, e, L1, Start(e, at, isBlue ? 0f : 180f, 400f)), seats[id]);
                    fight.Join(id, isBlue ? Side.Blue : Side.Red);
                }
                for (int id = 2; id <= 4; id++) seats[id].JoinOn(id, 1, id - 1);
                for (int id = 1; id <= 8; id++) seats[id].FightIn(id, fight);
                string crash = null;
                while (core.Tick < 240 * 50 && crash == null) { core.Step(); fight.Score(core); crash = BadCrash(core); }
                return (core.Kills.ToList(), core.Fixes, crash, fight.SideKills(Side.Blue), fight.SideKills(Side.Red), core.Rounds.RoundsFired);
            }
            var a = Run();
            var b = Run();
            Debug.Log($"[Aviator guns] 4 v 4, 240 s: {a.rounds} rounds fired; kills blue {a.blue}, red {a.red}; " + string.Join(" | ", a.kills.Select(k => k.ToString())));
            Assert.IsNull(a.crash, "no crash but shoot-downs");
            // Kills are not required: near-equal pilots stalemate in a level fight (METHODOLOGY C44), slice 3f's job.
            Assert.Greater(a.rounds, 0, "guns hot, the pilots shoot");
            Assert.AreEqual(-1, a.fixes.FirstDivergence(b.fixes), "the same fight twice: the same Fix on every tick");
            CollectionAssert.AreEqual(a.kills.Select(k => k.ToString()).ToList(), b.kills.Select(k => k.ToString()).ToList());
        }
    }
}
