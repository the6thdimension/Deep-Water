using System;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.State;

namespace DeepWater.Aviator.Core
{
    /// <summary>What a learned pilot asks for (M1): a flight path for the hands, the trigger and the missile pickle (W65).</summary>
    public struct LearnedCommand
    {
        public FlightPathRequest Request;
        public bool Trigger, Pickle;
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

        /// <summary>The policy's decision: flown from the next tick until the next decision.</summary>
        public void Order(in LearnedCommand command)
        {
            _command = command;
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
