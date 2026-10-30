using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Missiles;
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
    /// The game missiles in the pilots' hands (Air Wing W65, A16; SIX 2026-10-29: "everyone"): the learned pilot's pickle
    /// goes through its Pilot Inputs and its senses show its lock and a missile coming at it; the rule pilots launch with a
    /// lock on their own target inside their missile range, one at a time, and an armed fight with missiles repeats.
    /// </summary>
    public class MissileFightTests
    {
        const float Kt = (float)Units.KnotToMetrePerSecond;
        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);
        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        /// <summary>The heat-seeker's fight: radar missiles safe (A21: <c>SensorFightTests</c> is theirs).</summary>
        static PilotProfile Pilot(string path)
        {
            PilotProfile p = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(path).Bake();
            p.RadarMissileRangeM = 0f;
            return p;
        }

        static readonly LearnedPilotSettings Set = new LearnedPilotSettings
        {
            DecisionPeriodTicks = 5, MaxLifeS = 180f, TurnRangeDeg = 180f, ClimbRangeM = 1500f, MinKt = 150f, MaxKt = 600f, MinG = 2f,
        };

        static AirframeState Start(in AircraftProfile e, Vector3 at, float headingDeg, float kt) =>
            AircraftFactory.LevelStart(e, L1, at, headingDeg, kt * Kt, 0.6f * e.Airframe.InternalFuelKg, 0.7f);

        /// <summary>A crash that was not a shoot-down (a jet shot down falls and crashes by design).</summary>
        static string BadCrash(FlightCore core) =>
            core.Aircraft.Where(a => a.State.Status == FlightStatus.Crashed && a.KilledBy == 0).Select(a => $"aircraft {a.Id}: {a.CrashReason}").FirstOrDefault();

        [Test]
        public void TheLearnedPilot_PicklesThroughItsInputs_AndItsSensesShowTheLock_AndTheMissileComing()
        {
            AircraftProfile e = E();
            PilotProfile ace = Pilot(AviatorTools.AcePilotPath);
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: 9);
            var blue = new LearnedSeat(core, e, ace);
            var red = new LearnedSeat(core, e, ace);
            core.Add(AircraftFactory.Build(1, e, L1, Start(e, new Vector3(0f, 3048f, 0f), 0f, 400f)), blue);
            core.Add(AircraftFactory.Build(2, e, L1, Start(e, new Vector3(0f, 3048f, 4000f), 180f, 400f)), red);   // head on
            var fight = new Engagement();
            fight.Join(1, Side.Blue);
            fight.Join(2, Side.Red);
            var straight = new[] { 0f, 0f, 0.1f, 1f };
            var blueSenses = new LearnedSenses();
            var redSenses = new LearnedSenses();
            long launched = -1;
            bool sawLock = false, sawMissile = false;
            for (int t = 0; t < 8 * 50 && core.Missiles.Bursts == 0; t++)
            {
                if (core.Tick % 5 == 0)
                {
                    blue.Order(LearnedActions.Decode(straight, false, true, core.Find(1).State, Set, ace));   // the pickle held
                    red.Order(LearnedActions.Decode(straight, false, false, core.Find(2).State, Set, ace));
                }
                core.Step();
                if (launched < 0 && core.Missiles.Launched > 0) launched = core.Tick - 1;
                float[] b = blueSenses.Write(core, 1, blue.Eyes, fight, ace, false);
                int own = LearnedSenses.SizeVersion2 - LearnedSenses.OwnMissileSize - LearnedSenses.MissileSlots * LearnedSenses.MissileSize;
                if (b[own + 2] == 1f && b[own + 3] == 1f) sawLock = true;   // locked, on an enemy
                float[] r = redSenses.Write(core, 2, red.Eyes, fight, ace, false);
                int slot = own + LearnedSenses.OwnMissileSize;
                if (r[slot] == 1f && r[slot + 7] == 1f) sawMissile = true;   // a missile it sees, aimed at it
            }
            Debug.Log($"[Learned missile] launched at tick {launched}; {string.Join("; ", core.Missiles.Events)}");
            Assert.Greater(launched, 0, "a launch");
            Assert.IsTrue(core.Inputs.All.Where(x => x.AircraftId == 1 && x.Tick == launched).Single().Inputs.Pickle, "through the Pilot Inputs");
            Assert.IsTrue(sawLock, "its senses showed the lock, on an enemy");
            Assert.IsTrue(sawMissile, "the target's senses showed the missile coming at it");
            Assert.AreEqual(2, core.Find(1).State.Missiles + core.Missiles.Launched, "one gone from the pylons per launch (only blue pickled; two heat-seekers, J18)");
        }

        [Test]
        public void TheRulePilots_LaunchOnlyAtTheirTarget_OneAtATime_InsideTheirRange()
        {
            AircraftProfile e = E();
            PilotProfile ace = Pilot(AviatorTools.AcePilotPath), rookie = Pilot(AviatorTools.RookiePilotPath);
            Assert.AreEqual(2500f, ace.MissileRangeM, "the ace launches close");
            Assert.AreEqual(5000f, rookie.MissileRangeM, "the rookie as soon as it locks");
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: 33);
            var fight = new Engagement();
            var b = new AviatorSeat(core, e, ace, new RuleLog());
            var r = new AviatorSeat(core, e, rookie, new RuleLog());
            core.Add(AircraftFactory.Build(1, e, L1, Start(e, new Vector3(0f, 3048f, -4000f), 0f, 400f)), b);
            core.Add(AircraftFactory.Build(2, e, L1, Start(e, new Vector3(300f, 3048f, 4000f), 180f, 400f)), r);
            fight.Join(1, Side.Blue);
            fight.Join(2, Side.Red);
            b.FightIn(1, fight);
            r.FightIn(2, fight);
            float firstRange1 = -1f, firstRange2 = -1f;
            for (int t = 0; t < 60 * 50 && core.Kills.Count == 0; t++)
            {
                int before = core.Missiles.Launched;
                core.Step();
                fight.Score(core);
                Assert.IsNull(BadCrash(core));
                for (int k = before; k < core.Missiles.Launched; k++)
                {
                    MissileEvent launch = core.Missiles.Events.Last(ev => ev.Kind == MissileEventKind.Launched);
                    float range = (core.Find(launch.TargetId).State.Position - launch.Point).magnitude;
                    if (launch.ShooterId == 1 && firstRange1 < 0f) firstRange1 = range;
                    if (launch.ShooterId == 2 && firstRange2 < 0f) firstRange2 = range;
                    Assert.AreNotEqual(launch.ShooterId, launch.TargetId);
                    Assert.AreEqual(launch.ShooterId == 1 ? 2 : 1, launch.TargetId, "at its own target, the enemy");
                }
                // One at a time: never two of a pilot's missiles in flight at once at one target, unless the first was lost.
                foreach (int shooter in new[] { 1, 2 })
                    Assert.LessOrEqual(core.Missiles.Live.Count(m => m.ShooterId == shooter && !m.Lost), 1, $"aircraft {shooter}: one at a time");
            }
            Debug.Log($"[Aviator missiles] ace v rookie: {core.Missiles.Launched} launched, {core.Missiles.Bursts} bursts, kills {string.Join(" | ", core.Kills)}; first launches at {firstRange1:F0} m (ace) and {firstRange2:F0} m (rookie)");
            Assert.Greater(core.Missiles.Launched, 0, "missiles hot, the pilots launch");
            // The pilot judges the range on what it sees (late by its reaction delay, carried on); 150 m allows for a turn in that time.
            if (firstRange1 > 0f) Assert.LessOrEqual(firstRange1, 2500f + 150f, "the ace inside its 2.5 km");
            if (firstRange2 > 0f) Assert.LessOrEqual(firstRange2, 5000f + 150f, "the rookie inside its 5 km");
        }

        [Test]
        public void AFourVersusFour_MissilesHot_RepeatsKillForKill_AndNoOneCrashesUnshot()
        {
            AircraftProfile e = E();
            PilotProfile rookie = Pilot(AviatorTools.RookiePilotPath), line = Pilot(AviatorTools.LinePilotPath), ace = Pilot(AviatorTools.AcePilotPath);
            (string kills, string missiles, FixLog fixes, string crash) Run()
            {
                var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: 4242);
                var fight = new Engagement();
                PilotProfile[] reds = { rookie, line, line, ace };
                var seats = new AviatorSeat[9];
                for (int id = 1; id <= 8; id++)
                {
                    bool isBlue = id <= 4;
                    seats[id] = new AviatorSeat(core, e, isBlue ? line : reds[id - 5], new RuleLog());
                    Vector3 at = isBlue ? new Vector3(300f * (id - 1), 3048f, 0f) : new Vector3(300f * (id - 5), 3500f, 12000f);
                    core.Add(AircraftFactory.Build(id, e, L1, Start(e, at, isBlue ? 0f : 180f, 400f)), seats[id]);
                    fight.Join(id, isBlue ? Side.Blue : Side.Red);
                }
                for (int id = 1; id <= 8; id++) seats[id].FightIn(id, fight);
                string crash = null;
                while (core.Tick < 120 * 50 && crash == null) { core.Step(); fight.Score(core); crash = BadCrash(core); }
                return (string.Join(" | ", core.Kills), string.Join(" | ", core.Missiles.Events.Where(m => m.Kind != MissileEventKind.Launched)), core.Fixes, crash);
            }
            var a = Run();
            var b = Run();
            Debug.Log($"[Aviator missiles] 4 v 4, 120 s: kills {a.kills}; missiles {a.missiles}");
            Assert.IsNull(a.crash, "no crash but shoot-downs");
            Assert.AreEqual(-1, a.fixes.FirstDivergence(b.fixes), "the same Fix on every tick");
            Assert.AreEqual(a.kills, b.kills);
            Assert.AreEqual(a.missiles, b.missiles);
        }
    }
}
