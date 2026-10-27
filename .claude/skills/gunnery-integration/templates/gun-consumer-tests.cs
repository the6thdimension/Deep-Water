// TEMPLATE: EditMode tests for a new Gunnery consumer (a vehicle module that carries a gun).
// Copy to Assets/<Module>/Tests/Editor/<Vehicle>GunTests.cs. The test asmdef references DeepWater.Gunnery and RH.Shared
// (as Assets/Air Wing/Tests/Editor/DeepWater.AirWing.Tests.Editor.asmdef does).
// Modelled on Assets/Air Wing/Tests/Editor/GunTests.cs (3e34e232) and Assets/Gunnery/Tests/Editor/GunTests.cs.
//
// ExampleGunStage stands in for your vehicle core's guns stage (Air Wing: FlightCore.Guns, FlightCore.cs:150). Swap it
// for your core and your baked profile asset, and keep the tests: they pin the seam, not the vehicle. <REPLACE> marks
// the lines to change. As written it compiles against DeepWater.Gunnery, RH.Shared, NUnit and UnityEngine. Checked
// 2026-10-27 outside the Editor: built on .NET 8 against Unity 6000.4.11f1's UnityEngine.CoreModule with Debug.Log
// swapped for Console, and all eight tests passed. Not yet run in the Unity Test Runner.
using System.Collections.Generic;
using DeepWater.Gunnery.Ballistics;
using DeepWater.Gunnery.Guns;
using NUnit.Framework;
using RH.Shared.Contracts;
using UnityEngine;

namespace DeepWater.Example.Tests   // <REPLACE> your module's test namespace
{
    /// <summary>The gun in the vehicle's tick: rate, laying, hits into hit points, tick order, determinism, the pipper.</summary>
    public class ExampleGunConsumerTests   // <REPLACE> <Vehicle>GunTests
    {
        const float Dt = 0.02f;   // <REPLACE> your core's tick, s

        /// <summary><REPLACE> with your baked profile asset (Air Wing: AircraftProfileSO.Bake().Gun.Gun). Test data only.</summary>
        static GunProfile Profile(float dispersionMils = 2f) => new GunProfile
        {
            Name = "test gun",
            Round = new ProjectileData { MassKg = 0.1f, DiameterM = 0.02f, MuzzleVelocityMps = 1050f, FormFactor = 1f },
            RoundsPerMinute = 4500f,   // 1.5 rounds a tick at 50 Hz: exercises the carry
            RoundsCarried = 300,
            DispersionMils = dispersionMils,
            MaxTimeOfFlightS = 4f,
        };

        /// <summary>SplitMix64, as the vehicles' generators are. <REPLACE> with your core's seeded RNG (it implements IUniformSource).</summary>
        sealed class Draws : IUniformSource
        {
            ulong _s;
            public Draws(ulong seed) { _s = seed; }
            public float NextFloat01()
            {
                ulong z = _s += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return (z >> 40) * (1f / 16777216f);
            }
        }

        sealed class Target { public int Id; public Vector3 Was, At; public float RadiusM, HitPoints; }

        /// <summary>
        /// The stand-in consumer. The vehicle owns the mount (where it sits, where it points, its rates and limits), the fire
        /// command, the hit points and the tick (Gunnery K1, K3); Gunnery flies the rounds. Order per tick, as Air Wing's:
        /// slew; rounds already in flight fly and hit; hit reports take hit points; then this tick's rounds leave.
        /// </summary>
        sealed class ExampleGunStage
        {
            public readonly RoundsInFlight Rounds = new RoundsInFlight();
            public readonly List<HitReport> Hits = new List<HitReport>();
            public readonly List<Target> Targets = new List<Target>();
            readonly List<GunTarget> _gunTargets = new List<GunTarget>();
            readonly List<HitReport> _tickHits = new List<HitReport>();
            readonly IUniformSource _draws;
            public readonly GunProfile Gun;
            public long Tick;
            public int ShooterId = 1, RoundsLeft;
            public float Carry;
            public Vector3 Muzzle = new Vector3(0f, 10f, 0f), Velocity;
            public float TrainDeg, ElevationDeg;
            public float TrainRateDps = 60f, ElevateRateDps = 30f, ElevationMinDeg = -5f, ElevationMaxDeg = 60f, LayToleranceDeg = 0.5f;

            public ExampleGunStage(in GunProfile gun, ulong seed)
            {
                gun.Validate();
                Gun = gun;
                RoundsLeft = gun.RoundsCarried;
                _draws = new Draws(seed);
            }

            /// <summary>The bore: train from +z toward +x, elevation up. A real vehicle rotates its body-frame bore by its attitude.</summary>
            public static Vector3 Bore(float trainDeg, float elevationDeg)
            {
                float t = trainDeg * Mathf.Deg2Rad, e = elevationDeg * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(t) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(t) * Mathf.Cos(e));
            }

