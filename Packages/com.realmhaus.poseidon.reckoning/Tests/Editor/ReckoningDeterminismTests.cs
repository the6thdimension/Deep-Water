using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Debug = UnityEngine.Debug;

namespace POSEIDON.Reckoning.Tests
{
    /// <summary>Whole-world behaviour: the brief's cases (a) to (d), and tick cost.</summary>
    public class ReckoningDeterminismTests
    {
        private const int RunTicks = 10000;

        private static FixTrace RunTraced(ToyRun run, int ticks)
        {
            var trace = run.World.StartTrace();
            run.World.StepTo(ticks);
            return trace;
        }

        // (a)
        [Test]
        public void TwoRuns_GiveIdenticalFixes_EveryTick_For10000Ticks()
        {
            var first = new ToyRun(Toy.ScriptedLog(RunTicks));
            var second = new ToyRun(Toy.ScriptedLog(RunTicks));

            var result = FixDiff.Compare(RunTraced(first, RunTicks), RunTraced(second, RunTicks));

            Assert.AreEqual(FixDiffOutcome.Identical, result.Outcome, result.ToString());
            Assert.AreEqual(RunTicks, result.TicksCompared);
            Assert.AreEqual(first.World.CurrentFix(), second.World.CurrentFix());

            // A Fix that never moves would also be "identical". Prove state actually changed.
            Assert.AreNotEqual(first.World.Trace.SystemFixAt(0, 1), first.World.Trace.SystemFixAt(RunTicks - 1, 1));
            Assert.Greater(first.Beacon.Pings, 0, "Events should have fired.");
            Debug.Log($"[RECKONING] (a) identical over {result.TicksCompared} ticks, final Fix {first.World.CurrentFix():X16}, {first.World.Log.Count} Orders, {first.Beacon.Pings} events fired");
        }

        [Test]
        public void GoldenFix_OfTheToyRun_NeverChanges()
        {
            // Two runs in one process cannot notice a change to the rules, the hash or the tick order,
            // because both runs change together. This number can. It was first measured on 2026-11-05
            // (Unity 6000.4.11f1, Editor Mono, x64, Windows). It is also the first thing to compare on
            // another machine, a Player build or IL2CPP, none of which has been tried yet.
            var run = new ToyRun(Toy.ScriptedLog(RunTicks));
            run.World.StepTo(RunTicks);
            Assert.AreEqual(0x3DCFE134307A33AAUL, run.World.CurrentFix(), $"Now {run.World.CurrentFix():X16}. If this change is meant, it invalidates every saved run.");
        }

        [Test]
        public void DifferentSeed_GivesDifferentFixes()
        {
            var a = new ToyRun(Toy.ScriptedLog(100));
            var otherSeed = new OrderLog(Toy.ScenarioHash, Toy.ScenarioSeed + 1, Toy.BuildId);
            Toy.AppendScriptedOrders(otherSeed, 100);
            var b = new ToyRun(otherSeed);

            a.World.StepTo(100);
            b.World.StepTo(100);

            Assert.AreNotEqual(a.World.CurrentFix(), b.World.CurrentFix());
        }

        [Test]
        public void OrdersOnOneTick_ApplyByKey_NotByArrival()
        {
            var fromIssuer1First = Toy.NewLog();
            fromIssuer1First.Append(10, 1, Toy.ParticlesId, Toy.OrderPush, Toy.PushPayload(0, 1, 0));
            fromIssuer1First.Append(10, 2, Toy.WindId, Toy.OrderSetWind, Toy.WindPayload(2, 0));

            var fromIssuer2First = Toy.NewLog();
            fromIssuer2First.Append(10, 2, Toy.WindId, Toy.OrderSetWind, Toy.WindPayload(2, 0));
            fromIssuer2First.Append(10, 1, Toy.ParticlesId, Toy.OrderPush, Toy.PushPayload(0, 1, 0));

            Assert.AreEqual(1u, fromIssuer1First[0].Issuer);
            Assert.AreEqual(1u, fromIssuer2First[0].Issuer, "Key order wins over the order they arrived in.");
        }

