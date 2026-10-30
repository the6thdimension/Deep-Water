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
using DeepWater.Gunnery.Guns;

namespace DeepWater.Aviator.Tests
{
    /// <summary>
    /// The learned pilot's pure parts (TRAINING-CONCEPT M1, M5, M7; A13–A15): its seat flies the jet only through Pilot
    /// Inputs, its actions mean what they say, it sees only through the ace's eyes, and the referee pays it what M24
    /// lists. No ML-Agents here: the policy is stood in for by fixed action sequences.
    /// </summary>
    public class LearnedPilotTests
    {
        const float Kt = (float)Units.KnotToMetrePerSecond;
        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);
        static readonly FidelitySheet L0 = new FidelitySheet(FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, 4, 1442);
        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        static PilotProfile Ace() => AssetDatabase.LoadAssetAtPath<PilotProfileSO>(AviatorTools.AcePilotPath).Bake();

        /// <summary>SIX's picks (M23, 2026-10-29), as the training builder writes them.</summary>
        static readonly LearnedPilotSettings Set = new LearnedPilotSettings
        {
            DecisionPeriodTicks = 5, MaxLifeS = 180f, TurnRangeDeg = 180f, ClimbRangeM = 1500f, MinKt = 150f, MaxKt = 600f, MinG = 2f,
        };

        /// <summary>SIX's picks (M24, 2026-10-29).</summary>
        static readonly RewardWeights Pay = new RewardWeights
        {
            Kill = 1f, ShotDown = -1f, FriendlyKill = -1f, Crash = -1f, Collision = -1f, LeftArena = -1f,
            HitLanded = 0.05f, HitTaken = -0.05f, FriendlyHit = -0.05f, PointedConePerS = 0.02f, ConeDeg = 30f, ConeRangeM = 1000f, SquadronShare = 0.25f,
        };

        static AirframeState Level(in AircraftProfile e, in FidelitySheet sheet, Vector3 at, float headingDeg, float kt = 400f) =>
            AircraftFactory.LevelStart(e, sheet, at, headingDeg, kt * Kt, 0.6f * e.Airframe.InternalFuelKg, 0.7f);

        /// <summary>A seat that holds its throttle and stick and, between two ticks, the trigger.</summary>
        sealed class TriggerSeat : ISeat
        {
            public long FireFrom = long.MaxValue, FireUntil = long.MaxValue;
            public float Pitch;
            public SeatKind Kind => SeatKind.Scenario;
            public PilotInputs Issue(long tick, int aircraftId, in AirframeState state) =>
                new PilotInputs { Throttle = 0.6f, StickPitch = Pitch, Trigger = tick >= FireFrom && tick < FireUntil };
        }

        /// <summary>Fly <paramref name="seat"/> with a fixed action every decision for <paramref name="seconds"/>.</summary>
        static void Fly(FlightCore core, LearnedSeat seat, int id, float[] actions, bool trigger, float seconds)
        {
            PilotProfile ace = seat.Pilot;
            for (int i = 0; i < seconds * 50; i++)
            {
                if (core.Tick % Set.DecisionPeriodTicks == 0) seat.Order(LearnedActions.Decode(actions, trigger, false, core.Find(id).State, Set, ace));
                core.Step();
            }
        }

        [Test]
        public void TheLearnedSeat_FliesTheJet_OnlyThroughPilotInputs_SoItsInputLogReplaysTheFlightExactly()
        {
            AircraftProfile e = E();
            PilotProfile ace = Ace();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: 5);
            var seat = new LearnedSeat(core, e, ace);
            core.Add(AircraftFactory.Build(1, e, L1, Level(e, L1, new Vector3(0f, 3048f, 0f), 0f)), seat);
            core.Add(AircraftFactory.Build(2, e, L1, Level(e, L1, new Vector3(300f, 3048f, 1500f), 0f, 330f)), new HoldSeat(new PilotInputs { Throttle = 0.55f }));
            // A stand-in policy: every action swept, the trigger pulsed.
            var a = new float[LearnedActions.ContinuousCount];
            for (int i = 0; i < 40 * 50; i++)
            {
                if (core.Tick % Set.DecisionPeriodTicks == 0)
                {
                    float t = core.Tick * 0.02f;
                    a[0] = Mathf.Sin(0.3f * t); a[1] = Mathf.Sin(0.17f * t); a[2] = Mathf.Cos(0.11f * t); a[3] = Mathf.Sin(0.7f * t);
                    seat.Order(LearnedActions.Decode(a, (core.Tick / 50) % 4 == 0, (core.Tick / 50) % 6 == 3, core.Find(1).State, Set, ace));
                }
                core.Step();
            }
            Assert.IsTrue(core.Inputs.All.Where(x => x.AircraftId == 1).All(x => x.Seat == SeatKind.Learned), "every entry from the learned seat");
            Assert.Greater(core.Rounds.RoundsFired, 0, "it fired, through the trigger input");

