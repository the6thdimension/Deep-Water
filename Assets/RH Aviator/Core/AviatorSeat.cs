using System;
using System.Collections.Generic;
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
        int _leaderId, _slotPosition;
        float _holdAltitudeM, _holdMps;
        Runway _home;
        Strip _strip;
        bool _hasHome, _hasStrip, _goingAroundHome;
        long _legTick;
        Engagement _fight;
        Perception _eyes;
        int _targetId;
        bool _breaking, _vectored;
        Fighting.Pursuit _pursuit;
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
        /// <summary>In a fight: the aircraft it is after (0: none), how, and what it sees.</summary>
        public int TargetId => _targetId;
        public Fighting.Pursuit Pursuit => _pursuit;
        public Perception Eyes => _eyes;
        public Engagement Fight => _fight;
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

        /// <summary>Order: join aircraft <paramref name="leaderId"/>'s wing in fingertip (W40), in slot <paramref name="position"/> (see <see cref="Slot"/>).</summary>
        public void JoinOn(int aircraftId, int leaderId, int position = 0)
        {
            _slotPosition = position;
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

        /// <summary>
        /// Order: fight (Pilot L1, W38, W52, W53) in <paramref name="engagement"/>, which says the sides and whom red
        /// hunts. From now on the pilot sees only through its eyes: its field of view, late by its reaction delay.
        /// </summary>
        public void FightIn(int aircraftId, Engagement engagement)
        {
            _fight = engagement ?? throw new ArgumentNullException(nameof(engagement));
            if (_fight.SideOf(aircraftId) == Side.None) throw new ArgumentException($"aircraft {aircraftId} has no side in the engagement");
            _eyes = new Perception(aircraftId, _pilot, _core.Clock.TickSeconds, truth: false);
            _targetId = 0;
            _holdAltitudeM = float.NaN;
            Fire(_core.Tick, aircraftId, "ordered: fight", PilotMode.Fight, $"{_fight.SideOf(aircraftId)}; sees {_pilot.FieldOfViewDeg:F0}° round its nose, {_pilot.ReactionDelayS:F2} s late");
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
                _request = Formation.Plan(new Slot(lead, _pilot, _slotPosition), state, _pilot, Mode == PilotMode.Join);

            // In a fight the eyes look every tick (the delay is counted in ticks), and the plan follows what they see.
            if (_fight != null && Mode != PilotMode.Home && Mode != PilotMode.Landed)
            {
                _eyes.Look(tick, _core.Picture);
                if (Mode == PilotMode.Fight && !_breaking && _targetId != 0 && _eyes.TryGet(_targetId, out Contact target) && !_vectored)
                {
                    float floor = _core.Surface.HeightAt(state.Position.x, state.Position.z);
                    _request = Fighting.Plan(state, target, null, tick, _core.Clock.TickSeconds, floor, _pilot, out _pursuit);
                    if (_avoiding) _request.AltitudeM = Mathf.Max(_avoidHeight, floor + _pilot.FightFloorM);
                }
            }

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
            // The tight formation hands only near the slot: on a long join they porpoised (slice 3d: 2.2 G wings level,
            // 28 km behind at 2,000 ft); the route hands fly the way in.
            bool nearSlot = Mode == PilotMode.Fingertip || (Mode == PilotMode.Join && TryLeader(out AirframeState ld) && (new Slot(ld, _pilot, _slotPosition).Position - state.Position).sqrMagnitude < 500f * 500f);
            FlightPathHands hands = nearSlot ? _formationHands
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
            if (_fight != null && (Mode == PilotMode.Fight || Mode == PilotMode.Join || Mode == PilotMode.Fingertip)) FightRules(tick, id, s);

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
            var slot = new Slot(lead, _pilot, _slotPosition);
            slot.ErrorOf(s.Position, out float along, out float across, out float up);
            // Rules "in the slot" and "lost the slot" (W40).
            if (Mode == PilotMode.Join && Formation.InTheSlot(slot, s, _pilot))
                Fire(tick, id, "in the slot", PilotMode.Fingertip, $"on aircraft {_leaderId}: {along:F1} m along, {across:F1} m across, {up:F1} m up");
            else if (Mode == PilotMode.Fingertip && Formation.LostTheSlot(slot, s, _pilot))
                Fire(tick, id, "lost the slot", PilotMode.Join, $"{(slot.Position - s.Position).magnitude:F0} m from it; joining again");
        }

        /// <summary>
        /// The fight's rules, on the watch (W38, W52, W53): a first sight of an enemy is a "tally"; a seen enemy close in
        /// the pilot's own rear quarter makes it "break"; red goes for the jet the player flies, by a "vector" (a
        /// controller's steer, from the truth) until it sees it; blue goes for the nearest red it sees, and with none
        /// inside its clear range it is "clear" and rejoins the player's wing.
        /// </summary>
        void FightRules(long tick, int id, in AirframeState s)
        {
            Side mine = _fight.SideOf(id);
            int hunted = _fight.Hunted();
            // Tally: first sight of each enemy (the contact is as old as the delay).
            int nearest = 0, threatId = 0;
            float nearestD = float.MaxValue, threatD = float.MaxValue;
            IReadOnlyList<int> known = _eyes.Known;
            for (int i = 0; i < known.Count; i++)
            {
                int other = known[i];
                if (!_fight.Enemies(id, other) || !_eyes.TryGet(other, out Contact c)) continue;
                if (!_tallied.Contains(other))
                {
                    _tallied.Add(other);
                    Fire(tick, id, "tally", Mode, $"aircraft {other} seen at {(c.Seen.Position - s.Position).magnitude:F0} m (as of {(tick - c.SeenTick) * Math.Round(_core.Clock.TickSeconds, 6):F2} s ago)");
                }
                Vector3 guess = c.GuessPosition(tick, _core.Clock.TickSeconds);
                float d = (guess - s.Position).magnitude;
                if (d < nearestD) { nearestD = d; nearest = other; }
                Vector3 tail = -(s.Velocity.sqrMagnitude > 1f ? s.Velocity.normalized : s.Attitude * Vector3.forward);
                // A threat: close, in the pilot's rear quarter, and pointing at it (slice 3d: a jet just past a head-on
                // pass, flying away, made the ace break for nothing).
                bool pointing = Vector3.Angle(c.Seen.Velocity, s.Position - guess) < _pilot.ThreatConeDeg;
                if (c.InView && pointing && d < _pilot.ThreatRangeM && Vector3.Angle(guess - s.Position, tail) < _pilot.ThreatConeDeg && d < threatD)
                {
                    threatD = d;
                    threatId = other;
                }
            }

            // Blue, out of the fight: back to the player's wing; and back in when an enemy is seen inside the clear range.
            if (mine == Side.Blue && hunted != 0 && hunted != id)
            {
                if (Mode == PilotMode.Fight && (nearest == 0 || nearestD > _pilot.ClearRangeM))
                {
                    _leaderId = hunted;
                    _targetId = 0;
                    _fight.Claim(id, 0);
                    Fire(tick, id, "clear", PilotMode.Join, $"no enemy inside {_pilot.ClearRangeM:F0} m; rejoining aircraft {hunted}");
                    return;
                }
                if ((Mode == PilotMode.Join || Mode == PilotMode.Fingertip) && nearest != 0 && nearestD <= _pilot.ClearRangeM)
                    Fire(tick, id, "engage", PilotMode.Fight, $"aircraft {nearest} at {nearestD:F0} m");
            }
            if (Mode != PilotMode.Fight) return;

            // A target that has left the fight is no target (slice 3d: the player's jet kept chasing a red jet after red
            // left, and its wingmen could not rejoin a leader still turning hard).
            if (_targetId != 0 && !_fight.Enemies(id, _targetId))
            {
                Fire(tick, id, "target gone", PilotMode.Fight, $"aircraft {_targetId} is out of the fight");
                _targetId = 0;
                _fight.Claim(id, 0);
                _hasLastSeen = false;
            }

            // The target: red hunts the player's jet (W52); everyone else the nearest enemy it knows that no teammate is
            // already after, else the nearest (slice 3d smoke: three blue jets all chose the one nearest red and sat in
            // its cone for three minutes while the other three hunted the player unopposed).
            int want = mine == Side.Red && hunted != 0 && _fight.Enemies(id, hunted) ? hunted : nearest;
            if (!(mine == Side.Red && hunted != 0))
            {
                int free = 0;
                float freeD = float.MaxValue;
                for (int i = 0; i < known.Count; i++)
                {
                    int other = known[i];
                    if (!_fight.Enemies(id, other) || _fight.ClaimedBy(other, mine, id) > 0 || !_eyes.TryGet(other, out Contact c)) continue;
                    float d = (c.GuessPosition(tick, _core.Clock.TickSeconds) - s.Position).magnitude;
                    if (d < freeD) { freeD = d; free = other; }
                }
                if (free != 0) want = free;
            }
            if (want != _targetId && want != 0)
            {
                _targetId = want;
                _fight.Claim(id, want);
                Fire(tick, id, "engage", PilotMode.Fight, $"after aircraft {want}");
            }

            // A threat on its tail first: break.
            bool breaking = threatId != 0;
            if (breaking != _breaking)
            {
                _breaking = breaking;
                Fire(tick, id, breaking ? "break" : "tail clear", PilotMode.Fight, breaking ? $"aircraft {threatId} at {threatD:F0} m in its rear quarter" : "no threat behind");
            }
            // Collision avoidance: any jet predicted to pass too close, soon, from the picture (a near miss is seen).
            if (Avoid(s, out float avoidDy, out int avoidId))
            {
                if (!_avoiding) { _avoiding = true; Fire(tick, id, "avoid", PilotMode.Fight, $"aircraft {avoidId} predicted inside {_pilot.AvoidM:F0} m; {(avoidDy > 0f ? "climbing" : "diving")} clear"); }
                _avoidHeight = s.Position.y + Mathf.Sign(avoidDy) * 3f * _pilot.AvoidM;
            }
            else _avoiding = false;

            if (_breaking && _eyes.TryGet(threatId, out Contact threat))
            {
                _request = Fighting.Plan(s, threat, threat, tick, _core.Clock.TickSeconds, _core.Surface.HeightAt(s.Position.x, s.Position.z), _pilot, out _pursuit);
                return;
            }

            // Red with no sight of its quarry: a vector from the truth (a controller's steer), labelled in the log.
            bool seen = _targetId != 0 && _eyes.TryGet(_targetId, out _);
            bool vector = mine == Side.Red && hunted != 0 && !seen;
            if (vector != _vectored)
            {
                _vectored = vector;
                if (vector) Fire(tick, id, "vector", PilotMode.Fight, $"no sight of aircraft {hunted}; steering on a controller's vector (the truth)");
            }
            if (_vectored)
            {
                AirframeState truth = _core.Find(hunted).State;
                _request = new FlightPathRequest
                {
                    AimWorld = truth.Position,
                    AltitudeM = Mathf.Max(truth.Position.y, _core.Surface.HeightAt(s.Position.x, s.Position.z) + _pilot.FightFloorM),
                    SpeedMps = _pilot.CornerKt * (float)Units.KnotToMetrePerSecond,
                };
                return;
            }
            if (seen && _eyes.TryGet(_targetId, out Contact last)) { _lastSeen = last.GuessPosition(tick, _core.Clock.TickSeconds); _hasLastSeen = true; }
            if ((_targetId == 0 || !seen) && _hasLastSeen)
            {
                // Lost it: turn back to where it was last seen and look there (slice 3d: a rookie that forgot a jet
                // behind it after the pass flew straight on at 416 kt for the rest of the fight).
                if (!_searching) { _searching = true; Fire(tick, id, "search", PilotMode.Fight, $"lost sight; turning to where it was last seen, {(_lastSeen - s.Position).magnitude:F0} m"); }
                _request = new FlightPathRequest
                {
                    AimWorld = _lastSeen,
                    AltitudeM = Mathf.Max(_lastSeen.y, _core.Surface.HeightAt(s.Position.x, s.Position.z) + _pilot.FightFloorM),
                    SpeedMps = _pilot.CornerKt * (float)Units.KnotToMetrePerSecond,
                };
                if ((_lastSeen - s.Position).magnitude < 1000f) _hasLastSeen = false;   // nothing there: hold and look
                return;
            }
            _searching = false;
            if (_targetId == 0 || !seen)
            {
                // Nothing to fight yet: hold what it has, looking.
                if (float.IsNaN(_holdAltitudeM)) { _holdAltitudeM = s.Position.y; _holdMps = s.Velocity.magnitude; }
                Vector3 ahead = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
                _request = new FlightPathRequest
                {
                    AimWorld = s.Position + (ahead.sqrMagnitude > 1f ? ahead.normalized : Vector3.forward) * 10000f,
                    AltitudeM = _holdAltitudeM,
                    SpeedMps = _holdMps,
                };
            }
        }

        readonly HashSet<int> _tallied = new HashSet<int>();
        bool _avoiding;
        float _avoidHeight;

        /// <summary>Another jet's closest approach on straight lines, if inside the avoid distance within the avoid time; which way to go.</summary>
        bool Avoid(in AirframeState s, out float dy, out int with)
        {
            dy = 0f; with = 0;
            if (_pilot.AvoidM <= 0f) return false;
            var picture = _core.Picture;
            float best = float.MaxValue;
            for (int i = 0; i < picture.Count; i++)
            {
                AirframeState o = picture[i].State;
                Vector3 r = o.Position - s.Position;
                if (r.sqrMagnitude < 1f || o.Status == FlightStatus.Crashed) continue;   // itself
                Vector3 v = o.Velocity - s.Velocity;
                float t = Mathf.Clamp(-Vector3.Dot(r, v) / Mathf.Max(v.sqrMagnitude, 1e-3f), 0f, _pilot.AvoidS);
                float miss = (r + v * t).magnitude;
                if (miss < _pilot.AvoidM && miss < best) { best = miss; dy = -(r + v * t).y; if (dy == 0f) dy = picture[i].Id > _leaderId ? 1f : -1f; with = picture[i].Id; }
            }
            return with != 0;
        }
        Vector3 _lastSeen;
        bool _hasLastSeen, _searching;

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
                case PilotMode.Fight: return _targetId != 0 ? $"FIGHT · {_targetId}" : "FIGHT · LOOKING";
                default: return "HOLD COURSE";
            }
        }
    }
}
