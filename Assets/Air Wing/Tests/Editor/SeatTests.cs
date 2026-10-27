using System;
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

namespace DeepWater.AirWing.Tests
{
    /// <summary>Slice 1c: the seats (C19, W10, W15, W18, W27, W28), headless.</summary>
    public class SeatTests
    {
        const double Tick = 0.02;

        static readonly PlayerSeatSettings Settings = new PlayerSeatSettings
        {
            LeverRatePerSecond = 0.5f,
            AfterburnerDetent = 0.9f,
            Instructor = new InstructorSettings { RollGain = 2.5f, PitchGain = 4f, WingsLevelBlendDeg = 5f },
        };

        static AircraftProfile F18E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();

        static FidelitySheet Sheet() => new FidelitySheet(FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, FidelityLevel.L0, 4, 1442);

        static AirframeState Level(float speed = 220f) => AircraftFactory.LevelStart(F18E(), new Vector3(0f, 5000f, 0f), 0f, speed, 3000f);

        static DeviceSample Moved(float pitch = 0f, float roll = 0f, float pedals = 0f)
        {
            var s = DeviceSample.Idle;
            s.Moved = true;
            s.StickPitch = pitch;
            s.StickRoll = roll;
            s.Pedals = pedals;
            return s;
        }

        static DeviceFrame Frame(DeviceSample? stick = null, DeviceSample? pad = null, DeviceSample? keys = null, bool mouse = false, float yaw = 0f, float pitch = 0f)
        {
            DeviceFrame f = DeviceFrame.Idle;
            if (stick.HasValue) f.Stick = stick.Value;
            if (pad.HasValue) f.Gamepad = pad.Value;
            if (keys.HasValue) f.Keyboard = keys.Value;
            f.MouseMoved = mouse;
            f.AimYawDeg = yaw;
            f.AimPitchDeg = pitch;
            return f;
        }

        static PilotInputs Issue(PlayerSeat seat, in DeviceFrame frame, in AirframeState state, long tick = 0)
        {
            seat.Submit(frame);
            return seat.Issue(tick, 1, state);
        }

        // ── Arbitration (W27) and override (W15) ──────────────────────────

        [Test]
        public void TheLastSourceMovedFlies_AndTheInstructorYieldsAtOnce()
        {
            var seat = new PlayerSeat(Settings, Tick);
            AirframeState s = Level();

            Assert.AreEqual(0.5f, Issue(seat, Frame(stick: Moved(pitch: 0.5f)), s).StickPitch);
            Assert.AreEqual(DeviceSource.Stick, seat.Owner);

            Issue(seat, Frame(stick: Moved(pitch: 0.4f), mouse: true, yaw: 30f), s);
            Assert.AreEqual(DeviceSource.Stick, seat.Owner, "a held stick keeps the seat: the mouse cannot take it");

            Issue(seat, Frame(mouse: true, yaw: 30f), s);
            Assert.AreEqual(DeviceSource.Mouse, seat.Owner, "stick centred, mouse moved: the instructor flies");

            PilotInputs keys = Issue(seat, Frame(keys: Moved(roll: -1f), mouse: true, yaw: 5f), s);
            Assert.AreEqual(DeviceSource.Keyboard, seat.Owner, "a flight key overrides the instructor on the same tick");
            Assert.AreEqual(-1f, keys.StickRoll);

            Issue(seat, Frame(pad: Moved(roll: 0.3f), keys: Moved(roll: -1f)), s);
            Assert.AreEqual(DeviceSource.Keyboard, seat.Owner, "two sources on one tick: the owner keeps it");

            Assert.AreEqual(0.3f, Issue(seat, Frame(pad: Moved(roll: 0.3f)), s).StickRoll);
            Assert.AreEqual(DeviceSource.Gamepad, seat.Owner);
        }

