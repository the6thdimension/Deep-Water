using UnityEngine;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.State;

namespace DeepWater.AirWing.Core.Seats
{
    /// <summary>What a pilot asks the hands to fly (W37): toward an aim point, at an altitude and a speed.</summary>
    public struct FlightPathRequest
    {
        /// <summary>The point to steer toward; only its bearing is used, so a far point is a heading.</summary>
        public Vector3 AimWorld;
        /// <summary>The altitude to hold or reach, metres above the datum.</summary>
        public float AltitudeM;
        /// <summary>True airspeed to hold, m/s.</summary>
        public float SpeedMps;
        /// <summary>The gear and flaps switches the pilot wants (the hands pass them through).</summary>
        public PilotSwitches Switches;
        /// <summary>
        /// Feed-forward for following a moving path (RH Aviator slice 3b, formation): the flight-path angle to add to the
        /// altitude loop's, degrees, and the rate it is changing, degrees per second. Zero for a fixed altitude.
        /// </summary>
        public float PathDeg, PathRateDegS;
        /// <summary>
        /// Feed-forward bank, degrees, and roll rate, degrees per second (formation: the leader's): the hands bank this
        /// much plus what the aim asks, and roll at this rate plus their own correction, so a wingman rolls with its
        /// leader instead of waiting for the slot to move. Zero to steer on the aim alone.
        /// </summary>
        public float BankDeg, BankRateDegS;
        /// <summary>
        /// Landing: the ground guard stands down (the glide path is the guard), so the hands will fly the jet onto the
        /// runway (RH Aviator W41). False for everything else.
        /// </summary>
        public bool Landing;
        /// <summary>
        /// Limits for this request below the pilot's own, G and degrees of bank; zero for none (RH Aviator slice 3d: a
        /// pilot short of speed eases its turn to get it back).
        /// </summary>
        public float GLimit, BankLimitDeg;
    }

    /// <summary>The hands' gains and limits, part of a pilot's profile (RH Aviator W45). All AUTHORED.</summary>
    public struct HandsSettings
    {
        /// <summary>
        /// The path-following gain k in a = k·V²·sin η / L (the "L1" law: η the angle from the track to the aim, L the
        /// distance to it; 2 is the classic, well-damped value), and the bank never exceeded, degrees.
        /// </summary>
        public float GuidanceGain, MaxBankDeg;
        /// <summary>Roll rate commanded per degree of bank error, 1/s.</summary>
        public float RollRatePerBankDeg;
        /// <summary>Flight-path angle commanded per metre of altitude error, degrees, and its limits.</summary>
        public float ClimbDegPerM, MaxClimbDeg, MaxDiveDeg;
        /// <summary>Extra G per degree of flight-path angle error.</summary>
        public float GPerPathDeg;
        /// <summary>The most G this pilot will pull.</summary>
        public float MaxG;
        /// <summary>Throttle per knot of speed error, and the throttle trimmed to at zero error.</summary>
        public float ThrottlePerKt, ThrottleTrim;
        /// <summary>Throttle per knot-second of speed error: the integral that takes out the steady error.</summary>
        public float ThrottleIntegralPerKtS;
        /// <summary>Lights the afterburner when the throttle wants more than military.</summary>
        public bool UseAfterburner;
        /// <summary>Speed brake out above the requested speed by this many knots, with the throttle at idle.</summary>
        public float SpeedBrakeOverKt;
        /// <summary>
        /// The ground guard (Air Wing AP25): below this height above the surface, or heading there within the
        /// look-ahead at the present sink rate, the hands climb wings-nearly-level whatever was asked.
        /// </summary>
        public float FloorAglM, GuardLookAheadS, GuardClimbDeg, GuardMaxBankDeg;
    }

