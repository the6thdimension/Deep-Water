using System;
using System.Collections.Generic;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using RHRadarSuite.Detection;
using RHRadarSuite.Tracking;
using UnityEngine;

namespace DeepWater.AirWing.Core.Radar
{
    /// <summary>
    /// One jet's radar in the flight core (SENSORS-CONCEPT J1, J2, J6, J16): its mode, its track file and its lock, stepped
    /// once a tick after the jets have flown. The pilot commands it through <see cref="PilotInputs.Radar"/> (so the input
    /// log replays it); what it believes is <see cref="Tracks"/>, never the truth.
    /// <list type="bullet">
    /// <item>A search mode looks at every flying aircraft inside its volume once per frame, on the ticks
    /// <c>(tick + id) % frame == 0</c>, so sixteen radars do not all sweep on the same tick. The plots go to the track
    /// file (the radar suite's <c>TrackFile</c>: M-of-N confirmation, α-β filters, coasting, dropping).</item>
    /// <item>Dogfight mode locks the nearest contact it finds, at once.</item>
    /// <item>A lock (<see cref="RadarCommand.Lock"/> on a confirmed or coasting track inside the gimbal and range) puts it
    /// in single-target track: one look at that aircraft every <c>SingleTargetLookS</c>, with the extra gain; other tracks
    /// coast and drop. A run of missed looks, or leaving the gimbal, loses the lock, back to the search mode it came from.</item>
    /// <item>A reset of the jet (the arena's respawn) clears everything and returns to the start mode. A jet that is not
    /// flying is silent.</item>
    /// </list>
    /// Which aircraft made each track (<see cref="TryTruth"/>) is kept for the core's own use (the warning receiver and the
    /// radar missile, later slices). A pilot reads <see cref="Tracks"/>.
    /// </summary>
    public sealed class JetRadar
    {
        public readonly int AircraftId;
        public readonly RadarProfile Profile;

        readonly double _snrAt1mDb;
        readonly TrackFileSettings _fileSettings;
        TrackFile _file;
        readonly Dictionary<int, int> _truth = new Dictionary<int, int>();
        readonly List<int> _stale = new List<int>();
        readonly List<Plot> _plots = new List<Plot>();
        readonly List<int> _plotOwners = new List<int>();
        int _resets;
        int _misses;

        /// <summary>What the radar is doing now.</summary>
        public RadarMode Mode { get; private set; }
        /// <summary>The search mode a lock returns to when it is broken or lost.</summary>
        public RadarMode SearchMode { get; private set; }
        /// <summary>The locked track (0: none). Its id can change if a look re-associates the lock to a fresh track.</summary>
        public int LockedTrackId { get; private set; }
        /// <summary>The truth behind the lock (0: none): the core's, for the warning receiver and the radar missile.</summary>
        public int LockedAircraftId { get; private set; }
        /// <summary>True on ticks the jet is flying and the radar is on: other jets' warning receivers can hear it.</summary>
        public bool Emitting { get; private set; }
        /// <summary>Looks taken in all (each is one aircraft looked at once).</summary>
        public long Looks { get; private set; }
        /// <summary>The last look's result at each aircraft, by id (for tests and the debug view; overwritten each look).</summary>
        public IReadOnlyDictionary<int, JetLookResult> LastLooks => _lastLooks;
        readonly Dictionary<int, JetLookResult> _lastLooks = new Dictionary<int, JetLookResult>();

        /// <summary>Every live track: tentative, confirmed or coasting.</summary>
        public IReadOnlyList<RadarTrack> Tracks => _file.Tracks;

        public JetRadar(int aircraftId, in RadarProfile profile)
        {
            profile.Validate();
            AircraftId = aircraftId;
            Profile = profile;
            _snrAt1mDb = JetRadarLook.SnrAt1mDb(profile);
            TrackFileSettings s = TrackFileSettings.Textbook(profile.TrackSigmaM, profile.TrackMaxSpeedMps, profile.TrackMaxAccelMps2);
            s.ConfirmHits = profile.ConfirmHits;
            s.ConfirmLooks = profile.ConfirmLooks;
            s.DropAfterMisses = profile.DropAfterMisses;
            _fileSettings = s;
            Clear();
        }