        [Test]
        public void TheMouseTakesOver_WithTheAimOnTheFlightPath_SoNothingJumps()
        {
            var seat = new PlayerSeat(Settings, Tick);
            AirframeState s = Level();
            PilotInputs i = Issue(seat, Frame(mouse: true), s);
            Assert.AreEqual(DeviceSource.Mouse, seat.Owner);
            Assert.Less(Vector3.Angle(seat.AimDirection, s.Velocity), 0.01f, "the aim starts on the flight path");
            Assert.AreEqual(0f, i.StickRoll, 1e-4f);
            Assert.AreEqual(0f, i.StickPitch, 1e-4f, "level flight, aim on the path: hands off");
        }

        [Test]
        public void TheLever_IsSharedByEverySource_WithTheAfterburnerPastTheDetent()
        {
            var seat = new PlayerSeat(Settings, Tick, startLever: 0.45f);
            AirframeState s = Level();
            PilotInputs i = Issue(seat, Frame(), s);
            Assert.AreEqual(0.5f, i.Throttle, 1e-5f, "lever 0.45 of a 0.9 detent is half military power");
            Assert.IsFalse(i.Afterburner);

            DeviceSample keys = DeviceSample.Idle;
            keys.LeverRate = 1f;
            Issue(seat, Frame(keys: keys), s);
            Assert.AreEqual(0.45f + 0.5f * 0.02f, seat.Lever, 1e-5f, "throttle-up moves the lever at its rate");

            DeviceSample stick = DeviceSample.Idle;
            stick.LeverAbsolute = 0.95f;
            i = Issue(seat, Frame(stick: stick), s);
            Assert.IsTrue(i.Afterburner, "past the detent: afterburner");
            Assert.AreEqual(1f, i.Throttle);
            Assert.AreEqual(DeviceSource.None, seat.Owner, "moving the throttle does not take the stick");
        }

        [Test]
        public void SwitchToggles_FromAnySource_FlipTheSwitch()
        {
            var seat = new PlayerSeat(Settings, Tick);
            AirframeState s = Level();
            DeviceSample keys = DeviceSample.Idle;
            keys.Toggles = PilotSwitches.FlapsDown;
            Assert.IsTrue(Issue(seat, Frame(keys: keys), s).Has(PilotSwitches.FlapsDown));
            Assert.IsTrue(Issue(seat, Frame(), s).Has(PilotSwitches.FlapsDown), "a toggle stays until toggled again");
            DeviceSample pad = DeviceSample.Idle;
            pad.Toggles = PilotSwitches.FlapsDown | PilotSwitches.SpeedBrake;
            PilotInputs i = Issue(seat, Frame(pad: pad), s);
            Assert.IsFalse(i.Has(PilotSwitches.FlapsDown));
            Assert.IsTrue(i.Has(PilotSwitches.SpeedBrake));
        }

        // ── Reset (BLUEPRINT Q6) ──────────────────────────────────────────

        [Test]
        public void Reset_IsMomentary_FromAnySource_AndLeavesTheSwitchesAlone()
        {
            var seat = new PlayerSeat(Settings, Tick, startSwitches: PilotSwitches.FlapsDown);
            AirframeState s = Level();
            DeviceSample keys = DeviceSample.Idle, pad = DeviceSample.Idle;
            keys.Toggles = PilotSwitches.Reset;
            pad.Toggles = PilotSwitches.Reset;

            PilotInputs i = Issue(seat, Frame(keys: keys, pad: pad), s);
            Assert.IsTrue(i.Has(PilotSwitches.Reset), "two sources pressing it on one tick still reset (no XOR)");
            Assert.IsTrue(i.Has(PilotSwitches.FlapsDown), "the switches are untouched");
            i = Issue(seat, Frame(), s);
            Assert.IsFalse(i.Has(PilotSwitches.Reset), "momentary: one tick only");
            Assert.IsTrue(i.Has(PilotSwitches.FlapsDown));
        }