    /// <summary>
    /// The shared flight-path layer (RH Aviator W37, A2): turns a <see cref="FlightPathRequest"/> into Pilot Inputs
    /// on every tick, through the same stick the player uses (W6). A bounded law, unlike the mouse instructor's
    /// pursuit (AP25): the aim sets a sideways acceleration by the L1 path-following law, a = k·V²·sin η / L, and so a
    /// bank, tan φ = a / g, capped (slice 3a: a bank proportional to the heading error swung ±40–77 m across a gate's
    /// line and never settled); the altitude error sets a flight-path angle, capped;
    /// the flight-path error and the bank set the G (cos γ / cos φ to hold the path, plus a correction), capped by
    /// the pilot's own limit; the stick then asks the flight controls for that G and for the roll rate that brings
    /// the bank round. The throttle is proportional plus integral on the speed error, the integral held between ±1 (so
    /// it can reach any throttle from the trim) and frozen while the throttle sits at a stop (no wind-up). Deterministic: its only memory is that integral, and
    /// it reads nothing but the state it is handed; one instance per seat.
    /// </summary>
    public sealed class FlightPathHands
    {
        readonly HandsSettings _h;
        readonly ControlsProfile _c;
        readonly float _dt;
        float _throttleIntegral;
        float _seedThrottle = float.NaN;

        /// <summary>
        /// <paramref name="controls"/> is the aircraft's stick map (its G limits and roll rate), so the hands ask the
        /// flight controls for what they mean to.
        /// </summary>
        public FlightPathHands(in HandsSettings hands, in ControlsProfile controls, double tickSeconds = 0.02)
        {
            _h = hands;
            _c = controls;
            _dt = (float)tickSeconds;
        }

        /// <summary>
        /// Taking over mid-flight (Air Wing W42): the next <see cref="Fly"/> sets its integral so the throttle starts at
        /// <paramref name="throttle"/>, the one the aircraft had, instead of jumping to its trim.
        /// </summary>
        public void Resume(float throttle) => _seedThrottle = Mathf.Clamp01(throttle);

        /// <summary>Bank angle, degrees, right wing down positive.</summary>
        public static float BankDeg(in AirframeState s)
        {
            Vector3 right = s.Attitude * Vector3.right, up = s.Attitude * Vector3.up;
            return Mathf.Atan2(-right.y, up.y) * Mathf.Rad2Deg;
        }

        /// <summary>Flight-path angle, degrees, climbing positive.</summary>
        public static float PathDeg(in AirframeState s) =>
            Mathf.Asin(Mathf.Clamp(s.Velocity.y / Mathf.Max(s.Velocity.magnitude, 1f), -1f, 1f)) * Mathf.Rad2Deg;

        /// <summary>Heading of the flight path, degrees (0 = +Z, 90 = +X).</summary>
        public static float TrackDeg(in AirframeState s) => Mathf.Atan2(s.Velocity.x, s.Velocity.z) * Mathf.Rad2Deg;

