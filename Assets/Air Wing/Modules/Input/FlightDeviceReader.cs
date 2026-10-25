using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Seats;

namespace DeepWater.AirWing.Modules.Input
{
    /// <summary>The reader's settings (AUTHORED), baked from a <c>PlayerSeatSettingsSO</c>.</summary>
    public struct ReaderSettings
    {
        /// <summary>Flight axes below this magnitude read as zero and do not count as moving the device.</summary>
        public float Deadzone;
        /// <summary>Mouse aim, degrees per pixel of mouse travel.</summary>
        public float MouseDegreesPerPixel;
        /// <summary>Mouse travel per tick below which the mouse does not count as moving, pixels.</summary>
        public float MouseMoveThresholdPixels;
        /// <summary>The stick's lever change, 0 … 1 scale, that counts as moving it.</summary>
        public float LeverMoveThreshold;
        /// <summary>Flip the stick's lever (idle at the other end).</summary>
        public bool InvertLever;
    }

    /// <summary>
    /// Reads Air Wing's devices (W10): <c>AirWingFlight.inputactions</c> has one action map per source (Keyboard,
    /// Mouse, Gamepad, Stick), so each device is read on its own and the seat can tell which one moved (W27).
    /// The joystick paths match <c>VelocityOne.inputactions</c>, so the VelocityOne reads as it does elsewhere.
    /// <para>Two calls, because the Input System updates per frame and the core ticks at 50 Hz:
    /// <see cref="Poll"/> every frame (Update) gathers button presses and mouse travel, so a press between two ticks is
    /// neither lost nor counted twice; <see cref="Read"/> once per tick (FixedUpdate) takes the axes and hands over what
    /// was gathered.</para>
    /// </summary>
    public sealed class FlightDeviceReader : IDisposable
    {
        const string Keyboard = "Keyboard", Mouse = "Mouse", Gamepad = "Gamepad", Stick = "Stick";

        readonly InputActionAsset _actions;
        readonly ReaderSettings _s;
        readonly Source _keyboard, _gamepad, _stick;
        readonly InputAction _mouseDelta, _keyThrottleUp, _keyThrottleDown, _padThrottleUp, _padThrottleDown, _lever;

        Vector2 _mouseTravel;
        float _lastLever = float.NaN;

        /// <summary>The flight axes and switch buttons of one source.</summary>
        sealed class Source
        {
            public readonly InputAction Pitch, Roll, Pedals, Flaps, SpeedBrake, Gear;
            public PilotSwitches Pressed;

            public Source(InputActionMap map)
            {
                Pitch = map.FindAction("Pitch", true);
                Roll = map.FindAction("Roll", true);
                Pedals = map.FindAction("Pedals", true);
                Flaps = map.FindAction("Flaps", true);
                SpeedBrake = map.FindAction("SpeedBrake", true);
                Gear = map.FindAction("Gear", true);
            }

            public void Poll()
            {
                // Two presses between ticks flip a switch twice: XOR keeps that honest.
                if (Flaps.WasPressedThisFrame()) Pressed ^= PilotSwitches.FlapsDown;
                if (SpeedBrake.WasPressedThisFrame()) Pressed ^= PilotSwitches.SpeedBrake;
                if (Gear.WasPressedThisFrame()) Pressed ^= PilotSwitches.GearDown;
            }
        }

        public FlightDeviceReader(InputActionAsset actions, in ReaderSettings settings)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions), "assign AirWingFlight.inputactions");
            _actions = UnityEngine.Object.Instantiate(actions);
            _s = settings;
            _keyboard = new Source(_actions.FindActionMap(Keyboard, true));
            _gamepad = new Source(_actions.FindActionMap(Gamepad, true));
            _stick = new Source(_actions.FindActionMap(Stick, true));
            _mouseDelta = _actions.FindActionMap(Mouse, true).FindAction("Delta", true);
            _keyThrottleUp = _actions.FindActionMap(Keyboard, true).FindAction("ThrottleUp", true);
            _keyThrottleDown = _actions.FindActionMap(Keyboard, true).FindAction("ThrottleDown", true);
            _padThrottleUp = _actions.FindActionMap(Gamepad, true).FindAction("ThrottleUp", true);
            _padThrottleDown = _actions.FindActionMap(Gamepad, true).FindAction("ThrottleDown", true);
            _lever = _actions.FindActionMap(Stick, true).FindAction("Lever", true);
            _actions.Enable();
        }

        /// <summary>Every frame: gather button presses and mouse travel since the last tick.</summary>
        public void Poll()
        {
            _keyboard.Poll();
            _gamepad.Poll();
            _stick.Poll();
            _mouseTravel += _mouseDelta.ReadValue<Vector2>();
        }

        /// <summary>Once per tick: this tick's <see cref="DeviceFrame"/>, and reset what was gathered.</summary>
        public DeviceFrame Read()
        {
            var frame = new DeviceFrame
            {
                Keyboard = Sample(_keyboard),
                Gamepad = Sample(_gamepad),
                Stick = Sample(_stick),
            };

            frame.Keyboard.LeverRate = (_keyThrottleUp.IsPressed() ? 1f : 0f) - (_keyThrottleDown.IsPressed() ? 1f : 0f);
            frame.Gamepad.LeverRate = _padThrottleUp.ReadValue<float>() - _padThrottleDown.ReadValue<float>();

            // The lever is read from its control, not the action: an action reads 0 whenever its control sits at the
            // default value, so a lever at mid-travel (raw 0) would read as idle. Raw −1 … +1 becomes 0 … 1.
            var leverControls = _lever.controls;
            if (leverControls.Count > 0 && leverControls[0] is AxisControl axis)
            {
                float lever = (axis.ReadValue() + 1f) * 0.5f;
                if (_s.InvertLever) lever = 1f - lever;
                // The first read syncs the seat to where the physical lever sits; after that only a move counts.
                if (float.IsNaN(_lastLever) || Mathf.Abs(lever - _lastLever) >= _s.LeverMoveThreshold)
                {
                    frame.Stick.LeverAbsolute = lever;
                    _lastLever = lever;
                }
            }

            frame.MouseMoved = _mouseTravel.magnitude >= _s.MouseMoveThresholdPixels;
            frame.AimYawDeg = _mouseTravel.x * _s.MouseDegreesPerPixel;
            frame.AimPitchDeg = _mouseTravel.y * _s.MouseDegreesPerPixel;
            _mouseTravel = Vector2.zero;
            return frame;
        }

        DeviceSample Sample(Source src)
        {
            var s = DeviceSample.Idle;
            s.StickPitch = Dead(src.Pitch.ReadValue<float>());
            s.StickRoll = Dead(src.Roll.ReadValue<float>());
            s.Pedals = Dead(src.Pedals.ReadValue<float>());
            s.Moved = s.StickPitch != 0f || s.StickRoll != 0f || s.Pedals != 0f;
            s.Toggles = src.Pressed;
            src.Pressed = PilotSwitches.None;
            return s;
        }

        float Dead(float v) => Mathf.Abs(v) < _s.Deadzone ? 0f : Mathf.Clamp(v, -1f, 1f);

        public void Dispose()
        {
            _actions.Disable();
            if (Application.isPlaying) UnityEngine.Object.Destroy(_actions);
            else UnityEngine.Object.DestroyImmediate(_actions);
        }
    }
}
