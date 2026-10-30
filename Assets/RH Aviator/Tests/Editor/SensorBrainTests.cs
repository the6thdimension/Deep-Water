using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Missiles;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Radar;
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
    /// The learned pilot's sensors, SENSORS-CONCEPT slice 7 (J11, J13, J20; RH Aviator A22): senses version 3 show the
    /// radar, its tracks and the warning receiver; the six choices carry the radar, IFF, decoys and the radar pickle; and
    /// an ace riding along as a teacher, its every decision turned into actions and flown back through them (exactly what
    /// a recorded demonstration asks a brain to copy), still fights as the ace does: it shoots radar missiles at enemies,
    /// never at friends.
    /// </summary>
    public class SensorBrainTests
    {
        const float Kt = (float)Units.KnotToMetrePerSecond;
        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);
        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        static PilotProfile Pilot(string path) => AssetDatabase.LoadAssetAtPath<PilotProfileSO>(path).Bake();
        static AirframeState Start(in AircraftProfile e, Vector3 at, float headingDeg) =>
            AircraftFactory.LevelStart(e, L1, at, headingDeg, 400f * Kt, 0.6f * e.Airframe.InternalFuelKg, 0.7f);

        /// <summary>SIX's picks (M23, 2026-10-29), as the training builder writes them.</summary>
        static readonly LearnedPilotSettings Set = new LearnedPilotSettings
        {
            DecisionPeriodTicks = 5, MaxLifeS = 180f, TurnRangeDeg = 180f, ClimbRangeM = 1500f, MinKt = 150f, MaxKt = 600f, MinG = 2f,
        };

        [Test]
        public void TheSixChoices_SayWhatTheTeacherDid()
        {
            int[] slots = { 0, 17, 23, 0 };
            int[] d = new int[6];
            var held = new PilotInputs { Trigger = true, RadarPickle = true, Switches = PilotSwitches.Interrogate | PilotSwitches.Flare };
            LearnedActions.FromInputs(held, RadarCommand.Lock, 23, slots, d);
            Assert.AreEqual(LearnedActions.RadarLockSlot0 + 2, d[LearnedActions.RadarBranch], "a lock names the slot its track is in");
            var c = new LearnedCommand();
            LearnedActions.DecodeSensors(d, slots, ref c);
            Assert.AreEqual(RadarCommand.Lock, c.Radar);
            Assert.AreEqual(23, c.RadarTrack);
            Assert.IsTrue(c.Interrogate && c.Flare && !c.Chaff && c.RadarPickle);

            LearnedActions.FromInputs(default, RadarCommand.Lock, 99, slots, d);
            Assert.AreEqual(LearnedActions.RadarLeave, d[LearnedActions.RadarBranch], "a track in no slot cannot be named");
            d[LearnedActions.RadarBranch] = LearnedActions.RadarLockSlot0;   // slot 0 is empty
            LearnedActions.DecodeSensors(d, slots, ref c);
            Assert.AreEqual(RadarCommand.None, c.Radar, "a lock on an empty slot is nothing");
            LearnedActions.DecodeSensors(new[] { 1, 1 }, slots, ref c);
            Assert.AreEqual(RadarCommand.None, c.Radar, "an older brain's two branches leave the sensors alone");
            Assert.AreEqual(new[] { 2, 2, 7, 2, 4, 2 }, LearnedActions.Branches());
        }

        [Test]
        public void FromRequest_UndoesDecode()
        {
            AircraftProfile e = E();
            PilotProfile ace = Pilot(AviatorTools.AcePilotPath);
            AirframeState s = Start(e, new Vector3(0f, 3000f, 0f), 30f);
            float[] back = new float[4];
            foreach (float[] a in new[] { new[] { 0.3f, -0.4f, 0.2f, 0.9f }, new[] { -0.9f, 0.7f, -0.5f, -1f }, new[] { 0f, 0f, 0f, 0f } })
            {
                LearnedCommand c = LearnedActions.Decode(a, false, false, s, Set, ace);
                LearnedActions.FromRequest(c.Request, s, Set, ace, back);
                for (int i = 0; i < 4; i++) Assert.AreEqual(a[i], back[i], 1e-3f, $"action {i}");
            }
        }

        sealed class Fight
        {
            public FlightCore Core;
            public Engagement Sides;
            public LearnedSeat[] Seats;
            public LearnedSenses[] Senses;
            public int RadarLaunches, AtFriends, AtEnemies, TrackSlotsSeen, WarningsSeen, Lessons;
        }

        /// <summary>
        /// <paramref name="n"/> a side, every jet a learned seat with an ace riding along; each decision the teacher's lesson
        /// becomes actions (as the agent's heuristic makes them) and is flown back through Decode, as a brain's would be.
        /// </summary>
        static Fight Run(ulong seed, int n, float apartM, float seconds)
        {
            AircraftProfile e = E();
            PilotProfile ace = Pilot(AviatorTools.AcePilotPath);
            var f = new Fight { Core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: seed), Sides = new Engagement() };
            f.Seats = new LearnedSeat[2 * n + 1];
            f.Senses = new LearnedSenses[2 * n + 1];
            for (int id = 1; id <= 2 * n; id++)
            {
                bool blue = id <= n;
                int k = blue ? id - 1 : id - 1 - n;
                f.Seats[id] = new LearnedSeat(f.Core, e, ace);
                f.Senses[id] = new LearnedSenses();
                f.Core.Add(AircraftFactory.Build(id, e, L1, Start(e, blue ? new Vector3(600f * k, 3048f, 0f) : new Vector3(600f * k, 3300f, apartM), blue ? 0f : 180f)), f.Seats[id]);
                Side side = blue ? Side.Blue : Side.Red;
                f.Sides.Join(id, side);
                f.Core.SetIffCode(id, (int)side);
            }
            for (int id = 1; id <= 2 * n; id++)
            {
                var teacher = new AviatorSeat(f.Core, e, ace, new RuleLog());
                teacher.FightIn(id, f.Sides);
                f.Seats[id].RideAlong(teacher);
            }
            float[] a = new float[4];
            int[] d = new int[6];
            long end = (long)(seconds * 50);
            while (f.Core.Tick < end)
            {
                if (f.Core.Tick > 0 && f.Core.Tick % Set.DecisionPeriodTicks == 0)
                    for (int id = 1; id <= 2 * n; id++)
                    {
                        LearnedSeat seat = f.Seats[id];
                        AirframeState me = f.Core.Find(id).State;
                        if (me.Status != FlightStatus.Flying || seat.Eyes == null) continue;
                        LearnedSenses senses = f.Senses[id];
                        float[] v = senses.Write(f.Core, id, seat.Eyes, f.Sides, ace, false);
                        if (v[LearnedSenses.SizeVersion2 + LearnedSenses.OwnSensorSize] == 1f) f.TrackSlotsSeen++;
                        if (v[LearnedSenses.Size - LearnedSenses.WarningSlots * LearnedSenses.WarningSize] == 1f) f.WarningsSeen++;
                        FlightPathRequest asked = seat.TakeLesson(out PilotInputs held, out RadarCommand radar, out int track);
                        LearnedActions.FromRequest(asked, me, Set, ace, a);
                        LearnedActions.FromInputs(held, radar, track, senses.TrackSlotIds, d);
                        LearnedCommand c = LearnedActions.Decode(a, d[0] == 1, d[1] == 1, me, Set, ace);
                        LearnedActions.DecodeSensors(d, senses.TrackSlotIds, ref c);
                        seat.Order(c);
                        f.Lessons++;
                    }
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
                    if (f.Sides.SideOf(ev[i].ShooterId) == f.Sides.SideOf(ev[i].TargetId)) f.AtFriends++; else f.AtEnemies++;
                }
            }
            return f;
        }

        [Test]
        public void TheAce_CopiedThroughTheActions_ShootsEnemiesNotFriends()
        {
            int launches = 0, friends = 0, enemies = 0, tracks = 0, warnings = 0, lessons = 0;
            for (ulong seed = 1; seed <= 2; seed++)
            {
                Fight f = Run(seed, 2, 30000f, 120f);
                launches += f.RadarLaunches; friends += f.AtFriends; enemies += f.AtEnemies;
                tracks += f.TrackSlotsSeen; warnings += f.WarningsSeen; lessons += f.Lessons;
            }
            Debug.Log($"[SENSORS-BRAIN] the ace copied through the actions, 2 v 2 from 30 km, two seeds of 120 s: {lessons} lessons; " +
                      $"{launches} radar missiles, {enemies} at enemies, {friends} at friends; senses showed a track in {tracks} decisions, a warning in {warnings}");
            Assert.Greater(tracks, 0, "the senses show the radar's tracks");
            Assert.Greater(launches, 0, "the copied ace locks and launches through the six choices");
            Assert.AreEqual(0, friends, "never at a friend");
            Assert.Greater(warnings, 0, "the senses show the warning receiver");
        }

        [Test]
        public void SensesVersion3_AreFiniteAndBounded_AndVersion2KeepsItsPlaces()
        {
            Fight f = Run(3, 1, 20000f, 40f);
            float[] v = f.Senses[1].Values;
            Assert.AreEqual(LearnedSenses.Size, v.Length);
            Assert.IsTrue(v.All(x => !float.IsNaN(x) && !float.IsInfinity(x) && Mathf.Abs(x) <= 5f), "finite and bounded");
            Assert.AreEqual(LearnedSenses.SizeVersion2, LearnedSenses.OwnSize + LearnedSenses.EnemySlots * LearnedSenses.EnemySize
                            + LearnedSenses.FriendSlots * LearnedSenses.FriendSize + LearnedSenses.PipperSize + LearnedSenses.OwnMissileSize
                            + LearnedSenses.MissileSlots * LearnedSenses.MissileSize);
        }
    }
}
