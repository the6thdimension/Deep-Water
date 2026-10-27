using System.Collections.Generic;
using DeepWater.Gunnery.Ballistics;
using DeepWater.Gunnery.Guns;
using NUnit.Framework;
using RH.Shared.Contracts;
using UnityEngine;

namespace DeepWater.Gunnery.Tests
{
    /// <summary>The aircraft gun's model (K4): round flight, the hit test, the lead sight, determinism.</summary>
    public class GunTests
    {
        /// <summary>A 20 mm round of the M61A2's class (the numbers here are only test data).</summary>
        static GunProfile Gun(float formFactor = 1f, float dispersionMils = 0f) => new GunProfile
        {
            Name = "test 20 mm",
            Round = new ProjectileData { MassKg = 0.1f, DiameterM = 0.02f, MuzzleVelocityMps = 1050f, FormFactor = formFactor },
            RoundsPerMinute = 6000f,
            RoundsCarried = 412,
            DispersionMils = dispersionMils,
            MaxTimeOfFlightS = 3f,
        };

        /// <summary>SplitMix64, as the vehicles' generators are.</summary>
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

        static GunTarget Still(int id, Vector3 at, float radius) => new GunTarget { Id = id, From = at, To = at, RadiusM = radius };

        [Test]
        public void InAVacuum_ARoundDropsHalfGTSquared()
        {
            Vector3 p = new Vector3(0f, 5000f, 0f), v = new Vector3(0f, 0f, 1000f);
            for (int i = 0; i < 100; i++) RoundFlight.Step(ref p, ref v, 0f, 0.02f);
            float drop = 5000f - p.y, expected = 0.5f * FiringTable.Gravity * 2f * 2f;
            Assert.AreEqual(expected, drop, 1e-2f, "drop after 2 s");
            Assert.AreEqual(2000f, p.z, 1e-2f, "no drag, no slowing");
        }

        [Test]
        public void InAir_TheRoundFliesAsTheFiringTableFliesIt()
        {
            // The same physics as the ships' firing table (K1): a 3-D round fired at 10° matches the table's 2-D shot.
            GunProfile g = Gun();
            var xs = new List<float>();
            var ys = new List<float>();
            FiringTable.Fly(g.Round, 10f, xs, ys);
            float e = 10f * Mathf.Deg2Rad;
            Vector3 p = Vector3.zero, v = new Vector3(0f, Mathf.Sin(e), Mathf.Cos(e)) * g.Round.MuzzleVelocityMps;
            float k = RoundFlight.DragConstant(g.Round);
            for (int i = 0; i < 100; i++) RoundFlight.Step(ref p, ref v, k, 0.02f);   // 2 s = table sample 20
            Debug.Log(FormattableString($"[Gunnery] at 2 s: table ({xs[20]:F1}, {ys[20]:F1}) m, round ({p.z:F1}, {p.y:F1}) m"));
            Assert.AreEqual(xs[20], p.z, 1f, "downrange");
            Assert.AreEqual(ys[20], p.y, 1f, "height");
            Assert.Less(p.z, 2f * 1050f * Mathf.Cos(e), "drag slowed it");
        }

        [Test]
        public void AVolleyOnTarget_Hits_AndOneWellWide_Misses()
        {
            GunProfile g = Gun();
            var rounds = new RoundsInFlight();
            var hits = new List<HitReport>();
            var targets = new List<GunTarget> { Still(2, new Vector3(0f, 1000f, 500f), 5f) };
            // Aimed a little high, to allow for the drop over 500 m.
            rounds.Fire(1, 0, g, new Vector3(0f, 1000f, 0f), Vector3.zero, new Vector3(0f, 0.0025f, 1f), 3);
            for (long t = 1; t <= 50; t++) rounds.Step(t, 0.02f, targets, new Draws(7), hits);
            Assert.AreEqual(1, hits.Count, "one hit report");
            Assert.AreEqual(3, hits[0].Rounds, "with no dispersion every round in the group hits");
            Assert.AreEqual(1, hits[0].ShooterId);
            Assert.AreEqual(2, hits[0].TargetId);
            Assert.AreEqual(0, rounds.Live.Count, "a volley whose rounds all hit is gone");

            hits.Clear();
            rounds.Fire(1, 0, g, new Vector3(20f, 1000f, 0f), Vector3.zero, new Vector3(0f, 0.0025f, 1f), 3);
            for (long t = 1; t <= 150; t++) rounds.Step(t, 0.02f, targets, new Draws(7), hits);
            Assert.AreEqual(0, hits.Count, "20 m wide of a 5 m target misses");
        }

