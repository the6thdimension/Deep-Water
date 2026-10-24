using System;
using System.Collections.Generic;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Orders;
using UnityEngine;

namespace DeepWater.GeneralQuarters.Core.Helm
{
    /// <summary>
    /// The ship's motion in the horizontal plane, as the Helm and the Engine Room see it. Velocities are in the
    /// heading frame (surge forward, sway to starboard), so heel does not leak into them.
    /// </summary>
    public struct ShipMotion
    {
        public Vector3 Position;
        /// <summary>Compass heading, degrees (0 = +Z, 90 = +X).</summary>
        public float HeadingDeg;
        public float SurgeMs;
        public float SwayMs;
        /// <summary>Yaw rate, rad/s; positive turns the bow to starboard.</summary>
        public float YawRateRadS;
    }

    /// <summary>What the Helm asks of the Engine Room and the steering gear this step.</summary>
    public struct HelmOutput
    {
        /// <summary>Ordered rudder angle, degrees (both rudders at L0); positive turns to starboard.</summary>
        public float RudderOrderDeg;
        /// <summary>Ordered shaft speed, rev/s (both shafts at L0); negative is astern.</summary>
        public float ShaftOrderRps;
        /// <summary>The course being steered (ordered, or the route's), degrees; NaN in manual.</summary>
        public float SteeringCourseDeg;
        /// <summary>The speed being made good (ordered or the bell's), knots.</summary>
        public float TargetSpeedKn;
        /// <summary>Route only: the waypoint being steered for, −1 otherwise.</summary>
        public int Waypoint;
    }

    /// <summary>How a ship's hull and shafts answer, as the Helm needs to know them (from the Engine Room profile).</summary>
    public interface IShipAnswers
    {
        /// <summary>Shaft speed that holds <paramref name="speedMs"/> in a steady straight run, rev/s (negative astern).</summary>
        float SteadyShaftRps(float speedMs);
        /// <summary>Steady turning radius at <paramref name="rudderDeg"/> from an approach at <paramref name="speedMs"/>, m.</summary>
        float TurningRadiusM(float speedMs, float rudderDeg);
        float MaxRudderDeg { get; }
        float MaxShaftRps { get; }
        float MinShaftRps { get; }
    }

    /// <summary>Helm settings: telegraph bells, autopilot gains, route steering. AUTHORED unless a profile says otherwise.</summary>
    [Serializable]
    public struct HelmProfileData
    {
        /// <summary>Knots for each ahead bell, indexed by <see cref="EngineBell"/> (Stop, ⅓, ⅔, standard, full, flank).</summary>
        public float[] AheadBellKn;
        /// <summary>
        /// Astern bells as a fraction of the maximum astern revolutions, indexed by −bell − 1 (⅓, ⅔, full, emergency).
        /// Astern bells are revolutions, not speeds: holding a steady speed astern takes little thrust, but backing
        /// to stop a ship takes a lot.
        /// </summary>
        public float[] AsternBellFraction;
        /// <summary>Course hold: rudder degrees per degree of heading error.</summary>
        public float CourseKp;
        /// <summary>Course hold: rudder degrees per (degree/s) of yaw rate, damping the turn.</summary>
        public float CourseKd;
        /// <summary>Course hold: rudder degrees per (degree·s) of accumulated error, for steady yaw from current or wind.</summary>
        public float CourseKi;
        /// <summary>Course hold and routes use at most this much rudder; hard over is for the conn to order.</summary>
        public float CourseMaxRudderDeg;
        /// <summary>Speed hold: rev/s per knot of speed error, on top of the steady-speed feed-forward.</summary>
        public float SpeedKp;
        /// <summary>Speed hold: rev/s per (knot·s) of accumulated error.</summary>
        public float SpeedKi;
        /// <summary>Route: rudder used to plan turns (sets the wheel-over point), degrees.</summary>
        public float RouteRudderDeg;
        /// <summary>Route: line-of-sight look-ahead along the leg, in ship lengths.</summary>
        public float LookAheadShipLengths;
        public float ShipLengthM;