        [Test]
        public void IssuedOrders_MatchTheSameOrdersReplayedFromTheLog()
        {
            // Live: Orders arrive as the run goes. Replay: the Log that run wrote.
            var live = new ToyRun(Toy.NewLog());
            var liveTrace = live.World.StartTrace();
            for (int tick = 0; tick < 2000; tick++)
            {
                if (tick % 100 == 0) live.World.Issue(tick, 9, Toy.ParticlesId, Toy.OrderPush, Toy.PushPayload(1, 0.5, 0.25));
                live.World.Step();
            }

            var replay = new ToyRun(OrderLog.FromBytes(live.World.Log.ToBytes()));
            var result = FixDiff.Compare(liveTrace, RunTraced(replay, 2000));

            Assert.AreEqual(FixDiffOutcome.Identical, result.Outcome, result.ToString());
            Assert.AreEqual(2000, result.TicksCompared);
        }

        [Test]
        public void Issue_RefusesATickAlreadyPast_AndAnUnknownSystem()
        {
            var run = new ToyRun(Toy.NewLog());
            run.World.StepTo(5);
            Assert.Throws<ArgumentOutOfRangeException>(() => run.World.Issue(4, 1, Toy.WindId, Toy.OrderSetWind));
            Assert.Throws<InvalidOperationException>(() => run.World.Issue(5, 1, 99, Toy.OrderSetWind));
        }

        [Test]
        public void AnOrderSlippedInBehindTheClock_StopsTheWorld()
        {
            var run = new ToyRun(Toy.NewLog());
            run.World.StepTo(5);
            run.World.Log.Append(2, 1, Toy.WindId, Toy.OrderSetWind, Toy.WindPayload(1, 1));
            Assert.Throws<InvalidOperationException>(() => run.World.Step());
        }

        // (b)
        [Test]
        public void SnapshotAt5000_RestoreAndReplay_ReachesTheSameFinalFix()
        {
            var log = Toy.ScriptedLog(RunTicks);
            var original = new ToyRun(log);
            var originalTrace = original.World.StartTrace();

            original.World.StepTo(5000);
            byte[] saveBytes = original.World.Save().ToBytes();
            original.World.StepTo(RunTicks);

            // Everything the restored run knows comes through the save's bytes.
            var save = ReckoningSave.FromBytes(saveBytes);
            var restored = new ToyRun(OrderLog.FromBytes(save.LogBytes));
            restored.World.RestoreSnapshot(save.Snapshot);
            Assert.AreEqual(5000, restored.World.Now);

            var restoredTrace = RunTraced(restored, RunTicks);
            var result = FixDiff.Compare(originalTrace, restoredTrace);

            Assert.AreEqual(FixDiffOutcome.Identical, result.Outcome, result.ToString());
            Assert.AreEqual(5000, result.TicksCompared);
            Assert.AreEqual(original.World.CurrentFix(), restored.World.CurrentFix());
            Debug.Log($"[RECKONING] (b) restored at 5000, replayed {result.TicksCompared} ticks, final Fix {restored.World.CurrentFix():X16}, save {saveBytes.Length} bytes");
        }

        [Test]
        public void Restore_CatchesASystemThatForgetsStateInItsSave()
        {
            var log = Toy.ScriptedLog(1000);
            var run = new ToyRun(log);
            run.Beacon.ForgetsPingSumOnSave = true;
            run.World.StepTo(1000);
            Assert.Greater(run.Beacon.PingSum, 0);
            byte[] snapshot = run.World.SaveSnapshot();

            var restored = new ToyRun(log);
            var error = Assert.Throws<ReckoningFormatException>(() => restored.World.RestoreSnapshot(snapshot));
            StringAssert.Contains("Fix", error.Message);
        }

        [Test]
        public void Restore_RefusesAnotherScenarioOrBuild_AndCorruptBytes()
        {
            var run = new ToyRun(Toy.ScriptedLog(100));
            run.World.StepTo(100);
            byte[] snapshot = run.World.SaveSnapshot();

            var otherScenario = new ToyRun(new OrderLog(Toy.ScenarioHash + 1, Toy.ScenarioSeed, Toy.BuildId));
            Assert.Throws<ReckoningFormatException>(() => otherScenario.World.RestoreSnapshot(snapshot));

            var otherBuild = new ToyRun(new OrderLog(Toy.ScenarioHash, Toy.ScenarioSeed, "another-build"));
            Assert.Throws<ReckoningFormatException>(() => otherBuild.World.RestoreSnapshot(snapshot));

            byte[] corrupt = (byte[])snapshot.Clone();
            corrupt[corrupt.Length / 2] ^= 0xFF;
            var fresh = new ToyRun(Toy.ScriptedLog(100));
            Assert.Throws<ReckoningFormatException>(() => fresh.World.RestoreSnapshot(corrupt));
        }

