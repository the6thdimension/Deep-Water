using System.Collections.Generic;
using UnityEngine;

namespace DeepWater.AirWing.Core.Missiles
{
    /// <summary>
    /// Every game missile in flight and everything they did (W65). The flight core flies them inside its tick
    /// (<see cref="FlightCore"/>'s missile stage); presentation reads <see cref="Live"/> and <see cref="Events"/>.
    /// </summary>
    public sealed class MissilesInFlight
    {
        readonly List<MissileInFlight> _live = new List<MissileInFlight>(32);
        readonly List<MissileEvent> _events = new List<MissileEvent>(256);
        int _nextId = 1;

        public IReadOnlyList<MissileInFlight> Live => _live;
        /// <summary>Launches, bursts, losses and expiries, in tick order.</summary>
        public IReadOnlyList<MissileEvent> Events => _events;
        public int Launched { get; private set; }
        public int Bursts { get; private set; }
        /// <summary>Radar missiles launched (W68), and how many went active.</summary>
        public int RadarLaunched { get; private set; }
        public int WentActive { get; private set; }

        internal List<MissileInFlight> Mutable => _live;

        internal void Launch(long tick, int shooterId, int targetId, int targetResets, string name, Vector3 position, Vector3 velocity,
                             bool radar = false, int shooterResets = 0)
        {
            var m = new MissileInFlight
            {
                Id = _nextId++, ShooterId = shooterId, TargetId = targetId, TargetResets = targetResets,
                LaunchTick = tick, Position = position, Velocity = velocity, Name = name, Radar = radar, ShooterResets = shooterResets,
            };
            _live.Add(m);
            Launched++;
            if (radar) RadarLaunched++;
            _events.Add(new MissileEvent(tick, m.Id, shooterId, targetId, MissileEventKind.Launched, position, false));
        }

        internal void Log(in MissileEvent e)
        {
            if (e.Kind == MissileEventKind.Burst) Bursts++;
            if (e.Kind == MissileEventKind.Active) WentActive++;
            _events.Add(e);
        }
    }
}
