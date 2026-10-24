using System.Collections.Generic;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Simulation;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core.Magazine
{
    /// <summary>A kill the combat core reports back to the world, which owns whether targets are alive.</summary>
    public struct KillReport
    {
        public int TruthId;
        public int TrackId;
        public WeaponClass Weapon;
        /// <summary>The Kill event in the engagement log.</summary>
        public int Event;
    }

    /// <summary>
    /// Everything a weapon needs to fire and resolve on one tick, gathered once by the combat system so
    /// weapon code doesn't take a dozen parameters. Rebuilt every tick; never cached across ticks.
    /// </summary>
    public sealed class EngagementContext
    {
        public long Tick;
        public float TickSeconds;
        public OwnShipState Own;
        public IReadOnlyList<TruthTarget> World;
        public readonly Dictionary<int, int> TruthIndex = new Dictionary<int, int>();
        /// <summary>Targets killed this run whose death the truth feed may not show yet.</summary>
        public readonly HashSet<int> KilledTruth = new HashSet<int>();
        public ITrackManager Tracks;
        public DoctrineState Doctrine;
        public DeterministicRng Rng;
        public EngagementLog Log;
        public List<KillReport> Kills;
        /// <summary>The gun mount being stepped (its index in the loadout), for the events it logs; −1 otherwise.</summary>
        public int Mount = -1;

        public void IndexWorld(IReadOnlyList<TruthTarget> world)
        {
            World = world;
            TruthIndex.Clear();
            for (int i = 0; i < world.Count; i++) TruthIndex[world[i].Id] = i;
        }

        public bool TryTruth(int truthId, out TruthTarget target)
        {
            if (TruthIndex.TryGetValue(truthId, out int i)) { target = World[i]; return true; }
            target = default;
            return false;
        }

        public bool TargetAlive(int truthId) =>
            !KilledTruth.Contains(truthId) && TryTruth(truthId, out TruthTarget t) && t.Alive;

        public void RecordKill(int truthId, int trackId, WeaponClass weapon, int killEvent)
        {
            KilledTruth.Add(truthId);
            Tracks.MarkDestroyed(trackId);
            Kills.Add(new KillReport { TruthId = truthId, TrackId = trackId, Weapon = weapon, Event = killEvent });
        }
    }
}