        public float AheadKn(EngineBell bell) => AheadBellKn[(int)bell];
        public float AsternFraction(EngineBell bell) => AsternBellFraction[-(int)bell - 1];
    }

    /// <summary>A route: waypoints in order (world x/z, metres). The Helm steers leg by leg and stops answering the last one.</summary>
    public sealed class Route
    {
        public readonly Vector2[] Waypoints;

        public Route(params Vector2[] waypoints)
        {
            if (waypoints == null || waypoints.Length < 2) throw new ArgumentException("a route needs at least two waypoints");
            Waypoints = waypoints;
        }
    }

    public interface IHelm
    {
        FidelityResolution Fidelity { get; }
        void AddRoute(int id, Route route);
        HelmOutput Step(in HelmCommand command, in ShipMotion motion, float dt);
    }

    public static class HelmFactory
    {
        public static IHelm Create(FidelityLevel requested, in HelmProfileData profile, IShipAnswers ship)
        {
            FidelityResolution r = FidelityLadder.Resolve(ElementKind.Helm, requested);
            return new HelmL0(r, profile, ship);
        }
    }

    /// <summary>
    /// Helm L0: course hold by a PID autopilot on heading, speed hold by a steady-speed feed-forward plus PI
    /// trim, bells straight to shaft speed, and routes by line-of-sight steering with a wheel-over point.
    /// Truth position (L1 adds GPS/INS error).
    ///
    /// Wheel-over: a ship turning at radius R onto a leg Δψ away must start the turn R·tan(Δψ/2) before the
    /// waypoint, or it overshoots the new leg. R comes from the Engine Room's own turning table at the route
    /// rudder, so the planning matches how this hull actually turns.
    /// </summary>
    public sealed class HelmL0 : IHelm
    {
        const float KnToMs = 0.514444f;

        readonly HelmProfileData _p;
        readonly IShipAnswers _ship;
        readonly Dictionary<int, Route> _routes = new Dictionary<int, Route>();
        float _courseIntegral, _speedIntegral;
        int _routeId = -1, _leg;
        int _lastSequence = int.MinValue;

        public FidelityResolution Fidelity { get; }

        public HelmL0(FidelityResolution fidelity, in HelmProfileData profile, IShipAnswers ship)
        {
            Fidelity = fidelity;
            _p = profile;
            _ship = ship;
        }

        public void AddRoute(int id, Route route) => _routes[id] = route;

