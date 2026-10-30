using System;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.State;

namespace DeepWater.Aviator.Core
{
    /// <summary>
    /// What a learned pilot asks for (M1): a flight path for the hands, the trigger and the missile pickle (W65), and
    /// (SENSORS-CONCEPT J20) a radar command, given once, with the track a lock names, and the held IFF, chaff, flare and
    /// radar-missile buttons.
    /// </summary>
    public struct LearnedCommand
    {
        public FlightPathRequest Request;
        public bool Trigger, Pickle;
        public RadarCommand Radar;
        public int RadarTrack;
        public bool Interrogate, Chaff, Flare, RadarPickle;
    }

    /// <summary>
    /// The learned pilot's seat (TRAINING-CONCEPT M1, M2, M7; RH Aviator A13): an Air Wing seat like <see cref="AviatorSeat"/>,
    /// flying through the same Pilot Inputs (W6). A policy outside this class decides; this seat only carries its last
    /// <see cref="LearnedCommand"/> to the hands every tick, and keeps the pilot's eyes open (it looks every tick, so the
    /// reaction delay is counted in ticks, as for the rule pilots). It sees and flies under one pilot profile's limits
    /// (M17: the ace's field of view, delay, memory and hands). Pure and deterministic given its commands; a fight with a
    /// learned seat need not re-run exactly (M3), but its inputs in the log replay exactly.
    /// <para>After a respawn (the arena, W59) or a hand-back it starts fresh: new eyes, a level request at its present
    /// height and speed, and the throttle carried over, until the policy's next command.</para>
    /// </summary>
    public sealed class LearnedSeat : ISeat, ISeatStatus
    {
        readonly FlightCore _core;
        readonly PilotProfile _pilot;
        readonly FlightPathHands _hands;
        Perception _eyes;
        LearnedCommand _command;
        bool _radarGiven;
        AviatorSeat _teacher;
        PilotInputs _taught;
        RadarCommand _taughtRadar;
        int _taughtTrack;
        bool _taughtRadarPickle;
        long _lastTick = long.MinValue;
        int _resets = -1, _commands;
        string _status = "WAITING";

        public LearnedSeat(FlightCore core, in AircraftProfile aircraft, in PilotProfile pilot)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            pilot.Validate();
            _pilot = pilot;
            _hands = new FlightPathHands(pilot.Hands, aircraft.Controls, core.Clock.TickSeconds);
        }

        public SeatKind Kind => SeatKind.Learned;
        string ISeatStatus.Status => _status;

        /// <summary>The pilot profile it sees and flies under (M17).</summary>
        public PilotProfile Pilot => _pilot;
        /// <summary>What it sees (null before its first tick).</summary>
        public Perception Eyes => _eyes;
        /// <summary>The command the hands fly now.</summary>
        public LearnedCommand Command => _command;
        /// <summary>Commands received since the seat last started fresh.</summary>
        public int CommandsThisLife => _commands;
        /// <summary>The rule pilot riding along as a teacher (J13), or null.</summary>
        public AviatorSeat Teacher => _teacher;
        /// <summary>The teacher's inputs on the last tick (what it would have flown).</summary>
        public PilotInputs Taught => _taught;

        /// <summary>
        /// Rides a rule pilot along (SENSORS-CONCEPT J13): from now on, every tick, <paramref name="teacher"/> (already
        /// ordered to fight as aircraft <paramref name="aircraftId"/>) judges the jet's own state as if it flew it. Its
        /// inputs are never flown: they are kept for <see cref="TakeLesson"/>, which turns them into the actions a
        /// demonstration records.
        /// </summary>
        public void RideAlong(AviatorSeat teacher) => _teacher = teacher;

        /// <summary>
        /// What the teacher asked for since the last lesson: its flight path and held inputs now, the last radar command
        /// it gave, and its radar-missile pickle if it pressed it on any tick since (both momentary, judged on its watch;
        /// cleared by the call).
        /// </summary>
        public FlightPathRequest TakeLesson(out PilotInputs held, out RadarCommand radar, out int radarTrack)
        {
            held = _taught;
            held.RadarPickle |= _taughtRadarPickle;
            _taughtRadarPickle = false;
            radar = _taughtRadar;
            radarTrack = _taughtTrack;
            _taughtRadar = RadarCommand.None;
            _taughtTrack = 0;
            return _teacher != null ? _teacher.Request : default;
        }

        /// <summary>The policy's decision: flown from the next tick until the next decision.</summary>
        public void Order(in LearnedCommand command)
        {
            _command = command;
            _radarGiven = false;
            _commands++;
            _status = command.Pickle ? "MISSILE" : command.Trigger ? "GUNS" : "FLYING";   // constant strings, read on every frame; the pick list puts the kind (LEARNED) first
        }

        public PilotInputs Issue(long tick, int aircraftId, in AirframeState state)
        {
            Aircraft me = _core.Find(aircraftId);
            bool fresh = _eyes == null || me.Resets != _resets || tick != _lastTick + 1;
            if (fresh)
            {
                // A new life (or the seat handed back): look again from nothing, and hold what the jet has until told.
                _eyes = new Perception(aircraftId, _pilot, _core.Clock.TickSeconds, truth: false, seed: _core.Seed);
                _resets = me.Resets;
                _commands = 0;
                _command = Hold(state);
                _hands.Resume(me.LastInputs.Throttle > 0f ? me.LastInputs.Throttle : state.EngineSpool);
                _status = "NEW LIFE";
            }
            _lastTick = tick;
            _eyes.Look(tick, _core.Picture);
            float surface = _core.Surface.HeightAt(state.Position.x, state.Position.z);
            PilotInputs inputs = _hands.Fly(_command.Request, state, surface);
            inputs.Trigger = _command.Trigger;
            inputs.Pickle = _command.Pickle;
            // The sensors (J20): the radar command once, the buttons held until the next decision.
            if (!_radarGiven && _command.Radar != RadarCommand.None)
            {
                inputs.Radar = _command.Radar;
                inputs.RadarTrack = _command.RadarTrack;
            }
            _radarGiven = true;
            if (_command.Interrogate) inputs.Switches |= PilotSwitches.Interrogate;
            if (_command.Chaff) inputs.Switches |= PilotSwitches.Chaff;
            if (_command.Flare) inputs.Switches |= PilotSwitches.Flare;
            inputs.RadarPickle = _command.RadarPickle;

            if (_teacher != null)
            {
                // The teacher judges this tick as if it flew; its radar command is kept until the next lesson.
                _taught = _teacher.Issue(tick, aircraftId, state);
                if (_taught.Radar != RadarCommand.None) { _taughtRadar = _taught.Radar; _taughtTrack = _taught.RadarTrack; }
                _taughtRadarPickle |= _taught.RadarPickle;
            }
            return inputs;
        }

        /// <summary>Straight and level at the present height and speed, guns safe.</summary>
        public static LearnedCommand Hold(in AirframeState s)
        {
            Vector3 ahead = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
            return new LearnedCommand
            {
                Request = new FlightPathRequest
                {
                    AimWorld = s.Position + (ahead.sqrMagnitude > 1f ? ahead.normalized : Vector3.forward) * 10000f,
                    AltitudeM = s.Position.y,
                    SpeedMps = s.Velocity.magnitude,
                },
            };
        }
    }
}
