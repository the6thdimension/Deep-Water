using System;
using System.Collections.Generic;
using NUnit.Framework;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.Simulation;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>Slice 1: the contracts everything else stands on.</summary>
    public class CoreContractTests
    {
        // ── Randomness (P7) ───────────────────────────────────────────────

        [Test]
        public void Rng_MatchesTheSplitMix64Reference()
        {
            // SplitMix64's published first output for seed 0.
            Assert.AreEqual(0xE220A8397B1DCDAFUL, new DeterministicRng(0).NextULong());
        }

        [Test]
        public void Rng_SameSeedSameSequence_AndCountsDraws()
        {
            var a = new DeterministicRng(1442);
            var b = new DeterministicRng(1442);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextULong(), b.NextULong());
            Assert.AreEqual(1000, a.Draws);

            bool hit = a.Chance(0.5f, out ChanceRoot root);
            Assert.AreEqual(1442UL, root.Seed);
            Assert.AreEqual(1001, root.Draw, "the root names the draw that decided it");
            Assert.AreEqual(hit, b.Chance(0.5f, out _));
        }

        [Test]
        public void Rng_Float01_StaysInRange_AndIsRoughlyUniform()
        {
            var rng = new DeterministicRng(7);
            double sum = 0;
            for (int i = 0; i < 100000; i++)
            {
                float u = rng.NextFloat01();
                Assert.That(u, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                sum += u;
            }
            Assert.AreEqual(0.5, sum / 100000, 0.01);
        }

        // ── Watches (R15) ─────────────────────────────────────────────────

        [Test]
        public void Watch_RunsOnWholeTickMultiples()
        {
            var w = new Watch(5, 2);
            var due = new List<long>();
            for (long t = 0; t < 20; t++) if (w.IsDue(t)) due.Add(t);
            CollectionAssert.AreEqual(new long[] { 2, 7, 12, 17 }, due);
            Assert.AreEqual(5, Watch.FromRate(10f, 0.02f).PeriodTicks, "10 Hz at a 50 Hz tick is every 5th tick");
            Assert.Throws<ArgumentOutOfRangeException>(() => new Watch(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Watch(4, 4));
        }

        // ── Orders ────────────────────────────────────────────────────────

        [Test]
        public void OrderLog_StampsSequence_AndDrainsOnlyDueOrders_InIssueOrder()
        {
            var log = new OrderLog();
            log.Issue(Order.SetRoe(OrderIssuer.Captain, Roe.WeaponsFree), 10);
            log.Issue(Order.Engage(OrderIssuer.Tao, 4, WeaponClass.Essm), 12);
            log.Issue(Order.SetDoctrine(OrderIssuer.Tao, WeaponClass.Ciws, DoctrineMode.Auto), 10);

            var due = new List<Order>();
            log.Drain(10, due);
            Assert.AreEqual(2, due.Count);
            Assert.AreEqual(0, due[0].Sequence);
            Assert.AreEqual(2, due[1].Sequence);
            Assert.AreEqual(1, log.PendingCount);

            due.Clear();
            log.Drain(12, due);
            Assert.AreEqual(OrderKind.Engage, due[0].Kind);
            Assert.AreEqual(4, due[0].TrackId);
            Assert.AreEqual(3, log.All.Count, "the log keeps every Order for replay");
        }

        // ── The fidelity ladder ───────────────────────────────────────────

        [Test]
        public void Ladder_FallsBackToTheHighestBuiltLevel_AndSaysSo()
        {
            FidelityResolution r = FidelityLadder.Resolve(ElementKind.Radar, FidelityLevel.L2);
            Assert.AreEqual(FidelityLevel.L0, r.Resolved);
            Assert.IsTrue(r.FellBack);
            StringAssert.Contains("L2 requested, L0 used", r.ToString());

            FidelityResolution ok = FidelityLadder.Resolve(ElementKind.Guns, FidelityLevel.L0);
            Assert.IsFalse(ok.FellBack);
        }

        // ── The engagement log ────────────────────────────────────────────

        [Test]
        public void EngagementLog_ChainsCausesToTheirRoots()
        {
            var log = new EngagementLog();
            int order = log.Add(0, EventKind.OrderApplied, "ROE", "weapons free", root: RootKind.Order, orderSequence: 0);
            int scenario = log.Add(1, EventKind.TrackCreated, "Tracks L0", "track 1 created", 1, root: RootKind.Scenario);
            int assigned = log.Add(2, EventKind.Assigned, "TEWA L0", "track 1 → ESSM", 1, scenario, order);
            int kill = log.Add(90, EventKind.Kill, "VLS L0", "kill", 1, assigned, root: RootKind.Chance, chance: new ChanceRoot(5, 3));

            Assert.AreEqual(4, log.Chain(kill).Count);
            Assert.IsTrue(log.IsComplete(kill));

            int orphan = log.Add(91, EventKind.Note, "test", "no cause, no root");
            Assert.IsFalse(log.IsComplete(orphan), "a leaf without a root is an 'unrecorded' gap");
        }

        // ── The Fix ───────────────────────────────────────────────────────

        [Test]
        public void Fix_SeesEveryBit()
        {
            ulong a = CombatFix.Mix(CombatFix.Offset, 1.0f);
            ulong b = CombatFix.Mix(CombatFix.Offset, BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(1.0f) + 1));
            Assert.AreNotEqual(a, b, "one ulp apart must fingerprint differently");
            Assert.AreEqual(a, CombatFix.Mix(CombatFix.Offset, 1.0f));
        }
    }
}