        // (c) One test per system, because "names the system" only means something if it can name
        // each one. The planted change is 1e-9: far below anything a tolerance-based check would see.
        [TestCase(Toy.ParticlesId, "ToyParticles", 6789)]
        [TestCase(Toy.WindId, "ToyWind", 6790)]   // the wind only runs on multiples of 5
        [TestCase(Toy.BeaconId, "ToyBeacon", 6800)] // the beacon only runs on multiples of 50
        public void PlantedDesync_IsReported_AtExactlyThatTickAndSystem(uint systemId, string systemName, int tick)
        {
            var clean = new ToyRun(Toy.ScriptedLog(RunTicks));
            var dirty = new ToyRun(Toy.ScriptedLog(RunTicks));
            if (systemId == Toy.ParticlesId) dirty.Particles.PerturbAtTick = tick;
            if (systemId == Toy.WindId) dirty.Wind.PerturbAtTick = tick;
            if (systemId == Toy.BeaconId) dirty.Beacon.PerturbAtTick = tick;

            var result = FixDiff.Compare(RunTraced(clean, RunTicks), RunTraced(dirty, RunTicks));

            Assert.AreEqual(FixDiffOutcome.Diverged, result.Outcome, result.ToString());
            Assert.AreEqual(tick, result.Tick);
            Assert.AreEqual(systemId, result.SystemId);
            Assert.AreEqual(systemName, result.SystemName);
            Assert.AreEqual(tick, result.TicksCompared, "Every tick before the plant should have matched.");
            Debug.Log($"[RECKONING] (c) {result}");
        }

        [Test]
        public void FixDiff_SaysSoWhenTracesCannotBeCompared()
        {
            var a = new ToyRun(Toy.NewLog());
            a.World.StartTrace();
            a.World.StepTo(5);

            var noOverlap = new ToyRun(Toy.NewLog());
            noOverlap.World.StepTo(5);
            noOverlap.World.StartTrace();
            noOverlap.World.StepTo(10);
            Assert.AreEqual(FixDiffOutcome.NoOverlap, FixDiff.Compare(a.World.Trace, noOverlap.World.Trace).Outcome);

            var otherSystems = new ReckonedWorld(Toy.NewLog());
            otherSystems.Register(new ToyWind(), Watch.EveryTicks(1));
            otherSystems.StartTrace();
            otherSystems.StepTo(5);
            Assert.AreEqual(FixDiffOutcome.DifferentSystems, FixDiff.Compare(a.World.Trace, otherSystems.Trace).Outcome);
        }

        // (d)
        [TestCase(1, 5, 50, TestName = "SkipAhead_EqualsTickByTick_DenseWatches")]
        [TestCase(100, 300, 200, TestName = "SkipAhead_EqualsTickByTick_SparseWatches")]
        public void SkipAhead_EqualsTickByTick(int particlesEvery, int windEvery, int beaconEvery)
        {
            var stepped = new ToyRun(Toy.ScriptedLog(RunTicks), windEvery: windEvery, particlesEvery: particlesEvery, beaconEvery: beaconEvery);
            var steppedTrace = RunTraced(stepped, RunTicks);

            var skipped = new ToyRun(Toy.ScriptedLog(RunTicks), windEvery: windEvery, particlesEvery: particlesEvery, beaconEvery: beaconEvery);
            var skippedTrace = skipped.World.StartTrace();
            while (skipped.World.Now < RunTicks)
            {
                skipped.World.AdvanceToNextDue(RunTicks);
                if (skipped.World.Now < RunTicks) skipped.World.Step();
            }

            var result = FixDiff.Compare(steppedTrace, skippedTrace);
            Assert.AreEqual(FixDiffOutcome.Identical, result.Outcome, result.ToString());
            Assert.AreEqual(RunTicks, result.TicksCompared);
            Assert.AreEqual(stepped.World.CurrentFix(), skipped.World.CurrentFix());
            Assert.AreEqual(RunTicks, stepped.World.TicksExecuted);

            // In the sparse world the skip must really have skipped, or this proved nothing.
            if (particlesEvery > 1) Assert.Less(skipped.World.TicksExecuted, RunTicks / 4);
            Debug.Log($"[RECKONING] (d) watches {particlesEvery}/{windEvery}/{beaconEvery}: identical over {result.TicksCompared} ticks; stepping ran {stepped.World.TicksExecuted} ticks, skipping ran {skipped.World.TicksExecuted}");
        }