        /// <summary>
        /// This tick's inputs for <paramref name="request"/>. <paramref name="surfaceHeightM"/> is the ground or sea
        /// under the aircraft, for the guard.
        /// </summary>
        public PilotInputs Fly(in FlightPathRequest request, in AirframeState s, float surfaceHeightM)
        {
            float bank = BankDeg(s), path = PathDeg(s), track = TrackDeg(s);

            // The aim → a sideways acceleration (L1) → a bank. Beyond 90° off, turn at the limit.
            Vector3 toAim = request.AimWorld - s.Position;
            float wantTrack = Mathf.Atan2(toAim.x, toAim.z) * Mathf.Rad2Deg;
            float headingError = Mathf.DeltaAngle(track, wantTrack);
            float bankWanted;
            float maxBank = request.BankLimitDeg > 0f ? Mathf.Min(_h.MaxBankDeg, request.BankLimitDeg) : _h.MaxBankDeg;
            if (Mathf.Abs(headingError) >= 90f) bankWanted = Mathf.Sign(headingError) * maxBank;
            else
            {
                // L is capped at the turn radius at the bank limit, so a far aim (a heading) still turns firmly
                // (slice 3a: an aim 1,000 km away gave almost no bank once the error fell under 90°).
                float v = new Vector2(s.Velocity.x, s.Velocity.z).magnitude;
                float turnRadius = v * v / (Units.StandardGravity * Mathf.Tan(_h.MaxBankDeg * Mathf.Deg2Rad));
                float l = Mathf.Clamp(new Vector2(toAim.x, toAim.z).magnitude, 1f, Mathf.Max(turnRadius, 1f));
                float lateralAccel = _h.GuidanceGain * v * v * Mathf.Sin(headingError * Mathf.Deg2Rad) / l;
                bankWanted = Mathf.Clamp(request.BankDeg + Mathf.Atan2(lateralAccel, Units.StandardGravity) * Mathf.Rad2Deg, -maxBank, maxBank);
            }

            // Altitude → flight-path angle, plus a moving path's own angle (formation).
            float pathWanted = Mathf.Clamp(request.PathDeg + _h.ClimbDegPerM * (request.AltitudeM - s.Position.y), -_h.MaxDiveDeg, _h.MaxClimbDeg);

            // The ground guard overrides both (AP25).
            float agl = s.Position.y - surfaceHeightM;
            float sink = Mathf.Max(0f, -s.Velocity.y);
            if (!request.Landing && (agl < _h.FloorAglM || agl - sink * _h.GuardLookAheadS < _h.FloorAglM))
            {
                pathWanted = Mathf.Max(pathWanted, _h.GuardClimbDeg);
                bankWanted = Mathf.Clamp(bankWanted, -_h.GuardMaxBankDeg, _h.GuardMaxBankDeg);
            }

            // Path and bank → G. Upside down (|bank| > 90°) the 1/cos term is meaningless: roll first, pull gently.
            float cosBank = Mathf.Cos(Mathf.Clamp(bank, -85f, 85f) * Mathf.Deg2Rad);
            // A path that is curving up or down needs V·γ̇ / g more (γ̇ = (n cos φ − cos γ) g / V, solved for n).
            float pathRateG = request.PathRateDegS * Mathf.Deg2Rad * s.Velocity.magnitude / Units.StandardGravity;
            float g = Mathf.Abs(bank) < 90f
                ? (Mathf.Cos(path * Mathf.Deg2Rad) + pathRateG) / cosBank + _h.GPerPathDeg * (pathWanted - path)
                : 1f;
            float maxG = request.GLimit > 0f ? Mathf.Min(_h.MaxG, request.GLimit) : _h.MaxG;
            g = Mathf.Clamp(g, Mathf.Max(_c.LoadFactorMin, -1f), Mathf.Min(maxG, _c.LoadFactorMax));

            // Bank → roll rate.
            float rollRateDeg = request.BankRateDegS + _h.RollRatePerBankDeg * Mathf.DeltaAngle(bank, bankWanted);
            float maxRollDeg = _c.RollRateMaxRadS * Mathf.Rad2Deg;

            // Speed → throttle, afterburner and speed brake.
            float speedErrorKt = (request.SpeedMps - s.Velocity.magnitude) / (float)Units.KnotToMetrePerSecond;
            if (!float.IsNaN(_seedThrottle))
            {
                _throttleIntegral = Mathf.Clamp(_seedThrottle - _h.ThrottleTrim - _h.ThrottlePerKt * speedErrorKt, -1f, 1f);
                _seedThrottle = float.NaN;
            }
            float throttle = _h.ThrottleTrim + _h.ThrottlePerKt * speedErrorKt + _throttleIntegral;
            bool atStop = (throttle >= 1f && speedErrorKt > 0f) || (throttle <= 0f && speedErrorKt < 0f);
            // ±1, not ±0.5 (slice 3b): at ±0.5 round a 0.75 trim the hands could not reach the 0.22 that 350 kt needs at
            // 5,000 ft, and a wingman sat 1.8 kt slow of its command and 5–7 m out of its slot.
            if (!atStop) _throttleIntegral = Mathf.Clamp(_throttleIntegral + _h.ThrottleIntegralPerKtS * speedErrorKt * _dt, -1f, 1f);
            var inputs = new PilotInputs
            {
                StickPitch = StickForG(g),
                StickRoll = Mathf.Clamp(rollRateDeg / maxRollDeg, -1f, 1f),
                Throttle = Mathf.Clamp01(throttle),
                Afterburner = _h.UseAfterburner && throttle > 1.05f,
                Switches = request.Switches,
            };
            if (throttle <= 0f && -speedErrorKt > _h.SpeedBrakeOverKt) inputs.Switches |= PilotSwitches.SpeedBrake;
            return inputs;
        }

        /// <summary>The stick that asks the flight controls for load factor <paramref name="n"/> (the inverse of their map, W26).</summary>
        public float StickForG(float n) => n >= 1f
            ? Mathf.Clamp01((n - 1f) / (_c.LoadFactorMax - 1f))
            : -Mathf.Clamp01((1f - n) / (1f - _c.LoadFactorMin));
    }
}
