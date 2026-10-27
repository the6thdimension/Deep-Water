using System;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Gates;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// RH Aviator's AI pilot (W36, W37, W40, W44): an Air Wing seat that flies through the same Pilot Inputs as the
    /// player (W6). Every tick it watches its route, keeps its formation slot, and lets the hands fly the current
    /// request; on each watch (4 Hz on the tick count) its rules run and may change its mode. Pure and deterministic:
    /// it reads its own state, the flight core's picture of the previous tick (A1) and the truth feed's surface, and
    /// nothing else. A replay replays its inputs and does not re-run it.
    /// <para>Its task is an order: fly a route (<see cref="FlyRoute"/>), join a leader's wing (<see cref="JoinOn"/>),
    /// or hold what it has (<see cref="HoldCourse"/>). Each order is logged as a rule. When the player hands the jet
    /// back after flying it (W42), the seat picks up where the jet is, not where it left it.</para>
    /// </summary>
    public sealed class AviatorSeat : ISeat, ISeatStatus
    {
        public const int WatchHz = 4;

        readonly FlightCore _core;
        readonly PilotProfile _pilot;
        readonly RuleLog _log;
        readonly FlightPathHands _hands, _formationHands, _patternHands;
        FlightPathHands _flying;

        GateCourse _route;
        AirframeState _last;
        long _lastTick = long.MinValue;
        int _target;
        bool _goingAround;
        int _leaderId;
        float _holdAltitudeM, _holdMps;
        Runway _home;
        Strip _strip;
        bool _hasHome, _hasStrip, _goingAroundHome;
        long _legTick;
        FlightPathRequest _request;
        string _status;

        /// <summary>A pilot with no task yet: it holds the height and speed it has until it is given one.</summary>
        /// <param name="core">The flight core this seat sits in: its clock, its picture and its surface.</param>
        /// <param name="aircraft">The aircraft's profile, for its stick map.</param>
        public AviatorSeat(FlightCore core, in AircraftProfile aircraft, in PilotProfile pilot, RuleLog log)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            pilot.Validate();
            _pilot = pilot;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _hands = new FlightPathHands(pilot.Hands, aircraft.Controls, core.Clock.TickSeconds);
            _formationHands = new FlightPathHands(pilot.FormationHands, aircraft.Controls, core.Clock.TickSeconds);
            _patternHands = new FlightPathHands(pilot.PatternHands, aircraft.Controls, core.Clock.TickSeconds);
            _flying = _hands;
            Mode = PilotMode.HoldCourse;
            _holdAltitudeM = float.NaN;   // taken from the jet on the first watch
            _status = "HOLD COURSE";
        }

        /// <param name="route">The pilot's own copy of the route (it is watched as the pilot flies it).</param>
        public AviatorSeat(FlightCore core, in AircraftProfile aircraft, in PilotProfile pilot, GateCourse route, RuleLog log)
            : this(core, aircraft, pilot, log)
        {
            _route = route ?? throw new ArgumentNullException(nameof(route));
            Mode = PilotMode.Navigate;
            _target = _route.Next;
            _status = Status();
        }

        public SeatKind Kind => SeatKind.Ai;
        public PilotMode Mode { get; private set; }
        /// <summary>What the hands are flying now.</summary>
        public FlightPathRequest Request => _request;
        public GateCourse Route => _route;
        public RuleLog Log => _log;
        /// <summary>The aircraft this pilot formates on (0: none).</summary>
        public int LeaderId => _leaderId;
        /// <summary>Going home: the leg flown, and the tick it began.</summary>
        public PatternLeg Leg { get; private set; }
        public long LegTick => _legTick;
        /// <summary>The runway end this pilot goes home to, once it has one.</summary>
        public bool HasHome => _hasHome || _hasStrip;
        public Runway Home => _home;
        /// <summary>For the HUD's pick list (W42): the mode in a few words, updated when it changes.</summary>
        string ISeatStatus.Status => _status;

        /// <summary>Order: fly <paramref name="route"/> from its next gate.</summary>
        public void FlyRoute(int aircraftId, GateCourse route)
        {
            _route = route ?? throw new ArgumentNullException(nameof(route));
            _target = _route.Next;
            _goingAround = false;
            Fire(_core.Tick, aircraftId, "ordered: fly the route", PilotMode.Navigate, $"{_route.Gates.Count} gates, next {_route.Gates[_target].Name}");
        }

        /// <summary>Order: join aircraft <paramref name="leaderId"/>'s wing in fingertip (W40).</summary>
        public void JoinOn(int aircraftId, int leaderId)
        {
            if (leaderId == aircraftId) throw new ArgumentException("a pilot cannot join its own wing", nameof(leaderId));
            _core.Find(leaderId);   // throws for an unknown aircraft
            _leaderId = leaderId;
            Fire(_core.Tick, aircraftId, "ordered: join", PilotMode.Join, $"on aircraft {leaderId}, {(_pilot.SlotRightM > 0f ? "right" : "left")} wing");
        }

        /// <summary>
        /// Where home is (W47): the runway the pilot goes to when ordered home or at bingo. Setting it orders nothing.
        /// </summary>
        public void SetHome(in Runway runway)
        {
            _home = runway;
            _hasHome = true;
        }

        /// <summary>Where home is, as a strip: the end is picked when the pilot turns for home. Setting it orders nothing.</summary>
        public void SetHome(in Strip strip)
        {
            _strip = strip;
            _hasStrip = true;
        }

        /// <summary>Order: go home to the strip set by <see cref="SetHome(in Strip)"/>, by the nearer end (the player's order, A3).</summary>
        public void GoHome(int aircraftId)
        {
            if (!_hasStrip) throw new InvalidOperationException("no home strip set");
            _home = _strip.EndFor(_core.Find(aircraftId).State.Position, _pilot);
            _hasHome = true;
            StartHome(_core.Tick, aircraftId, "ordered: go home", $"to {_home.Name}");
        }

        /// <summary>Order: go home to <paramref name="runway"/> and land (W41, W47; the player's order, A3).</summary>
        public void GoHome(int aircraftId, in Runway runway)
        {
            SetHome(runway);
            StartHome(_core.Tick, aircraftId, "ordered: go home", $"to {runway.Name}");
        }

        void StartHome(long tick, int id, string rule, string reason)
        {
            _goingAroundHome = false;
            Leg = PatternLeg.ToInitial;
            _legTick = tick;
            Fire(tick, id, rule, PilotMode.Home, $"{reason}; to the initial at {_pilot.BreakHeightM / 0.3048f:F0} ft");
        }

        /// <summary>Order: hold the present height and speed on the present heading.</summary>
        public void HoldCourse(int aircraftId)
        {
            _holdAltitudeM = float.NaN;
            Fire(_core.Tick, aircraftId, "ordered: hold course", PilotMode.HoldCourse, "holding what it has");
        }

        public PilotInputs Issue(long tick, int aircraftId, in AirframeState state)
        {
            // Back from the player (W42): the last state it saw is stale, so nothing is judged across the gap; the
            // throttle starts where the player left it, and the brain looks again at once.
            bool first = _lastTick == long.MinValue;
            bool resumed = !first && tick != _lastTick + 1;
            if (resumed) _flying.Resume(_core.Find(aircraftId).LastInputs.Throttle);

            // Every tick: did the last tick's flight cross the next gate?
            if (_lastTick == tick - 1 && Mode == PilotMode.Navigate) _route.Observe(tick - 1, _last, state, out _);
            _last = state;
            _lastTick = tick;

            // A seat that starts between watches decides at once, rather than fly an empty request until the next.
            if (first || resumed || Watch.IsWatch(tick, _core.Clock.TickSeconds, WatchHz)) Decide(tick, aircraftId, state);

            // Formation is flown off the slot every tick (Formation, W44: the stick every tick).
            if ((Mode == PilotMode.Join || Mode == PilotMode.Fingertip) && TryLeader(out AirframeState lead))
                _request = Formation.Plan(new Slot(lead, _pilot), state, _pilot, Mode == PilotMode.Join);

            // Going home is planned every tick too: the flare and the touchdown cannot wait a quarter second.
            if (Mode == PilotMode.Home || Mode == PilotMode.Landed)
            {
                WheelsRules(tick, aircraftId, state);
                if (Leg == PatternLeg.Rollout || Leg == PatternLeg.Stopped)
                {
                    PilotInputs onWheels = Pattern.Rollout(_home, state, _pilot);
                    if (Leg == PatternLeg.Stopped) onWheels.Brakes = true;
                    return onWheels;
                }
                _request = Pattern.Plan(Leg, _home, state, _pilot, _goingAroundHome);
            }

            // Formation flies on its own hands (tighter on height), the pattern's slow legs on hands held to its bank;
            // a change of hands carries the throttle over.
            FlightPathHands hands = Mode == PilotMode.Join || Mode == PilotMode.Fingertip ? _formationHands
                : Mode == PilotMode.Home && Leg >= PatternLeg.Downwind ? _patternHands
                : _hands;
            if (hands != _flying && !first) hands.Resume(_core.Find(aircraftId).LastInputs.Throttle);
            _flying = hands;

            float surface = _core.Surface.HeightAt(state.Position.x, state.Position.z);
            return hands.Fly(_request, state, surface);
        }

        /// <summary>One watch: the rules in order, then the mode's plan.</summary>
        void Decide(long tick, int id, in AirframeState s)
        {
            // Rule "bingo": the flight-time STAND-IN for bingo fuel (W47) sends a pilot with a home there.
            if (HasHome && _pilot.BingoFlightTimeS > 0f && Mode != PilotMode.Home && Mode != PilotMode.Landed
                && tick * Math.Round(_core.Clock.TickSeconds, 6) >= _pilot.BingoFlightTimeS)
            {
                if (_hasStrip) { _home = _strip.EndFor(s.Position, _pilot); _hasHome = true; }
                StartHome(tick, id, "bingo", $"{_pilot.BingoFlightTimeS:F0} s flown (a flight-time stand-in for bingo fuel); home to {_home.Name}");
            }
            if (Mode == PilotMode.Navigate) NavigateRules(tick, id, s);
            if (Mode == PilotMode.Join || Mode == PilotMode.Fingertip) FormationRules(tick, id, s);
            if (Mode == PilotMode.Home) PatternRules(tick, id, s);

            switch (Mode)
            {
                case PilotMode.Navigate:
                    _request = Navigate.Plan(_route.Gates[_target], s, _pilot, _goingAround);
                    break;
                case PilotMode.HoldCourse:
                    if (float.IsNaN(_holdAltitudeM))
                    {
                        _holdAltitudeM = s.Position.y;
                        _holdMps = s.Velocity.magnitude;
                    }
                    Vector3 ahead = s.Velocity;
                    ahead.y = 0f;
                    _request = new FlightPathRequest
                    {
                        AimWorld = s.Position + (ahead.sqrMagnitude > 1f ? ahead.normalized : Vector3.forward) * 10000f,
                        AltitudeM = _holdAltitudeM,
                        SpeedMps = _holdMps,
                    };
                    break;
            }
        }

        void NavigateRules(long tick, int id, in AirframeState s)
        {
            // Rule "course flown": a whole lap done → hold course.
            if (_route.Laps >= 1)
            {
                _holdAltitudeM = _pilot.HoldAltitudeM;
                _holdMps = _pilot.HoldKt * (float)Units.KnotToMetrePerSecond;
                Fire(tick, id, "course flown", PilotMode.HoldCourse, $"all {_route.Gates.Count} gates flown; holding {_pilot.HoldAltitudeM:F0} m at {_pilot.HoldKt:F0} kt");
                return;
            }
            // Rule "next gate": the route moved on → retarget, saying how the last gate went.
            if (_route.Next != _target)
            {
                GatePass pass = _route.Passes[_route.Passes.Count - 1];
                _target = _route.Next;
                _goingAround = false;
                Fire(tick, id, "next gate", PilotMode.Navigate,
                     $"{_route.Gates[pass.Gate].Name} flown at {pass.SpeedKt:F0} kt, {pass.AltitudeFt:F0} ft{(pass.Clean ? "" : " (outside its bands)")}; now {_route.Gates[_route.Next].Name}");
            }

            // Rules "go around" and "back on the line" (sticky until back out at the lead-in).
            Gate gate = _route.Gates[_target];
            if (!_goingAround && Navigate.ShouldGoAround(gate, s, _pilot))
            {
                Navigate.OnTheLine(gate, s, out float along, out float lateral);
                _goingAround = true;
                Fire(tick, id, "go around", PilotMode.Navigate, $"{gate.Name}: {-along:F0} m to go, {lateral:F0} m off the line; not lined up");
            }
            else if (_goingAround && Navigate.BackOnTheLine(gate, s, _pilot))
            {
                _goingAround = false;
                Fire(tick, id, "back on the line", PilotMode.Navigate, $"{gate.Name}: out at the lead-in; trying the line again");
            }
        }

        void FormationRules(long tick, int id, in AirframeState s)
        {
            // Rule "leader lost": the leader crashed (or is gone) → hold course.
            if (!TryLeader(out AirframeState lead))
            {
                _holdAltitudeM = float.NaN;
                Fire(tick, id, "leader lost", PilotMode.HoldCourse, $"aircraft {_leaderId} is down; holding what it has");
                return;
            }
            var slot = new Slot(lead, _pilot);
            slot.ErrorOf(s.Position, out float along, out float across, out float up);
            // Rules "in the slot" and "lost the slot" (W40).
            if (Mode == PilotMode.Join && Formation.InTheSlot(slot, s, _pilot))
                Fire(tick, id, "in the slot", PilotMode.Fingertip, $"on aircraft {_leaderId}: {along:F1} m along, {across:F1} m across, {up:F1} m up");
            else if (Mode == PilotMode.Fingertip && Formation.LostTheSlot(slot, s, _pilot))
                Fire(tick, id, "lost the slot", PilotMode.Join, $"{(slot.Position - s.Position).magnitude:F0} m from it; joining again");
        }

        /// <summary>The pattern's rules, on the watch: each leg hands over to the next as its geometry is met.</summary>
        void PatternRules(long tick, int id, in AirframeState s)
        {
            const float Kt = (float)Units.KnotToMetrePerSecond;
            _home.Frame(s.Position, out float along, out float lateral);
            Vector3 track = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
            Gate gate = Pattern.BreakGate(_home, _pilot);
            switch (Leg)
            {
                case PatternLeg.ToInitial:
                case PatternLeg.Initial:
                    // The initial's go-around, as Navigate's: too close to line up, out and back onto the line.
                    if (!_goingAroundHome && Navigate.ShouldGoAround(gate, s, _pilot))
                    {
                        _goingAroundHome = true;
                        Rule(tick, id, "go around", Leg, $"{gate.Name}: not lined up; out and back to the initial");
                        Leg = PatternLeg.ToInitial;
                    }
                    else if (_goingAroundHome && Navigate.BackOnTheLine(gate, s, _pilot))
                    {
                        _goingAroundHome = false;
                        Rule(tick, id, "back on the line", Leg, $"{gate.Name}: trying the initial again");
                    }
                    Navigate.OnTheLine(gate, s, out float toBreak, out _);
                    if (Leg == PatternLeg.ToInitial && !_goingAroundHome && Navigate.Lined(gate, s, _pilot) && toBreak > -_pilot.InitialM)
                        Leg = Next(tick, id, "initial", PatternLeg.Initial, $"on the initial, {-toBreak:F0} m to the break, {s.Velocity.magnitude / Kt:F0} kt");
                    else if (Leg == PatternLeg.Initial && toBreak >= 0f)
                        Leg = Next(tick, id, "break", PatternLeg.Break, $"over the runway {along:F0} m past the threshold at {(s.Position.y - _home.HeightM) / 0.3048f:F0} ft; left break");
                    break;
                case PatternLeg.Break:
                    if (Vector3.Angle(track, -_home.Along) < 30f)
                        Leg = Next(tick, id, "downwind", PatternLeg.Downwind, $"{-lateral:F0} m abeam, {s.Velocity.magnitude / Kt:F0} kt");
                    break;
                case PatternLeg.Downwind:
                    if (along <= -_pilot.FinalTurnAlongM)
                        Leg = Next(tick, id, "the 180", PatternLeg.Base, $"{-along:F0} m before the threshold; turning to final");
                    break;
                case PatternLeg.Base:
                    if (Vector3.Angle(track, _home.Along) < 15f && Mathf.Abs(lateral) < 150f)
                        Leg = Next(tick, id, "final", PatternLeg.Final, $"{-along:F0} m out, {lateral:F0} m off the centreline, {(s.Position.y - _home.HeightM) / 0.3048f:F0} ft");
                    break;
            }
        }

        /// <summary>Touchdown and stopping, every tick: the wheels cannot wait for the watch.</summary>
        void WheelsRules(long tick, int id, in AirframeState s)
        {
            if (Mode == PilotMode.Home && Leg < PatternLeg.Rollout && s.InContact)
            {
                _home.Frame(s.Position, out float along, out float lateral);
                Leg = Next(tick, id, "touchdown", PatternLeg.Rollout, $"{along:F0} m past the threshold, {lateral:F1} m off the centreline, sinking {Mathf.Max(0f, -s.Velocity.y) / 0.3048f:F1} ft/s");
            }
            else if (Leg == PatternLeg.Rollout && s.Velocity.magnitude < 0.5f)
            {
                _home.Frame(s.Position, out float along, out float lateral);
                _legTick = tick;
                Leg = PatternLeg.Stopped;
                Fire(tick, id, "stopped", PilotMode.Landed, $"{along:F0} m down {_home.Name}, {lateral:F1} m off the centreline");
            }
        }

        /// <summary>A leg change, logged as a rule (the mode stays Home).</summary>
        PatternLeg Next(long tick, int id, string rule, PatternLeg to, string reason)
        {
            Rule(tick, id, rule, to, reason);
            _legTick = tick;
            return to;
        }

        void Rule(long tick, int id, string rule, PatternLeg leg, string reason)
        {
            _log.Add(new RuleFiring(tick, id, rule, Mode, Mode, $"{leg}: {reason}"));
            Leg = leg;
            _status = Status();
        }

        /// <summary>The leader as the last tick left it (A1), if it is still flying.</summary>
        bool TryLeader(out AirframeState lead)
        {
            var picture = _core.Picture;
            for (int i = 0; i < picture.Count; i++)
            {
                if (picture[i].Id != _leaderId) continue;
                lead = picture[i].State;
                return lead.Status != FlightStatus.Crashed;
            }
            lead = default;
            return false;
        }

        void Fire(long tick, int id, string rule, PilotMode to, string reason)
        {
            _log.Add(new RuleFiring(tick, id, rule, Mode, to, reason));
            Mode = to;
            _status = Status();
        }

        string Status()
        {
            switch (Mode)
            {
                case PilotMode.Navigate: return $"NAVIGATE · {_route.Gates[_target].Name.ToUpperInvariant()}";
                case PilotMode.Join: return $"JOIN ON {_leaderId}";
                case PilotMode.Fingertip: return $"FINGERTIP ON {_leaderId}";
                case PilotMode.Home: return $"HOME · {Leg.ToString().ToUpperInvariant()}";
                case PilotMode.Landed: return $"LANDED · {_home.Name.ToUpperInvariant()}";
                default: return "HOLD COURSE";
            }
        }
    }
}