        [Test]
        public void SkipAhead_StopsForAnOrderOrAnEvent_AndAtTheLimit()
        {
            var log = Toy.NewLog();
            log.Append(123, 1, Toy.WindId, Toy.OrderSetWind, Toy.WindPayload(1, 1));
            var world = new ToyRun(log, windEvery: 1000, particlesEvery: 1000, beaconEvery: 1000).World;

            world.Step(); // tick 0: all three run; the beacon schedules a ping for tick 7
            Assert.AreEqual(7, world.AdvanceToNextDue(10000), "Stops for the event.");
            world.Step();
            Assert.AreEqual(123, world.AdvanceToNextDue(10000), "Stops for the Order.");
            world.Step();
            Assert.AreEqual(500, world.AdvanceToNextDue(500), "Stops at the limit.");
            Assert.AreEqual(1000, world.AdvanceToNextDue(10000), "Stops for the Watch.");
        }

        [Test]
        public void Measure_TickCost_ToyWorld()
        {
            var run = new ToyRun(Toy.ScriptedLog(RunTicks));
            run.World.StepTo(1); // settle first-run JIT out of the timing

            var clock = Stopwatch.StartNew();
            run.World.StepTo(RunTicks);
            clock.Stop();

            double msPerTick = clock.Elapsed.TotalMilliseconds / (RunTicks - 1);
            Debug.Log($"[RECKONING] tick cost: {msPerTick:F5} ms/tick over {RunTicks - 1} ticks ({clock.Elapsed.TotalMilliseconds:F1} ms total), 8 particles, Editor Mono, no trace; {1000.0 / (Chronometer.TicksPerSecond * msPerTick):F0}x real time");
            Assert.Pass();
        }
    }

    /// <summary>The parts on their own: Chronometer, scheduler, Log, causes.</summary>
    public class ReckoningPartsTests
    {
        private static TickContext Context() => new TickContext(1, new EventScheduler(), new List<CauseRecord>());

        [Test]
        public void Chronometer_RunsDueSystems_InTheWrittenOrder()
        {
            var calls = new List<string>();
            var clock = new Chronometer();
            clock.Register(new ToyRecorder(5, "Sea", calls), Watch.EveryTicks(1));
            clock.Register(new ToyRecorder(1, "Ships", calls), Watch.EveryTicks(1));
            clock.Register(new ToyRecorder(9, "Sensors", calls), Watch.EveryTicks(2));
            clock.Register(new ToyRecorder(3, "Captains", calls), Watch.EveryTicks(5));

            var context = Context();
            for (int i = 0; i < 6; i++) clock.Step(context);

            CollectionAssert.AreEqual(new[]
            {
                "0:Sea", "0:Ships", "0:Sensors", "0:Captains",
                "1:Sea", "1:Ships",
                "2:Sea", "2:Ships", "2:Sensors",
                "3:Sea", "3:Ships",
                "4:Sea", "4:Ships", "4:Sensors",
                "5:Sea", "5:Ships", "5:Captains",
            }, calls, "Written order, not id order; each on its own Watch.");
            Assert.AreEqual(6, clock.Now);
        }

