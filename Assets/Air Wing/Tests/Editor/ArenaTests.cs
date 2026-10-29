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
using DeepWater.Gunnery.Guns;

namespace DeepWater.AirWing.Tests
{
    /// <summary>
    /// The arena (W59–W61): a jet that goes down comes back at its spawn slot after the delay, whole and protected; a
    /// protected jet can't be hit and can't fire; the respawn's jitter stays in its bounds and repeats from its seed;
    /// jets that meet both go down; a jet that leaves the box goes down. All on the tick count, so a run repeats.
    /// </summary>
    public class ArenaTests
    {
        static readonly FidelitySheet L0 = new FidelitySheet(FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, 4, 1442);
        const float Speed = 400f * 0.514444f;
        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();

        /// <summary>The training arena's rules (SIX, 2026-10-29), with the jitter and the box off unless a test turns them on.</summary>
        static ArenaSettings Rules(float jitterM = 0f, float jitterDeg = 0f, float collisionM = 0f, float halfWidthM = 0f) => new ArenaSettings
        {
            RespawnDelayS = 3f, ProtectionS = 5f, JitterHeightM = jitterM, JitterHeadingDeg = jitterDeg,
            CollisionM = collisionM, HalfWidthXM = halfWidthM, HalfWidthZM = halfWidthM,
        };

        /// <summary>A seat that holds its throttle and stick and, between two ticks, the trigger.</summary>
        sealed class TriggerSeat : ISeat
        {
            public long FireFrom = long.MaxValue, FireUntil = long.MaxValue;
            public float Pitch;
            public int Issued;
            public SeatKind Kind => SeatKind.Scenario;
            public PilotInputs Issue(long tick, int aircraftId, in AirframeState state)
            {
                Issued++;
                return new PilotInputs { Throttle = 0.6f, StickPitch = Pitch, Trigger = tick >= FireFrom && tick < FireUntil };
            }
        }

        static AirframeState Level(in AircraftProfile e, Vector3 at, float headingDeg) =>
            AircraftFactory.LevelStart(e, L0, at, headingDeg, Speed, 0.6f * e.Airframe.InternalFuelKg, 0.6f);

        /// <summary>
        /// A shooter at 10,000 ft and 400 kt and a target flying alongside where its rounds will be in 400 m (as
        /// <c>GunTests</c>), with an arena; the target has its spawn slot where it starts.
        /// </summary>
        static FlightCore Range(in ArenaSettings rules, out Arena arena, out TriggerSeat shooter, out TriggerSeat target)
        {
            AircraftProfile e = E();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: 7);
            shooter = new TriggerSeat();
            target = new TriggerSeat();
            core.Add(AircraftFactory.Build(1, e, L0, Level(e, new Vector3(0f, 3048f, 0f), 0f)), shooter);
            core.Step();   // settle onto the angle of attack before aiming
            AirframeState s = core.Find(1).State;
            GunMount m = e.Gun;
            Vector3 muzzle = s.Position + s.Attitude * m.MuzzleM;
            Assert.IsTrue(LeadSight.RoundAt(m.Gun, muzzle, s.Velocity, s.Attitude * m.BoreBody, 400f, out Vector3 at, out float tof));
            core.Add(AircraftFactory.Build(2, e, L0, Level(e, at - s.Velocity * tof, 0f)), target);
            arena = new Arena(rules, core.Clock.TickSeconds, seed: 99);
            core.UseArena(arena);
            arena.Assign(2, core.Find(2).Start);
            return core;
        }