        /// <summary>The aircraft whose plot last updated track <paramref name="trackId"/>: the core's truth, not a pilot's.</summary>
        public bool TryTruth(int trackId, out int aircraftId) => _truth.TryGetValue(trackId, out aircraftId);

        public bool TryTrack(int trackId, out RadarTrack track) => _file.TryGet(trackId, out track);

        void Clear()
        {
            _file = new TrackFile(_fileSettings);
            _truth.Clear();
            _lastLooks.Clear();
            Mode = Profile.StartMode;
            SearchMode = Profile.StartMode == RadarMode.Off ? RadarMode.RangeWhileSearch : Profile.StartMode;
            LockedTrackId = 0;
            LockedAircraftId = 0;
            _misses = 0;
        }

        /// <summary>
        /// One tick, after every jet has flown: a reset first, then the pilot's command, then this tick's looks.
        /// <paramref name="self"/> is this radar's jet; <paramref name="all"/> every aircraft in id order.
        /// </summary>
        internal void Step(long tick, double tickS, Aircraft self, in PilotInputs command, IReadOnlyList<Aircraft> all,
                           ISurfaceTruth surface, in RadarEnvironment world, DeterministicRng draws)
        {
            if (self.Resets != _resets) { _resets = self.Resets; Clear(); }
            Emitting = false;
            if (self.State.Status != FlightStatus.Flying) return;

            Obey(command, self, all);
            if (Mode == RadarMode.Off) return;
            Emitting = true;

            double timeS = (tick + 1) * tickS;
            double? seaGamma = world.Sea ? Clutter.SeaGammaDb(world.SeaBeaufort, RadarEquation.WavelengthM(Profile.Terms.FrequencyHz)) : (double?)null;
            float myHeight = HeightAbove(self.State.Position, surface);

            if (Mode == RadarMode.SingleTarget)
            {
                if (!Due(tick, Profile.SingleTargetLookS, tickS)) return;
                LookAtLock(timeS, self, all, surface, seaGamma, myHeight, draws);
                return;
            }

            RadarModeProfile volume = Profile.Volume(Mode).Value;
            if (!Due(tick, volume.FrameS, tickS)) return;
            _plots.Clear();
            _plotOwners.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft o = all[i];
                if (o.Id == AircraftId || o.State.Status != FlightStatus.Flying || !o.Profile.RadarSignature.IsSet) continue;
                if (!JetRadarLook.InVolume(volume, self.State.Position, self.State.Attitude, o.State.Position, out _)) continue;
                JetLookResult look = LookAt(self, o, 0.0, surface, seaGamma, myHeight, draws);
                if (!look.Detected) continue;
                _plots.Add(look.Plot);
                _plotOwners.Add(o.Id);
            }
            _file.Scan(timeS, _plots);
            RecordTruth();

            if (Mode == RadarMode.Dogfight && _plots.Count > 0)
            {
                // The first contact it finds: the nearest plot this frame.
                int nearest = 0;
                double best = double.PositiveInfinity;
                for (int k = 0; k < _plots.Count; k++)
                {
                    double r = Sq(_plots[k], self.State.Position);
                    if (r < best) { best = r; nearest = k; }
                }
                foreach (RadarTrack t in _file.Tracks)
                    if (t.LastPlot == nearest) { LockOn(t.Id, _plotOwners[nearest]); break; }
            }
        }

        /// <summary>The pilot's command this tick (<see cref="PilotInputs.Radar"/>, <see cref="PilotInputs.RadarTrack"/>).</summary>
        void Obey(in PilotInputs command, Aircraft self, IReadOnlyList<Aircraft> all)
        {
            switch (command.Radar)
            {
                case RadarCommand.None: return;
                case RadarCommand.Off: Unlock(); Mode = RadarMode.Off; return;
                case RadarCommand.RangeWhileSearch: Search(RadarMode.RangeWhileSearch); return;
                case RadarCommand.TrackWhileScan: Search(RadarMode.TrackWhileScan); return;
                case RadarCommand.Dogfight: Search(RadarMode.Dogfight); return;
                case RadarCommand.Unlock: if (Mode == RadarMode.SingleTarget) { Unlock(); Mode = SearchMode; } return;
                case RadarCommand.Lock:
                {
                    if (Mode == RadarMode.Off || !_file.TryGet(command.RadarTrack, out RadarTrack t)) return;
                    if (t.State != TrackState.Confirmed && t.State != TrackState.Coasting) return;
                    if (!_truth.TryGetValue(t.Id, out int who)) return;
                    Vector3 at = new Vector3((float)t.Position.X, (float)t.Position.Y, (float)t.Position.Z);
                    if (!JetRadarLook.InGimbal(Profile, self.State.Position, self.State.Attitude, at)) return;
                    LockOn(t.Id, who);
                    return;
                }
            }
        }