            public void Step(bool trigger, float orderTrainDeg, float orderElevationDeg)
            {
                TrainDeg = Mathf.MoveTowards(TrainDeg, orderTrainDeg, TrainRateDps * Dt);
                ElevationDeg = Mathf.MoveTowards(ElevationDeg, Mathf.Clamp(orderElevationDeg, ElevationMinDeg, ElevationMaxDeg), ElevateRateDps * Dt);

                _gunTargets.Clear();
                foreach (Target t in Targets)
                    if (t.HitPoints > 0f) _gunTargets.Add(new GunTarget { Id = t.Id, From = t.Was, To = t.At, RadiusM = t.RadiusM });
                _tickHits.Clear();
                Rounds.Step(Tick, Dt, _gunTargets, _draws, _tickHits);
                foreach (HitReport h in _tickHits)
                {
                    Hits.Add(h);
                    Target t = Targets.Find(x => x.Id == h.TargetId);
                    if (t.HitPoints > 0f) t.HitPoints = Mathf.Max(0f, t.HitPoints - h.Rounds);   // one hit point a round, abstract
                }

                bool laid = Mathf.Abs(TrainDeg - orderTrainDeg) <= LayToleranceDeg && Mathf.Abs(ElevationDeg - orderElevationDeg) <= LayToleranceDeg;
                if (!trigger || !laid || RoundsLeft <= 0) { Carry = 0f; Tick++; return; }   // a burst starts from a whole round
                Carry += Gun.RoundsPerMinute / 60f * Dt;
                int n = Mathf.Min((int)Carry, RoundsLeft);
                Carry -= (int)Carry;
                RoundsLeft -= n;
                Rounds.Fire(ShooterId, Tick, Gun, Muzzle, Velocity, Bore(TrainDeg, ElevationDeg), n);
                Tick++;
            }

            /// <summary>Move every target on by its velocity over one tick (the world's job, before the vehicle's tick).</summary>
            public void MoveTargets(Vector3 velocity)
            {
                foreach (Target t in Targets) { t.Was = t.At; t.At += velocity * Dt; }
            }
        }

        /// <summary>A stationary target where the gun's rounds will be at <paramref name="rangeM"/>, from a laid mount at rest.</summary>
        static ExampleGunStage Range(float rangeM, ulong seed = 7, float dispersionMils = 2f, float radiusM = 4f)
        {
            var stage = new ExampleGunStage(Profile(dispersionMils), seed);
            Assert.IsTrue(LeadSight.RoundAt(stage.Gun, stage.Muzzle, stage.Velocity, ExampleGunStage.Bore(0f, 0f), rangeM, out Vector3 at, out _));
            stage.Targets.Add(new Target { Id = 2, Was = at, At = at, RadiusM = radiusM, HitPoints = 10f });
            return stage;
        }

        [Test]
        public void TheProfile_Validates_AndCarriesItsCitedNumbers()
        {
            GunProfile g = Profile();
            Assert.DoesNotThrow(() => g.Validate());
            Assert.AreEqual(300, g.RoundsCarried, "<REPLACE> with a PUBLIC figure and its source, as Air Wing's TheF18E_CarriesTheM61A2_AndHitPoints does");
            GunProfile empty = g;
            empty.RoundsCarried = 0;
            Assert.Throws<System.ArgumentException>(() => empty.Validate(), "a gun with no rounds refuses to build: no silent fallback");
        }

        [Test]
        public void HoldingFire_FiresAtTheGunsRate_AndLettingGo_DropsTheCarry()
        {
            ExampleGunStage s = Range(400f);
            for (int i = 0; i < 50; i++) s.Step(true, 0f, 0f);   // one second
            Assert.AreEqual(75, s.Rounds.RoundsFired, "4,500 a minute is 75 in a second, carried across ticks");
            Assert.AreEqual(225, s.RoundsLeft);
            s.Step(false, 0f, 0f);
            Assert.AreEqual(0f, s.Carry, "a released trigger starts the next burst on a whole round");
        }

        [Test]
        public void TheMount_FiresOnlyOnceLaid()
        {
            ExampleGunStage s = Range(400f);
            long first = -1;
            for (int i = 0; i < 200 && first < 0; i++)
            {
                s.Step(true, 90f, 0f);
                if (s.Rounds.RoundsFired > 0) first = s.Tick - 1;
            }
            // Ticks count from 0, so the step that brings it within the tolerance (the 75th here) is tick 74.
            long laidOnTick = Mathf.CeilToInt((90f - s.LayToleranceDeg) / s.TrainRateDps / Dt) - 1;
            Debug.Log($"[Gun consumer] laid on tick {laidOnTick}; first round on tick {first}");
            Assert.GreaterOrEqual(first, laidOnTick, "it cannot fire before it has trained round");
            Assert.LessOrEqual(first, laidOnTick + 1, "and fires as soon as it is laid");
        }