            // The same jets flown again from nothing but the input log: if the seat had touched the jet any other way,
            // the Fix would part.
            var replay = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: 5);
            replay.Add(AircraftFactory.Build(1, e, L1, Level(e, L1, new Vector3(0f, 3048f, 0f), 0f)), new ReplaySeat(core.Inputs));
            replay.Add(AircraftFactory.Build(2, e, L1, Level(e, L1, new Vector3(300f, 3048f, 1500f), 0f, 330f)), new ReplaySeat(core.Inputs));
            while (replay.Tick < core.Tick) replay.Step();
            Assert.AreEqual(-1, core.Fixes.FirstDivergence(replay.Fixes), "the input log alone repeats the learned flight, tick for tick");
            Debug.Log($"[Learned] 40 s of swept actions: {core.Rounds.RoundsFired} rounds fired, {core.Hits.Count} hits; replayed from the log with no divergence");
        }

        [Test]
        public void ATurnAction_TurnsTheJet_AClimbAction_ClimbsIt_AndThePilotsHandsKeepItFlying()
        {
            AircraftProfile e = E();
            PilotProfile ace = Ace();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            var seat = new LearnedSeat(core, e, ace);
            core.Add(AircraftFactory.Build(1, e, L1, Level(e, L1, new Vector3(0f, 3048f, 0f), 0f)), seat);
            Fly(core, seat, 1, new[] { 0.5f, 0f, 0f, 1f }, false, 1f);   // a right turn: 90° off the present track, decision after decision
            Fly(core, seat, 1, new[] { 0.5f, 0f, 0f, 1f }, false, 3f);
            AirframeState s = core.Find(1).State;
            float turned = FlightPathHands.TrackDeg(s);
            Debug.Log($"[Learned] turn +0.5 for 4 s: track {turned:F0}°, {s.Position.y:F0} m, {s.Velocity.magnitude / Kt:F0} kt, {s.LoadFactor:F1} g");
            Assert.Greater(turned, 30f, "turning right");
            Assert.AreEqual(FlightStatus.Flying, s.Status);

            float before = core.Find(1).State.Position.y;
            Fly(core, seat, 1, new[] { 0f, 1f, 0.6f, 1f }, false, 30f);   // climb 1,500 m above, decision after decision
            float climbed = core.Find(1).State.Position.y - before;
            Debug.Log($"[Learned] climb +1 for 30 s: {climbed:F0} m higher");
            Assert.Greater(climbed, 1000f);

            // The hands' bank cap holds whatever it asks: never past the ace's 75°.
            float maxBank = 0f;
            for (int i = 0; i < 20 * 50; i++)
            {
                if (core.Tick % 5 == 0) seat.Order(LearnedActions.Decode(new[] { (i / 100) % 2 == 0 ? 1f : -1f, -1f, 1f, 1f }, false, false, core.Find(1).State, Set, ace));
                core.Step();
                maxBank = Mathf.Max(maxBank, Mathf.Abs(FlightPathHands.BankDeg(core.Find(1).State)));
            }
            Debug.Log($"[Learned] hard reversals for 20 s: bank never past {maxBank:F0}°, {core.Find(1).State.Status}");
            Assert.AreEqual(FlightStatus.Flying, core.Find(1).State.Status, "no departure from the asked-for reversals");
            Assert.Less(maxBank, ace.Hands.MaxBankDeg + 10f);
        }

        [Test]
        public void TheActions_MeanWhatTheSettingsSay()
        {
            PilotProfile ace = Ace();
            AirframeState s = Level(E(), L0, new Vector3(100f, 2000f, 0f), 90f);
            LearnedCommand Of(float turn, float climb, float speed, float g, bool trigger = false) =>
                LearnedActions.Decode(new[] { turn, climb, speed, g }, trigger, false, s, Set, ace);

            LearnedCommand straight = Of(0f, 0f, -1f, -1f);
            Vector3 aim = straight.Request.AimWorld - s.Position;
            Assert.AreEqual(90f, Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg, 0.01f, "turn 0: the present track");
            Assert.AreEqual(2000f, straight.Request.AltitudeM, 1e-3f);
            Assert.AreEqual(150f * Kt, straight.Request.SpeedMps, 1e-3f, "speed −1: the slow end");
            Assert.AreEqual(2f, straight.Request.GLimit, 1e-5f, "G −1: 2 g");
            Assert.IsFalse(straight.Trigger);

            LearnedCommand hard = Of(0.5f, 1f, 1f, 1f, true);
            aim = hard.Request.AimWorld - s.Position;
            Assert.AreEqual(0f, Mathf.DeltaAngle(180f, Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg), 0.01f, "turn +0.5: 90° right of the track");
            Assert.AreEqual(3500f, hard.Request.AltitudeM, 1e-3f, "climb +1: 1,500 m up");
            Assert.AreEqual(600f * Kt, hard.Request.SpeedMps, 1e-3f);
            Assert.AreEqual(ace.Hands.MaxG, hard.Request.GLimit, 1e-5f, "G +1: the ace's most");
            Assert.IsTrue(hard.Trigger);

            Assert.AreEqual(hard.Request.SpeedMps, Of(0.5f, 1f, 3f, 1f).Request.SpeedMps, "past ±1 is clamped");
            Assert.Throws<ArgumentException>(() => Of(float.NaN, 0f, 0f, 0f), "a NaN is a broken policy, not a zero");
            Assert.Throws<ArgumentException>(() => LearnedActions.Decode(new float[3], false, false, s, Set, ace));
        }

        [Test]
        public void TheSenses_AreTheAcesEyes_NeverTheTruth()
        {
            AircraftProfile e = E();
            PilotProfile ace = Ace();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            var seat = new LearnedSeat(core, e, ace);
            var fight = new Engagement();
            core.Add(AircraftFactory.Build(1, e, L0, Level(e, L0, new Vector3(0f, 3048f, 0f), 0f)), seat);
            core.Add(AircraftFactory.Build(2, e, L0, Level(e, L0, new Vector3(0f, 3048f, -1000f), 0f)), new HoldSeat(new PilotInputs { Throttle = 0.7f }));   // behind: outside 150°
            core.Add(AircraftFactory.Build(3, e, L0, Level(e, L0, new Vector3(0f, 3200f, 2000f), 0f)), new HoldSeat(new PilotInputs { Throttle = 0.7f }));   // ahead
            core.Add(AircraftFactory.Build(4, e, L0, Level(e, L0, new Vector3(-500f, 3048f, 800f), 0f)), new HoldSeat(new PilotInputs { Throttle = 0.7f }));  // a friend ahead-left
            fight.Join(1, Side.Blue); fight.Join(4, Side.Blue);
            fight.Join(2, Side.Red); fight.Join(3, Side.Red);
            var senses = new LearnedSenses();
            int delay = (int)Math.Round(ace.ReactionDelayS / 0.02);

            int firstSeen = -1;
            for (int t = 0; t < 3 * 50; t++)
            {
                core.Step();
                float[] v = senses.Write(core, 1, seat.Eyes, fight, ace, protectedNow: false);
                Assert.AreEqual(LearnedSenses.Size, v.Length);
                Assert.IsTrue(v.All(x => !float.IsNaN(x) && !float.IsInfinity(x) && Mathf.Abs(x) <= 5f), "finite and bounded");
                if (firstSeen < 0 && v[LearnedSenses.OwnSize] == 1f) firstSeen = t;
            }
            Assert.AreEqual(140, LearnedSenses.Size, "version 3: 68, then 20 for the missiles, then 52 for the sensors");
            Assert.AreEqual(88, LearnedSenses.SizeVersion2, "version 2's numbers keep their places");
            Assert.GreaterOrEqual(firstSeen, delay, "nothing arrives before the ace's reaction delay");
            float[] now = senses.Values;
            int e0 = LearnedSenses.OwnSize, e1 = e0 + LearnedSenses.EnemySize;
            Assert.AreEqual(1f, now[e0], "the enemy ahead is in the first enemy slot");
            Assert.AreEqual(3, senses.NearestEnemy, "and it is aircraft 3, ahead");
            Assert.Greater(now[e0 + 5], 0f, "ahead: +z in the body frame");
            Assert.AreEqual(0f, now[e1], "the enemy behind, outside the field of view, never appears");
            int f0 = e0 + LearnedSenses.EnemySlots * LearnedSenses.EnemySize;
            Assert.AreEqual(1f, now[f0], "the friend is in the friend slot");
            Assert.Less(now[f0 + 2], 0f, "to the left");
            Assert.AreEqual(0f, now[f0 + LearnedSenses.FriendSize], "one friend: the second slot is empty");
            Debug.Log($"[Learned] senses: enemy ahead first seen at tick {firstSeen} (delay {delay}); enemy behind unseen; friend slot filled");
        }

        /// <summary>A shooter (blue 1) and a target (red 2) where its rounds will be in 400 m, a squadmate for each far away.</summary>
        static FlightCore Range(out TriggerSeat shooter, out Engagement fight, out RewardLedger ledger)
        {
            AircraftProfile e = E();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: 7);
            shooter = new TriggerSeat();
            core.Add(AircraftFactory.Build(1, e, L0, Level(e, L0, new Vector3(0f, 3048f, 0f), 0f)), shooter);
            core.Step();
            AirframeState s = core.Find(1).State;
            GunMount m = e.Gun;
            Assert.IsTrue(LeadSight.RoundAt(m.Gun, s.Position + s.Attitude * m.MuzzleM, s.Velocity, s.Attitude * m.BoreBody, 400f, out Vector3 at, out float tof));
            core.Add(AircraftFactory.Build(2, e, L0, Level(e, L0, at - s.Velocity * tof, 0f)), new TriggerSeat());
            core.Add(AircraftFactory.Build(3, e, L0, Level(e, L0, new Vector3(5000f, 3048f, 0f), 0f)), new TriggerSeat());
            core.Add(AircraftFactory.Build(4, e, L0, Level(e, L0, new Vector3(-5000f, 3048f, 0f), 0f)), new TriggerSeat());
            fight = new Engagement();
            fight.Join(1, Side.Blue); fight.Join(3, Side.Blue);
            fight.Join(2, Side.Red); fight.Join(4, Side.Red);
            ledger = new RewardLedger(Pay, fight);
            for (int id = 1; id <= 4; id++) ledger.Track(id);
            ledger.Observe(core);
            for (int id = 1; id <= 4; id++) ledger.Take(id);
            return core;
        }

        [Test]
        public void AKill_PaysTheShooterAndItsSquadron_AndCostsTheVictimAndItsSquadron_Once()
        {
            FlightCore core = Range(out TriggerSeat shooter, out Engagement fight, out RewardLedger ledger);
            // Range's settling tick already counted the shooter pointed (its pay was taken away there): count from here.
            double pointedBefore = ledger.PointedSeconds(1);
            shooter.FireFrom = core.Tick;
            var paid = new float[5];
            while (core.Kills.Count == 0 && core.Tick < 300)
            {
                core.Step(); fight.Score(core); ledger.Observe(core);
                for (int id = 1; id <= 4; id++) paid[id] += ledger.Take(id);
            }
            Assert.AreEqual(1, core.Kills.Count);
            int roundsHit = core.Hits.Where(h => h.TargetId == 2).Sum(h => h.Rounds);
            Assert.IsTrue(ledger.TakeLoss(2, out Loss loss));
            Assert.AreEqual(Loss.ShotDown, loss);
            Assert.IsFalse(ledger.TakeLoss(2, out _), "a loss is reported once");
            // The shooter sat 400 m behind its target, nose on: it earned pointed cone time too.
            double pointed = ledger.PointedSeconds(1) - pointedBefore;
            Assert.Greater(pointed, 0.0);
            Assert.AreEqual(1f + 0.05f * roundsHit + 0.02f * (float)pointed, paid[1], 1e-4f, "the shooter: the kill, its hits and its pointed seconds");
            Assert.AreEqual(-1f - 0.05f * roundsHit, paid[2], 1e-4f, "the victim: shot down and the hits taken");
            Assert.AreEqual(0.25f, paid[3], 1e-4f, "the shooter's squadmate: a quarter of the kill");
            Assert.AreEqual(-0.25f, paid[4], 1e-4f, "the victim's squadmate: a quarter of the loss");
            Assert.AreEqual(1, ledger.KillsBy(1));

            // The wreck falls and meets the sea: not a second loss, not a crash.
            shooter.FireUntil = core.Tick;
            for (int i = 0; i < 60 * 50 && core.Find(2).State.Status == FlightStatus.Killed; i++) { core.Step(); ledger.Observe(core); paid[2] += ledger.Take(2); }
            Assert.AreEqual(FlightStatus.Crashed, core.Find(2).State.Status);
            Assert.AreEqual(-1f - 0.05f * roundsHit, paid[2], 1e-4f, "the fall costs nothing more");
            Assert.IsFalse(ledger.TakeLoss(2, out _));
            Debug.Log($"[Learned] a kill: shooter {paid[1]:F2}, victim {paid[2]:F2}, squadmates {paid[3]:F2} / {paid[4]:F2} ({roundsHit} rounds hit)");
        }

        [Test]
        public void ACrash_ACollision_AndLeavingTheBox_EachCostOne()
        {
            AircraftProfile e = E();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            core.Add(AircraftFactory.Build(1, e, L0, Level(e, L0, new Vector3(0f, 2000f, -1000f), 0f)), new TriggerSeat());
            core.Add(AircraftFactory.Build(2, e, L0, Level(e, L0, new Vector3(0f, 2000f, 1000f), 180f)), new TriggerSeat());
            core.Add(AircraftFactory.Build(3, e, L0, Level(e, L0, new Vector3(4000f, 300f, 0f), 90f)), new TriggerSeat { Pitch = -1f });
            core.Add(AircraftFactory.Build(4, e, L0, Level(e, L0, new Vector3(-9000f, 2000f, 0f), 270f)), new TriggerSeat());
            var arena = new Arena(new ArenaSettings { RespawnDelayS = 3f, ProtectionS = 5f, CollisionM = 15f, HalfWidthXM = 10000f, HalfWidthZM = 10000f }, 0.02, 1);
            core.UseArena(arena);
            var fight = new Engagement();
            fight.Join(1, Side.Blue); fight.Join(3, Side.Blue);
            fight.Join(2, Side.Red); fight.Join(4, Side.Red);
            var ledger = new RewardLedger(new RewardWeights { Kill = 1f, ShotDown = -1f, FriendlyKill = -1f, Crash = -1f, Collision = -1f, LeftArena = -1f, ConeDeg = 30f, ConeRangeM = 1000f }, fight);
            for (int id = 1; id <= 4; id++) ledger.Track(id);
            var paid = new float[5];
            var how = new Loss[5];
            for (int t = 0; t < 20 * 50; t++)
            {
                core.Step(); fight.Score(core); ledger.Observe(core);
                for (int id = 1; id <= 4; id++) { paid[id] += ledger.Take(id); if (ledger.TakeLoss(id, out Loss l) && how[id] == Loss.None) how[id] = l; }
            }
            Debug.Log($"[Learned] losses: {string.Join(", ", Enumerable.Range(1, 4).Select(i => $"{i} {how[i]} {paid[i]:F2}"))}");
            Assert.AreEqual(Loss.Collided, how[1]);
            Assert.AreEqual(Loss.Collided, how[2]);
            Assert.AreEqual(Loss.Crashed, how[3]);
            Assert.AreEqual(Loss.LeftArena, how[4]);
            for (int id = 1; id <= 4; id++) Assert.AreEqual(-1f, paid[id], 1e-5f, $"aircraft {id}: one loss, −1 (no squadron share: the share is off here)");
        }

        [Test]
        public void PointedConeTime_PaysOnlyAJetBehindItsTarget_AndPointingAtIt()
        {
            AircraftProfile e = E();
            var fight = new Engagement();
            fight.Join(1, Side.Blue); fight.Join(2, Side.Red);
            var ledger = new RewardLedger(Pay, fight);
            AirframeState target = Level(e, L0, new Vector3(0f, 3000f, 0f), 0f);
            AirframeState behind = Level(e, L0, new Vector3(0f, 3000f, -500f), 0f);
            AirframeState behindLookingAway = Level(e, L0, new Vector3(0f, 3000f, -500f), 90f);
            AirframeState headOn = Level(e, L0, new Vector3(0f, 3000f, 500f), 180f);
            AirframeState passedHeadOn = Level(e, L0, new Vector3(0f, 3000f, -500f), 180f);   // just past a head-on pass: in the cone by position
            Assert.IsTrue(ledger.Pointed(behind, target), "on its tail, nose on it");
            Assert.IsFalse(ledger.Pointed(behindLookingAway, target), "on its tail by position, nose 90° off");
            Assert.IsFalse(ledger.Pointed(headOn, target), "in front");
            Assert.IsTrue(RearCone.Inside(passedHeadOn, target, 30f, 1000f), "the position-only cone scores the pass (A9)");
            Assert.IsFalse(ledger.Pointed(passedHeadOn, target), "the pointed cone does not");

            // Flown: a jet sitting 500 m behind for a second earns 0.02.
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            core.Add(AircraftFactory.Build(1, e, L0, behind), new HoldSeat(new PilotInputs { Throttle = 0.7f }));
            core.Add(AircraftFactory.Build(2, e, L0, target), new HoldSeat(new PilotInputs { Throttle = 0.7f }));
            ledger.Track(1); ledger.Track(2);
            float paid = 0f;
            for (int t = 0; t < 50; t++) { core.Step(); ledger.Observe(core); paid += ledger.Take(1); }
            Assert.AreEqual(0.02f, paid, 1e-4f);
            Assert.AreEqual(0f, ledger.Take(2), "the target earns nothing");
        }

        [Test]
        public void AJetThatRespawns_RejoinsTheFight_OnItsSide()
        {
            AircraftProfile e = E();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            core.Add(AircraftFactory.Build(1, e, L0, Level(e, L0, new Vector3(0f, 300f, 0f), 0f)), new TriggerSeat { Pitch = -1f });
            var arena = new Arena(new ArenaSettings { RespawnDelayS = 3f, ProtectionS = 5f }, 0.02, 1);
            core.UseArena(arena);
            arena.Assign(1, core.Find(1).Start);
            var fight = new Engagement();
            fight.Join(1, Side.Red);
            while (core.Find(1).State.Status == FlightStatus.Flying) { core.Step(); fight.Score(core); }
            Assert.AreEqual(Side.Red, fight.SideOf(1), "a crash into the sea leaves it on its side, as before the arena");
            for (int i = 0; i < 160; i++) { core.Step(); fight.Score(core); }
            Assert.AreEqual(FlightStatus.Flying, core.Find(1).State.Status);
            Assert.AreEqual(Side.Red, fight.SideOf(1), "back on its side after the respawn");

            var core2 = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            core2.Add(AircraftFactory.Build(1, e, L0, Level(e, L0, new Vector3(0f, 2000f, 9900f), 0f)), new TriggerSeat());
            var arena2 = new Arena(new ArenaSettings { RespawnDelayS = 3f, HalfWidthXM = 10000f, HalfWidthZM = 10000f }, 0.02, 1);
            core2.UseArena(arena2);
            arena2.Assign(1, Level(e, L0, new Vector3(0f, 2000f, 0f), 0f));
            var fight2 = new Engagement();
            fight2.Join(1, Side.Blue);
            for (int i = 0; i < 2 * 50; i++) { core2.Step(); fight2.Score(core2); }
            Assert.AreEqual(Side.None, fight2.SideOf(1), "out of the box: out of the fight, as a kill's victim");
            Assert.AreEqual(Side.Blue, fight2.SideEver(1), "but still blue's");
            for (int i = 0; i < 3 * 50; i++) { core2.Step(); fight2.Score(core2); }
            Assert.AreEqual(Side.Blue, fight2.SideOf(1), "back in the fight after its respawn");
        }
    }
}