        [Test]
        public void AJetShotDown_ComesBackAtItsSlot_ThreeSecondsLater_WithFullRoundsAndHitPoints()
        {
            FlightCore core = Range(Rules(), out Arena arena, out TriggerSeat shooter, out _);
            Aircraft victim = core.Find(2);
            AirframeState slot = victim.Start;
            int resets = victim.Resets;
            shooter.FireFrom = core.Tick;
            while (core.Kills.Count == 0 && core.Tick < 300) core.Step();
            Assert.AreEqual(1, core.Kills.Count, "shot down");
            shooter.FireUntil = core.Tick;
            long down = core.Kills[0].Tick;

            while (arena.Events.Count == 0 && core.Tick < down + 400) core.Step();
            ArenaEvent back = arena.Events.Single();
            Debug.Log($"[ARENA] shot down at tick {down}, {back}; out {(back.Tick - down) * 0.02:F2} s");
            Assert.AreEqual(ArenaEventKind.Respawned, back.Kind);
            Assert.AreEqual(2, back.AircraftId);
            Assert.AreEqual(down + 150, back.Tick, "back three seconds (150 ticks) after it went down");

            // The tick it came back on flew from the slot: one tick of level flight from it.
            Assert.AreEqual(FlightStatus.Flying, victim.State.Status);
            Assert.AreEqual(412, victim.State.Rounds, "a full gun");
            Assert.AreEqual(E().Combat.HitPoints, victim.State.HitPoints, "whole");
            Assert.AreEqual(0, victim.KilledBy);
            Assert.IsNull(victim.CrashReason);
            Assert.AreEqual(resets + 1, victim.Resets, "presentation snaps, it does not sweep");
            Assert.Less((victim.State.Position - (slot.Position + slot.Velocity * 0.02f)).magnitude, 1f, "where its slot flies to in one tick");
            Assert.AreEqual(1, arena.RespawnsOf(2));
        }

        [Test]
        public void AJetThatFliesIntoTheSea_ComesBackToo()
        {
            AircraftProfile e = E();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            var seat = new TriggerSeat { Pitch = -1f };
            core.Add(AircraftFactory.Build(1, e, L0, Level(e, new Vector3(0f, 300f, 0f), 0f)), seat);
            var arena = new Arena(Rules(), core.Clock.TickSeconds, 1);
            core.UseArena(arena);
            arena.Assign(1, core.Find(1).Start);
            while (core.Find(1).State.Status == FlightStatus.Flying && core.Tick < 1000) core.Step();
            Assert.AreEqual(FlightStatus.Crashed, core.Find(1).State.Status, "pushed into the sea");
            seat.Pitch = 0f;
            for (int i = 0; i < 151; i++) core.Step();
            Assert.AreEqual(FlightStatus.Flying, core.Find(1).State.Status, "back after three seconds");
            Assert.AreEqual(ArenaEventKind.Respawned, arena.Events.Last().Kind);
            Assert.Greater(core.Find(1).State.Position.y, 250f, "at its slot's height, not in the sea");
        }

        [Test]
        public void AProtectedJet_CannotBeHit_AndCannotFire_ForFiveSeconds()
        {
            FlightCore core = Range(Rules(), out Arena arena, out TriggerSeat shooter, out TriggerSeat target);
            Aircraft victim = core.Find(2);
            arena.Assign(2, victim.State);   // its slot is where it is: a respawn now changes nothing but the protection
            arena.Respawn(victim, core.Tick);
            Assert.IsTrue(arena.Protected(2, core.Tick));
            // A one-second burst while it is protected (the whole five seconds would empty the gun: 412 rounds at 100 a second).
            shooter.FireFrom = target.FireFrom = core.Tick;
            shooter.FireUntil = core.Tick + 50;
            long protectedUntil = core.Tick + arena.ProtectionTicks;
            Assert.AreEqual(250, arena.ProtectionTicks, "five seconds");
            while (core.Tick < protectedUntil) core.Step();
            Debug.Log($"[ARENA] protected 5 s: shooter fired {core.Rounds.RoundsFired} rounds, {core.Hits.Count} hits; the protected jet has {victim.State.Rounds} rounds");
            Assert.AreEqual(0, core.Hits.Count, "rounds pass through a protected jet");
            Assert.AreEqual(E().Combat.HitPoints, victim.State.HitPoints);
            Assert.AreEqual(412, victim.State.Rounds, "a protected jet's trigger does nothing");
            Assert.Greater(core.Rounds.RoundsFired, 0, "the shooter did fire");
            Assert.IsFalse(arena.Protected(2, core.Tick));

            // Protection over: the same trigger now fires, and the same burst now hits.
            long from = core.Tick;
            shooter.FireFrom = from;
            shooter.FireUntil = long.MaxValue;
            while (core.Hits.Count == 0 && core.Tick < from + 100) core.Step();
            Assert.Greater(core.Hits.Count, 0, "hit once protection is over");
            Assert.Less(victim.State.Rounds, 412, "and it fires");
        }

