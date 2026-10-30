using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Missiles;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Editor;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Tests
{
    /// <summary>
    /// The rule pilots use the jet's sensors, SENSORS-CONCEPT slice 6 (J9; RH Aviator A21), in headless fights: the ace
    /// asks before it shoots an unknown, they answer a missile warning with chaff or flares, and no rule pilot launches a
    /// radar missile at a friend.
    /// </summary>
    public class SensorFightTests
    {
        const float Kt = (float)Units.KnotToMetrePerSecond;
        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);
        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        static PilotProfile Pilot(string path) => AssetDatabase.LoadAssetAtPath<PilotProfileSO>(path).Bake();
        static AirframeState Start(in AircraftProfile e, Vector3 at, float headingDeg) =>
            AircraftFactory.LevelStart(e, L1, at, headingDeg, 400f * Kt, 0.6f * e.Airframe.InternalFuelKg, 0.7f);

        sealed class Fight
        {
            public FlightCore Core;
            public Engagement Sides;
            public AviatorSeat[] Seats;   // by aircraft id (0 unused)
            public RuleLog[] Logs;
            public int FriendlyRadarLaunches, RadarLaunches;
        }

        /// <summary>Blue (ids 1 …) heading north at the origin, red (the rest) heading south <paramref name="apartM"/> away; one IFF code per side.</summary>
        static Fight Build(ulong seed, float apartM, PilotProfile[] blue, PilotProfile[] red)
        {
            AircraftProfile e = E();
            var f = new Fight { Core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: seed), Sides = new Engagement() };
            int n = blue.Length + red.Length;
            f.Seats = new AviatorSeat[n + 1];
            f.Logs = new RuleLog[n + 1];
            for (int id = 1; id <= n; id++)
            {
                bool isBlue = id <= blue.Length;
                int k = isBlue ? id - 1 : id - 1 - blue.Length;
                f.Logs[id] = new RuleLog();
                f.Seats[id] = new AviatorSeat(f.Core, e, isBlue ? blue[k] : red[k], f.Logs[id]);
                Vector3 at = isBlue ? new Vector3(600f * k, 3048f, 0f) : new Vector3(600f * k, 3300f, apartM);
                f.Core.Add(AircraftFactory.Build(id, e, L1, Start(e, at, isBlue ? 0f : 180f)), f.Seats[id]);
                Side side = isBlue ? Side.Blue : Side.Red;
                f.Sides.Join(id, side);
                f.Core.SetIffCode(id, (int)side);
            }
            for (int id = 1; id <= n; id++) f.Seats[id].FightIn(id, f.Sides);
            return f;
        }

        static void Run(Fight f, float seconds)
        {
            long end = f.Core.Tick + (long)(seconds * 50);
            while (f.Core.Tick < end)
            {
                int before = f.Core.Missiles.Events.Count;
                f.Core.Step();
                f.Sides.Score(f.Core);
                var ev = f.Core.Missiles.Events;
                for (int i = before; i < ev.Count; i++)
                {
                    if (ev[i].Kind != MissileEventKind.Launched) continue;
                    MissileInFlight m = f.Core.Missiles.Live.FirstOrDefault(x => x.Id == ev[i].MissileId);
                    if (!m.Radar) continue;
                    f.RadarLaunches++;
                    if (f.Sides.SideOf(ev[i].ShooterId) == f.Sides.SideOf(ev[i].TargetId)) f.FriendlyRadarLaunches++;
                }
            }
        }

        [Test]
        public void TheAce_AsksBeforeItShootsAnUnknown()
        {
            PilotProfile ace = Pilot(AviatorTools.AcePilotPath), line = Pilot(AviatorTools.LinePilotPath);
            Assert.AreEqual(0f, ace.IffForgetChance, "the ace always asks");
            Fight f = Build(7, 30000f, new[] { ace, ace }, new[] { line, line });
            Run(f, 90f);
            SensorHabits a1 = f.Seats[1].Habits, a2 = f.Seats[2].Habits;
            int asked = a1.LaunchesAfterAsking + a2.LaunchesAfterAsking, launched = a1.RadarLaunches + a2.RadarLaunches;
            var aceLog = f.Logs[1].Firings.Concat(f.Logs[2].Firings).ToList();
            Debug.Log($"[SENSORS] two aces v two line pilots from 30 km, 90 s: the aces launched {launched} radar missiles, {asked} after asking, {launched - asked} on sight; " +
                      $"IFF challenges {f.Core.RadarOf(1).Interrogations + f.Core.RadarOf(2).Interrogations}, answers {f.Core.RadarOf(1).Answers + f.Core.RadarOf(2).Answers}; first: {aceLog.FirstOrDefault(r => r.Rule == "radar missile")}");
            Assert.Greater(launched, 0, "the aces shot");
            Assert.IsFalse(aceLog.Any(r => r.Rule == "iff: forgot"), "the ace never forgets to ask");
            Assert.AreEqual(launched, a1.LaunchesAfterAsking + a1.LaunchesOnSight + a2.LaunchesAfterAsking + a2.LaunchesOnSight, "every launch on a track it asked about or saw");
            Assert.Greater(asked, 0, "beyond sight, it asked first");
            Assert.AreEqual(0, f.FriendlyRadarLaunches);
        }

        [Test]
        public void AMissileWarning_IsAnsweredWithChaff()
        {
            PilotProfile ace = Pilot(AviatorTools.AcePilotPath);
            Fight f = Build(11, 18000f, new[] { ace }, new[] { ace });
            Run(f, 60f);
            SensorHabits blue = f.Seats[1].Habits, red = f.Seats[2].Habits;
            Debug.Log($"[SENSORS] ace v ace from 18 km, 60 s: {f.RadarLaunches} radar missiles; warnings blue {blue.Warnings} ({blue.WarningsAnswered} answered), red {red.Warnings} ({red.WarningsAnswered}); chaff {f.Core.Missiles.ChaffDropped}, flares {f.Core.Missiles.FlaresDropped}, decoyed {f.Core.Missiles.Decoyed}; kills {string.Join(" | ", f.Core.Kills)}");
            Assert.Greater(f.RadarLaunches, 0, "set-up: a radar missile in the air");
            Assert.Greater(blue.Warnings + red.Warnings, 0, "a warning came");
            Assert.AreEqual(blue.Warnings + red.Warnings, blue.WarningsAnswered + red.WarningsAnswered, "the ace answers every one");
            Assert.Greater(f.Core.Missiles.ChaffDropped + f.Core.Missiles.FlaresDropped, 0);
        }

        [Test]
        public void TheRulePilots_NeverLaunchARadarMissileAtAFriend_AndAFightRepeats()
        {
            PilotProfile rookie = Pilot(AviatorTools.RookiePilotPath), line = Pilot(AviatorTools.LinePilotPath), ace = Pilot(AviatorTools.AcePilotPath);
            PilotProfile[] side = { rookie, line, line, ace };
            int total = 0, friendly = 0, warnings = 0, answered = 0, forgot = 0;
            string first = null;
            for (ulong seed = 1; seed <= 3; seed++)
            {
                Fight f = Build(seed, 30000f, side, side);
                Run(f, 150f);
                total += f.RadarLaunches;
                friendly += f.FriendlyRadarLaunches;
                for (int id = 1; id <= 8; id++)
                {
                    warnings += f.Seats[id].Habits.Warnings;
                    answered += f.Seats[id].Habits.WarningsAnswered;
                    forgot += f.Logs[id].Firings.Count(r => r.Rule == "iff: forgot");
                }
                string story = string.Join("\n", f.Core.Missiles.Events) + string.Join("\n", Enumerable.Range(1, 8).SelectMany(i => f.Logs[i].Firings));
                if (seed == 1) first = story;
            }
            Fight again = Build(1, 30000f, side, side);
            Run(again, 150f);
            string repeat = string.Join("\n", again.Core.Missiles.Events) + string.Join("\n", Enumerable.Range(1, 8).SelectMany(i => again.Logs[i].Firings));
            Debug.Log($"[SENSORS] 4 v 4 (rookie, line, line, ace a side) from 30 km, three seeds of 150 s: {total} radar missiles, {friendly} at a friend; {warnings} warnings, {answered} answered; {forgot} contacts the pilots forgot to ask about");
            Assert.Greater(total, 0, "the rule pilots launched radar missiles");
            Assert.AreEqual(0, friendly, "none at a friend");
            Assert.AreEqual(first, repeat, "the same seed, the same fight: missiles and every rule");
        }
    }
}
