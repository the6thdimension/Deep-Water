using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Seats;
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
    /// <para>It shapes its own Behavior Parameters when it wakes (SENSORS-CONCEPT J11, J20): the senses and actions as
    /// they are now, whatever the scene was saved with, or version 2's when the brain it flies was trained on them (a
    /// kept brain from before the sensors), so an old brain still flies, with guns and heat missiles only.</para>
    /// </summary>
    [RequireComponent(typeof(AircraftView))]
    public sealed class FighterAgent : Agent
    {
        [Tooltip("The side it fights for. Its Behavior Parameters' Team Id follows it (blue 0, red 1) for self-play.")]
        [SerializeField] Side side = Side.Blue;

        readonly LearnedSenses _senses = new LearnedSenses();
        readonly float[] _scripted = new float[LearnedActions.ContinuousCount];
        readonly int[] _choices = new int[6];
        FighterTraining _training;
        LearnedSeat _seat;
        SensorHabits _kit;
        int _id, _sensesSize = LearnedSenses.Size;

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

        /// <summary>The senses it is given: <see cref="LearnedSenses.Size"/>, or version 2's for a version 2 brain.</summary>
        public int SensesSize => _sensesSize;

        protected override void Awake()
        {
            // Before the agent reads its Behavior Parameters (in OnEnable, just after). The squadron (order −200) has set the
            // brain already.
            var behaviour = GetComponent<BehaviorParameters>();
            if (behaviour != null) _sensesSize = Shape(behaviour);
            base.Awake();
        }

        /// <summary>
        /// Sets the observation size and the action spec for the brain the behaviour flies: version 2's (88 senses, the
        /// trigger and pickle branches) for a brain trained on them, else the senses and actions as they are now. Returns
        /// the observation size.
        /// </summary>
        public static int Shape(BehaviorParameters behaviour)
        {
            bool v2 = behaviour.Model != null && BrainLibrary.SensesOf(behaviour.Model) == LearnedSenses.SizeVersion2;
            int size = v2 ? LearnedSenses.SizeVersion2 : LearnedSenses.Size;
            int[] branches = v2 ? new[] { LearnedActions.TriggerChoices, LearnedActions.PickleChoices } : LearnedActions.Branches();
            behaviour.BrainParameters.VectorObservationSize = size;
            behaviour.BrainParameters.NumStackedVectorObservations = 1;
            behaviour.BrainParameters.ActionSpec = new ActionSpec(LearnedActions.ContinuousCount, branches);
            return size;
        }

        internal void Bind(FighterTraining training, LearnedSeat seat, int aircraftId)
        {
            _training = training;
            _seat = seat;
            _id = aircraftId;
            _kit = new SensorHabits(training.Core, seat.Pilot, aircraftId, (t, a, rule, why) => { });   // the stand-in's hands on the kit
            MaxStep = 0;   // the director cuts a life at its length; ML-Agents' own count would include the time spent down
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // Before binding, or before the seat's first look, there is nothing to see: an empty vector of the right size.
            if (_seat == null || _seat.Eyes == null)
            {
                for (int i = 0; i < _sensesSize; i++) sensor.AddObservation(0f);
                return;
            }
            FlightCore core = _training.Core;
            float[] v = _senses.Write(core, _id, _seat.Eyes, _training.Fight, _seat.Pilot, _training.IsProtected(_id));
            for (int i = 0; i < _sensesSize; i++) sensor.AddObservation(v[i]);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (_seat == null) return;
            ActionSegment<float> c = actions.ContinuousActions;
            ActionSegment<int> d = actions.DiscreteActions;
            bool trigger = d.Length > LearnedActions.TriggerBranch && d[LearnedActions.TriggerBranch] == 1;
            bool pickle = d.Length > LearnedActions.PickleBranch && d[LearnedActions.PickleBranch] == 1;
            LearnedCommand command = LearnedActions.Decode(new ReadOnlySpan<float>(c.Array, c.Offset, c.Length), trigger, pickle,
                                                           _training.Core.Find(_id).State, _training.Settings, _seat.Pilot);
            LearnedActions.DecodeSensors(new ReadOnlySpan<int>(d.Array, d.Offset, d.Length), _senses.TrackSlotIds, ref command);
            _seat.Order(command);
        }

        /// <summary>
        /// With a teacher riding along (J13, recording demonstrations): what the teacher asked for since the last decision.
        /// Otherwise the scripted stand-in: lead pursuit on the nearest enemy it knows of, fire when the pipper's miss is
        /// inside the pilot's own trigger discipline (the ace's 6 mils), launch when locked on an enemy inside its missile
        /// range (the ace's 2.5 km), and the jet's sensors as a rule pilot works them (<see cref="SensorHabits"/>: search,
        /// ask, lock, a radar missile, chaff and flares). Reads the senses written just before.
        /// </summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            if (_seat == null) return;
            ActionSegment<int> d = actionsOut.DiscreteActions;
            if (_seat.Teacher != null)
            {
                Teach(actionsOut);
                return;
            }
            bool shoot = LearnedActions.Scripted(_training.Core.Find(_id).State, _senses.NearestEnemy != 0, _senses.NearestEnemyGuess,
                                                 _senses.NearestEnemyVelocity, _senses.PipperMissMils, _senses.LockedOnEnemy, _seat.Pilot,
                                                 _training.Settings, _scripted, out bool pickle);
            // One at a time, as the rule pilots (A16): never while one of its own is still guided.
            var live = _training.Core.Missiles.Live;
            for (int i = 0; i < live.Count && pickle; i++) if (live[i].ShooterId == _id && !live[i].Lost) pickle = false;
            ActionSegment<float> c = actionsOut.ContinuousActions;
            for (int i = 0; i < c.Length && i < _scripted.Length; i++) c[i] = _scripted[i];
            if (d.Length > LearnedActions.TriggerBranch) d[LearnedActions.TriggerBranch] = shoot ? 1 : 0;
            if (d.Length > LearnedActions.PickleBranch) d[LearnedActions.PickleBranch] = pickle ? 1 : 0;
            if (d.Length < _choices.Length) return;   // a version 2 brain's two branches: no sensors

            // The kit, judged at each decision as a rule pilot's habits judge it on a watch.
            FlightCore core = _training.Core;
            var kit = new PilotInputs();
            _kit.Apply(core.Tick, _id, core.Find(_id).State, true, _seat.Eyes, _training.Fight, ref kit);
            LearnedActions.FromInputs(kit, kit.Radar, kit.RadarTrack, _senses.TrackSlotIds, _choices);
            for (int i = LearnedActions.RadarBranch; i < _choices.Length; i++) d[i] = _choices[i];
        }

        /// <summary>The teacher's lesson as actions: its flight path as the continuous four, its inputs as the six choices.</summary>
        void Teach(in ActionBuffers actionsOut)
        {
            FlightPathRequest asked = _seat.TakeLesson(out PilotInputs held, out RadarCommand radar, out int track);
            LearnedActions.FromRequest(asked, _training.Core.Find(_id).State, _training.Settings, _seat.Pilot, _scripted);
            ActionSegment<float> c = actionsOut.ContinuousActions;
            for (int i = 0; i < c.Length && i < _scripted.Length; i++) c[i] = _scripted[i];
            LearnedActions.FromInputs(held, radar, track, _senses.TrackSlotIds, _choices);
            ActionSegment<int> d = actionsOut.DiscreteActions;
            for (int i = 0; i < d.Length && i < _choices.Length; i++) d[i] = _choices[i];
            Lessons++;
        }

        /// <summary>Decisions the teacher gave (J13).</summary>
        public int Lessons { get; private set; }
    }
}