        [Test]
        public void TheShooter_CannotHitItself()
        {
            GunProfile g = Gun();
            var rounds = new RoundsInFlight();
            var hits = new List<HitReport>();
            var targets = new List<GunTarget> { new GunTarget { Id = 1, From = Vector3.zero, To = new Vector3(0f, 0f, 4f), RadiusM = 8f } };
            rounds.Fire(1, 0, g, Vector3.zero, new Vector3(0f, 0f, 200f), Vector3.forward, 2);
            for (long t = 1; t <= 5; t++) rounds.Step(t, 0.02f, targets, new Draws(1), hits);
            Assert.AreEqual(0, hits.Count);
        }

        [Test]
        public void WithDispersion_SomeRoundsHit_AndTheSameSeedHitsTheSameWay()
        {
            GunProfile g = Gun(dispersionMils: 8f);
            int Run(ulong seed, out long fired)
            {
                var rounds = new RoundsInFlight();
                var hits = new List<HitReport>();
                var draws = new Draws(seed);
                var targets = new List<GunTarget> { Still(2, new Vector3(0f, 1000f, 600f), 5f) };
                for (long t = 0; t < 100; t++)
                {
                    rounds.Step(t, 0.02f, targets, draws, hits);
                    if (t < 50) rounds.Fire(1, t, g, new Vector3(0f, 1000f, 0f), Vector3.zero, new Vector3(0f, 0.003f, 1f), 2);
                }
                fired = rounds.RoundsFired;
                int n = 0;
                foreach (HitReport h in hits) n += h.Rounds;
                return n;
            }
            int a = Run(42, out long firedA), b = Run(42, out _), c = Run(43, out _);
            Debug.Log(FormattableString($"[Gunnery] 8 mil dispersion at 600 m, 5 m target: {a} of {firedA} rounds hit (seed 42), {c} (seed 43)"));
            Assert.Greater(a, 0, "some hit");
            Assert.Less(a, firedA, "not all hit: at 600 m, 8 mils is 4.8 m of spread");
            Assert.AreEqual(a, b, "the same seed gives the same hits");
        }

        [Test]
        public void ThePipper_IsWhereTheRoundIs_AtThatTimeOfFlight()
        {
            GunProfile g = Gun();
            Vector3 muzzle = new Vector3(0f, 3000f, 0f), shooter = new Vector3(0f, 0f, 200f), bore = new Vector3(0.1f, 0.05f, 1f);
            Assert.IsTrue(LeadSight.RoundAt(g, muzzle, shooter, bore, 800f, out Vector3 pipper, out float tof));
            Assert.AreEqual(800f, (pipper - muzzle).magnitude, 0.5f, "the pipper is at the range asked");

            var rounds = new RoundsInFlight();
            rounds.Fire(1, 0, g, muzzle, shooter, bore, 1);
            int steps = Mathf.RoundToInt(tof / 0.02f);
            for (int i = 0; i < steps; i++) rounds.Step(i, 0.02f, new List<GunTarget>(), new Draws(1), new List<HitReport>());
            Assert.AreEqual(0f, (rounds.Live[0].Position - pipper).magnitude, 25f, "a real round passes the pipper (within one step of flight)");
        }

        [Test]
        public void TheMissDistance_IsSmall_WhenLeadingAStraightTarget_AndLargeWhenNot()
        {
            GunProfile g = Gun();
            Vector3 muzzle = new Vector3(0f, 3000f, 0f), shooter = new Vector3(0f, 0f, 200f);
            Vector3 target = new Vector3(0f, 3000f, 600f), crossing = new Vector3(150f, 0f, 200f);
            // Find the lead: aim where the target will be after the time of flight, with the drop allowed for.
            // The round is flown to where the target will be (its range then), as MissDistance does.
            Vector3 aim = target, later = target;
            for (int i = 0; i < 8; i++)
            {
                LeadSight.RoundAt(g, muzzle, shooter, aim - muzzle, (later - muzzle).magnitude, out Vector3 at, out float tof);
                later = target + crossing * tof;
                aim += later - at;
            }
            float led = LeadSight.MissDistance(g, muzzle, shooter, aim - muzzle, target, crossing);
            float pure = LeadSight.MissDistance(g, muzzle, shooter, target - muzzle, target, crossing);
            Debug.Log(FormattableString($"[Gunnery] crossing target at 600 m: miss {led:F2} m leading, {pure:F1} m pointing at it"));
            Assert.Less(led, 1f, "leading it");
            Assert.Greater(pure, 50f, "pointing straight at a crossing target misses by its motion over the time of flight");
        }

        static string FormattableString(System.FormattableString s) => System.FormattableString.Invariant(s);
    }
}
