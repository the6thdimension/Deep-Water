using System;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
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
        /// <summary>This source's brake control is held (it does not take the stick).</summary>
        public bool Brakes;
        /// <summary>This source's gun trigger is held (it does not take the stick).</summary>
        public bool Fire;
        /// <summary>This source's missile pickle is held (W65; it does not take the stick).</summary>
        public bool FireMissile;

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
        /// <summary>
        /// Level hold (W54, SIX 2026-10-27: "can't hold level flight" on mouse and keyboard): with the keyboard or the
        /// pad flying and its stick centred this long (seconds), the instructor holds the jet level on the heading it had
        /// when the keys were let go. Zero: off (the stick stays centred, as a real stick does).
        /// </summary>
        public float LevelHoldDelayS;
        /// <summary>Mouse aim within this angle of the horizon snaps to level once the mouse stops (degrees; 0: off).</summary>
        public float AimLevelDetentDeg;
        /// <summary>The level hold's hands (the AI's bounded law, A2): its bank and G limits and gains. The throttle stays the player's.</summary>
        public HandsSettings Hold;
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
        int _centredTicks;
        float _holdYawDeg, _holdHeightM;
        FlightPathHands _holdHands;
        float _aimYawDeg, _aimPitchDeg;
        PilotSwitches _switches;

        public SeatKind Kind => SeatKind.Player;

        /// <summary>Which source flies the jet now; the HUD shows it (W27).</summary>
        public DeviceSource Owner { get; private set; } = DeviceSource.None;

        /// <summary>The throttle lever, 0 … 1 (the afterburner detent sits at <see cref="PlayerSeatSettings.AfterburnerDetent"/>).</summary>
        public float Lever { get; private set; }

        /// <summary>
        /// The aircraft's stick map, for the level hold's hands (W54). The runner passes it when it builds the jets;
        /// without it the hold stays off.
        /// </summary>
        public void UseControls(in ControlsProfile controls) => _holdHands = new FlightPathHands(_s.Hold, controls, _tickSeconds);

        /// <summary>True while the level hold flies the jet (W54); the HUD shows it.</summary>
        public bool LevelHolding { get; private set; }

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
            inputs.Brakes = f.Stick.Brakes || f.Gamepad.Brakes || f.Keyboard.Brakes;   // any source's brakes brake
            inputs.Trigger = f.Stick.Fire || f.Gamepad.Fire || f.Keyboard.Fire;      // and any source's trigger fires
            inputs.Pickle = f.Stick.FireMissile || f.Gamepad.FireMissile || f.Keyboard.FireMissile;   // and any source's pickle launches

            LevelHolding = false;
            switch (Owner)
            {
                case DeviceSource.Mouse:
                    if (f.MouseMoved)
                    {
                        _aimYawDeg += f.AimYawDeg;
                        _aimPitchDeg = Mathf.Clamp(_aimPitchDeg + f.AimPitchDeg, -89f, 89f);
                    }
                    // The detent: an aim left near the horizon is the horizon (W54).
                    else if (_s.AimLevelDetentDeg > 0f && Mathf.Abs(_aimPitchDeg) < _s.AimLevelDetentDeg) _aimPitchDeg = 0f;
                    _instructor.Steer(AimDirection, state, out inputs.StickPitch, out inputs.StickRoll);
                    break;
                case DeviceSource.Stick: Axes(f.Stick, ref inputs); break;
                case DeviceSource.Gamepad: HandsOffOr(f.Gamepad, state, ref inputs); break;
                case DeviceSource.Keyboard: HandsOffOr(f.Keyboard, state, ref inputs); break;
            }
            return inputs;
        }

        /// <summary>
        /// The keyboard's or the pad's axes, or, centred long enough, the level hold (W54): the instructor flies the
        /// level line of the heading the jet had when the stick was let go, so the jet neither drifts nor sinks.
        /// </summary>
        void HandsOffOr(in DeviceSample s, in AirframeState state, ref PilotInputs inputs)
        {
            bool centred = s.StickPitch == 0f && s.StickRoll == 0f && s.Pedals == 0f;
            if (!centred || _s.LevelHoldDelayS <= 0f || _holdHands == null || state.InContact)
            {
                _centredTicks = 0;
                Axes(s, ref inputs);
                return;
            }
            _centredTicks++;
            if (_centredTicks * _tickSeconds < _s.LevelHoldDelayS) { Axes(s, ref inputs); return; }
            if (_centredTicks * _tickSeconds - _tickSeconds < _s.LevelHoldDelayS)
            {
                // The moment it takes over: hold the present track and height. (Slice W54's first try steered the
                // instructor at a level aim; let go at 121° of bank it dived to −16° and wandered 6,000 ft: its pursuit
                // has no bank limit, Air Wing AP25. The hands are the bounded law.)
                Vector3 v = state.Velocity.sqrMagnitude > 1f ? state.Velocity : state.Attitude * Vector3.forward;
                _holdYawDeg = Mathf.Atan2(v.x, v.z) * Units.RadToDeg;
                _holdHeightM = state.Position.y;
            }
            LevelHolding = true;
            var hold = new FlightPathRequest
            {
                AimWorld = state.Position + Quaternion.Euler(0f, _holdYawDeg, 0f) * Vector3.forward * 10000f,
                AltitudeM = _holdHeightM,
                SpeedMps = state.Velocity.magnitude,   // unused: the throttle stays the player's lever
            };
            PilotInputs h = _holdHands.Fly(hold, state, float.NegativeInfinity);
            inputs.StickPitch = h.StickPitch;
            inputs.StickRoll = h.StickRoll;
        }

        /// <summary>
        /// Sets the switches and the lever for a start that differs from the seat's own: a ground start holds the gear
        /// down with the lever at idle (slice 2d: the airborne start's lever rolled the jet down the runway on its own).
        /// </summary>
        public void SetStart(PilotSwitches switches, float lever)
        {
            _switches = switches & ~PilotSwitches.Reset;
            Lever = Mathf.Clamp01(lever);
        }

        /// <summary>
        /// The player takes over an aircraft in flight (W42): the lever goes to where its throttle was and the switches
        /// to how it was flown, so nothing changes until the player moves something; the mouse aim starts on its flight
        /// path. Whoever owned the stick keeps it.
        /// </summary>
        public void TakeOver(in AirframeState state, in PilotInputs last)
        {
            _switches = last.Switches & ~PilotSwitches.Reset;
            float detent = _s.AfterburnerDetent;
            // Military power sits just below the detent: at the detent itself the lever would light the afterburner.
            Lever = last.Afterburner ? 1f : Mathf.Min(Mathf.Clamp01(last.Throttle) * detent, detent - 1e-4f);
            AimAlongPath(state);
            _realignAim = false;
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
