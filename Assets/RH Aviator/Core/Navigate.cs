using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Gates;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.State;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// Navigate's plan for one gate (W39): where to aim, how high and how fast.
    /// <list type="bullet">
    /// <item><b>On the way:</b> it aims at a carrot on the gate's line, ahead of its own place along the line by at
    /// least its distance off the line (so it never cuts in steeper than 45°) and at least about a turn radius (so it
    /// does not swing across the line: slice 3a, a 400 m carrot against a 1.2 km turn swung −2,500 → +309 → −124 m).
    /// It rides the line through the ring (slice 3a: a carrot fixed at the entry point cut in at 90° and missed).</item>
    /// <item><b>Too close to line up, or past the gate:</b> it goes around: it aims back down the line, the lead-in
    /// plus two turn radii before the gate and two turn radii out to its own side, so the point is never inside its turn (slice 3a: an
    /// aim point inside the turning circle made it orbit for ever). The go-around is sticky: it holds until the pilot
    /// is back out at the lead-in (slice 3a: deciding afresh each watch flipped it at the boundary). The seat's rules
    /// decide it (<see cref="ShouldGoAround"/>, <see cref="BackOnTheLine"/>) and log it.</item>
    /// </list>
    /// It asks for the gate's altitude from the start, and a speed inside the gate's band.
    /// </summary>
    public static class Navigate
    {
        /// <summary>Where the pilot is relative to the gate's line: along it (negative before the gate) and off it (right positive).</summary>
        public static void OnTheLine(in Gate gate, in AirframeState s, out float along, out float lateral)
        {
            Vector3 f = gate.Facing;
            Vector3 rel = s.Position - gate.Centre;
            rel.y = 0f;
            along = Vector3.Dot(rel, f);
            lateral = Vector3.Dot(rel, Vector3.Cross(Vector3.up, f));
        }

        /// <summary>
        /// Lined up: heading within the capture angle of the line, and on its present track it would cross the gate's
        /// plane inside the capture fraction of the ring (slice 3a: a fixed offset tested at one moment rejected a
        /// pilot still settling toward the centre, and missed that gate 4's ring is smaller).
        /// </summary>
        public static bool Lined(in Gate gate, in AirframeState s, in PilotProfile p)
        {
            OnTheLine(gate, s, out float along, out float lateral);
            Vector3 track = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
            float trackError = Vector3.SignedAngle(gate.Facing, track, Vector3.up);   // positive: turning toward the right of the line
            if (Mathf.Abs(trackError) >= p.CaptureDeg) return false;
            float atTheGate = lateral + Mathf.Max(0f, -along) * Mathf.Tan(trackError * Mathf.Deg2Rad);
            return Mathf.Abs(atTheGate) < p.CaptureFraction * gate.RadiusM;
        }

        /// <summary>Rule: too close to line up (or past the gate) and not lined up.</summary>
        public static bool ShouldGoAround(in Gate gate, in AirframeState s, in PilotProfile p)
        {
            OnTheLine(gate, s, out float along, out _);
            return along > -p.MinRunM && !Lined(gate, s, p);
        }

        /// <summary>Rule: going around, the pilot is back out at the go-around point and can try the line again.</summary>
        public static bool BackOnTheLine(in Gate gate, in AirframeState s, in PilotProfile p)
        {
            OnTheLine(gate, s, out float along, out _);
            return along < -(p.LeadInM + 2f * TurnRadius(s, p));
        }

        /// <summary>The pilot's turn radius at its present speed and its bank limit, metres.</summary>
        public static float TurnRadius(in AirframeState s, in PilotProfile p)
        {
            float v = s.Velocity.magnitude;
            return v * v / (Units.StandardGravity * Mathf.Tan(p.Hands.MaxBankDeg * Mathf.Deg2Rad));
        }

        public static FlightPathRequest Plan(in Gate gate, in AirframeState s, in PilotProfile p, bool goingAround)
        {
            Vector3 f = gate.Facing;
            Vector3 side = Vector3.Cross(Vector3.up, f);        // the right of the line, looking along it
            OnTheLine(gate, s, out float along, out float lateral);

            Vector3 aim;
            if (goingAround)
            {
                float turnRadius = TurnRadius(s, p);
                aim = gate.Centre - f * (p.LeadInM + 2f * turnRadius) + side * (lateral >= 0f ? 1f : -1f) * 2f * turnRadius;
            }
            else
            {
                float carrot = Mathf.Max(Mathf.Max(p.CarrotM, p.CarrotTurnRadii * TurnRadius(s, p)), Mathf.Abs(lateral));
                aim = gate.Centre + f * Mathf.Min(along + carrot, p.ThroughM);
            }
            return new FlightPathRequest
            {
                AimWorld = aim,
                AltitudeM = gate.Centre.y,
                SpeedMps = SpeedFor(gate.SpeedKt, p) * (float)Units.KnotToMetrePerSecond,
            };
        }

        /// <summary>A speed inside the band, knots: its middle, or the cruise speed kept clear of an open edge.</summary>
        public static float SpeedFor(in Band band, in PilotProfile p)
        {
            bool hasMin = !float.IsNegativeInfinity(band.Min), hasMax = !float.IsPositiveInfinity(band.Max);
            if (hasMin && hasMax) return 0.5f * (band.Min + band.Max);
            if (hasMin) return Mathf.Max(p.CruiseKt, band.Min + p.BandMarginKt);
            if (hasMax) return Mathf.Min(p.CruiseKt, band.Max - p.BandMarginKt);
            return p.CruiseKt;
        }
    }
}
