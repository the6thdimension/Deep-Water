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

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// The game missiles (W65; SIX 2026-10-29, all four picks): four on pylons; a lock is a target inside 30° of the nose
    /// between 500 m and 5 km for a second; the pickle launches one a second while locked; proportional navigation under a
    /// turn limit; a 15 m burst kills seven times in ten and otherwise takes 6 hit points; a lost seeker never bursts.
    /// All in the tick, so a run and its input log's replay repeat.
    /// </summary>
    public class MissileTests
    {
        static readonly FidelitySheet L0 = new FidelitySheet(FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, 4, 1442);
        const float Kt = 0.514444f;
        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();

        static AirframeState Level(in AircraftProfile e, Vector3 at, float headingDeg, float kt = 400f) =>
            AircraftFactory.LevelStart(e, L0, at, headingDeg, kt * Kt, 0.6f * e.Airframe.InternalFuelKg, 0.7f);

        /// <summary>A seat that holds the throttle and, from a tick on, the pickle.</summary>
        sealed class PickleSeat : ISeat
        {
            public long PickleFrom = long.MaxValue;
            public float Throttle = 0.7f;
            public SeatKind Kind => SeatKind.Scenario;
            public PilotInputs Issue(long tick, int aircraftId, in AirframeState state) =>
                new PilotInputs { Throttle = Throttle, Pickle = tick >= PickleFrom };
        }

        /// <summary>A shooter at 10,000 ft heading north and a target <paramref name="ahead"/> ahead, both straight and level.</summary>
        static FlightCore Pair(Vector3 ahead, float targetHeading, float targetKt, out PickleSeat shooter, ulong seed = 3)
        {
            AircraftProfile e = E();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: seed);
            shooter = new PickleSeat();
            core.Add(AircraftFactory.Build(1, e, L0, Level(e, new Vector3(0f, 3048f, 0f), 0f)), shooter);
            core.Add(AircraftFactory.Build(2, e, L0, Level(e, new Vector3(0f, 3048f, 0f) + ahead, targetHeading, targetKt)), new PickleSeat());
            return core;
        }

        [Test]
        public void TheF18E_CarriesTwoHeatSeekers_AndStartsWithThemLoaded()
        {
            AircraftProfile e = E();
            Assert.IsTrue(e.Missiles.IsSet);
            Assert.AreEqual(2, e.Missiles.Carried, "SENSORS-CONCEPT J18: two heat-seekers outboard (four until the radar missiles took the inboard pair)");
            Assert.AreEqual(2, Level(e, new Vector3(0f, 3000f, 0f), 0f).Missiles, "a jet starts loaded (and so respawns loaded: the arena restores its start)");
            MissileProfile m = e.Missiles.Missile;
            Assert.AreEqual(30f, m.LockConeDeg); Assert.AreEqual(500f, m.LockMinM); Assert.AreEqual(5000f, m.LockMaxM); Assert.AreEqual(1f, m.LockTimeS);
            Assert.AreEqual(15f, m.LethalRadiusM); Assert.AreEqual(0.7f, m.KillChance); Assert.AreEqual(6f, m.DamageHp); Assert.AreEqual(20f, m.LifeS);
        }

        [Test]
        public void ALock_TakesASecond_InsideTheConeAndRange_AndNeverOutside()
        {
            FlightCore core = Pair(new Vector3(0f, 0f, 2000f), 0f, 400f, out _);
            for (int i = 0; i < 49; i++) core.Step();
            Aircraft me = core.Find(1);
            Assert.AreEqual(2, me.State.LockTargetId, "the seeker is on it");
            Assert.IsFalse(core.IsLocked(me), "not yet a second");
            core.Step();
            Assert.IsTrue(core.IsLocked(me), "locked after 50 ticks");

            foreach ((Vector3 at, string why) in new[]
            {
                (Quaternion.Euler(0f, 40f, 0f) * new Vector3(0f, 0f, 2000f), "40° off the nose"),
                (new Vector3(0f, 0f, 300f), "inside 500 m"),
                (new Vector3(0f, 0f, 6000f), "past 5 km"),
            })
            {
                FlightCore c = Pair(at, 0f, 400f, out _);
                for (int i = 0; i < 100; i++) c.Step();
                Assert.AreEqual(0, c.Find(1).State.LockTargetId, why);
            }
        }

        [Test]
        public void ThePickle_LaunchesOnlyWithALock_ThenOneASecondWhileHeld_UntilTheyAreGone()
        {
            FlightCore core = Pair(new Vector3(0f, 0f, 3000f), 0f, 400f, out PickleSeat shooter);
            shooter.PickleFrom = 0;
            for (int i = 0; i < 49; i++) core.Step();
            Assert.AreEqual(0, core.Missiles.Launched, "no lock, no launch");
            for (int i = 0; i < 25 * 50; i++) core.Step();
            var launches = core.Missiles.Events.Where(e => e.Kind == MissileEventKind.Launched).Select(e => e.Tick).ToList();
            Debug.Log($"[MISSILE] launches at ticks {string.Join(", ", launches)}; bursts {core.Missiles.Bursts}");
            Assert.LessOrEqual(launches.Count, 2, "two heat-seekers on the pylons");
            Assert.AreEqual(49, launches[0], "the tick the lock completes");
            for (int i = 1; i < launches.Count; i++) Assert.GreaterOrEqual(launches[i] - launches[i - 1], 50, "one a second at most");
            Assert.AreEqual(2 - launches.Count, core.Find(1).State.Missiles);
            Assert.Greater(core.Missiles.Bursts, 0, "a tail chase from 3 km on a jet at the same speed closes inside the 20 s life");
        }

        [Test]
        public void AMissile_RunsDownAStraightFlyingTarget_AndBurstsInside15m()
        {
            FlightCore core = Pair(new Vector3(0f, 0f, 3000f), 0f, 330f, out PickleSeat shooter, seed: 11);
            shooter.PickleFrom = 0;
            while (core.Missiles.Launched == 0 && core.Tick < 200) core.Step();
            shooter.PickleFrom = long.MaxValue;
            float closest = float.MaxValue;
            long launch = core.Tick;
            while (core.Missiles.Bursts == 0 && core.Tick < launch + 25 * 50)
            {
                core.Step();
                if (core.Missiles.Live.Count > 0) closest = Mathf.Min(closest, (core.Missiles.Live[0].Position - core.Find(2).State.Position).magnitude);
            }
            MissileEvent burst = core.Missiles.Events.Single(e => e.Kind == MissileEventKind.Burst);
            Aircraft target = core.Find(2);
            Debug.Log($"[MISSILE] {burst} after {(burst.Tick - launch) * 0.02f:F1} s; target {target.State.Status}, HP {target.State.HitPoints}");
            Assert.AreEqual(2, burst.TargetId);
            Assert.Less((burst.Tick - launch) * 0.02f, 20f, "inside its life");
            if (burst.Killed) { Assert.AreEqual(FlightStatus.Killed, target.State.Status); Assert.AreEqual(1, core.Kills.Single().ShooterId); }
            else Assert.AreEqual(E().Combat.HitPoints - 6f, target.State.HitPoints, 1e-4f, "6 hit points");
            Assert.AreEqual(1, core.Hits.Count(h => h.Weapon == E().Missiles.Missile.Name), "the burst is a hit report");
        }

        [Test]
        public void ABurst_Kills_SevenTimesInTen()
        {
            int bursts = 0, kills = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                FlightCore core = Pair(new Vector3(0f, 0f, 2000f), 0f, 330f, out PickleSeat shooter, seed: (ulong)seed);
                shooter.PickleFrom = 0;
                while (core.Missiles.Launched == 0 && core.Tick < 200) core.Step();
                shooter.PickleFrom = long.MaxValue;
                while (core.Missiles.Bursts == 0 && core.Tick < 25 * 50) core.Step();
                bursts += core.Missiles.Bursts;
                kills += core.Missiles.Events.Count(e => e.Kind == MissileEventKind.Burst && e.Killed);
            }
            Debug.Log($"[MISSILE] 60 shots at a straight target: {bursts} bursts, {kills} kills");
            Assert.AreEqual(60, bursts, "every shot at a straight target bursts");
            Assert.That(kills, Is.InRange(32, 52), "70 % of 60 is 42, σ ≈ 3.5");
        }

        [Test]
        public void ATargetThatRespawns_IsLostToTheSeeker_AndNeverHit()
        {
            FlightCore core = Pair(new Vector3(0f, 0f, 3000f), 0f, 330f, out PickleSeat shooter);
            var arena = new Arena(new ArenaSettings { RespawnDelayS = 3f, ProtectionS = 5f }, 0.02, 1);
            core.UseArena(arena);
            arena.Assign(2, core.Find(2).Start);
            shooter.PickleFrom = 0;
            while (core.Missiles.Launched == 0 && core.Tick < 200) core.Step();
            shooter.PickleFrom = long.MaxValue;
            arena.Respawn(core.Find(2), core.Tick);   // the target is a new jet now
            for (int i = 0; i < 25 * 50; i++) core.Step();
            Assert.AreEqual(1, core.Missiles.Events.Count(e => e.Kind == MissileEventKind.Lost));
            Assert.AreEqual(0, core.Missiles.Bursts);
            Assert.AreEqual(1, core.Missiles.Events.Count(e => e.Kind == MissileEventKind.Expired), "it flies on and expires");
            Assert.AreEqual(0, core.Missiles.Live.Count);
        }

        [Test]
        public void AProtectedJet_CannotBeLocked_AndCannotLaunch()
        {
            FlightCore core = Pair(new Vector3(0f, 0f, 2000f), 0f, 400f, out PickleSeat shooter);
            var arena = new Arena(new ArenaSettings { RespawnDelayS = 3f, ProtectionS = 5f }, 0.02, 1);
            core.UseArena(arena);
            arena.Assign(2, core.Find(2).State);
            arena.Respawn(core.Find(2), core.Tick);   // the target protected for 5 s
            shooter.PickleFrom = 0;
            for (int i = 0; i < 200; i++) core.Step();
            Assert.AreEqual(0, core.Find(1).State.LockTargetId, "a protected jet is not a seeker's target");
            Assert.AreEqual(0, core.Missiles.Launched);

            FlightCore c2 = Pair(new Vector3(0f, 0f, 2000f), 0f, 400f, out PickleSeat s2);
            var a2 = new Arena(new ArenaSettings { RespawnDelayS = 3f, ProtectionS = 5f }, 0.02, 1);
            c2.UseArena(a2);
            a2.Assign(1, c2.Find(1).State);
            a2.Respawn(c2.Find(1), c2.Tick);   // the shooter protected
            s2.PickleFrom = 0;
            for (int i = 0; i < 200; i++) c2.Step();
            Assert.IsTrue(c2.IsLocked(c2.Find(1)), "it may lock");
            Assert.AreEqual(0, c2.Missiles.Launched, "but not launch while protected");
            for (int i = 0; i < 60; i++) c2.Step();
            Assert.AreEqual(1, c2.Missiles.Launched, "protection over: away");
        }

        [Test]
        public void AMissileFight_RepeatsTickForTick_AndItsInputLogReplaysIt()
        {
            AircraftProfile e = E();
            (FlightCore core, string events) Run()
            {
                FlightCore core = Pair(new Vector3(300f, 200f, 3500f), 20f, 360f, out PickleSeat shooter, seed: 21);
                shooter.PickleFrom = 30;
                for (int i = 0; i < 30 * 50; i++) core.Step();
                return (core, string.Join("; ", core.Missiles.Events));
            }
            var a = Run();
            var b = Run();
            Debug.Log($"[MISSILE] {a.events}");
            Assert.Greater(a.core.Missiles.Launched, 0);
            Assert.AreEqual(-1, a.core.Fixes.FirstDivergence(b.core.Fixes), "the same Fix on every tick");
            Assert.AreEqual(a.events, b.events);

            var replay = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: 21);
            replay.Add(AircraftFactory.Build(1, e, L0, Level(e, new Vector3(0f, 3048f, 0f), 0f)), new ReplaySeat(a.core.Inputs));
            replay.Add(AircraftFactory.Build(2, e, L0, Level(e, new Vector3(300f, 3248f, 3500f), 20f, 360f)), new ReplaySeat(a.core.Inputs));
            while (replay.Tick < a.core.Tick) replay.Step();
            Assert.AreEqual(-1, a.core.Fixes.FirstDivergence(replay.Fixes), "the pickle is an input: the log replays the launches and the bursts");
        }

        [Test]
        public void Guidance_LeadsACrossingTarget_AndKeepsToItsTurnLimit()
        {
            // The missile flies north; the target, 2 km ahead, crosses eastward: the command turns it east (toward the lead).
            Vector3 a = MissileGuidance.Command(Vector3.zero, new Vector3(0f, 0f, 400f), new Vector3(0f, 0f, 2000f), new Vector3(200f, 0f, 0f), 4f, 30f);
            Assert.Greater(a.x, 0f, "toward where the target is going");
            Assert.AreEqual(0f, Vector3.Dot(a, Vector3.forward), 1e-3f, "sideways only");
            Assert.LessOrEqual(a.magnitude, 30f * 9.80665f + 1e-3f);
            // A target dead ahead on the same line asks for nothing.
            Assert.Less(MissileGuidance.Command(Vector3.zero, new Vector3(0f, 0f, 400f), new Vector3(0f, 0f, 2000f), new Vector3(0f, 0f, 200f), 4f, 30f).magnitude, 1e-3f);
            // Behind it and opening: turn hard, at the cap.
            Assert.AreEqual(30f * 9.80665f, MissileGuidance.Command(Vector3.zero, new Vector3(0f, 0f, 400f), new Vector3(100f, 0f, -500f), new Vector3(0f, 0f, -300f), 4f, 30f).magnitude, 1e-2f);
            Assert.IsTrue(MissileGuidance.InCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 100f), 45f));
            Assert.IsFalse(MissileGuidance.InCone(Vector3.zero, Vector3.forward, new Vector3(100f, 0f, 50f), 45f));
        }
    }
}