        [Test]
        public void ARunwayStart_HoldsTheGearDown_WithTheLeverAtIdle()
        {
            var seat = new PlayerSeat(Settings, Tick, startLever: 0.8f);   // the scenes' airborne cruise lever
            seat.SetStart(PilotSwitches.GearDown | PilotSwitches.Reset, 0f);
            PilotInputs i = Issue(seat, Frame(), Level());
            Assert.AreEqual(0f, i.Throttle, "idle: the jet must not roll off on its own (slice 2d)");
            Assert.IsFalse(i.Afterburner);
            Assert.IsTrue(i.Has(PilotSwitches.GearDown));
            Assert.IsFalse(i.Has(PilotSwitches.Reset), "a start never carries a reset");
        }

        [Test]
        public void AReset_PutsTheJetBackAtItsStart_EvenAfterACrash_AndTheReplayRepeatsIt()
        {
            AircraftProfile p = F18E();
            AirframeState low = AircraftFactory.LevelStart(p, new Vector3(0f, 300f, 0f), 0f, 200f, 3000f);
            var seat = new PlayerSeat(Settings, Tick, startLever: 0.8f);
            var live = new FlightCore(new SubstepClock(Tick, 4), new FlatSurface(0f));
            live.Add(AircraftFactory.Build(1, p, Sheet(), low), seat);
            Core.Aircraft jet = live.Find(1);

            long t = 0;
            for (; t < 1000 && jet.State.Status == FlightStatus.Flying; t++) { seat.Submit(Frame(keys: Moved(pitch: -1f))); live.Step(); }
            Assert.AreEqual(FlightStatus.Crashed, jet.State.Status, "full forward stick from 300 m meets the sea");

            DeviceSample reset = DeviceSample.Idle;
            reset.Toggles = PilotSwitches.Reset;
            seat.Submit(Frame(keys: reset));
            live.Step();
            Assert.AreEqual(1, jet.Resets);
            Assert.AreEqual(FlightStatus.Flying, jet.State.Status, "back in the air");

            // The reset tick flies exactly what a fresh jet flies on its first tick with the same inputs.
            live.Inputs.TryGet(t, 1, out InputEntry resetTick);
            var fresh = new FlightCore(new SubstepClock(Tick, 4), new FlatSurface(0f));
            PilotInputs same = resetTick.Inputs;
            same.Switches &= ~PilotSwitches.Reset;
            fresh.Add(AircraftFactory.Build(1, p, Sheet(), low), new HoldSeat(same));
            fresh.Step();
            Assert.AreEqual(fresh.Find(1).State.Position, jet.State.Position);
            Assert.AreEqual(fresh.Find(1).State.Velocity, jet.State.Velocity);

            for (int k = 0; k < 200; k++) { seat.Submit(Frame()); live.Step(); }
            var replay = new FlightCore(new SubstepClock(Tick, 4), new FlatSurface(0f));
            replay.Add(AircraftFactory.Build(1, p, Sheet(), low), new ReplaySeat(live.Inputs));
            for (long k = 0; k < live.Tick; k++) replay.Step();
            Assert.AreEqual(-1, live.Fixes.FirstDivergence(replay.Fixes), "crash, reset and the flight after it replay bit for bit");
            Assert.AreEqual(1, replay.Find(1).Resets);
        }

        // ── The instructor (W15) flies the L0 jet ─────────────────────────