        [Test]
        public void Chronometer_RefusesBadRegistrations_AndLateOnes()
        {
            var calls = new List<string>();
            var clock = new Chronometer();
            clock.Register(new ToyRecorder(1, "A", calls), Watch.EveryTicks(1));

            Assert.Throws<ArgumentException>(() => clock.Register(new ToyRecorder(1, "Same id", calls), Watch.EveryTicks(1)));
            Assert.Throws<ArgumentException>(() => clock.Register(new ToyRecorder(ReckonedWorld.SchedulerSystemId, "Reserved", calls), Watch.EveryTicks(1)));
            Assert.Throws<ArgumentException>(() => clock.Register(new ToyRecorder(2, "No rate", calls), default));
            Assert.Throws<ArgumentOutOfRangeException>(() => Watch.EveryTicks(0));

            clock.Seal();
            Assert.Throws<InvalidOperationException>(() => clock.Register(new ToyRecorder(2, "Late", calls), Watch.EveryTicks(1)));
        }

        [Test]
        public void Chronometer_BaseTick_Is20ms()
        {
            Assert.AreEqual(20000, Chronometer.BaseTickMicroseconds);
            Assert.AreEqual(50, Chronometer.TicksPerSecond);
        }

        [Test]
        public void Chronometer_RefusesToSkipADueSystem()
        {
            var clock = new Chronometer();
            clock.Register(new ToyRecorder(1, "A", new List<string>()), Watch.EveryTicks(10));
            Assert.Throws<InvalidOperationException>(() => clock.SkipTo(5), "Tick 0 is due.");
        }

        [Test]
        public void Events_FireByKey_WhateverOrderTheyWereScheduledIn()
        {
            (int phase, uint source, uint entity)[] keys = { (2, 1u, 1u), (1, 9u, 9u), (1, 2u, 8u), (1, 2u, 3u) };
            int[][] insertionOrders = { new[] { 0, 1, 2, 3 }, new[] { 3, 2, 1, 0 }, new[] { 2, 0, 3, 1 } };

            foreach (var insertion in insertionOrders)
            {
                var scheduler = new EventScheduler();
                foreach (int i in insertion)
                    scheduler.Schedule(0, 5, keys[i].phase, keys[i].source, keys[i].entity, 1, 0);

                var fired = new List<string>();
                while (scheduler.TryPopDue(5, out var e)) fired.Add($"{e.Phase}/{e.SourceSystem}/{e.Entity}");

                CollectionAssert.AreEqual(new[] { "1/2/3", "1/2/8", "1/9/9", "2/1/1" }, fired);
            }
        }

        [Test]
        public void Events_IdenticalKeys_FallBackToSequence_AndEarlierTicksComeFirst()
        {
            var scheduler = new EventScheduler();
            scheduler.Schedule(0, 9, 0, 1, 1, 1, 0, data: 100);
            scheduler.Schedule(0, 5, 0, 1, 1, 1, 0, data: 200);
            scheduler.Schedule(0, 5, 0, 1, 1, 1, 0, data: 300);

            Assert.AreEqual(5, scheduler.NextTick);
            Assert.IsFalse(scheduler.TryPopDue(4, out _), "Nothing is due at 4.");
            Assert.IsTrue(scheduler.TryPopDue(5, out var first));
            Assert.AreEqual(200, first.Data);
            Assert.IsTrue(scheduler.TryPopDue(5, out var second));
            Assert.AreEqual(300, second.Data);
            Assert.IsFalse(scheduler.TryPopDue(5, out _), "The tick-9 event is not due yet.");
        }

        [Test]
        public void Events_MustBeInTheFuture()
        {
            var scheduler = new EventScheduler();
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(10, 10, 0, 1, 1, 1, 0));
        }

        [Test]
        public void OrderLog_RoundTripsToIdenticalBytes()
        {
            var log = Toy.ScriptedLog(10000);
            byte[] bytes = log.ToBytes();
            var back = OrderLog.FromBytes(bytes);

            CollectionAssert.AreEqual(bytes, back.ToBytes());
            Assert.AreEqual(log.ScenarioHash, back.ScenarioHash);
            Assert.AreEqual(log.ScenarioSeed, back.ScenarioSeed);
            Assert.AreEqual(log.BuildId, back.BuildId);
            Assert.AreEqual(log.Count, back.Count);
            Assert.Greater(log.Count, 100);

            // The sequence counter must survive too, or Orders issued after a load could collide.
            Assert.Greater(back.Append(20000, 1, Toy.WindId, 0).Sequence, back[0].Sequence);
        }