        [Test]
        public void TheRespawnJitter_StaysInItsBounds_KeepsTheSlotsSpeedAndClimb_AndRepeatsFromItsSeed()
        {
            AircraftProfile e = E();
            float[] Draws(ulong seed)
            {
                var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
                core.Add(AircraftFactory.Build(1, e, L0, Level(e, new Vector3(0f, 1829f, 2000f), 0f)), new HoldSeat(default));
                var arena = new Arena(Rules(jitterM: 300f, jitterDeg: 15f), core.Clock.TickSeconds, seed);
                Aircraft a = core.Find(1);
                arena.Assign(1, a.Start);
                var seen = new float[400];
                for (int i = 0; i < 200; i++)
                {
                    arena.Respawn(a, i);
                    float dh = a.State.Position.y - a.Start.Position.y;
                    float dpsi = Mathf.DeltaAngle(FlightPathHands.TrackDeg(a.Start), FlightPathHands.TrackDeg(a.State));
                    Assert.LessOrEqual(Mathf.Abs(dh), 300.001f);
                    Assert.LessOrEqual(Mathf.Abs(dpsi), 15.001f);
                    Assert.AreEqual(a.Start.Velocity.magnitude, a.State.Velocity.magnitude, 1e-3f, "the slot's speed");
                    Assert.AreEqual(a.Start.Velocity.y, a.State.Velocity.y, 1e-4f, "turned about the vertical: still level");
                    Assert.AreEqual(a.Start.Position.x, a.State.Position.x, 1e-3f, "only the height moves");
                    seen[2 * i] = dh;
                    seen[2 * i + 1] = dpsi;
                }
                return seen;
            }
            float[] a1 = Draws(5), a2 = Draws(5), b = Draws(6);
            CollectionAssert.AreEqual(a1, a2, "same seed, same respawns");
            CollectionAssert.AreNotEqual(a1, b);
            Assert.Greater(a1.Where((v, i) => i % 2 == 0).Max(), 200f, "the height spreads over its band");
            Assert.Less(a1.Where((v, i) => i % 2 == 0).Min(), -200f);
            Assert.Greater(a1.Where((v, i) => i % 2 == 1).Max(), 10f, "and the heading over its");
        }

        [Test]
        public void TwoJetsThatMeet_BothGoDown_AndWithoutTheRule_FlyThroughEachOther()
        {
            AircraftProfile e = E();
            (FlightCore core, Arena arena) Merge(float collisionM, float offsetM)
            {
                var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
                core.Add(AircraftFactory.Build(1, e, L0, Level(e, new Vector3(0f, 3048f, -1000f), 0f)), new TriggerSeat());
                core.Add(AircraftFactory.Build(2, e, L0, Level(e, new Vector3(offsetM, 3048f, 1000f), 180f)), new TriggerSeat());
                var arena = new Arena(Rules(collisionM: collisionM), core.Clock.TickSeconds, 1);
                core.UseArena(arena);
                for (int i = 0; i < 10 * 50; i++) core.Step();
                return (core, arena);
            }

            var hit = Merge(15f, 0f);
            Debug.Log($"[ARENA] head-on at the same height: {string.Join("; ", hit.arena.Events)}");
            Assert.AreEqual(2, hit.arena.Events.Count(ev => ev.Kind == ArenaEventKind.Collided));
            foreach (int id in new[] { 1, 2 })
            {
                Aircraft a = hit.core.Find(id);
                Assert.AreNotEqual(FlightStatus.Flying, a.State.Status, $"aircraft {id} went down");
                Assert.AreEqual(0, a.KilledBy, "nobody shot it");
                StringAssert.Contains($"collided with {3 - id}", a.CrashReason);
            }
            Assert.AreEqual(0, hit.core.Kills.Count, "a collision is not a kill");

            var off = Merge(15f, 40f);
            Assert.AreEqual(0, off.arena.Events.Count, "40 m apart: a near miss");
            var none = Merge(0f, 0f);
            Assert.AreEqual(0, none.arena.Events.Count);
            Assert.IsTrue(none.core.Aircraft.All(a => a.State.Status == FlightStatus.Flying), "with the rule off they fly through each other, as before the arena");
        }

