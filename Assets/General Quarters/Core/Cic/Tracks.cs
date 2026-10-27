using System.Collections.Generic;
using UnityEngine;
using RHRadarSuite.Tracking;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;

namespace DeepWater.GeneralQuarters.Core.Cic
{
    [System.Serializable]
    public struct TrackProfileData
    {
        /// <summary>Ticks without a detection before a track is dropped (Tracks L0).</summary>
        public int DropAfterTicks;
        /// <summary>Tracks L1's track-file settings. Unused at L0.</summary>
        public TrackL1Data L1;
    }

    /// <summary>
    /// Tracks L1's data (G64): RH Radar Suite's track file, its M-of-N confirmation, its drop rule, its gate and its
    /// α-β gains. Every value is AUTHORED; the loadout's provenance says why each was chosen.
    /// </summary>
    [System.Serializable]
    public struct TrackL1Data
    {
        /// <summary>M of M-of-N: plots needed within a new track's first <see cref="ConfirmLooks"/> looks.</summary>
        public int ConfirmHits;
        /// <summary>N of M-of-N.</summary>
        public int ConfirmLooks;
        /// <summary>Looks in a row without a plot after which a confirmed track, coasting, is dropped.</summary>
        public int DropAfterMisses;
        /// <summary>The plots' noise, one sigma per axis, m: the gate's width.</summary>
        public float MeasurementSigmaM;
        /// <summary>Gate half-width in sigmas.</summary>
        public float GateSigmas;
        /// <summary>The fastest target to expect, m/s: how far a one-plot track gates per second.</summary>
        public float MaxSpeedMps;
        /// <summary>The hardest manoeuvre to expect, m/s²: how far a track with a velocity gates beyond its prediction.</summary>
        public float MaxAccelMps2;
        /// <summary>The α-β filter's α; β follows Benedict–Bordner (β = α²/(2 − α)).</summary>
        public float Alpha;

        /// <summary>False until the loadout fills it in (an L0 loadout leaves it empty).</summary>
        public bool IsSet => ConfirmHits > 0 && ConfirmLooks > 0 && MeasurementSigmaM > 0f && Alpha > 0f;

        public TrackFileSettings ToSettings() => new TrackFileSettings
        {
            ConfirmHits = ConfirmHits, ConfirmLooks = ConfirmLooks, DropAfterMisses = DropAfterMisses,
            MeasurementSigmaM = MeasurementSigmaM, GateSigmas = GateSigmas,
            MaxSpeedMps = MaxSpeedMps, MaxAccelMps2 = MaxAccelMps2,
            Gains = AlphaBetaGains.BenedictBordner(Alpha),
        };
    }

    /// <summary>The CIC's belief about one contact. Fire control and guidance use tracks, never truth.</summary>
    public struct Track
    {
        public int Id;
        /// <summary>The truth object whose report last updated it (the game's bookkeeping for kills and identity, G68).</summary>
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
        /// <summary>Plots in its last N looks, over N (Tracks L1, counted by the track file); 1 at L0.</summary>
        public float Quality;
        /// <summary>True while it flies on its prediction after a missed look (Tracks L1).</summary>
        public bool Coasting;

        /// <summary>Dead-reckoned position at <paramref name="tick"/> (constant velocity from the last update).</summary>
        public Vector3 PredictedPosition(long tick, float tickSeconds) => Position + Velocity * ((tick - LastTick) * tickSeconds);
    }

    public interface ITrackManager
    {
        FidelityLevel Level { get; }
        IReadOnlyList<Track> Tracks { get; }
        /// <summary>
        /// Take this tick's detections. <paramref name="look"/> is true on the ticks the radar's Watch is due, whether or
        /// not it could sweep (a radar offline makes empty looks, and its tracks coast and drop).
        /// </summary>
        void Update(long tick, bool look, List<Detection> detections, EngagementLog log);
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

        public void Update(long tick, bool look, List<Detection> detections, EngagementLog log)
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
                    FirstTick = tick, LastTick = tick, Active = true, CreatedEvent = ev, Quality = 1f,
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

    /// <summary>
    /// Tracks L1 (G64): RH Radar Suite's track file in the CIC. Each look's plots are associated with the tracks whose
    /// gates they fall in, nearest first, never by which target they came from; the rest start tentative tracks. A
    /// tentative track that makes M plots in its first N looks becomes a CIC track (TrackCreated); a confirmed track that
    /// misses coasts on its α-β prediction and is dropped after the set run of misses. Fire control and guidance read the
    /// filtered position and velocity, never truth. Identity and kind come from the truth object of the plot that last
    /// updated the track (G68); association never reads them.
    /// What it does NOT model (L2+): a Kalman filter with its own covariance and a range-dependent gate, multi-sensor
    /// fusion, identification from the track's own behaviour, and false-alarm tracks (Radar L1 makes no false plots).
    /// </summary>
    public sealed class TrackManagerL1 : ITrackManager
    {
        const string Rule = "Tracks L1 (α-β, M of N, gating)";

        readonly TrackL1Data _data;
        readonly float _tickSeconds;
        readonly TrackFile _file;
        readonly List<Plot> _plots = new List<Plot>();
        readonly List<Track> _tracks = new List<Track>();
        readonly List<int> _fileIds = new List<int>();                         // per CIC track: its track-file id, −1 once gone
        readonly Dictionary<int, int> _byId = new Dictionary<int, int>();      // CIC track id → index
        readonly Dictionary<int, int> _byFileId = new Dictionary<int, int>();  // live track-file id → index
        readonly Dictionary<int, int> _byTruth = new Dictionary<int, int>();   // truth id → index of the track its latest plot updated
        readonly List<Lost> _lost = new List<Lost>();
        bool _assessing;
        int _nextId = 1;