        [Test]
        public void ABurstOnTheTarget_TakesHitPoints_AndAMiss_TakesNone()
        {
            ExampleGunStage s = Range(400f);
            for (int i = 0; i < 100 && s.Targets[0].HitPoints > 0f; i++) s.Step(i < 10, 0f, 0f);
            Debug.Log($"[Gun consumer] {s.Rounds.RoundsFired} fired, {s.Rounds.RoundsHit} hit; HP {s.Targets[0].HitPoints}");
            Assert.Greater(s.Hits.Count, 0, "rounds that pass through the target hit it");
            Assert.AreEqual(1, s.Hits[0].ShooterId);
            Assert.AreEqual(2, s.Hits[0].TargetId);
            Assert.AreEqual(Mathf.Max(0f, 10f - s.Rounds.RoundsHit), s.Targets[0].HitPoints, 1e-4f, "one hit point a round, through the hit report");

            ExampleGunStage wide = Range(400f);
            wide.Targets[0].At = wide.Targets[0].Was = wide.Targets[0].At + new Vector3(60f, 0f, 0f);
            for (int i = 0; i < 100; i++) wide.Step(i < 10, 0f, 0f);
            Assert.AreEqual(0, wide.Hits.Count, "60 m wide of a 4 m target misses");
            Assert.AreEqual(10f, wide.Targets[0].HitPoints);
        }

        [Test]
        public void RoundsFiredThisTick_FlyFromTheNext()
        {
            ExampleGunStage s = Range(400f);
            s.Step(true, 0f, 0f);
            Assert.AreEqual(1, s.Rounds.Live.Count, "one volley per gun per tick");
            Assert.AreEqual(0f, s.Rounds.Live[0].AgeS, "it has not flown on the tick it left, so firing order cannot decide who is hit");
            Assert.AreEqual(s.Muzzle, s.Rounds.Live[0].Position);
        }

        [Test]
        public void TwoRuns_WithTheSameSeed_HitTheSameWay_TickForTick()
        {
            // Wide dispersion and a small target, so the seed decides some hits and not all.
            List<string> Run(ulong seed)
            {
                ExampleGunStage s = Range(600f, seed, dispersionMils: 8f, radiusM: 2f);
                s.Targets[0].HitPoints = 1000f;
                var log = new List<string>();
                for (int i = 0; i < 150; i++)
                {
                    s.MoveTargets(Vector3.zero);
                    s.Step(i < 100, 0f, 0f);
                    foreach (HitReport h in s.Hits) log.Add(h.ToString());
                    s.Hits.Clear();
                }
                Assert.Greater(s.Rounds.RoundsHit, 0, "some hit");
                Assert.Less(s.Rounds.RoundsHit, s.Rounds.RoundsFired, "not all hit: the draws decide");
                return log;
            }
            List<string> a = Run(42), b = Run(42);
            Debug.Log($"[Gun consumer] seed 42: {a.Count} hit reports; seed 43: {Run(43).Count}");
            CollectionAssert.AreEqual(a, b, "same seed, same orders: the same hits on the same ticks");
        }

        [Test]
        public void ThePipper_AgreesWithTheRounds_AtThisConsumersTick()
        {
            // LeadSight flies 0.02 s steps; the rounds fly your tick. A real round must pass the pipper within one step of flight.
            ExampleGunStage s = Range(400f);
            s.Velocity = new Vector3(0f, 0f, 50f);
            Vector3 bore = ExampleGunStage.Bore(10f, 5f);
            Assert.IsTrue(LeadSight.RoundAt(s.Gun, s.Muzzle, s.Velocity, bore, 800f, out Vector3 pipper, out float tof));
            s.Rounds.Fire(1, 0, s.Gun, s.Muzzle, s.Velocity, bore, 1);
            int steps = Mathf.RoundToInt(tof / Dt);
            for (int i = 0; i < steps; i++) s.Rounds.Step(i, Dt, new List<GunTarget>(), new Draws(1), new List<HitReport>());
            float apart = (s.Rounds.Live[0].Position - pipper).magnitude;
            Debug.Log($"[Gun consumer] at 800 m the round is {apart:F2} m from the pipper");
            Assert.LessOrEqual(apart, s.Gun.Round.MuzzleVelocityMps * Dt, "within one step of flight");
        }

        /// <summary>Keep only if your fire control looks up the firing table (FiringTable.TrySolve), as General Quarters' does (AP21).</summary>
        [Test]
        public void TheFiringTable_SolvesEveryRangeInReach_NoFlicker()
        {
            GunProfile g = Profile();
            FiringTable t = FiringTable.For(g.Round, -5f, 60f);   // <REPLACE> your mount's elevation limits
            int checkedPoints = 0;
            for (float x = 500f; x <= 0.95f * t.MaxRangeM; x += 25f, checkedPoints++)
                Assert.IsTrue(t.TrySolve(x, -2.7f, out _, out _), $"a target 2.7 m below the muzzle at {x} m is in reach");
            Debug.Log($"[Gun consumer] firing table: greatest range {t.MaxRangeM:F0} m at {t.MaxRangeElevationDeg:F1} deg; {checkedPoints} ranges solved");
            Assert.IsFalse(t.TrySolve(1.2f * t.MaxRangeM, 0f, out _, out _), "beyond the greatest range");
        }
    }
}