        [Test]
        public void OrderLog_RejectsCorruptAndTruncatedBytes()
        {
            byte[] bytes = Toy.ScriptedLog(1000).ToBytes();

            byte[] flipped = (byte[])bytes.Clone();
            flipped[bytes.Length / 2] ^= 1;
            Assert.Throws<ReckoningFormatException>(() => OrderLog.FromBytes(flipped));

            byte[] truncated = new byte[bytes.Length - 3];
            Array.Copy(bytes, truncated, truncated.Length);
            Assert.Throws<ReckoningFormatException>(() => OrderLog.FromBytes(truncated));

            Assert.Throws<ReckoningFormatException>(() => OrderLog.FromBytes(new byte[3]));
        }

        [Test]
        public void Order_PayloadCannotBeChangedAfterItIsLogged()
        {
            var log = Toy.NewLog();
            byte[] payload = { 1, 2, 3 };
            log.Append(0, 1, Toy.WindId, 0, payload);
            payload[0] = 99;
            Assert.AreEqual(1, log[0].OpenPayload().ReadByte());
        }

        [Test]
        public void FixHasher_MatchesPublishedFnv1a64Vectors()
        {
            Assert.AreEqual(0xCBF29CE484222325UL, FixHasher.Start().Value, "empty input");
            Assert.AreEqual(0xAF63DC4C8601EC8CUL, FixHasher.Of(new[] { (byte)'a' }, 0, 1), "\"a\"");
            byte[] foobar = System.Text.Encoding.ASCII.GetBytes("foobar");
            Assert.AreEqual(0x85944171F73967E8UL, FixHasher.Of(foobar, 0, foobar.Length), "\"foobar\"");
        }

        [Test]
        public void FixHasher_SeesRawBits_EvenWhereValuesCompareEqual()
        {
            var positive = FixHasher.Start();
            positive.AddDouble(0.0);
            var negative = FixHasher.Start();
            negative.AddDouble(-0.0);
            Assert.AreNotEqual(positive.Value, negative.Value);
        }

        [Test]
        public void CauseRecord_NamesTheDrawBehindAStrongGust()
        {
            var run = new ToyRun(Toy.NewLog());
            run.World.StepTo(10000);

            Assert.Greater(run.World.Causes.Count, 0, "The toy should see at least one 3.5-sigma gust in 80,000 draws.");
            var cause = run.World.Causes[0];
            Assert.AreEqual(Toy.RuleStrongGust, cause.RuleId);
            Assert.AreEqual(CauseLinkKind.Draw, cause.Links[0].Kind);

            // The link is enough to recompute the very number the record quotes, without a replay.
            var draw = new DrawId(cause.Links[0].Source, cause.Links[0].Entity, cause.Links[0].Tick, (uint)cause.Links[0].Sequence);
            Assert.AreEqual(ReckonedRandom.Normal(Toy.ScenarioSeed, draw), cause.Numbers[0].Value);
            Debug.Log($"[RECKONING] causes: {run.World.Causes.Count} strong gusts recorded in 10,000 ticks; first at tick {cause.Tick}, entity {cause.Entity}, {cause.Numbers[0].Name} {cause.Numbers[0].Value:F3}");
        }

        [Test]
        public void CauseLinks_CarryTheirKeys()
        {
            var log = Toy.NewLog();
            var order = log.Append(4, 6, Toy.WindId, 0);
            var orderLink = CauseLink.ToOrder(order);
            Assert.AreEqual(CauseLinkKind.Order, orderLink.Kind);
            Assert.AreEqual(4, orderLink.Tick);
            Assert.AreEqual(6u, orderLink.Source);

            var scheduled = new EventScheduler().Schedule(0, 8, 1, 2, 3, 2, 0);
            var eventLink = CauseLink.ToEvent(scheduled);
            Assert.AreEqual(CauseLinkKind.Event, eventLink.Kind);
            Assert.AreEqual(8, eventLink.Tick);
            Assert.AreEqual(2u, eventLink.Source);
            Assert.AreEqual(3u, eventLink.Entity);

            Assert.AreEqual(CauseLinkKind.Scenario, CauseLink.ToScenario().Kind);
        }
    }
}
