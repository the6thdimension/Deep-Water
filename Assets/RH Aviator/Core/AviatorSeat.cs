using System;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Gates;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.State;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// RH Aviator's AI pilot (W36, W37, W44): an Air Wing seat that flies through the same Pilot Inputs as the player
    /// (W6). Every tick it watches its route and lets the hands fly the current request; on each watch (4 Hz on the
    /// tick count) its rules run, may change its mode, and the mode writes a new request. Pure and deterministic: it
    /// reads its own state, the flight core's picture of the previous tick (A1) and the truth feed's surface, and
    /// nothing else. A replay replays its inputs and does not re-run it.
    /// </summary>
    public sealed class AviatorSeat : ISeat
    {
        public const int WatchHz = 4;

        readonly FlightCore _core;
        readonly PilotProfile _pilot;
        readonly GateCourse _route;
        readonly RuleLog _log;
        readonly FlightPathHands _hands;

        AirframeState _last;
        bool _hasLast;
        int _target;
        bool _goingAround;
        FlightPathRequest _request;

        /// <param name="core">The flight core this seat sits in: its clock, its picture and its surface.</param>
        /// <param name="aircraft">The aircraft's profile, for its stick map.</param>
        /// <param name="route">The pilot's own copy of the route (it is watched as the pilot flies it).</param>
        public AviatorSeat(FlightCore core, in AircraftProfile aircraft, in PilotProfile pilot, GateCourse route, RuleLog log)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            pilot.Validate();
            _pilot = pilot;
            _route = route ?? throw new ArgumentNullException(nameof(route));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _hands = new FlightPathHands(pilot.Hands, aircraft.Controls, core.Clock.TickSeconds);
            Mode = PilotMode.Navigate;
            _target = _route.Next;
        }

        public SeatKind Kind => SeatKind.Ai;
        public PilotMode Mode { get; private set; }
        /// <summary>What the hands are flying now.</summary>
        public FlightPathRequest Request => _request;
        public GateCourse Route => _route;
        public RuleLog Log => _log;

        public PilotInputs Issue(long tick, int aircraftId, in AirframeState state)
        {
            // Every tick: did the last tick's flight cross the next gate?
            if (_hasLast && Mode == PilotMode.Navigate) _route.Observe(tick - 1, _last, state, out _);
            _last = state;
            _hasLast = true;

            if (Watch.IsWatch(tick, _core.Clock.TickSeconds, WatchHz)) Decide(tick, aircraftId, state);

            float surface = _core.Surface.HeightAt(state.Position.x, state.Position.z);
            return _hands.Fly(_request, state, surface);
        }

        /// <summary>One watch: the rules in order, then the mode's plan.</summary>
        void Decide(long tick, int id, in AirframeState s)
        {
            if (Mode == PilotMode.Navigate)
            {
                // Rule "course flown": a whole lap done → hold course.
                if (_route.Laps >= 1)
                    Fire(tick, id, "course flown", PilotMode.HoldCourse, $"all {_route.Gates.Count} gates flown; holding {_pilot.HoldAltitudeM:F0} m at {_pilot.HoldKt:F0} kt");
                // Rule "next gate": the route moved on → retarget, saying how the last gate went.
                else if (_route.Next != _target)
                {
                    GatePass pass = _route.Passes[_route.Passes.Count - 1];
                    Fire(tick, id, "next gate", PilotMode.Navigate,
                         $"{_route.Gates[pass.Gate].Name} flown at {pass.SpeedKt:F0} kt, {pass.AltitudeFt:F0} ft{(pass.Clean ? "" : " (outside its bands)")}; now {_route.Gates[_route.Next].Name}");
                    _target = _route.Next;
                    _goingAround = false;
                }

                // Rules "go around" and "back on the line" (sticky until back out at the lead-in).
                Gate gate = _route.Gates[_target];
                if (Mode == PilotMode.Navigate && !_goingAround && Navigate.ShouldGoAround(gate, s, _pilot))
                {
                    Navigate.OnTheLine(gate, s, out float along, out float lateral);
                    Fire(tick, id, "go around", PilotMode.Navigate, $"{gate.Name}: {-along:F0} m to go, {lateral:F0} m off the line; not lined up");
                    _goingAround = true;
                }
                else if (_goingAround && Navigate.BackOnTheLine(gate, s, _pilot))
                {
                    Fire(tick, id, "back on the line", PilotMode.Navigate, $"{gate.Name}: out at the lead-in; trying the line again");
                    _goingAround = false;
                }
            }

            switch (Mode)
            {
                case PilotMode.Navigate:
                    _request = Navigate.Plan(_route.Gates[_target], s, _pilot, _goingAround);
                    break;
                case PilotMode.HoldCourse:
                    Vector3 ahead = s.Velocity;
                    ahead.y = 0f;
                    _request = new FlightPathRequest
                    {
                        AimWorld = s.Position + (ahead.sqrMagnitude > 1f ? ahead.normalized : Vector3.forward) * 10000f,
                        AltitudeM = _pilot.HoldAltitudeM,
                        SpeedMps = _pilot.HoldKt * (float)Units.KnotToMetrePerSecond,
                    };
                    break;
            }
        }

        void Fire(long tick, int id, string rule, PilotMode to, string reason)
        {
            _log.Add(new RuleFiring(tick, id, rule, Mode, to, reason));
            Mode = to;
        }
    }
}
