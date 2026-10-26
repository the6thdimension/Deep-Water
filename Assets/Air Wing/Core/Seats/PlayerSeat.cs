using System;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.State;
using UnityEngine;

namespace DeepWater.AirWing.Core.Seats
{
    /// <summary>Where a player's input came from (W10, W27). The keyboard and the mouse are separate sources.</summary>
    public enum DeviceSource : byte
    {
        None = 0,
        Stick = 1,
        Gamepad = 2,
        Keyboard = 3,
        /// <summary>Mouse aim: the instructor flies (W15).</summary>
        Mouse = 4,
    }

    /// <summary>One source's input for one tick, already read from its device (the Unity side does the reading).</summary>
    public struct DeviceSample
    {
        /// <summary>A flight axis moved past the deadzone, or a flight button was pressed, this tick.</summary>
        public bool Moved;
        public float StickPitch;
        public float StickRoll;
        public float Pedals;
        /// <summary>A throttle lever's position, 0 (idle) … 1 (full afterburner); NaN when this source has no lever or did not move it.</summary>
        public float LeverAbsolute;
        /// <summary>Throttle-up minus throttle-down held this tick, −1 … +1 (keys, triggers).</summary>
        public float LeverRate;
        /// <summary>Switch toggles pressed this tick (each press flips that switch).</summary>
        public PilotSwitches Toggles;

        public static DeviceSample Idle => new DeviceSample { LeverAbsolute = float.NaN };
    }

    /// <summary>Every player source for one tick, plus how far the mouse turned the aim.</summary>
    public struct DeviceFrame
    {
        public DeviceSample Stick;
        public DeviceSample Gamepad;
        public DeviceSample Keyboard;
        /// <summary>The mouse moved enough to count this tick.</summary>
        public bool MouseMoved;
        /// <summary>Aim turned this tick, degrees: right and up.</summary>
        public float AimYawDeg;
        public float AimPitchDeg;

        public static DeviceFrame Idle => new DeviceFrame { Stick = DeviceSample.Idle, Gamepad = DeviceSample.Idle, Keyboard = DeviceSample.Idle };
    }

    /// <summary>The player seat's settings (AUTHORED), baked from a <c>PlayerSeatSettingsSO</c>.</summary>
    public struct PlayerSeatSettings
    {
        /// <summary>Lever travel per second at full throttle-up or throttle-down (keys, triggers).</summary>
        public float LeverRatePerSecond;
        /// <summary>Lever position of the afterburner detent: below it the throttle runs idle → military; at or above it, afterburner.</summary>
        public float AfterburnerDetent;
        public InstructorSettings Instructor;
    }

    /// <summary>
    /// The player's seat (C19, W10, W27). The Unity side reads every device once per tick and hands this seat a
    /// <see cref="DeviceFrame"/>; the seat decides who flies and issues ordinary Pilot Inputs, so nothing downstream
    /// knows or cares which device it was.
    /// <list type="bullet">
    /// <item>The last source moved past its deadzone owns stick and pedals. When two move on the same tick, the owner
    /// keeps it, else the stick wins, then the pad, then the keyboard.</item>
    /// <item>The mouse takes over only on a tick when no other source moved: any stick axis or flight key overrides the
    /// instructor at once (W15). When it takes over, the aim starts on the current flight path, so nothing jumps.</item>
    /// <item>The throttle is one lever shared by every source: a lever sets it, keys and triggers move it.</item>
    /// <item>Switch toggles from any source flip the switch.</item>
    /// </list>
    /// Pure and deterministic: the same frames give the same inputs, which the input log records (W28).
    /// </summary>
    public sealed class PlayerSeat : ISeat
    {
        readonly PlayerSeatSettings _s;
        readonly MouseInstructor _instructor;
        readonly double _tickSeconds;

        DeviceFrame _frame = DeviceFrame.Idle;
        bool _frameFresh, _realignAim;
        float _aimYawDeg, _aimPitchDeg;
        PilotSwitches _switches;

        public SeatKind Kind => SeatKind.Player;

        /// <summary>Which source flies the jet now; the HUD shows it (W27).</summary>
        public DeviceSource Owner { get; private set; } = DeviceSource.None;

        /// <summary>The throttle lever, 0 … 1 (the afterburner detent sits at <see cref="PlayerSeatSettings.AfterburnerDetent"/>).</summary>
        public float Lever { get; private set; }

        /// <summary>The mouse aim direction, world space (meaningful while <see cref="Owner"/> is <see cref="DeviceSource.Mouse"/>).</summary>
        public Vector3 AimDirection => Quaternion.Euler(-_aimPitchDeg, _aimYawDeg, 0f) * Vector3.forward;

        public PlayerSeat(in PlayerSeatSettings settings, double tickSeconds, PilotSwitches startSwitches = PilotSwitches.None, float startLever = 0f)
        {
            if (!(settings.AfterburnerDetent > 0f && settings.AfterburnerDetent <= 1f))
                throw new ArgumentOutOfRangeException(nameof(settings), settings.AfterburnerDetent, "the afterburner detent must be in (0, 1]");
            if (!(tickSeconds > 0)) throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "a tick must be positive");
            _s = settings;
            _instructor = new MouseInstructor(settings.Instructor);
            _tickSeconds = tickSeconds;
            _switches = startSwitches;
            Lever = Mathf.Clamp01(startLever);
        }

