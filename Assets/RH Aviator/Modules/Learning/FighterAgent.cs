using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.Aviator.Core;

namespace DeepWater.Aviator.Learning
{
    /// <summary>
    /// The ML-Agents face of a learned pilot (TRAINING-CONCEPT M1, M7; A13): an <see cref="Agent"/> on the jet that turns
    /// what its <see cref="LearnedSeat"/>'s eyes see into the observation vector (<see cref="LearnedSenses"/>) and the
    /// policy's actions into the seat's next command (<see cref="LearnedActions"/>). It never touches the jet: the seat
    /// flies it through Pilot Inputs (W6). <see cref="FighterTraining"/> binds it to its seat, asks it for decisions,
    /// pays it and ends its episodes. With no trainer connected and no model it flies a scripted stand-in
    /// (<see cref="Heuristic"/>), so the scene can be watched without Python.
    /// </summary>
    [RequireComponent(typeof(AircraftView))]
    public sealed class FighterAgent : Agent
    {
        [Tooltip("The side it fights for. Its Behavior Parameters' Team Id follows it (blue 0, red 1) for self-play.")]
        [SerializeField] Side side = Side.Blue;

        readonly LearnedSenses _senses = new LearnedSenses();
        readonly float[] _scripted = new float[LearnedActions.ContinuousCount];
        FighterTraining _training;
        LearnedSeat _seat;
        int _id;

        public Side Side => side;
        public int AircraftId => GetComponent<AircraftView>().AircraftId;
        public LearnedSeat Seat => _seat;
        public LearnedSenses Senses => _senses;

        /// <summary>Sets the side from code (a training squadron's setting, before play). True when it changed.</summary>
        public bool SetSide(Side fightSide)
        {
            if (side == fightSide) return false;
            side = fightSide;
            return true;
        }

        internal void Bind(FighterTraining training, LearnedSeat seat, int aircraftId)
        {
            _training = training;
            _seat = seat;
            _id = aircraftId;
            MaxStep = 0;   // the director cuts a life at its length; ML-Agents' own count would include the time spent down
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // Before binding, or before the seat's first look, there is nothing to see: an empty vector of the right size.
            if (_seat == null || _seat.Eyes == null)
            {
                for (int i = 0; i < LearnedSenses.Size; i++) sensor.AddObservation(0f);
                return;
            }
            FlightCore core = _training.Core;
            float[] v = _senses.Write(core, _id, _seat.Eyes, _training.Fight, _seat.Pilot, _training.IsProtected(_id));
            for (int i = 0; i < v.Length; i++) sensor.AddObservation(v[i]);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (_seat == null) return;
            ActionSegment<float> c = actions.ContinuousActions;
            bool trigger = actions.DiscreteActions.Length > 0 && actions.DiscreteActions[0] == 1;
            _seat.Order(LearnedActions.Decode(new ReadOnlySpan<float>(c.Array, c.Offset, c.Length), trigger,
                                              _training.Core.Find(_id).State, _training.Settings, _seat.Pilot));
        }

        /// <summary>
        /// The scripted stand-in: lead pursuit on the nearest enemy it knows of, and fire when the pipper's miss is inside
        /// the pilot's own trigger discipline (the ace's 6 mils). Reads the senses written just before.
        /// </summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            if (_seat == null) return;
            bool shoot = LearnedActions.Scripted(_training.Core.Find(_id).State, _senses.NearestEnemy != 0, _senses.NearestEnemyGuess,
                                                 _senses.NearestEnemyVelocity, _senses.PipperMissMils, _seat.Pilot, _training.Settings, _scripted);
            ActionSegment<float> c = actionsOut.ContinuousActions;
            for (int i = 0; i < c.Length && i < _scripted.Length; i++) c[i] = _scripted[i];
            ActionSegment<int> d = actionsOut.DiscreteActions;
            if (d.Length > 0) d[0] = shoot ? 1 : 0;
        }
    }
}