        public HelmOutput Step(in HelmCommand command, in ShipMotion motion, float dt)
        {
            if (command.OrderSequence != _lastSequence)
            {
                // A new order: start its controllers fresh, and a new route from its first leg.
                _lastSequence = command.OrderSequence;
                _courseIntegral = _speedIntegral = 0f;
                if (command.Course == CourseMode.Route && command.RouteId != _routeId) { _routeId = command.RouteId; _leg = 0; }
            }

            var output = new HelmOutput { Waypoint = -1, SteeringCourseDeg = float.NaN };
            float speedMs = motion.SurgeMs;

            // ── Steering ──
            switch (command.Course)
            {
                case CourseMode.Manual:
                    output.RudderOrderDeg = Mathf.Clamp(command.RudderDeg, -_ship.MaxRudderDeg, _ship.MaxRudderDeg);
                    break;
                case CourseMode.Route when _routes.TryGetValue(command.RouteId, out Route route):
                    output.SteeringCourseDeg = RouteCourse(route, motion, out output.Waypoint);
                    output.RudderOrderDeg = HoldCourse(output.SteeringCourseDeg, motion, dt);
                    break;
                default:
                    // Course hold; a route that does not exist holds the last ordered course.
                    output.SteeringCourseDeg = command.CourseDeg;
                    output.RudderOrderDeg = HoldCourse(command.CourseDeg, motion, dt);
                    break;
            }

            // ── Speed ──
            if (command.Speed == SpeedMode.Bell && command.Bell >= EngineBell.Stop)
            {
                // An ahead bell is a shaft speed, not a speed held: the engine room rings up the revolutions that
                // make that speed in a straight run, and the ship gets there or not.
                output.TargetSpeedKn = _p.AheadKn(command.Bell);
                output.ShaftOrderRps = _ship.SteadyShaftRps(output.TargetSpeedKn * KnToMs);
            }
            else if (command.Speed == SpeedMode.Bell)
            {
                output.TargetSpeedKn = float.NaN;   // astern revolutions, not a speed
                output.ShaftOrderRps = _p.AsternFraction(command.Bell) * _ship.MinShaftRps;
            }
            else
            {
                output.TargetSpeedKn = command.SpeedKn;
                float errorKn = command.SpeedKn - speedMs / KnToMs;
                float rps = _ship.SteadyShaftRps(command.SpeedKn * KnToMs) + _p.SpeedKp * errorKn + _p.SpeedKi * _speedIntegral;
                float clamped = Mathf.Clamp(rps, _ship.MinShaftRps, _ship.MaxShaftRps);
                if (clamped == rps) _speedIntegral += errorKn * dt;   // anti-windup: stop integrating at the stops
                output.ShaftOrderRps = clamped;
            }
            return output;
        }

        float HoldCourse(float courseDeg, in ShipMotion motion, float dt)
        {
            float errorDeg = CompassMath.Difference(motion.HeadingDeg, courseDeg);
            float rateDegS = motion.YawRateRadS * Mathf.Rad2Deg;
            float rudder = _p.CourseKp * errorDeg - _p.CourseKd * rateDegS + _p.CourseKi * _courseIntegral;
            float limit = Mathf.Min(_p.CourseMaxRudderDeg, _ship.MaxRudderDeg);
            float clamped = Mathf.Clamp(rudder, -limit, limit);
            if (clamped == rudder) _courseIntegral += errorDeg * dt;
            return clamped;
        }

        /// <summary>Line-of-sight course along the current leg, advancing legs at the wheel-over point.</summary>
        float RouteCourse(Route route, in ShipMotion motion, out int waypoint)
        {
            Vector2 p = new Vector2(motion.Position.x, motion.Position.z);
            Vector2[] w = route.Waypoints;
            while (_leg < w.Length - 2)
            {
                Vector2 to = w[_leg + 1];
                float legIn = Bearing(w[_leg], to), legOut = Bearing(to, w[_leg + 2]);
                float turn = Mathf.Abs(CompassMath.Difference(legIn, legOut)) * Mathf.Deg2Rad;
                float radius = _ship.TurningRadiusM(Mathf.Max(motion.SurgeMs, 1f), _p.RouteRudderDeg);
                float wheelOver = radius * Mathf.Tan(0.5f * turn);
                Vector2 dir = (to - w[_leg]).normalized;
                float alongToGo = Vector2.Dot(to - p, dir);
                if (alongToGo > wheelOver) break;
                _leg++;
            }
            Vector2 a = w[_leg], b = w[_leg + 1];
            Vector2 legDir = (b - a).normalized;
            // Cross-track error, positive when the ship is to starboard of the leg.
            float crossTrack = legDir.x * (p.y - a.y) * -1f + legDir.y * (p.x - a.x);
            float lookAhead = Mathf.Max(1f, _p.LookAheadShipLengths * _p.ShipLengthM);
            waypoint = _leg + 1;
            return CompassMath.Normalize(Bearing(a, b) - Mathf.Atan2(crossTrack, lookAhead) * Mathf.Rad2Deg);
        }

        static float Bearing(Vector2 from, Vector2 to) => CompassMath.Normalize(Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg);
    }
}