        struct Lost
        {
            public int FileId, Misses;
            public double LastHitS;
        }

        public TrackManagerL1(in TrackProfileData profile, float tickSeconds)
        {
            if (!profile.L1.IsSet)
                throw new System.ArgumentException("Tracks L1 needs its profile data (TrackProfileData.L1): M of N, the gate and α", nameof(profile));
            if (!(tickSeconds > 0f)) throw new System.ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "the tick must be positive");
            _data = profile.L1;
            _tickSeconds = tickSeconds;
            _file = new TrackFile(profile.L1.ToSettings());
            _file.StateChanged += (t, from) =>
            {
                if (t.State == TrackState.Dropped && !_assessing) _lost.Add(new Lost { FileId = t.Id, Misses = t.Misses, LastHitS = t.LastHitS });
            };
        }

        public FidelityLevel Level => FidelityLevel.L1;
        public IReadOnlyList<Track> Tracks => _tracks;
        /// <summary>The track file underneath, tentative tracks included (for tests and the TAO's diagnostics).</summary>
        public TrackFile File => _file;

        public void Update(long tick, bool look, List<Detection> detections, EngagementLog log)
        {
            if (!look) return;
            _plots.Clear();
            for (int i = 0; i < detections.Count; i++)
            {
                Vector3 p = detections[i].Position;
                _plots.Add(new Plot(p.x, p.y, p.z));
            }
            _lost.Clear();
            double timeS = tick * (double)_tickSeconds;
            _file.Scan(timeS, _plots);

            // Tracks the file let go this look: a confirmed track out of misses (tentative ones never reached the CIC).
            for (int i = 0; i < _lost.Count; i++)
            {
                Lost lost = _lost[i];
                if (!_byFileId.TryGetValue(lost.FileId, out int index)) continue;
                _byFileId.Remove(lost.FileId);
                _fileIds[index] = -1;
                Track t = _tracks[index];
                t.Active = false;
                t.Coasting = false;
                _tracks[index] = t;
                log.Add(tick, EventKind.TrackDropped, Rule,
                    System.FormattableString.Invariant($"track {t.Id} dropped: no plot for {lost.Misses} looks ({timeS - lost.LastHitS:0.0} s)"),
                    t.Id, t.CreatedEvent);
            }

            // Live tracks: a newly confirmed one joins the CIC; every confirmed or coasting one takes its filter's state.
            IReadOnlyList<RadarTrack> live = _file.Tracks;
            for (int i = 0; i < live.Count; i++)
            {
                RadarTrack rt = live[i];
                if (rt.State == TrackState.Tentative) continue;
                bool hit = rt.LastPlot >= 0;
                Detection d = hit ? detections[rt.LastPlot] : default;
                if (!_byFileId.TryGetValue(rt.Id, out int index))
                {
                    // Only a plot confirms a track, so this look's plot is the one that did it, and its draw is the luck.
                    int id = _nextId++;
                    int ev = log.Add(tick, EventKind.TrackCreated, Rule,
                        System.FormattableString.Invariant(
                            $"track {id}: {d.Identity} {d.Kind} at {d.RangeM / 1000f:0.0} km, confirmed {_data.ConfirmHits} of {_data.ConfirmLooks} (SCNR {d.ScnrDb:0.0} dB)"),
                        id, root: RootKind.Chance, chance: d.Chance);
                    index = _tracks.Count;
                    _tracks.Add(new Track { Id = id, FirstTick = tick, CreatedEvent = ev });
                    _fileIds.Add(rt.Id);
                    _byId[id] = index;
                    _byFileId[rt.Id] = index;
                }

                Track t = _tracks[index];
                if (hit)
                {
                    t.TruthId = d.TargetId;
                    t.Kind = d.Kind;
                    t.Identity = d.Identity;
                    _byTruth[d.TargetId] = index;
                }
                Plot at = rt.Position;
                t.Position = new Vector3((float)at.X, (float)at.Y, (float)at.Z);
                t.Velocity = new Vector3((float)rt.VelocityX, (float)rt.VelocityY, (float)rt.VelocityZ);
                // The filter stands at this look (a hit, or a coast once it has a velocity); a one-plot track has no velocity to fly on.
                t.LastTick = tick;
                t.Active = true;
                t.Quality = (float)rt.Quality;
                t.Coasting = rt.State == TrackState.Coasting;
                _tracks[index] = t;
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
            if (_byTruth.TryGetValue(truthId, out int i) && _tracks[i].TruthId == truthId) { track = _tracks[i]; return true; }
            track = default;
            return false;
        }

        /// <summary>A kill assessed: the track is finished, and the track file lets it go at once so its gate claims no more plots.</summary>
        public void MarkDestroyed(int trackId)
        {
            if (!_byId.TryGetValue(trackId, out int i)) return;
            Track t = _tracks[i];
            t.Destroyed = true;
            t.Active = false;
            t.Coasting = false;
            _tracks[i] = t;
            int fileId = _fileIds[i];
            if (fileId < 0) return;
            _fileIds[i] = -1;
            _byFileId.Remove(fileId);
            _assessing = true;
            _file.Drop(fileId);
            _assessing = false;
        }
    }

    public static class TrackFactory
    {
        public static ITrackManager Create(FidelityLevel requested, in TrackProfileData profile, float tickSeconds, out FidelityResolution resolution)
        {
            resolution = FidelityLadder.Resolve(ElementKind.Tracks, requested);
            switch (resolution.Resolved)
            {
                case FidelityLevel.L1: return new TrackManagerL1(profile, tickSeconds);
                default: return new TrackManagerL0(profile);
            }
        }
    }
}
