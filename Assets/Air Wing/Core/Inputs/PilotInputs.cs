using System;

namespace DeepWater.AirWing.Core.Inputs
{
    /// <summary>Two-state controls in the cockpit. Gear and hook do nothing until their parts exist (L1, carrier stream).</summary>
    [Flags]
    public enum PilotSwitches : ushort
    {
        None = 0,
        FlapsDown = 1 << 0,
        SpeedBrake = 1 << 1,
        GearDown = 1 << 2,
        HookDown = 1 << 3,
        AltitudeHold = 1 << 4,
        ApproachSpeedHold = 1 << 5,
        /// <summary>
        /// Momentary, not a switch (BLUEPRINT Q6): set for one tick, it puts the aircraft back at its start state
        /// before that tick flies. It travels in the Pilot Inputs so the input log records it and a replay repeats it.
        /// </summary>
        Reset = 1 << 15,
    }

    /// <summary>
    /// What the pilot asks of the radar this tick (SENSORS-CONCEPT J2, J6): a momentary command, not a held switch;
    /// <see cref="None"/> leaves it as it is. <see cref="Lock"/> names the track in <see cref="PilotInputs.RadarTrack"/>.
    /// </summary>
    public enum RadarCommand : byte { None = 0, Off = 1, RangeWhileSearch = 2, TrackWhileScan = 3, Dogfight = 4, Lock = 5, Unlock = 6 }

    /// <summary>
    /// The only way anything changes an aircraft (BLUEPRINT §1, rule 1; W6). The player's devices, the mouse
    /// instructor and the AI pilot all fill this same struct once per tick, and it means the same thing at every
    /// ladder level (W26): stick pitch commands a load factor, stick roll a roll rate. Dimensionless; the
    /// airframe's profile turns it into G and degrees per second.
    /// </summary>
    public struct PilotInputs : IEquatable<PilotInputs>
    {
        /// <summary>−1 … +1: commanded load factor between the profile's negative and positive G limits.</summary>
        public float StickPitch;
        /// <summary>−1 … +1: commanded roll rate up to the profile's maximum.</summary>
        public float StickRoll;
        /// <summary>−1 … +1: commanded sideslip / yaw rate.</summary>
        public float Pedals;
        /// <summary>0 … 1: military power.</summary>
        public float Throttle;
        /// <summary>Throttle past the afterburner detent.</summary>
        public bool Afterburner;
        /// <summary>Wheel brakes, held (not a switch): any source holding its brake control brakes.</summary>
        public bool Brakes;
        /// <summary>The gun's trigger, held (Gunnery K4): rounds go while it is held and the gun has any.</summary>
        public bool Trigger;
        /// <summary>The missile pickle, held (W65): with a lock, a missile goes, then one each launch interval while held.</summary>
        public bool Pickle;
        /// <summary>The radar command this tick (J2, J6); None leaves the radar as it is.</summary>
        public RadarCommand Radar;
        /// <summary>The track a <see cref="RadarCommand.Lock"/> names (the radar's own track id).</summary>
        public int RadarTrack;
        public PilotSwitches Switches;

        /// <summary>Stick centred, throttle idle, everything off.</summary>
        public static PilotInputs Neutral => default;

        public bool Has(PilotSwitches s) => (Switches & s) == s;

        /// <summary>
        /// Range-limited copy: devices overshoot, so finite values are clamped. A NaN or infinity is a bug in
        /// whoever issued it and throws, rather than being quietly zeroed.
        /// </summary>
        public PilotInputs Clamped()
        {
            return new PilotInputs
            {
                StickPitch = Clamp(StickPitch, -1f, 1f, nameof(StickPitch)),
                StickRoll = Clamp(StickRoll, -1f, 1f, nameof(StickRoll)),
                Pedals = Clamp(Pedals, -1f, 1f, nameof(Pedals)),
                Throttle = Clamp(Throttle, 0f, 1f, nameof(Throttle)),
                Afterburner = Afterburner,
                Brakes = Brakes,
                Trigger = Trigger,
                Pickle = Pickle,
                Radar = Radar,
                RadarTrack = RadarTrack,
                Switches = Switches,
            };
        }

        static float Clamp(float v, float lo, float hi, string field)
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
                throw new ArgumentException($"Pilot input {field} is {v}; a seat must issue finite values.");
            return v < lo ? lo : v > hi ? hi : v;
        }

        // Exact comparison on purpose: the input log and replay compare bit for bit (W28).
        public bool Equals(PilotInputs o) =>
            StickPitch.Equals(o.StickPitch) && StickRoll.Equals(o.StickRoll) && Pedals.Equals(o.Pedals) &&
            Throttle.Equals(o.Throttle) && Afterburner == o.Afterburner && Brakes == o.Brakes && Trigger == o.Trigger && Pickle == o.Pickle &&
            Radar == o.Radar && RadarTrack == o.RadarTrack && Switches == o.Switches;

        public override bool Equals(object obj) => obj is PilotInputs o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = StickPitch.GetHashCode();
                h = h * 31 + StickRoll.GetHashCode();
                h = h * 31 + Pedals.GetHashCode();
                h = h * 31 + Throttle.GetHashCode();
                h = h * 31 + (Afterburner ? 1 : 0);
                h = h * 31 + (Brakes ? 1 : 0);
                h = h * 31 + (Trigger ? 1 : 0);
                h = h * 31 + (Pickle ? 1 : 0);
                h = h * 31 + (int)Radar;
                h = h * 31 + RadarTrack;
                return h * 31 + (int)Switches;
            }
        }

        public override string ToString() => FormattableString.Invariant(
            $"pitch {StickPitch:+0.00;-0.00} roll {StickRoll:+0.00;-0.00} pedals {Pedals:+0.00;-0.00} throttle {Throttle:0.00}{(Afterburner ? " AB" : "")}{(Brakes ? " BRAKES" : "")}{(Trigger ? " GUN" : "")}{(Pickle ? " MSL" : "")}{(Radar != RadarCommand.None ? $" RADAR {Radar} {RadarTrack}" : "")} [{Switches}]");
    }
}
