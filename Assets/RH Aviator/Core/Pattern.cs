using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Gates;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.State;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// A runway, as one landing direction (W41): its threshold on the centreline at the surface, the direction of
    /// landing, its length and width. Runway 09/27 is two of these; <see cref="PickEnd"/> chooses one.
    /// </summary>
    public readonly struct Runway
    {
        public readonly string Name;
        /// <summary>The landing threshold, on the centreline, at the runway's surface.</summary>
        public readonly Vector3 Threshold;
        /// <summary>The direction of landing (level, unit) and its right.</summary>
        public readonly Vector3 Along, Right;
        public readonly float LengthM, WidthM;

        public Runway(string name, Vector3 threshold, float landingHeadingDeg, float lengthM, float widthM)
        {
            Name = name;
            Threshold = threshold;
            Along = Quaternion.Euler(0f, landingHeadingDeg, 0f) * Vector3.forward;
            Right = Vector3.Cross(Vector3.up, Along);
            LengthM = lengthM;
            WidthM = widthM;
        }

        public float HeightM => Threshold.y;
        public float HeadingDeg => Mathf.Repeat(Mathf.Atan2(Along.x, Along.z) * Mathf.Rad2Deg, 360f);

        /// <summary>Where a point is: along the landing direction from the threshold, and to its right.</summary>
        public void Frame(Vector3 p, out float along, out float lateral)
        {
            Vector3 rel = p - Threshold;
            rel.y = 0f;
            along = Vector3.Dot(rel, Along);
            lateral = Vector3.Dot(rel, Right);
        }

        public bool Contains(Vector3 p)
        {
            Frame(p, out float along, out float lateral);
            return along >= 0f && along <= LengthM && Mathf.Abs(lateral) <= 0.5f * WidthM;
        }

        /// <summary>
        /// Of a strip's two landing directions (its centre, the heading of its length, its size), the one whose initial
        /// point lies nearer <paramref name="from"/>, so the pilot flies the shorter way round.
        /// </summary>
        public static Runway PickEnd(string name, Vector3 centre, float stripHeadingDeg, float lengthM, float widthM, Vector3 from, in PilotProfile p)
        {
            Runway a = End(name, centre, stripHeadingDeg, lengthM, widthM), b = End(name, centre, stripHeadingDeg + 180f, lengthM, widthM);
            float da = (Pattern.InitialPoint(a, p) - from).sqrMagnitude, db = (Pattern.InitialPoint(b, p) - from).sqrMagnitude;
            return da <= db ? a : b;
        }

        static Runway End(string name, Vector3 centre, float landingHeadingDeg, float lengthM, float widthM)
        {
            Vector3 along = Quaternion.Euler(0f, landingHeadingDeg, 0f) * Vector3.forward;
            int number = Mathf.RoundToInt(Mathf.Repeat(landingHeadingDeg, 360f) / 10f);
            if (number == 0) number = 36;
            return new Runway($"{name} {number:00}", centre - along * (0.5f * lengthM), landingHeadingDeg, lengthM, widthM);
        }
    }

    /// <summary>
    /// A runway strip, both ends (W41): its centre on the surface, the heading of its length, its size. The pilot picks
    /// the end when it turns for home, from where it is then (<see cref="EndFor"/>).
    /// </summary>
    public readonly struct Strip
    {
        public readonly string Name;
        public readonly Vector3 Centre;
        public readonly float HeadingDeg, LengthM, WidthM;

        public Strip(string name, Vector3 centre, float headingDeg, float lengthM, float widthM)
        {
            Name = name;
            Centre = centre;
            HeadingDeg = headingDeg;
            LengthM = lengthM;
            WidthM = widthM;
        }

        public Runway EndFor(Vector3 from, in PilotProfile p) => Runway.PickEnd(Name, Centre, HeadingDeg, LengthM, WidthM, from, p);
    }

    /// <summary>The legs of going home and the overhead break (W41, W47), in order.</summary>
    public enum PatternLeg : byte
    {
        /// <summary>Flying to the initial: the runway's line at the break height, well out.</summary>
        ToInitial = 0,
        /// <summary>On the initial: lined up with the runway, at the break height and speed, heading for the break point.</summary>
        Initial = 1,
        /// <summary>The break: a level turn at the pilot's bank limit onto the downwind side, slowing.</summary>
        Break = 2,
        /// <summary>Downwind: the reciprocal, abeam the runway, slowing to the approach speed, gear and flaps down.</summary>
        Downwind = 3,
        /// <summary>The turn to final (the "180"): a descending left turn at the pattern bank onto the centreline.</summary>
        Base = 4,
        /// <summary>The final: down the 3° path on the centreline, the flare.</summary>
        Final = 5,
        /// <summary>On the wheels: idle, the nose down, the brakes, the centreline held with the pedals.</summary>
        Rollout = 6,
        /// <summary>Stopped on the runway, brakes on.</summary>
        Stopped = 7,
    }

    /// <summary>
    /// The overhead break and the landing as legs on the hands (W41): each leg turns the jet's state into a flight-path
    /// request, as Navigate does for a gate; only the rollout works the controls itself (the hands fly, they do not
    /// taxi). Heights are above the runway. The break is to the left, as the U.S. Navy flies it.
    /// </summary>
    public static class Pattern
    {
        /// <summary>The break point: over the runway, this far past the threshold, at the break height.</summary>
        public static Vector3 BreakPoint(in Runway r, in PilotProfile p) =>
            r.Threshold + r.Along * p.BreakAlongM + Vector3.up * p.BreakHeightM;

        /// <summary>Where the initial begins: the break point, back down the runway's line by the initial's length.</summary>
        public static Vector3 InitialPoint(in Runway r, in PilotProfile p) => BreakPoint(r, p) - r.Along * p.InitialM;

        /// <summary>The initial as a gate at the break point, so Navigate's line-following and go-around fly it.</summary>
        public static Gate BreakGate(in Runway r, in PilotProfile p) =>
            new Gate($"{r.Name} initial", BreakPoint(r, p), r.HeadingDeg, p.InitialCaptureM, new Band(p.InitialKt, p.InitialKt), Band.Any);

        /// <summary>The 3° path's height above the runway at a point this far along from the threshold, metres.</summary>
        public static float GlidePathHeight(in Runway r, float along, in PilotProfile p) =>
            Mathf.Tan(p.GlidePathDeg * Mathf.Deg2Rad) * Mathf.Max(0f, p.TouchdownM - along);

        /// <summary>The downwind line's offset to the left of the centreline, metres.</summary>
        public static float DownwindOffset(in PilotProfile p) => p.DownwindOffsetM;

        public static FlightPathRequest Plan(PatternLeg leg, in Runway r, in AirframeState s, in PilotProfile p, bool goingAround)
        {
            const float Kt = (float)Units.KnotToMetrePerSecond;
            r.Frame(s.Position, out float along, out float lateral);
            float speed = s.Velocity.magnitude;
            PilotSwitches dirty = speed < p.GearKt * Kt ? PilotSwitches.GearDown | PilotSwitches.FlapsDown : PilotSwitches.None;
            switch (leg)
            {
                case PatternLeg.ToInitial:
                case PatternLeg.Initial:
                {
                    FlightPathRequest q = Navigate.Plan(BreakGate(r, p), s, p, goingAround);
                    q.SpeedMps = p.InitialKt * Kt;
                    return q;
                }
                case PatternLeg.Break:
                    // A level turn to the left, at the bank limit: the aim behind on the downwind side keeps the heading
                    // error past 90°, so the hands hold the limit until the turn is nearly round.
                    return new FlightPathRequest
                    {
                        AimWorld = s.Position - r.Along * 20000f - r.Right * p.DownwindOffsetM,
                        AltitudeM = r.HeightM + p.BreakHeightM,
                        SpeedMps = p.BreakKt * Kt,
                        Switches = dirty,
                    };
                case PatternLeg.Downwind:
                {
                    // The reciprocal line, offset to the left of the runway (as seen landing), a carrot ahead on it.
                    float carrot = Mathf.Max(p.CarrotM, Mathf.Abs(lateral + p.DownwindOffsetM));
                    Vector3 onLine = r.Threshold - r.Right * p.DownwindOffsetM + r.Along * (along - carrot);
                    return new FlightPathRequest
                    {
                        AimWorld = onLine,
                        AltitudeM = r.HeightM + p.DownwindHeightM,
                        SpeedMps = p.ApproachKt * Kt,
                        Switches = dirty,
                    };
                }
                case PatternLeg.Base:
                case PatternLeg.Final:
                {
                    // Onto the extended centreline: a carrot ahead on it, at least a turn radius at the pattern bank.
                    float turnRadius = speed * speed / (Units.StandardGravity * Mathf.Tan(p.PatternBankDeg * Mathf.Deg2Rad));
                    // On the final itself a short carrot pulls the jet onto the centreline in time (slice 3c: a turn
                    // radius of carrot, 680 m, left it 44 m off at 700 m out and touching down 10 m off).
                    float carrot = leg == PatternLeg.Final
                        ? Mathf.Max(p.FinalCarrotM, Mathf.Abs(lateral))
                        : Mathf.Max(Mathf.Max(p.CarrotM, turnRadius), Mathf.Abs(lateral));
                    float agl = s.Position.y - r.HeightM;
                    Vector3 aim = r.Threshold + r.Along * (along + carrot);
                    // In the turn, while more than 90° off the runway's heading, the aim is held off to the left, so the
                    // hands turn the pattern's way and not the short way round, which would carry the jet away from the runway.
                    Vector3 track = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
                    if (leg == PatternLeg.Base && Vector3.Angle(track, r.Along) > 90f)
                        aim = s.Position + Quaternion.Euler(0f, -120f, 0f) * track.normalized * 5000f;
                    var q = new FlightPathRequest
                    {
                        AimWorld = aim,
                        AltitudeM = r.HeightM + Mathf.Min(GlidePathHeight(r, along, p), p.DownwindHeightM),
                        SpeedMps = p.ApproachKt * Kt,
                        Switches = PilotSwitches.GearDown | PilotSwitches.FlapsDown,
                        // The ground guard stands down on the final only: in the turn it still guards (at the pattern's floor).
                        Landing = leg == PatternLeg.Final,
                    };
                    // On the path, its angle as feed-forward; past the downwind height the path is level.
                    if (GlidePathHeight(r, along, p) < p.DownwindHeightM) q.PathDeg = -p.GlidePathDeg;
                    // The flare: below its height, the descent eases toward a gentle angle and the height loop lets go.
                    if (agl < p.FlareHeightM)
                    {
                        q.AltitudeM = s.Position.y;
                        q.PathDeg = -Mathf.Lerp(p.FlarePathDeg, p.GlidePathDeg, Mathf.Clamp01(agl / p.FlareHeightM));
                    }
                    return q;
                }
                default:
                    return new FlightPathRequest
                    {
                        AimWorld = s.Position + r.Along * 1000f,
                        AltitudeM = s.Position.y,
                        SpeedMps = 0f,
                        Switches = PilotSwitches.GearDown | PilotSwitches.FlapsDown,
                        Landing = true,
                    };
            }
        }

        /// <summary>
        /// On the wheels (the rollout and stopped legs): idle; the nose flown down onto its wheel and then held;
        /// the brakes once it is down; the pedals hold the centreline (the nosewheel steers at taxi speed).
        /// </summary>
        public static PilotInputs Rollout(in Runway r, in AirframeState s, in PilotProfile p)
        {
            r.Frame(s.Position, out _, out float lateral);
            Vector3 nose = s.Attitude * Vector3.forward;
            float pitchDeg = Mathf.Asin(Mathf.Clamp(nose.y, -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 track = s.Velocity.sqrMagnitude > 1f ? s.Velocity : nose;
            track.y = 0f;
            float headingError = Vector3.SignedAngle(r.Along, track, Vector3.up);   // positive: pointing right of the line
            return new PilotInputs
            {
                Throttle = 0f,
                // Aft stick only to check the nose's fall (BodyRates.x > 0: falling). A term in the pitch itself, as Air
                // Wing's landing test flies, rotated the jet again after a flare at 141 kt and struck the tail (slice 3c).
                StickPitch = Mathf.Clamp01(0.03f * s.BodyRates.x * Mathf.Rad2Deg),
                Pedals = Mathf.Clamp(-p.RolloutPedalPerM * lateral - p.RolloutPedalPerDeg * headingError, -1f, 1f),
                Brakes = pitchDeg < 0.5f,
                Switches = PilotSwitches.GearDown | PilotSwitches.FlapsDown,
            };
        }
    }
}