        [Test]
        public void TheInstructor_BringsTheFlightPathOntoAFixedAimPoint()
        {
            AircraftProfile p = F18E();
            var seat = new PlayerSeat(Settings, Tick, startLever: 0.85f);
            var core = new FlightCore(new SubstepClock(Tick, 4), new FlatSurface(0f));
            core.Add(AircraftFactory.Build(1, p, Sheet(), Level()), seat);

            seat.Submit(Frame(mouse: true, yaw: 60f, pitch: 10f));   // aim 60° right, 10° up of the flight path
            core.Step();
            Vector3 aim = seat.AimDirection;
            Assert.AreEqual(DeviceSource.Mouse, seat.Owner);
            float startOff = Vector3.Angle(core.Find(1).State.Velocity, aim);

            for (int t = 0; t < 1000; t++) { seat.Submit(Frame()); core.Step(); }   // 20 s, hands off the mouse

            AirframeState s = core.Find(1).State;
            float off = Vector3.Angle(s.Velocity, aim);
            Debug.Log($"[AIRWING] instructor: {startOff:F1}° off the aim at the start, {off:F2}° after 20 s");
            Assert.AreEqual(FlightStatus.Flying, s.Status);
            Assert.Less(off, 2f, "the flight path is on the aim within 2° after 20 s");
        }

        // ── Replay (W28) and seat swap (W18) ──────────────────────────────

        static DeviceFrame Script(long t)
        {
            if (t == 0) return Frame(mouse: true, yaw: 40f, pitch: 5f);
            if (t < 400) return Frame();
            if (t < 450) return Frame(keys: Moved(roll: 0.5f, pitch: 0.2f));
            if (t == 600) return Frame(mouse: true, yaw: -70f, pitch: -3f);
            DeviceFrame f = Frame();
            if (t % 200 == 0) f.Keyboard.LeverRate = 1f;
            return f;
        }

        [Test]
        public void ReplayingTheInputLog_ReproducesTheFixBitForBit()
        {
            AircraftProfile p = F18E();
            var seat = new PlayerSeat(Settings, Tick, startLever: 0.8f);
            var live = new FlightCore(new SubstepClock(Tick, 4), new FlatSurface(0f));
            live.Add(AircraftFactory.Build(1, p, Sheet(), Level()), seat);
            for (long t = 0; t < 1500; t++) { seat.Submit(Script(t)); live.Step(); }

            var replay = new FlightCore(new SubstepClock(Tick, 4), new FlatSurface(0f));
            replay.Add(AircraftFactory.Build(1, p, Sheet(), Level()), new ReplaySeat(live.Inputs));
            for (long t = 0; t < 1500; t++) replay.Step();

            Assert.AreEqual(-1, live.Fixes.FirstDivergence(replay.Fixes), "the log alone reproduces the run (W28)");
            Assert.AreEqual(SeatKind.Player, live.Inputs.All[0].Seat);
            Assert.AreEqual(SeatKind.Replay, replay.Inputs.All[0].Seat);
            Assert.AreEqual(FlightStatus.Flying, live.Find(1).State.Status);
        }

        [Test]
        public void ASeatChangesHandsMidFlight_AndTheLogKeepsItsShape()
        {
            AircraftProfile p = F18E();
            var player = new PlayerSeat(Settings, Tick, startLever: 0.8f);
            var core = new FlightCore(new SubstepClock(Tick, 4), new FlatSurface(0f));
            core.Add(AircraftFactory.Build(1, p, Sheet(), Level()), player);
            for (long t = 0; t < 300; t++) { player.Submit(Script(t)); core.Step(); }

            core.Inputs.TryGet(299, 1, out InputEntry last);
            core.SetSeat(1, new HoldSeat(last.Inputs));
            for (int t = 0; t < 300; t++) core.Step();

            Assert.AreEqual(600, core.Inputs.Count, "one entry per tick, whoever sits in the seat");
            Assert.AreEqual(SeatKind.Player, core.Inputs.All[299].Seat);
            Assert.AreEqual(SeatKind.Hold, core.Inputs.All[300].Seat);
            Assert.AreEqual(last.Inputs, core.Inputs.All[300].Inputs, "the hold seat carries on with the last inputs");
        }

        [Test]
        public void AReplayWithAGapInItsLog_Throws()
        {
            var seat = new ReplaySeat(new InputLog());
            Assert.Throws<InvalidOperationException>(() => seat.Issue(0, 1, Level()));
        }
    }
}
