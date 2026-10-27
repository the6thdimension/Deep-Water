// TEMPLATE (ladder-rung-slice skill). It lives outside Assets/, so Unity never compiles it here.
//
// Copy it to Assets/General Quarters/Tests/Editor/<Element>L<n>Tests.cs (or merge its tests into that file), rename the
// class, and change every line marked CHANGE. As written it pins Radar L1 + Tracks L1 (Stream A, 831e2ff6), the newest
// rung, so it compiles against today's types. Each test names the real one it is modelled on:
//   - CombatCoreL0Tests.RaidL0_PlaysEndToEnd_ExplainsEveryKill_AndRepeatsExactly
//   - RadarL1Tests.RaidL1_SameSeedSameFix_EveryTick_AndEveryKillTracesToADetection
//   - GunsL1Tests.Saturation_AtGunsL1_Resolves_Repeats_AndStaysInBudget (the warm-up run, the budget)
//   - DamageControlL1Tests.PastTheMargin_SevenStations_SinksHer_TheSameWayEveryTime (a verdict on the same tick)
//   - RadarL1Tests.L1_WithoutItsProfileData_RefusesToBuild, GunsL1Tests.L1_WithoutBallisticsData_RefusesToBuild
//   - CoreContractTests.Ladder_FallsBackToTheHighestBuiltLevel_AndSaysSo
//
// The golden: when the range loadout moves up to this rung, pin the levels the golden was recorded at, in
// PresentationTests.Saturation_Fix_IsUnchangedByPresentationData (one line per element, as each slice did). This
// rung gets no golden constant of its own (METHODOLOGY C29): it gets the run-twice pin below.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.Scenario;
using DeepWater.GeneralQuarters.Core.World;
using DeepWater.GeneralQuarters.Editor;
using Debug = UnityEngine.Debug;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>
    /// CHANGE: one sentence on the rung and its decisions. A seeded raid with scripted Orders at this rung repeats its
    /// Fix on every tick, another seed changes it, every kill traces to its roots, and the core fits the budget (G16).
    /// Asking for the rung without its data refuses to build; asking above it falls back to it out loud.
    /// </summary>
    public class RungFixPinTemplate   // CHANGE: <Element>L<n>FixPinTests, or merge into <Element>L<n>Tests
    {
        // CHANGE: the element and the rung this file pins.
        const ElementKind Element = ElementKind.Radar;
        const FidelityLevel Rung = FidelityLevel.L1;

        const ulong Seed = 1442, OtherSeed = 7;
        const int Threats = 8;
        static readonly OwnShipState Own = new OwnShipState { Position = Vector3.zero, Velocity = Vector3.zero, HeadingDeg = 0f };

        /// <summary>
        /// CHANGE: the L0 test loadout raised to the rung, with the rung's data from the same builder the range asset
        /// uses, so the test and the shipped loadout cannot drift apart. (Radar L1 needs Tracks L1 with it, G64.)
        /// </summary>
        static CombatLoadout AtRung(ulong seed)
        {
            CombatLoadout l = TestLoadouts.Ddg51L0(seed);
            l.RadarLevel = FidelityLevel.L1;
            l.Radar.L1 = Ddg51CombatData.RadarL1();
            l.TracksLevel = FidelityLevel.L1;
            l.Tracks.L1 = Ddg51CombatData.TracksL1();
            return l;
        }

        /// <summary>CHANGE: set this element's level field. The same field AtRung sets.</summary>
        static void Ask(CombatLoadout l, FidelityLevel level) => l.RadarLevel = level;

        /// <summary>
        /// The Orders are part of what the pin pins: the same seed with the same Orders must give the same Fix. ROE is
        /// in the fingerprint, so this script moves it. CHANGE: add the Orders your rung answers to.
        /// </summary>
        static readonly (long tick, Order order)[] Script =
        {
            (0, Order.SetRoe(OrderIssuer.Captain, Roe.WeaponsTight)),
            (250, Order.SetRoe(OrderIssuer.Captain, Roe.WeaponsFree)),
        };

        /// <summary>One seeded run on the fixed tick: Orders, then the raid, then the core. The Fix of every tick and the core's cost.</summary>
        static (List<ulong> fixes, CombatSystem cs, ScriptedRaid raid, double msPerTick) Run(CombatLoadout loadout, int maxTicks = 12_000)
        {
            // CHANGE: feed whatever the rung reads from the world. Radar L1 reads the sea (CombatSystem.Sea); calm here.
            var cs = new CombatSystem(loadout) { Sea = default(SeaEnvironment) };
            var raid = new ScriptedRaid(TestLoadouts.SeaSkimmerRaid(Threats), Own);
            var truth = new List<TruthTarget>();
            var hits = new List<ShipHit>();
            var kills = new List<KillReport>();
            var fixes = new List<ulong>();
            var watch = new Stopwatch();
            for (int i = 0; i < maxTicks; i++)
            {
                foreach (var (tick, order) in Script)
                    if (tick == cs.Tick) cs.Issue(order);
                hits.Clear();
                raid.Step(cs.Tick, cs.TickSeconds, Own, truth, hits);
                kills.Clear();
                watch.Start();
                cs.Step(Own, truth, hits, kills);
                watch.Stop();
                raid.ApplyKills(kills);
                fixes.Add(cs.Fix);
                if (raid.Resolved) break;
            }
            return (fixes, cs, raid, watch.Elapsed.TotalMilliseconds / fixes.Count);
        }

        [Test]
        public void AtTheRung_SameSeedAndOrders_GiveTheSameFixEveryTick_AnotherSeedDoesNot_AndItFitsTheBudget()
        {
            Run(AtRung(Seed));   // warm-up: JIT and any tables the rung builds once, so the timed run is honest
            var (fixesA, cs, raid, ms) = Run(AtRung(Seed));
            var (fixesB, _, _, _) = Run(AtRung(Seed));
            var (fixesC, _, _, _) = Run(AtRung(OtherSeed));

            Debug.Log($"[GQ] {Element} {Rung} Fix pin, {Threats} threats: {raid.Killed} killed, {raid.Leaked} hit; " +
                      $"{fixesA.Count} ticks, {ms:0.000} ms per fixed step (Editor); Fix {fixesA.Last():X16}");

            Assert.IsFalse(cs.Fidelity.Any(f => f.Element == Element && f.FellBack), $"{Element} ran at {Rung}, as asked");
            Assert.IsTrue(raid.Resolved, "every threat was killed or hit the ship");
            Assert.AreEqual(Threats, raid.Killed + raid.Leaked, "every threat is accounted for");
            CollectionAssert.AreEqual(fixesA, fixesB, "the same seed and Orders give the same Fix on every tick");
            Assert.AreNotEqual(fixesA.Last(), fixesC.Last(), "another seed: other draws, and the Fix shows it");
            foreach (EngagementEvent e in cs.Log.Events.Where(x => x.Kind == EventKind.Kill))
                Assert.IsTrue(cs.Log.IsComplete(e.Id), $"kill {e.Id} has a cause chain ending in roots: {e.Text}");
            Assert.LessOrEqual(ms, 1.0, "combat-core time per fixed step (G16)");
        }

        [Test]
        public void TheRung_WithoutItsData_RefusesToBuild()
        {
            CombatLoadout l = TestLoadouts.Ddg51L0();
            Ask(l, Rung);   // the level, with its data left empty
            Assert.Throws<System.ArgumentException>(() => new CombatSystem(l), $"{Element} {Rung} with no data: no silent fall back");
        }

        [Test]
        public void AskingAboveTheRung_FallsBackToIt_AndSaysSo()
        {
            var above = (FidelityLevel)((int)Rung + 1);
            Assert.AreEqual(Rung, FidelityLadder.HighestImplemented(Element), "raise the ladder's table in the same commit as the rung");

            CombatLoadout l = AtRung(Seed);
            Ask(l, above);
            var cs = new CombatSystem(l);

            FidelityResolution r = cs.Fidelity.First(f => f.Element == Element);
            Assert.AreEqual(above, r.Requested);
            Assert.AreEqual(Rung, r.Resolved, "it runs at the highest built level");
            Assert.IsTrue(cs.Log.Events.Any(e => e.Kind == EventKind.FidelityFallback && e.Text.Contains($"{above} requested, {Rung} used")),
                "and the log says so, for the debrief and the HUD");
        }
    }
}