        /// <summary>This tick's devices. Call once per tick, before the core steps.</summary>
        public void Submit(in DeviceFrame frame)
        {
            _frame = frame;
            _frameFresh = true;
        }

        public PilotInputs Issue(long tick, int aircraftId, in AirframeState state)
        {
            // A tick with no fresh frame reuses the last axes but none of its edges or movement.
            DeviceFrame f = _frame;
            if (!_frameFresh)
            {
                f.Stick.Moved = f.Gamepad.Moved = f.Keyboard.Moved = f.MouseMoved = false;
                f.Stick.Toggles = f.Gamepad.Toggles = f.Keyboard.Toggles = PilotSwitches.None;
                f.Stick.LeverAbsolute = f.Gamepad.LeverAbsolute = f.Keyboard.LeverAbsolute = float.NaN;
                f.Stick.LeverRate = f.Gamepad.LeverRate = f.Keyboard.LeverRate = 0f;
            }
            _frameFresh = false;

            ChooseOwner(f, state);
            MoveLever(f.Stick);
            MoveLever(f.Gamepad);
            MoveLever(f.Keyboard);
            // Switches latch (each press flips one); Reset is momentary, and any source pressing it resets.
            PilotSwitches pressed = f.Stick.Toggles | f.Gamepad.Toggles | f.Keyboard.Toggles;
            bool reset = (pressed & PilotSwitches.Reset) != 0;
            _switches ^= (f.Stick.Toggles ^ f.Gamepad.Toggles ^ f.Keyboard.Toggles) & ~PilotSwitches.Reset;
            if (reset) _realignAim = true;
            else if (_realignAim)
            {
                // The tick after a reset: the jet is back at its start, so the mouse aim starts on its new flight path.
                AimAlongPath(state);
                _realignAim = false;
            }

            var inputs = new PilotInputs { Switches = reset ? _switches | PilotSwitches.Reset : _switches };
            float detent = _s.AfterburnerDetent;
            inputs.Afterburner = detent < 1f && Lever >= detent;
            inputs.Throttle = inputs.Afterburner ? 1f : Mathf.Clamp01(Lever / detent);

            switch (Owner)
            {
                case DeviceSource.Mouse:
                    if (f.MouseMoved)
                    {
                        _aimYawDeg += f.AimYawDeg;
                        _aimPitchDeg = Mathf.Clamp(_aimPitchDeg + f.AimPitchDeg, -89f, 89f);
                    }
                    _instructor.Steer(AimDirection, state, out inputs.StickPitch, out inputs.StickRoll);
                    break;
                case DeviceSource.Stick: Axes(f.Stick, ref inputs); break;
                case DeviceSource.Gamepad: Axes(f.Gamepad, ref inputs); break;
                case DeviceSource.Keyboard: Axes(f.Keyboard, ref inputs); break;
            }
            return inputs;
        }

        void ChooseOwner(in DeviceFrame f, in AirframeState state)
        {
            bool stick = f.Stick.Moved, pad = f.Gamepad.Moved, keys = f.Keyboard.Moved;
            if (stick || pad || keys)
            {
                bool ownerMoved = (Owner == DeviceSource.Stick && stick) || (Owner == DeviceSource.Gamepad && pad) || (Owner == DeviceSource.Keyboard && keys);
                if (!ownerMoved) Owner = stick ? DeviceSource.Stick : pad ? DeviceSource.Gamepad : DeviceSource.Keyboard;
                return;
            }
            if (f.MouseMoved && Owner != DeviceSource.Mouse)
            {
                // Start the aim on the flight path, so taking over with the mouse changes nothing until it moves.
                AimAlongPath(state);
                Owner = DeviceSource.Mouse;
            }
        }

        void AimAlongPath(in AirframeState state)
        {
            Vector3 path = state.Velocity.sqrMagnitude > 1f ? state.Velocity.normalized : state.Attitude * Vector3.forward;
            _aimYawDeg = Mathf.Atan2(path.x, path.z) * Units.RadToDeg;
            _aimPitchDeg = Mathf.Asin(Mathf.Clamp(path.y, -1f, 1f)) * Units.RadToDeg;
        }

        void MoveLever(in DeviceSample s)
        {
            if (!float.IsNaN(s.LeverAbsolute)) Lever = Mathf.Clamp01(s.LeverAbsolute);
            if (s.LeverRate != 0f) Lever = Mathf.Clamp01(Lever + s.LeverRate * _s.LeverRatePerSecond * (float)_tickSeconds);
        }

        static void Axes(in DeviceSample s, ref PilotInputs inputs)
        {
            inputs.StickPitch = s.StickPitch;
            inputs.StickRoll = s.StickRoll;
            inputs.Pedals = s.Pedals;
        }
    }
}
