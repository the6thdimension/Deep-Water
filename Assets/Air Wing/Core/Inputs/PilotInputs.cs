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
    }

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
            Throttle.Equals(o.Throttle) && Afterburner == o.Afterburner && Switches == o.Switches;

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
                return h * 31 + (int)Switches;
            }
        }

        public override string ToString() => FormattableString.Invariant(
            $"pitch {StickPitch:+0.00;-0.00} roll {StickRoll:+0.00;-0.00} pedals {Pedals:+0.00;-0.00} throttle {Throttle:0.00}{(Afterburner ? " AB" : "")} [{Switches}]");
    }
}
