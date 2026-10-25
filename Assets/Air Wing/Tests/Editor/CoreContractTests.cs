using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Tests
{
    /// <summary>Slice 1a: the contracts everything else stands on (ROADMAP, slice 1a).</summary>
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

        // ── Fidelity ladder (C14) ─────────────────────────────────────────

        sealed class Part
        {
            public readonly FidelityLevel Level;
            public readonly string Profile;
            public Part(FidelityLevel level, string profile) { Level = level; Profile = profile; }
        }

        [Test]
        public void Ladder_RequestAboveBuilt_FallsBackOutLoud()
        {
            var ladder = new PartLadder<string, Part>(PartKind.Airframe)
                .Register(FidelityLevel.L0, p => new Part(FidelityLevel.L0, p));

            Part part = ladder.Build(FidelityLevel.L2, "F/A-18E", out FidelityResolution res);

            Assert.AreEqual(FidelityLevel.L0, part.Level);
            Assert.AreEqual("F/A-18E", part.Profile, "the builder gets the profile");
            Assert.IsTrue(res.FellBack);
            Assert.AreEqual(FidelityLevel.L2, res.Requested);
            Assert.AreEqual(FidelityLevel.L0, res.Resolved);
            StringAssert.Contains("L2 requested, L0 used", res.ToString(), "the fallback is said, for the log and the HUD");
        }

        [Test]
        public void Ladder_BuiltLevel_IsUsedAsIs()
        {
            var ladder = new PartLadder<string, Part>(PartKind.FlightControls)
                .Register(FidelityLevel.L0, p => new Part(FidelityLevel.L0, p))
                .Register(FidelityLevel.L1, p => new Part(FidelityLevel.L1, p));

            Part part = ladder.Build(FidelityLevel.L1, "x", out FidelityResolution res);

            Assert.AreEqual(FidelityLevel.L1, part.Level);
            Assert.IsFalse(res.FellBack);
        }

        [Test]
        public void Ladder_NeverRunsAboveTheRequest_AcrossAGap()
        {
            var ladder = new PartLadder<string, Part>(PartKind.Powerplant)
                .Register(FidelityLevel.L0, p => new Part(FidelityLevel.L0, p))
                .Register(FidelityLevel.L2, p => new Part(FidelityLevel.L2, p));

            Assert.AreEqual(FidelityLevel.L0, ladder.Resolve(FidelityLevel.L1).Resolved);
            Assert.AreEqual(FidelityLevel.L2, ladder.Resolve(FidelityLevel.L3).Resolved);
        }

        [Test]
        public void Ladder_WithNothingBuilt_Throws_AndRefusesDuplicates()
        {
            var empty = new PartLadder<string, Part>(PartKind.LandingGear);
            Assert.Throws<InvalidOperationException>(() => empty.Resolve(FidelityLevel.L1));

            var ladder = new PartLadder<string, Part>(PartKind.Atmosphere).Register(FidelityLevel.L0, p => new Part(FidelityLevel.L0, p));
            Assert.Throws<InvalidOperationException>(() => ladder.Register(FidelityLevel.L0, p => new Part(FidelityLevel.L0, p)));
        }

        // ── Fidelity sheet (W25) ──────────────────────────────────────────

        [Test]
        public void FidelitySheet_BakesEveryPartsLevel_TheSubsteps_AndTheSeed()
        {
            var so = ScriptableObject.CreateInstance<FidelitySheetSO>();
            try
            {
                so.airframe = FidelityLevel.L1;
                so.powerplant = FidelityLevel.L0;
                so.flightControls = FidelityLevel.L1;
                so.landingGear = FidelityLevel.L1;
                so.atmosphere = FidelityLevel.L2;
                so.substepsPerTick = 8;
                so.seed = 77;

                FidelitySheet sheet = so.Bake();

                Assert.AreEqual(FidelityLevel.L1, sheet.Level(PartKind.Airframe));
                Assert.AreEqual(FidelityLevel.L0, sheet.Level(PartKind.Powerplant));
                Assert.AreEqual(FidelityLevel.L1, sheet.Level(PartKind.FlightControls));
                Assert.AreEqual(FidelityLevel.L1, sheet.Level(PartKind.LandingGear));
                Assert.AreEqual(FidelityLevel.L2, sheet.Level(PartKind.Atmosphere));
                Assert.AreEqual(8, sheet.SubstepsPerTick);
                Assert.AreEqual(77UL, sheet.Seed);

                so.substepsPerTick = 0;
                Assert.Throws<ArgumentOutOfRangeException>(() => so.Bake(), "0 substeps is refused, not clamped");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ── Substep clock (W24) ───────────────────────────────────────────

        [Test]
        public void Clock_SubstepsTileTheTick_Exactly()
        {
            var clock = new SubstepClock(0.02, 4);
            Assert.AreEqual(0.005, clock.SubstepSeconds, 1e-15);
            Assert.AreEqual(clock.TimeAt(7), clock.TimeAt(6, 4), 1e-12, "the last substep ends where the next tick starts");
            Assert.AreEqual(clock.TimeAt(6) + 2 * 0.005, clock.TimeAt(6, 2), 1e-12);
        }

        [Test]
        public void Clock_TimeComesFromTheTickCount_SoItCannotDrift()
        {
            // Unity's fixed timestep is the float 0.02f, which is not exactly 0.02.
            var clock = new SubstepClock(0.02f, 4);
            const long ticks = 1_000_000; // 5.6 hours at 50 Hz
            Assert.AreEqual(ticks * (double)0.02f, clock.TimeAt(ticks), 0.0, "one multiplication, no accumulated error");

            // Why it matters: adding the step up in a float, the way Time-style accumulators do, drifts.
            float accumulated = 0f;
            for (long i = 0; i < ticks; i++) accumulated += 0.02f;
            Assert.Greater(Math.Abs(accumulated - clock.TimeAt(ticks)), 1.0, "a float accumulator is off by over a second here");
        }

        [Test]
        public void Clock_RefusesNonsense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SubstepClock(0.02, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SubstepClock(0.02, SubstepClock.MaxSubsteps + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SubstepClock(0, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SubstepClock(double.NaN, 4));
        }

        // ── Pilot Inputs and the input log (W6, W28) ──────────────────────

        static PilotInputs Stick(float pitch, float roll = 0f, float throttle = 0.8f) =>
            new PilotInputs { StickPitch = pitch, StickRoll = roll, Throttle = throttle };

        [Test]
        public void InputLog_KeepsTickOrder_AndOneEntryPerAircraftPerTick()
        {
            var log = new InputLog();
            log.Append(0, 1, SeatKind.Player, Stick(0.1f));
            log.Append(0, 2, SeatKind.Ai, Stick(-0.2f));
            log.Append(1, 1, SeatKind.Player, Stick(0.3f));

            Assert.AreEqual(3, log.Count);
            Assert.Throws<InvalidOperationException>(() => log.Append(1, 1, SeatKind.Player, Stick(0f)), "one entry per aircraft per tick");
            Assert.Throws<InvalidOperationException>(() => log.Append(0, 3, SeatKind.Player, Stick(0f)), "append-only: no going back a tick");

            Assert.IsTrue(log.TryGet(0, 2, out InputEntry e));
            Assert.AreEqual(SeatKind.Ai, e.Seat, "the log names the seat that issued the inputs");
            Assert.AreEqual(-0.2f, e.Inputs.StickPitch);
            Assert.IsFalse(log.TryGet(1, 2, out _));
            Assert.IsFalse(log.TryGet(5, 1, out _));
        }

        [Test]
        public void InputLog_ClampsDeviceOvershoot_ButRefusesNaN()
        {
            var log = new InputLog();
            InputEntry e = log.Append(0, 1, SeatKind.Player, new PilotInputs { StickPitch = 1.07f, StickRoll = -1.3f, Pedals = 0.5f, Throttle = 1.2f });

            Assert.AreEqual(1f, e.Inputs.StickPitch);
            Assert.AreEqual(-1f, e.Inputs.StickRoll);
            Assert.AreEqual(0.5f, e.Inputs.Pedals);
            Assert.AreEqual(1f, e.Inputs.Throttle);
            Assert.Throws<ArgumentException>(() => log.Append(1, 1, SeatKind.Ai, Stick(float.NaN)), "a NaN is a seat bug, not an input");
        }

        [Test]
        public void Seat_IssuesThroughTheSameDoor_WhoeverSitsInIt()
        {
            // Two different issuers fill the same struct for the same aircraft; the log records which one did (C19).
            var log = new InputLog();
            var state = Level();
            ISeat player = new FixedSeat(SeatKind.Player, Stick(0.5f));
            ISeat ai = new FixedSeat(SeatKind.Ai, Stick(0.5f));

            log.Append(0, 1, player.Kind, player.Issue(0, 1, state));
            log.Append(1, 1, ai.Kind, ai.Issue(1, 1, state));

            Assert.AreEqual(log.All[0].Inputs, log.All[1].Inputs, "same inputs, whoever issued them");
            Assert.AreNotEqual(log.All[0].Seat, log.All[1].Seat);
        }

        sealed class FixedSeat : ISeat
        {
            readonly PilotInputs _inputs;
            public FixedSeat(SeatKind kind, PilotInputs inputs) { Kind = kind; _inputs = inputs; }
            public SeatKind Kind { get; }
            public PilotInputs Issue(long tick, int aircraftId, in AirframeState state) => _inputs;
        }

        // ── The Fix (W7) ──────────────────────────────────────────────────

        static AirframeState Level() => new AirframeState
        {
            Position = new Vector3(0f, 3000f, 0f),
            Velocity = new Vector3(0f, 0f, 200f),
            Attitude = Quaternion.identity,
            LoadFactor = 1f,
            MassKg = 21000f,
            FuelKg = 5000f,
            EngineSpool = 0.8f,
        };

        static FixRecord FixOf(long tick, params AirframeState[] states)
        {
            var list = new List<AircraftRecord>();
            for (int i = 0; i < states.Length; i++) list.Add(new AircraftRecord(i + 1, states[i]));
            return FlightFix.Of(tick, list);
        }

        [Test]
        public void Fix_IsStableForIdenticalStates_AndSeesASingleUlp()
        {
            AirframeState a = Level(), b = Level();
            Assert.AreEqual(FixOf(10, a, b).Hash, FixOf(10, a, b).Hash);

            AirframeState nudged = Level();
            nudged.Position.y = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(nudged.Position.y) + 1);
            Assert.AreNotEqual(FixOf(10, a, b).Hash, FixOf(10, a, nudged).Hash, "one ulp of altitude changes the Fix");
            Assert.AreNotEqual(FixOf(10, a, b).Hash, FixOf(11, a, b).Hash, "the tick is part of the Fix");
        }

        [Test]
        public void Fix_FlagsContactTicks_AndInsistsOnIdOrder()
        {
            AirframeState onDeck = Level();
            onDeck.InContact = true;
            Assert.IsFalse(FixOf(0, Level()).Contact);
            Assert.IsTrue(FixOf(0, Level(), onDeck).Contact);

            var unordered = new List<AircraftRecord> { new AircraftRecord(2, Level()), new AircraftRecord(1, Level()) };
            Assert.Throws<ArgumentException>(() => FlightFix.Of(0, unordered));
        }

        [Test]
        public void FixLog_NamesTheFirstTickWhereTwoRunsSplit()
        {
            var run1 = new FixLog();
            var run2 = new FixLog();
            for (long t = 0; t < 5; t++)
            {
                AirframeState s = Level();
                s.Position.z = t * 4f;
                run1.Append(FixOf(t, s));
                if (t == 3) s.Position.z += 0.001f;
                run2.Append(FixOf(t, s));
            }
            Assert.AreEqual(3, run1.FirstDivergence(run2));
            Assert.AreEqual(-1, run1.FirstDivergence(run1));
            Assert.Throws<InvalidOperationException>(() => run1.Append(FixOf(4, Level())), "append-only");
        }
    }
}
