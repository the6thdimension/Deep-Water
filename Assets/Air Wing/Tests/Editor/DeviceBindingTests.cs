using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.XInput;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Modules.Input;

namespace DeepWater.AirWing.Tests
{
    /// <summary>The state of a test flight stick: a stick, a twist (z), a throttle lever (rz) and four buttons.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 20)]
    public struct AirWingTestStickState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('A', 'W', 'S', 'T');

        [InputControl(name = "stick", layout = "Stick")]
        [FieldOffset(0)] public Vector2 stick;
        [InputControl(name = "z", layout = "Axis")]
        [FieldOffset(8)] public float z;
        [InputControl(name = "rz", layout = "Axis")]
        [FieldOffset(12)] public float rz;
        [InputControl(name = "trigger", layout = "Button", bit = 0)]
        [InputControl(name = "button2", layout = "Button", bit = 1)]
        [InputControl(name = "button3", layout = "Button", bit = 2)]
        [InputControl(name = "button4", layout = "Button", bit = 3)]
        [FieldOffset(16)] public uint buttons;
    }

    /// <summary>A generic HID flight stick, as Unity sees a stick it has no special layout for (the VelocityOne included).</summary>
    [InputControlLayout(stateType = typeof(AirWingTestStickState))]
    public class AirWingTestStick : Joystick { }

    /// <summary>
    /// Slice 1c, C18: <c>AirWingFlight.inputactions</c> driven by virtual devices, one test per device class: keyboard
    /// and mouse, an Xbox controller (XInput), and a generic HID flight stick with a twist and a throttle lever. Then the
    /// reader and the seat together. Proves the bindings, not the physical hardware: a real VelocityOne may still need
    /// its lever direction set (<c>invertLever</c>).
    /// </summary>
    public class DeviceBindingTests : InputTestFixture
    {
        const string ActionsPath = "Assets/Air Wing/Modules/Input/AirWingFlight.inputactions";

        static readonly ReaderSettings Reader = new ReaderSettings
        {
            Deadzone = 0.05f, MouseDegreesPerPixel = 0.1f, MouseMoveThresholdPixels = 0.5f, LeverMoveThreshold = 0.01f, InvertLever = false,
        };

        FlightDeviceReader _reader;

        public override void Setup()
        {
            base.Setup();
            InputSystem.RegisterLayout<AirWingTestStick>();
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            Assert.IsNotNull(asset, ActionsPath);
            _reader = new FlightDeviceReader(asset, Reader);
        }

        public override void TearDown()
        {
            _reader?.Dispose();
            base.TearDown();
        }

        DeviceFrame Tick()
        {
            _reader.Poll();
            return _reader.Read();
        }

        /// <summary>
        /// Unity's own per-axis stick deadzone (defaults 0.125 … 0.925, rescaled to 0 … 1), which applies before the
        /// reader sees a value. Measured on these virtual sticks: a raw 0.7 reads 0.71875.
        /// </summary>
        static float UnityAxisDeadzone(float v) =>
            Mathf.Sign(v) * Mathf.Clamp01((Mathf.Abs(v) - 0.125f) / (0.925f - 0.125f));

        [Test]
        public void KeyboardAndMouse()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();

            Press(keyboard.sKey);
            DeviceFrame f = Tick();
            Assert.AreEqual(1f, f.Keyboard.StickPitch, "S pulls");
            Assert.IsTrue(f.Keyboard.Moved);
            Release(keyboard.sKey);
            Press(keyboard.wKey); Assert.AreEqual(-1f, Tick().Keyboard.StickPitch, "W pushes"); Release(keyboard.wKey);
            Press(keyboard.dKey); Assert.AreEqual(1f, Tick().Keyboard.StickRoll, "D rolls right"); Release(keyboard.dKey);
            Press(keyboard.qKey); Assert.AreEqual(-1f, Tick().Keyboard.Pedals, "Q: left pedal"); Release(keyboard.qKey);

            f = Tick();
            Assert.IsFalse(f.Keyboard.Moved, "no flight key held: the keyboard has not moved");

            Press(keyboard.rightBracketKey);
            f = Tick();
            Assert.AreEqual(1f, f.Keyboard.LeverRate, "] raises the throttle");
            Assert.IsFalse(f.Keyboard.Moved, "the throttle keys do not take the stick");
            Release(keyboard.rightBracketKey);
            Press(keyboard.leftBracketKey); Assert.AreEqual(-1f, Tick().Keyboard.LeverRate, "[ lowers it"); Release(keyboard.leftBracketKey);

            Press(keyboard.fKey);
            _reader.Poll();
            Release(keyboard.fKey);
            Press(keyboard.bKey);
            _reader.Poll();
            Release(keyboard.bKey);
            f = _reader.Read();
            Assert.AreEqual(PilotSwitches.FlapsDown | PilotSwitches.SpeedBrake, f.Keyboard.Toggles, "F flaps, B speed brake, both kept until the tick");
            Assert.AreEqual(PilotSwitches.None, Tick().Keyboard.Toggles, "and handed over once");

            Press(keyboard.rKey); _reader.Poll(); Release(keyboard.rKey);
            Press(keyboard.rKey); _reader.Poll(); Release(keyboard.rKey);
            Assert.AreEqual(PilotSwitches.Reset, _reader.Read().Keyboard.Toggles, "R: reset; two presses between ticks are still one reset");

            Set(mouse.delta, new Vector2(10f, -4f));
            f = Tick();
            Assert.IsTrue(f.MouseMoved);
            Assert.AreEqual(1f, f.AimYawDeg, 1e-4f, "10 px at 0.1°/px");
            Assert.AreEqual(-0.4f, f.AimPitchDeg, 1e-4f);
        }

        [Test]
        public void XboxController()
        {
            var pad = InputSystem.AddDevice<XInputController>();

            Set(pad.leftStick, new Vector2(0.3f, -0.4f));
            DeviceFrame f = Tick();
            Assert.AreEqual(UnityAxisDeadzone(0.4f), f.Gamepad.StickPitch, 1e-3f, "stick back pulls");
            Assert.AreEqual(UnityAxisDeadzone(0.3f), f.Gamepad.StickRoll, 1e-3f);
            Assert.IsTrue(f.Gamepad.Moved);
            Set(pad.leftStick, new Vector2(0.05f, 0.05f));
            Assert.IsFalse(Tick().Gamepad.Moved, "inside the deadzone");

            Press(pad.rightShoulder); Assert.AreEqual(1f, Tick().Gamepad.Pedals, "RB: right pedal"); Release(pad.rightShoulder);
            Set(pad.rightTrigger, 1f); Assert.AreEqual(1f, Tick().Gamepad.LeverRate, 1e-3f, "RT raises the throttle"); Set(pad.rightTrigger, 0f);
            Set(pad.leftTrigger, 0.5f); Assert.AreEqual(-0.5f, Tick().Gamepad.LeverRate, 1e-3f, "LT lowers it"); Set(pad.leftTrigger, 0f);

            Press(pad.buttonNorth);
            f = Tick();
            Release(pad.buttonNorth);
            Assert.AreEqual(PilotSwitches.FlapsDown, f.Gamepad.Toggles, "Y: flaps");
            Press(pad.buttonWest); Assert.AreEqual(PilotSwitches.SpeedBrake, Tick().Gamepad.Toggles, "X: speed brake"); Release(pad.buttonWest);
            Press(pad.selectButton); Assert.AreEqual(PilotSwitches.Reset, Tick().Gamepad.Toggles, "View: reset"); Release(pad.selectButton);
        }

        [Test]
        public void GenericHidFlightStick()
        {
            var stick = (AirWingTestStick)InputSystem.AddDevice<AirWingTestStick>();

            DeviceFrame f = Tick();
            Assert.AreEqual(0.5f, f.Stick.LeverAbsolute, 1e-3f, "the first read syncs the seat to where the lever sits (raw 0: mid-travel)");
            Assert.IsFalse(f.Stick.Moved);

            Set(stick.stick, new Vector2(-0.6f, -0.7f));
            f = Tick();
            Assert.AreEqual(UnityAxisDeadzone(0.7f), f.Stick.StickPitch, 1e-3f, "stick back pulls");
            Assert.AreEqual(UnityAxisDeadzone(-0.6f), f.Stick.StickRoll, 1e-3f);
            Assert.IsTrue(f.Stick.Moved);
            Assert.IsTrue(float.IsNaN(f.Stick.LeverAbsolute), "an unmoved lever reports nothing");
            Set(stick.stick, Vector2.zero);

            Set((AxisControl)stick["z"], 0.5f);
            Assert.AreEqual(0.5f, Tick().Stick.Pedals, 1e-3f, "twist: pedals");
            Set((AxisControl)stick["z"], 0f);

            Set((AxisControl)stick["rz"], 1f);
            f = Tick();
            Assert.AreEqual(1f, f.Stick.LeverAbsolute, 1e-3f, "lever forward: full");
            Assert.IsFalse(f.Stick.Moved, "the lever does not take the stick");
            Set((AxisControl)stick["rz"], 0f);
            Assert.AreEqual(0.5f, Tick().Stick.LeverAbsolute, 1e-3f, "lever mid-travel: half");

            var button2 = (ButtonControl)stick["button2"];
            Press(button2);
            f = Tick();
            Release(button2);
            Assert.AreEqual(PilotSwitches.FlapsDown, f.Stick.Toggles, "button 2: flaps");
        }

        [Test]
        public void ReaderAndSeat_TheStickOverridesTheMouseOnTheTickItMoves()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var stick = (AirWingTestStick)InputSystem.AddDevice<AirWingTestStick>();
            var seat = new PlayerSeat(new PlayerSeatSettings
            {
                LeverRatePerSecond = 0.5f, AfterburnerDetent = 0.9f,
                Instructor = new InstructorSettings { RollGain = 2.5f, PitchGain = 4f, WingsLevelBlendDeg = 5f },
            }, 0.02);
            var state = new Core.State.AirframeState { Velocity = new Vector3(0f, 0f, 200f), Attitude = Quaternion.identity, MassKg = 20000f };

            Set(mouse.delta, new Vector2(20f, 0f));
            seat.Submit(Tick());
            seat.Issue(0, 1, state);
            Assert.AreEqual(DeviceSource.Mouse, seat.Owner);

            Set(mouse.delta, new Vector2(20f, 0f));
            Set(stick.stick, new Vector2(0.9f, 0f));
            seat.Submit(Tick());
            PilotInputs i = seat.Issue(1, 1, state);
            Assert.AreEqual(DeviceSource.Stick, seat.Owner, "the stick moved: it flies, at once");
            Assert.AreEqual(UnityAxisDeadzone(0.9f), i.StickRoll, 1e-3f);
        }
    }
}
