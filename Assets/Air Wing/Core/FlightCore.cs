using System;
using System.Collections.Generic;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Simulation;

namespace DeepWater.AirWing.Core
{
    /// <summary>
    /// The flight core's tick (BLUEPRINT §3.2): for each aircraft, in ascending id order, its seat issues this tick's
    /// Pilot Inputs, they go into the input log, and the aircraft steps its substeps; then the tick's Fix is written.
    /// A Unity adapter calls <see cref="Step"/> from FixedUpdate (P2). Same aircraft, same seats issuing the same
    /// inputs, same Fix sequence (W7, W28).
    /// </summary>
    public sealed class FlightCore
    {
        readonly List<Aircraft> _aircraft = new List<Aircraft>();
        readonly List<ISeat> _seats = new List<ISeat>();
        readonly List<AircraftRecord> _records = new List<AircraftRecord>();
        readonly ISurfaceTruth _surface;
        readonly IContactProbe _probe;

        public readonly SubstepClock Clock;
        public readonly InputLog Inputs = new InputLog();
        public readonly FixLog Fixes = new FixLog();

        /// <summary>The next tick to run. Ticks count from 0.</summary>
        public long Tick { get; private set; }

        public IReadOnlyList<Aircraft> Aircraft => _aircraft;

        /// <summary>
        /// The flight core over <paramref name="surface"/>. The landing gear meets the ground through
        /// <paramref name="probe"/> (the scenes pass PhysX's); without one it uses the truth feed's level ground.
        /// </summary>
        public FlightCore(in SubstepClock clock, ISurfaceTruth surface, IContactProbe probe = null)
        {
            Clock = clock;
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _probe = probe ?? new SurfaceProbe(surface);
        }

        /// <summary>Add an aircraft and the seat that flies it. Ids must rise, so the tick order and the Fix are stable.</summary>
        public void Add(Aircraft aircraft, ISeat seat)
        {
            if (aircraft == null) throw new ArgumentNullException(nameof(aircraft));
            if (_aircraft.Count > 0 && aircraft.Id <= _aircraft[_aircraft.Count - 1].Id)
                throw new ArgumentException($"Aircraft ids must rise: {aircraft.Id} after {_aircraft[_aircraft.Count - 1].Id}.", nameof(aircraft));
            _aircraft.Add(aircraft);
            _seats.Add(seat ?? throw new ArgumentNullException(nameof(seat)));
        }

        /// <summary>Hand aircraft <paramref name="id"/> to another seat. Takes effect on the next tick (W18).</summary>
        public void SetSeat(int id, ISeat seat)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            _seats[IndexOf(id)] = seat;
        }

        public ISeat SeatOf(int id) => _seats[IndexOf(id)];

        public Aircraft Find(int id) => _aircraft[IndexOf(id)];

        int IndexOf(int id)
        {
            for (int i = 0; i < _aircraft.Count; i++)
                if (_aircraft[i].Id == id) return i;
            throw new KeyNotFoundException($"No aircraft {id}.");
        }

        public void Step()
        {
            long tick = Tick;
            _records.Clear();
            for (int i = 0; i < _aircraft.Count; i++)
            {
                Aircraft a = _aircraft[i];
                ISeat seat = _seats[i];
                InputEntry entry = Inputs.Append(tick, a.Id, seat.Kind, seat.Issue(tick, a.Id, a.State));
                float surface = _surface.HeightAt(a.State.Position.x, a.State.Position.z);
                a.Step(entry.Inputs, surface, _probe, Clock);
                _records.Add(new AircraftRecord(a.Id, a.State));
            }
            Fixes.Append(FlightFix.Of(tick, _records));
            Tick = tick + 1;
        }
    }
}
