using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.State;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// Where a wingman's slot is and how it moves (W40, fingertip): the leader's place from the flight core's picture
    /// (A1), with the slot offset in the leader's <b>level</b> frame (right, back and up along the leader's track, the
    /// horizon kept level), so a wingman stays level with its leader through a bank instead of riding up or down the
    /// leader's wing plane. The slot is moved on along the leader's present turn and climb, so a wingman aims where
    /// the slot will be, not where it was (a straight-line guess lags the slot round a turn).
    /// </summary>
    public readonly struct Slot
    {
        /// <summary>The slot now, and its velocity (on the leader's turn the outside slot is faster).</summary>
        public readonly Vector3 Position, Velocity;
        /// <summary>Along the slot's track (level, unit), and to its right.</summary>
        public readonly Vector3 Forward, Right;
        /// <summary>The leader's flight-path angle and its rate, degrees and degrees per second (feed-forward for the hands).</summary>
        public readonly float PathDeg, PathRateDegS;
        /// <summary>The leader's bank and roll rate, degrees and degrees per second (feed-forward for the hands).</summary>
        public readonly float BankDeg, BankRateDegS;

        readonly Vector3 _leader, _leaderForward, _leaderRight, _offset;
        readonly float _speedH, _climbMps, _turnRateRadS;

        /// <param name="position">
        /// Which slot in a flight (slice 3d: three wingmen all flew the one right-wing slot, on top of each other):
        /// 0 the profile's own; 1 the left wing; 2 the right wing; 3 outside the right wing, twice as far out and back.
        /// </param>
        public Slot(in AirframeState lead, in PilotProfile p, int position = 0)
        {
            float side = Mathf.Abs(p.SlotRightM), back = p.SlotBackM, right;
            switch (position)
            {
                case 1: right = -side; break;
                case 2: right = side; break;
                case 3: right = 2f * side; back *= 2f; break;
                default: right = p.SlotRightM; break;
            }
            Vector3 vh = lead.Velocity;
            vh.y = 0f;
            _speedH = vh.magnitude;
            Vector3 f = _speedH > 1f ? vh / _speedH : Level(lead.Attitude * Vector3.forward);
            _leaderForward = f;
            _leaderRight = Vector3.Cross(Vector3.up, f);
            _leader = lead.Position;
            _climbMps = lead.Velocity.y;
            _offset = _leaderRight * right - f * back + Vector3.up * p.SlotUpM;

            // The leader's turn and climb, without history, part from how it is rotating (its body rates turned into
            // the world) and part from its G (γ̇ = (n cos φ − cos γ) g / V), shared by the profile. In a steady turn the two agree; in a pull
            // the rotation leads the G by the leader's own pitch response. Slice 3b: on the G alone the wingman fell
            // 10 m behind the leader's 2 G pull; on the rotation alone it pulled 2.6 G for the leader's 2.13 and ended
            // 7 m high; the blend times it between the two.
            Vector3 spin = lead.Attitude * lead.BodyRates;   // Unity's sense: +X noses down, +Y yaws right
            float speed = Mathf.Max(lead.Velocity.magnitude, 1f), g = Units.StandardGravity;
            float bank = FlightPathHands.BankDeg(lead) * Mathf.Deg2Rad, path = FlightPathHands.PathDeg(lead) * Mathf.Deg2Rad;
            float turnFromG = lead.LoadFactor * Mathf.Sin(bank) * g / (speed * Mathf.Max(Mathf.Cos(path), 0.1f));
            float climbFromG = (lead.LoadFactor * Mathf.Cos(bank) - Mathf.Cos(path)) * g / speed;
            float w = p.SlotRotationShare;
            _turnRateRadS = w * Vector3.Dot(spin, Vector3.up) + (1f - w) * turnFromG;
            PathDeg = path * Mathf.Rad2Deg;
            PathRateDegS = (w * -Vector3.Dot(spin, _leaderRight) + (1f - w) * climbFromG) * Mathf.Rad2Deg;
            // The leader's own bank, not one worked out from its turn rate (tan φ = V ψ̇ / g holds only in a level 1 G
            // turn: in the leader's banked 2 G pull it asked 22° for the leader's 7°, slice 3b). At 40 m out on a 5.7 km
            // turn the slot's own bank differs by under 1 %. The roll rate: Unity's +Z body rate rolls the right wing up.
            BankDeg = FlightPathHands.BankDeg(lead);
            BankRateDegS = -lead.BodyRates.z * Mathf.Rad2Deg;

            Position = _leader + _offset;
            // A struct's constructor cannot call its own methods before every field is set: the static form does the work.
            Velocity = Predict(_leader, _leaderForward, _leaderRight, _offset, _speedH, _climbMps, _turnRateRadS, 0.5f)
                     - Predict(_leader, _leaderForward, _leaderRight, _offset, _speedH, _climbMps, _turnRateRadS, -0.5f);
            Vector3 track = Level(Velocity);
            Forward = track.sqrMagnitude > 0f ? track : f;
            Right = Vector3.Cross(Vector3.up, Forward);
        }

        /// <summary>Where the slot will be in <paramref name="seconds"/>, if the leader holds its turn and climb.</summary>
        public Vector3 PositionIn(float seconds) =>
            Predict(_leader, _leaderForward, _leaderRight, _offset, _speedH, _climbMps, _turnRateRadS, seconds);

        static Vector3 Predict(Vector3 leader, Vector3 forward, Vector3 right, Vector3 offset, float speedH, float climbMps, float turnRateRadS, float seconds)
        {
            float turn = turnRateRadS * seconds;
            Vector3 travel;
            if (Mathf.Abs(turn) < 1e-4f) travel = forward * (speedH * seconds);
            else
            {
                // An arc: forward by R sin θ, toward the turn by R (1 − cos θ); a right turn is a positive rate.
                float r = speedH / turnRateRadS;
                travel = forward * (r * Mathf.Sin(turn)) + right * (r * (1f - Mathf.Cos(turn)));
            }
            travel.y = climbMps * seconds;
            return leader + travel + Quaternion.Euler(0f, turn * Mathf.Rad2Deg, 0f) * offset;
        }

        /// <summary>The wingman's error from the slot: along the slot's track (positive: the slot is ahead), across it (positive: to the right) and up.</summary>
        public void ErrorOf(Vector3 wingman, out float along, out float across, out float up)
        {
            Vector3 rel = Position - wingman;
            up = rel.y;
            rel.y = 0f;
            along = Vector3.Dot(rel, Forward);
            across = Vector3.Dot(rel, Right);
        }

        /// <summary>How far the wingman is ahead of the leader itself, along the leader's track (positive: past it).</summary>
        public float AheadOfLeader(Vector3 wingman)
        {
            Vector3 rel = wingman - _leader;
            rel.y = 0f;
            return Vector3.Dot(rel, _leaderForward);
        }

        static Vector3 Level(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }
    }

    /// <summary>
    /// Join and Fingertip's plan (W40), worked out <b>every tick</b> from the picture, not on the 4 Hz watch: at
    /// 180 m/s the slot moves 45 m between watches, and the slot is held to ±5 m. Holding it is the hands' work (W44:
    /// the stick every tick); the watch decides the mode.
    /// <list type="bullet">
    /// <item><b>Speed:</b> the slot's own speed plus an overtake that closes the along-track gap: linear near the slot
    /// (the gain), never more than the pilot can take off by the time it arrives (√(2 a d)), and capped; asked of the
    /// hands with a lead on the gap to the speed flown, and the speed brake out when well over it (slice 3b: the
    /// wingman lagged a √(2 a d) plan built on 2 m/s² by 20–30 kt and slid 51 m past its leader).</item>
    /// <item><b>Steering:</b> the leader's bank and roll rate as a feed-forward, and the L1 law (A2) toward a point a
    /// look-ahead along the slot's present track to take out the error across.</item>
    /// <item><b>Height:</b> the slot's height, with the leader's flight-path angle and its rate as feed-forward, so a
    /// leader's pull is followed as it happens instead of after the height has gone; flown on the formation hands
    /// (<see cref="PilotProfile.FormationHands"/>), whose height and path gains are tighter than a route's.</item>
    /// </list>
    /// </summary>
    public static class Formation
    {
        public static FlightPathRequest Plan(in Slot slot, in AirframeState s, in PilotProfile p, bool joining)
        {
            slot.ErrorOf(s.Position, out float along, out _, out _);
            float d = Mathf.Abs(along);
            float maxOvertake = (joining ? p.JoinOvertakeKt : p.SlotOvertakeKt) * (float)Units.KnotToMetrePerSecond;
            float overtake = Mathf.Sign(along) * Mathf.Min(p.SlotGainPerS * d, Mathf.Sqrt(2f * p.JoinDecelMps2 * d), maxOvertake);
            float slotSpeed = slot.Velocity.magnitude;
            float target = Mathf.Max(slotSpeed + overtake, p.JoinMinKt * (float)Units.KnotToMetrePerSecond);
            float flown = s.Velocity.magnitude;
            // The speed lead: ask past the target by a share of the gap, so the hands' slow speed loop closes it in time.
            float speed = Mathf.Max(target + p.SlotSpeedLead * (target - flown), 1f);
            PilotSwitches switches = flown - speed > p.SlotSpeedBrakeOverKt * (float)Units.KnotToMetrePerSecond ? PilotSwitches.SpeedBrake : PilotSwitches.None;

            // The turn is flown by the slot's bank (feed-forward); the aim, a look-ahead along the slot's present track
            // from the slot (beyond it while still behind, so the turn-in is gentle), only corrects the place. A first law
            // aimed on the slot's predicted arc instead: the aim swung with every change in the leader's turn rate and
            // the wingman over-rolled each time, +22° then −15° round the leader's pull, and drifted 11 m across.
            float aheadM = p.SlotLookAheadM + Mathf.Max(0f, along);
            // Joining slow from below, level off and get the speed back before climbing to the slot (slice 3d: wingmen
            // rejoining from a fight 2,000 ft below at 160–220 kt hung in a full-afterburner climb and never closed).
            bool slowBelow = joining && flown < p.JoinMinKt * (float)Units.KnotToMetrePerSecond && slot.Position.y > s.Position.y;
            if (slowBelow)
                return new FlightPathRequest
                {
                    AimWorld = slot.Position + slot.Forward * aheadM,
                    AltitudeM = s.Position.y,
                    SpeedMps = speed,
                    Switches = switches,
                };
            return new FlightPathRequest
            {
                AimWorld = slot.Position + slot.Forward * aheadM,
                BankDeg = slot.BankDeg,
                BankRateDegS = slot.BankRateDegS,
                AltitudeM = slot.Position.y,
                SpeedMps = speed,
                Switches = switches,
                PathDeg = slot.PathDeg,
                PathRateDegS = slot.PathRateDegS,
            };
        }

        /// <summary>Rule "in the slot": within the slot's box (metres) and closing slower than the margin.</summary>
        public static bool InTheSlot(in Slot slot, in AirframeState s, in PilotProfile p)
        {
            slot.ErrorOf(s.Position, out float along, out float across, out float up);
            float closureKt = Vector3.Dot(s.Velocity - slot.Velocity, slot.Forward) / (float)Units.KnotToMetrePerSecond;
            return Mathf.Abs(along) < p.InSlotM && Mathf.Abs(across) < p.InSlotM && Mathf.Abs(up) < p.InSlotM && Mathf.Abs(closureKt) < p.InSlotClosureKt;
        }

        /// <summary>Rule "lost the slot": farther than the margin from it, in any direction.</summary>
        public static bool LostTheSlot(in Slot slot, in AirframeState s, in PilotProfile p) =>
            (slot.Position - s.Position).magnitude > p.LostSlotM;
    }
}
