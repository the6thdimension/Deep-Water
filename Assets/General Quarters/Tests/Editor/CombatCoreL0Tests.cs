using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.Scenario;
using DeepWater.GeneralQuarters.Core.World;
using Debug = UnityEngine.Debug;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>Slice 2: the combat core at L0, headless. Measurements are logged with a [GQ] tag.</summary>
    public class CombatCoreL0Tests
    {
        static readonly OwnShipState Own = new OwnShipState { Position = Vector3.zero, Velocity = Vector3.zero, HeadingDeg = 0f };

        static TruthTarget Target(int id, TargetKind kind, Identity identity, Vector3 position, Vector3 velocity) =>
            new TruthTarget { Id = id, Kind = kind, Identity = identity, Position = position, Velocity = velocity, Alive = true };

        // ── Radar L0 ──────────────────────────────────────────────────────

        [Test]
        public void RadarL0_SeesOnlyAboveTheHorizon_AndWithinInstrumentedRange()
        {
            var radar = new RadarL0(new RadarProfileData { InstrumentedRangeM = 185_000f, AntennaHeightM = 20f, SweepPeriodTicks = 1 });
            float horizon = RadarMath.HorizonM(20f, 5f);   // 4 120 × (√20 + √5) ≈ 27.6 km
            var world = new List<TruthTarget>
            {
                Target(1, TargetKind.AntiShipMissile, Identity.Hostile, new Vector3(0f, 5f, horizon - 200f), Vector3.zero),
                Target(2, TargetKind.AntiShipMissile, Identity.Hostile, new Vector3(0f, 5f, horizon + 200f), Vector3.zero),
                Target(3, TargetKind.Aircraft, Identity.Hostile, new Vector3(0f, 6000f, 150_000f), Vector3.zero),
                Target(4, TargetKind.Aircraft, Identity.Hostile, new Vector3(0f, 12000f, 190_000f), Vector3.zero),
            };
            var seen = new List<Detection>();
            radar.Sweep(Own, world, seen);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, seen.Select(d => d.TargetId).ToArray());
            Debug.Log($"[GQ] radar horizon for a 5 m sea-skimmer from a 20 m antenna: {horizon / 1000f:0.0} km");
        }

        // ── Tracks L0 ─────────────────────────────────────────────────────

        [Test]
        public void TracksL0_KeepStableIds_AndDropAfterSilence()
        {
            var log = new EngagementLog();
            var tracks = new TrackManagerL0(new TrackProfileData { DropAfterTicks = 10 });
            var d = new List<Detection> { new Detection { TargetId = 77, Kind = TargetKind.Aircraft, Identity = Identity.Hostile, Position = Vector3.one } };
            tracks.Update(0, d, log);
            tracks.Update(5, d, log);
            Assert.AreEqual(1, tracks.Tracks.Count);
            Assert.IsTrue(tracks.TryGetByTruth(77, out Track t));
            Assert.AreEqual(1, t.Id);
            tracks.Update(16, new List<Detection>(), log);
            Assert.IsFalse(tracks.Tracks[0].Active, "dropped after more than 10 silent ticks");
            Assert.AreEqual(1, log.Count(EventKind.TrackCreated));
            Assert.AreEqual(1, log.Count(EventKind.TrackDropped));
        }

        // ── Threat ranking and ROE ────────────────────────────────────────

        [Test]
        public void ThreatRanking_OrdersByTimeToCpa_AndObeysRoe()
        {
            var log = new EngagementLog();
            var tracks = new TrackManagerL0(new TrackProfileData { DropAfterTicks = 100 });
            tracks.Update(0, new List<Detection>
            {
                new Detection { TargetId = 1, Kind = TargetKind.AntiShipMissile, Identity = Identity.Hostile, Position = new Vector3(0, 5, 20_000), Velocity = new Vector3(0, 0, -290) },
                new Detection { TargetId = 2, Kind = TargetKind.AntiShipMissile, Identity = Identity.Hostile, Position = new Vector3(10_000, 5, 0), Velocity = new Vector3(-290, 0, 0) },
                new Detection { TargetId = 3, Kind = TargetKind.Aircraft, Identity = Identity.Unknown, Position = new Vector3(0, 3000, -8_000), Velocity = new Vector3(0, 0, 200) },
                new Detection { TargetId = 4, Kind = TargetKind.Aircraft, Identity = Identity.Friend, Position = new Vector3(5_000, 3000, 0), Velocity = new Vector3(-200, 0, 0) },
                new Detection { TargetId = 5, Kind = TargetKind.AntiShipMissile, Identity = Identity.Hostile, Position = new Vector3(-5_000, 5, 0), Velocity = new Vector3(-290, 0, 0) }, // opening
            }, log);
            var doctrine = new DoctrineState();
            var ranked = new List<RankedThreat>();

            doctrine.SetRoe(Roe.WeaponsFree, -1);
            ThreatRanking.Rank(0, 0.02f, Own, tracks, doctrine, 5000f, false, ranked);
            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, ranked.Select(r => r.Track.TruthId).ToArray(),
                "free: nearest CPA time first (34 s, 40 s, 69 s); never the friend; never an opening track");

            doctrine.SetRoe(Roe.WeaponsTight, -1);
            ThreatRanking.Rank(0, 0.02f, Own, tracks, doctrine, 5000f, false, ranked);
            CollectionAssert.AreEqual(new[] { 2, 1 }, ranked.Select(r => r.Track.TruthId).ToArray(), "tight: hostiles only");

            doctrine.SetRoe(Roe.WeaponsHold, -1);
            ThreatRanking.Rank(0, 0.02f, Own, tracks, doctrine, 5000f, false, ranked);
            Assert.IsEmpty(ranked, "hold: nothing outside self-defence");
            ThreatRanking.Rank(0, 0.02f, Own, tracks, doctrine, 5000f, true, ranked);
            CollectionAssert.AreEqual(new[] { 2, 1 }, ranked.Select(r => r.Track.TruthId).ToArray(), "hold: self-defence weapons still see hostiles");
        }

        // ── Intercept geometry ────────────────────────────────────────────

        [Test]
        public void Intercept_CatchesAPassBetweenTicks()
        {
            // Head-on at 1 300 m/s closing: 26 m per 20 ms step. The ends of the step are 13 m apart
            // either side, but the pass itself comes within 2 m.
            float d = Intercept.MinDistance(new Vector3(2f, 0f, -13f), new Vector3(2f, 0f, 7f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, -6f), out float at);
            Assert.AreEqual(2f, d, 1e-3f);
            Assert.That(at, Is.InRange(0.4f, 0.6f));
        }

        // ── Doctrine modes ────────────────────────────────────────────────

        [TestCase(DoctrineMode.Auto, 1, 0)]
        [TestCase(DoctrineMode.SemiAuto, 0, 1)]
        [TestCase(DoctrineMode.Manual, 0, 0)]
        public void Tewa_AssignsOnlyAsDoctrineAllows(DoctrineMode mode, int launches, int proposals)
        {
            CombatLoadout loadout = TestLoadouts.Ddg51L0();
            loadout.DefaultDoctrine = new[] { new DoctrineDefault { Weapon = WeaponClass.Essm, Mode = mode } };
            var cs = new CombatSystem(loadout);
            var world = new List<TruthTarget> { Target(1, TargetKind.AntiShipMissile, Identity.Hostile, new Vector3(0, 5, 20_000), new Vector3(0, 0, -290)) };
            var kills = new List<KillReport>();
            for (int i = 0; i < 10; i++) cs.Step(Own, world, new List<ShipHit>(), kills);
            Assert.AreEqual(launches, cs.Log.Count(EventKind.Launched), $"{mode}: launches");
            Assert.AreEqual(proposals, cs.Log.Count(EventKind.Proposed), $"{mode}: proposals");

            if (mode == DoctrineMode.SemiAuto)
            {
                // The TAO confirms the proposal: an EngageOrder fires it.
                cs.Issue(Order.Engage(OrderIssuer.Tao, cs.Tracks.Tracks[0].Id, WeaponClass.Essm));
                cs.Step(Own, world, new List<ShipHit>(), kills);
                Assert.AreEqual(1, cs.Log.Count(EventKind.Launched), "the TAO's order launched");
                int launch = cs.Log.Events.Last(e => e.Kind == EventKind.Launched).Id;
                Assert.IsTrue(cs.Log.Chain(launch).Any(e => e.Root == RootKind.Order), "the launch traces back to the TAO's Order");
            }
        }

        // ── Directed gunfire and cease fire ───────────────────────────────

        [Test]
        public void Orders_EngageAndCeaseFire_DriveAGunInManual()
        {
            CombatLoadout loadout = TestLoadouts.Ddg51L0();
            loadout.DefaultDoctrine = new[] { new DoctrineDefault { Weapon = WeaponClass.Mk38, Mode = DoctrineMode.Manual } };
            // This test is about Orders, not lethality: a lucky first burst must not end the engagement early.
            loadout.Guns[2].PkPerBurstAtMinRange = 0f;
            loadout.Guns[2].PkPerBurstAtMaxRange = 0f;
            var cs = new CombatSystem(loadout);
            // A surface contact 1.5 km off the port beam, inside the port Mk 38's arc and range.
            var world = new List<TruthTarget> { Target(9, TargetKind.SurfaceShip, Identity.Hostile, new Vector3(-1500, 0, 0), Vector3.zero) };
            var kills = new List<KillReport>();
            for (int i = 0; i < 30; i++) cs.Step(Own, world, new List<ShipHit>(), kills);
            Assert.AreEqual(0, cs.Log.Count(EventKind.BurstFired), "manual: the Mk 38 holds fire without an order");

            cs.Issue(Order.Engage(OrderIssuer.Tao, cs.Tracks.Tracks[0].Id, WeaponClass.Mk38, rounds: 2));
            for (int i = 0; i < 300; i++) cs.Step(Own, world, new List<ShipHit>(), kills);
            Assert.AreEqual(2, cs.Log.Count(EventKind.BurstFired), "exactly the two bursts ordered");
        }

        // ── Damage Control L0 ─────────────────────────────────────────────

        [Test]
        public void DamageL0_AHitTakesTheNearestSystemOffline_AndStopsIt()
        {
            var cs = new CombatSystem(TestLoadouts.Ddg51L0());
            var hit = new ShipHit { ThreatId = 5, Kind = TargetKind.AntiShipMissile, Point = new Vector3(0f, 12f, -26f), WarheadKg = 220f };
            cs.Step(Own, new List<TruthTarget>(), new List<ShipHit> { hit }, new List<KillReport>());
            Assert.IsFalse(cs.DamageControl.IsOnline(ShipSystem.Ciws));
            Assert.IsFalse(cs.Guns.First(g => g.Profile.Weapon == WeaponClass.Ciws).Online, "the CIWS mount went offline with its system");
            Assert.AreEqual(780f, cs.DamageControl.HullHitPoints, 1e-3f);
            Assert.AreEqual(1, cs.Log.Count(EventKind.SystemOffline));
        }

        // ── The whole engagement ──────────────────────────────────────────

        static (List<ulong> fixes, CombatSystem cs, ScriptedRaid raid, double msPerTick) RunRaid(int threats, ulong seed, int maxTicks = 9000)
        {
            var cs = new CombatSystem(TestLoadouts.Ddg51L0(seed));
            var raid = new ScriptedRaid(TestLoadouts.SeaSkimmerRaid(threats), Own);
            var truth = new List<TruthTarget>();
            var hits = new List<ShipHit>();
            var kills = new List<KillReport>();
            var fixes = new List<ulong>();
            var watch = new Stopwatch();
            for (int i = 0; i < maxTicks; i++)
            {
                hits.Clear();
                raid.Step(cs.Tick, cs.TickSeconds, Own, truth, hits);
                kills.Clear();
                watch.Start();
                cs.Step(Own, truth, hits, kills);
                watch.Stop();
                raid.ApplyKills(kills);
                fixes.Add(cs.Fix);
                if (raid.Resolved && cs.Vls.InFlight.Count == 0) break;
            }
            return (fixes, cs, raid, watch.Elapsed.TotalMilliseconds / fixes.Count);
        }

        [Test]
        public void RaidL0_PlaysEndToEnd_ExplainsEveryKill_AndRepeatsExactly()
        {
            var (fixesA, cs, raid, ms) = RunRaid(8, 1442);
            var (fixesB, _, _, _) = RunRaid(8, 1442);

            int kills = cs.Log.Count(EventKind.Kill);
            List<EngagementEvent> killEvents = cs.Log.Events.Where(e => e.Kind == EventKind.Kill).ToList();
            Debug.Log($"[GQ] raid L0, 8 sea-skimmers from 40 km: {raid.Killed} killed, {raid.Leaked} hit the ship, " +
                      $"{cs.Log.Count(EventKind.Launched)} ESSM launched, {cs.Log.Count(EventKind.BurstFired)} gun bursts, " +
                      $"{fixesA.Count} ticks ({fixesA.Count * 0.02f:0.0} s), hull {cs.DamageControl.HullHitPoints:0}/1000, {ms:0.000} ms/tick");
            foreach (EngagementEvent e in killEvents.Take(3))
                Debug.Log("[GQ] why: " + string.Join("  ←  ", cs.Log.Chain(e.Id).Select(c => c.Text)));

            Assert.IsTrue(raid.Resolved, "every threat was killed or hit the ship");
            Assert.AreEqual(8, raid.Killed + raid.Leaked);
            Assert.AreEqual(raid.Killed, kills, "every kill the world applied is in the log");
            foreach (EngagementEvent e in killEvents)
            {
                Assert.IsTrue(cs.Log.IsComplete(e.Id), $"kill {e.Id} has a chain ending in roots: {e.Text}");
                Assert.AreEqual(RootKind.Chance, e.Root, "every kill names its luck");
                Assert.IsTrue(cs.Log.Chain(e.Id).Any(c => c.Kind == EventKind.TrackCreated), "every kill traces to a track");
            }
            CollectionAssert.AreEqual(fixesA, fixesB, "same seed, same Orders, same truth: identical Fix every tick");

            var (fixesC, _, _, _) = RunRaid(8, 7);
            Assert.AreNotEqual(fixesA.Last(), fixesC.Last(), "a different seed changes the luck, and the Fix shows it");
        }

        [Test]
        public void RaidL0_StaysInsideTheTickBudget_At24Threats()
        {
            RunRaid(24, 1442, 1000);   // warm-up: JIT, Burst-free paths, allocations
            var (fixes, cs, raid, ms) = RunRaid(24, 1442);
            Debug.Log($"[GQ] raid L0, 24 sea-skimmers: {raid.Killed} killed, {raid.Leaked} hit; {ms:0.000} ms/tick over {fixes.Count} ticks (Editor); budget 1 ms (G16)");
            Assert.LessOrEqual(ms, 1.0, "combat-core time per fixed step");
        }
    }
}