        void Search(RadarMode mode)
        {
            Unlock();
            Mode = mode;
            SearchMode = mode;
        }

        void LockOn(int trackId, int aircraftId)
        {
            if (Mode != RadarMode.SingleTarget) SearchMode = Mode;
            Mode = RadarMode.SingleTarget;
            LockedTrackId = trackId;
            LockedAircraftId = aircraftId;
            _misses = 0;
        }

        void Unlock()
        {
            LockedTrackId = 0;
            LockedAircraftId = 0;
            _misses = 0;
        }

        void LookAtLock(double timeS, Aircraft self, IReadOnlyList<Aircraft> all, ISurfaceTruth surface, double? seaGamma, float myHeight, DeterministicRng draws)
        {
            _plots.Clear();
            _plotOwners.Clear();
            Aircraft target = null;
            for (int i = 0; i < all.Count; i++) if (all[i].Id == LockedAircraftId) { target = all[i]; break; }
            if (target != null && target.State.Status == FlightStatus.Flying && target.Profile.RadarSignature.IsSet
                && JetRadarLook.InGimbal(Profile, self.State.Position, self.State.Attitude, target.State.Position))
            {
                JetLookResult look = LookAt(self, target, Profile.SingleTargetGainDb, surface, seaGamma, myHeight, draws);
                if (look.Detected) { _plots.Add(look.Plot); _plotOwners.Add(target.Id); }
            }
            _file.Scan(timeS, _plots);
            RecordTruth();
            if (_plots.Count == 0)
            {
                if (++_misses >= Profile.SingleTargetDropMisses) { Unlock(); Mode = SearchMode; }
                return;
            }
            _misses = 0;
            foreach (RadarTrack t in _file.Tracks)
                if (t.LastPlot == 0) { LockedTrackId = t.Id; break; }
        }

        JetLookResult LookAt(Aircraft self, Aircraft o, double extraGainDb, ISurfaceTruth surface, double? seaGamma, float myHeight, DeterministicRng draws)
        {
            Looks++;
            JetLookResult look = JetRadarLook.Look(Profile, _snrAt1mDb, extraGainDb, seaGamma, self.State.Position, myHeight,
                                                   o.State.Position, o.State.Attitude, HeightAbove(o.State.Position, surface), o.Profile.RadarSignature.Rcs, draws);
            _lastLooks[o.Id] = look;
            return look;
        }

        /// <summary>After a scan: each track's maker is the owner of the plot that last updated it; dropped tracks are forgotten.</summary>
        void RecordTruth()
        {
            foreach (RadarTrack t in _file.Tracks)
                if (t.LastPlot >= 0) _truth[t.Id] = _plotOwners[t.LastPlot];
            if (_truth.Count <= _file.Tracks.Count) return;
            _stale.Clear();
            foreach (int id in _truth.Keys) if (!_file.TryGet(id, out _)) _stale.Add(id);
            foreach (int id in _stale) _truth.Remove(id);
        }

        bool Due(long tick, float periodS, double tickS)
        {
            long every = Math.Max(1L, (long)Math.Round(periodS / tickS));
            return (tick + AircraftId) % every == 0;
        }

        static float HeightAbove(Vector3 p, ISurfaceTruth surface) => p.y - surface.HeightAt(p.x, p.z);

        static double Sq(in Plot p, Vector3 v)
        {
            double dx = p.X - v.x, dy = p.Y - v.y, dz = p.Z - v.z;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