        [Test]
        public void AJetThatLeavesTheBox_GoesDown_AndComesBackInside()
        {
            AircraftProfile e = E();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f));
            core.Add(AircraftFactory.Build(1, e, L0, Level(e, new Vector3(0f, 3048f, 19000f), 0f)), new TriggerSeat());
            var arena = new Arena(Rules(halfWidthM: 20000f), core.Clock.TickSeconds, 1);
            core.UseArena(arena);
            arena.Assign(1, core.Find(1).Start);
            while (arena.Events.Count == 0 && core.Tick < 20 * 50) core.Step();
            ArenaEvent left = arena.Events.Single();
            Assert.AreEqual(ArenaEventKind.LeftArena, left.Kind);
            Assert.AreEqual(FlightStatus.Killed, core.Find(1).State.Status);
            Assert.AreEqual("left the arena", core.Find(1).CrashReason);
            Assert.Greater(core.Find(1).State.Position.z, 20000f);
            for (int i = 0; i < 150; i++) core.Step();
            Assert.AreEqual(ArenaEventKind.Respawned, arena.Events.Last().Kind);
            Assert.AreEqual(FlightStatus.Flying, core.Find(1).State.Status);
            Assert.Less(core.Find(1).State.Position.z, 20000f, "back inside");
        }

        [Test]
        public void AFightWithAnArena_RepeatsTickForTick_AndRespawnsAreNotInputs()
        {
            AircraftProfile e = E();
            (FixLog fixes, string events, int entries, long ticks) Run()
            {
                var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: 3);
                // Four jets: two that will meet head on, one diving into the sea, one flying out of a small box.
                core.Add(AircraftFactory.Build(1, e, L0, Level(e, new Vector3(0f, 2000f, -800f), 0f)), new TriggerSeat());
                core.Add(AircraftFactory.Build(2, e, L0, Level(e, new Vector3(0f, 2000f, 800f), 180f)), new TriggerSeat());
                core.Add(AircraftFactory.Build(3, e, L0, Level(e, new Vector3(3000f, 400f, 0f), 90f)), new TriggerSeat { Pitch = -1f });
                core.Add(AircraftFactory.Build(4, e, L0, Level(e, new Vector3(-4000f, 2000f, 0f), 270f)), new TriggerSeat());
                var arena = new Arena(Rules(jitterM: 300f, jitterDeg: 15f, collisionM: 15f, halfWidthM: 5000f), core.Clock.TickSeconds, 77);
                core.UseArena(arena);
                foreach (Aircraft a in core.Aircraft) arena.Assign(a.Id, a.Start);
                for (int i = 0; i < 40 * 50; i++) core.Step();
                return (core.Fixes, string.Join("; ", arena.Events), core.Inputs.Count, core.Tick);
            }
            var r1 = Run();
            var r2 = Run();
            Debug.Log($"[ARENA] 40 s, four jets: {r1.events}");
            StringAssert.Contains("collided", r1.events);
            StringAssert.Contains("left the arena", r1.events);
            StringAssert.Contains("respawned", r1.events);
            Assert.AreEqual(-1, r1.fixes.FirstDivergence(r2.fixes), "the same arena and seeds: the same Fix on every tick");
            Assert.AreEqual(r1.events, r2.events);
            Assert.AreEqual(4 * r1.ticks, r1.entries, "one input entry per aircraft per tick: a respawn is not an input");
        }

        [Test]
        public void TheArena_RefusesNonsense()
        {
            Assert.Throws<ArgumentException>(() => new Arena(new ArenaSettings { RespawnDelayS = -1f }, 0.02, 1));
            Assert.Throws<ArgumentException>(() => new Arena(new ArenaSettings { HalfWidthXM = 100f }, 0.02, 1), "a box needs both half-widths");
            Assert.Throws<ArgumentException>(() => new Arena(new ArenaSettings { CollisionM = float.NaN }, 0.02, 1));
            var arena = new Arena(new ArenaSettings(), 0.02, 1);
            Assert.Throws<ArgumentException>(() => arena.Assign(1, new AirframeState { Status = FlightStatus.Crashed }));
        }

        [Test]
        public void TheClosestApproach_CatchesAPassBetweenTicks()
        {
            // Two jets closing at 800 m/s pass each other between the ends of a tick: the ends are 8 m apart each way.
            Assert.AreEqual(0f, Arena.ClosestApproachSq(new Vector3(0, 0, 0), new Vector3(0, 0, 8), new Vector3(0, 0, 8), new Vector3(0, 0, 0)), 1e-6f);
            Assert.AreEqual(25f, Arena.ClosestApproachSq(Vector3.zero, Vector3.zero, new Vector3(5, 0, 0), new Vector3(5, 0, 0)), 1e-6f, "standing still: the distance");
            Assert.AreEqual(9f, Arena.ClosestApproachSq(Vector3.zero, Vector3.zero, new Vector3(3, 0, -10), new Vector3(3, 0, 10)), 1e-4f, "passing by: the miss");
        }
    }
}
