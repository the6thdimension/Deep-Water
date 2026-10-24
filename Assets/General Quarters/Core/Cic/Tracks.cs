using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;

namespace DeepWater.GeneralQuarters.Core.Cic
{
    public struct TrackProfileData
    {
        /// <summary>Ticks without a detection before a track is dropped.</summary>
        public int DropAfterTicks;
    }

    /// <summary>The CIC's belief about one contact. Fire control and guidance use tracks, never truth.</summary>
    public struct Track
    {
        public int Id;
        public int TruthId;
        public TargetKind Kind;
        public Identity Identity;
        /// <summary>Position at <see cref="LastTick"/>; use <see cref="PredictedPosition"/> for other ticks.</summary>
        public Vector3 Position;
        public Vector3 Velocity;
        public long FirstTick;
        public long LastTick;
        public bool Active;
        /// <summary>True once a kill on it is confirmed (L0: perfect kill assessment).</summary>
        public bool Destroyed;
        /// <summary>The engagement-log event that created the track: the cause later events cite.</summary>
        public int CreatedEvent;

        /// <summary>Dead-reckoned position at <paramref name="tick"/> (constant velocity from the last update).</summary>
        public Vector3 PredictedPosition(long tick, float tickSeconds) => Position + Velocity * ((tick - LastTick) * tickSeconds);
    }

    public interface ITrackManager
    {
        FidelityLevel Level { get; }
        IReadOnlyList<Track> Tracks { get; }
        void Update(long tick, List<Detection> detections, EngagementLog log);
        bool TryGet(int trackId, out Track track);
        bool TryGetByTruth(int truthId, out Track track);
        void MarkDestroyed(int trackId);
    }

    /// <summary>
    /// Tracks L0: perfect correlation. Each truth object gets exactly one track with a stable id, its state
    /// copied from the detection. What it does NOT model (L1+): measurement noise, filtering, track
    /// quality, mis-association, identification errors.
    /// </summary>
    public sealed class TrackManagerL0 : ITrackManager
    {
        readonly TrackProfileData _profile;
        readonly List<Track> _tracks = new List<Track>();
        readonly Dictionary<int, int> _byTruth = new Dictionary<int, int>();
        readonly Dictionary<int, int> _byId = new Dictionary<int, int>();
        int _nextId = 1;

        public TrackManagerL0(in TrackProfileData profile) => _profile = profile;

        public FidelityLevel Level => FidelityLevel.L0;
        public IReadOnlyList<Track> Tracks => _tracks;

        public void Update(long tick, List<Detection> detections, EngagementLog log)
        {
            // Detections arrive in truth-feed order; that order is deterministic, and so are the track ids.
            for (int i = 0; i < detections.Count; i++)
            {
                Detection d = detections[i];
                if (_byTruth.TryGetValue(d.TargetId, out int index))
                {
                    Track t = _tracks[index];
                    if (t.Destroyed) continue;
                    t.Position = d.Position;
                    t.Velocity = d.Velocity;
                    t.LastTick = tick;
                    t.Active = true;
                    _tracks[index] = t;
                    continue;
                }
                int id = _nextId++;
                int ev = log.Add(tick, EventKind.TrackCreated, "Tracks L0 (perfect correlation)",
                    System.FormattableString.Invariant($"track {id}: {d.Identity} {d.Kind} at {d.RangeM / 1000f:0.0} km"),
                    id, root: RootKind.Scenario);
                _byTruth[d.TargetId] = _tracks.Count;
                _byId[id] = _tracks.Count;
                _tracks.Add(new Track
                {
                    Id = id, TruthId = d.TargetId, Kind = d.Kind, Identity = d.Identity,
                    Position = d.Position, Velocity = d.Velocity,
                    FirstTick = tick, LastTick = tick, Active = true, CreatedEvent = ev,
                });
            }

            for (int i = 0; i < _tracks.Count; i++)
            {
                Track t = _tracks[i];
                if (!t.Active || t.Destroyed || tick - t.LastTick <= _profile.DropAfterTicks) continue;
                t.Active = false;
                _tracks[i] = t;
                log.Add(tick, EventKind.TrackDropped, "Tracks L0 (perfect correlation)",
                    System.FormattableString.Invariant($"track {t.Id} dropped: no detection for {tick - t.LastTick} ticks"),
                    t.Id, t.CreatedEvent);
            }
        }

        public bool TryGet(int trackId, out Track track)
        {
            if (_byId.TryGetValue(trackId, out int i)) { track = _tracks[i]; return true; }
            track = default;
            return false;
        }

        public bool TryGetByTruth(int truthId, out Track track)
        {
            if (_byTruth.TryGetValue(truthId, out int i)) { track = _tracks[i]; return true; }
            track = default;
            return false;
        }

        public void MarkDestroyed(int trackId)
        {
            if (!_byId.TryGetValue(trackId, out int i)) return;
            Track t = _tracks[i];
            t.Destroyed = true;
            t.Active = false;
            _tracks[i] = t;
        }
    }

    public static class TrackFactory
    {
        public static ITrackManager Create(FidelityLevel requested, in TrackProfileData profile, out FidelityResolution resolution)
        {
            resolution = FidelityLadder.Resolve(ElementKind.Tracks, requested);
            switch (resolution.Resolved)
            {
                default: return new TrackManagerL0(profile);
            }
        }
    }
}
