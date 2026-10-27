using System;
using System.Collections.Generic;

namespace RHRadarSuite.Tracking
{
    /// <summary>One detection from one look: a position in the world frame, metres (Unity axes: x east, y up, z north).</summary>
    public readonly struct Plot
    {
        public readonly double X, Y, Z;
        public Plot(double x, double y, double z) { X = x; Y = y; Z = z; }
    }

    public enum TrackState { Tentative, Confirmed, Coasting, Dropped }

    /// <summary>
    /// How a track file confirms, keeps and drops tracks, and how wide it gates (P1: a radar's profile supplies these;
    /// <see cref="Textbook"/> is the classic starting point, not a particular radar).
    /// </summary>
    public struct TrackFileSettings
    {
        /// <summary>M of M-of-N: hits needed within a tentative track's first <see cref="ConfirmLooks"/> looks to confirm it.</summary>
        public int ConfirmHits;
        /// <summary>N of M-of-N (at most 32).</summary>
        public int ConfirmLooks;
        /// <summary>Consecutive misses after which a confirmed track, coasting, is dropped.</summary>
        public int DropAfterMisses;
        /// <summary>Measurement noise, one sigma per axis, metres.</summary>
        public double MeasurementSigmaM;
        /// <summary>Gate half-width in measurement sigmas (both the plot's and the prediction's noise).</summary>
        public double GateSigmas;
        /// <summary>The fastest target to expect, m/s: a one-hit track, whose velocity is unknown, gates this far per second.</summary>
        public double MaxSpeedMps;
        /// <summary>The hardest manoeuvre to expect, m/s²: a track with a velocity gates ½·a·t² beyond its prediction.</summary>
        public double MaxAccelMps2;
        public AlphaBetaGains Gains;

        /// <summary>2 of 3 to confirm, 3 misses to drop, a 3σ gate, Benedict–Bordner α 0.5.</summary>
        public static TrackFileSettings Textbook(double measurementSigmaM, double maxSpeedMps, double maxAccelMps2) => new TrackFileSettings
        {
            ConfirmHits = 2, ConfirmLooks = 3, DropAfterMisses = 3,
            MeasurementSigmaM = measurementSigmaM, GateSigmas = 3.0,
            MaxSpeedMps = maxSpeedMps, MaxAccelMps2 = maxAccelMps2,
            Gains = AlphaBetaGains.BenedictBordner(0.5),
        };
    }

    /// <summary>One track: what the radar believes about one target, never the target itself.</summary>
    public sealed class RadarTrack
    {
        public int Id { get; }
        public TrackState State { get; internal set; }
        public double BornS { get; }
        /// <summary>Time of the last plot that updated it.</summary>
        public double LastHitS { get; internal set; }
        /// <summary>Plots that have updated it, in all.</summary>
        public int Hits { get; internal set; }
        /// <summary>Looks since the last hit (0 right after one).</summary>
        public int Misses { get; internal set; }
        /// <summary>Hits in the last N looks (N = the settings' ConfirmLooks), over N: counted, not declared.</summary>
        public double Quality => (double)PopCount(History & LookMask) / LookCount;
        /// <summary>True once it has two hits, and so a velocity.</summary>
        public bool HasVelocity => Hits >= 2;

        internal AlphaBetaFilter X, Y, Z;
        internal uint History;          // bit 0 is the latest look: 1 a hit, 0 a miss
        internal uint LookMask;
        internal int LookCount;
        internal int Looks;             // looks since birth, the birth look included
        internal Plot FirstPlot;

        internal RadarTrack(int id, double timeS, in Plot plot, int lookCount)
        {
            Id = id;
            State = TrackState.Tentative;
            BornS = LastHitS = timeS;
            Hits = 1;
            LookCount = lookCount;
            LookMask = lookCount >= 32 ? uint.MaxValue : (1u << lookCount) - 1u;
            History = 1u;
            Looks = 1;
            FirstPlot = plot;
            X = new AlphaBetaFilter(plot.X, 0.0);
            Y = new AlphaBetaFilter(plot.Y, 0.0);
            Z = new AlphaBetaFilter(plot.Z, 0.0);
        }

        /// <summary>The filtered position at the last hit or coast.</summary>
        public Plot Position => new Plot(X.Position, Y.Position, Z.Position);
        public double VelocityX => X.Velocity;
        public double VelocityY => Y.Velocity;
        public double VelocityZ => Z.Velocity;
        public double Speed => Math.Sqrt(X.Velocity * X.Velocity + Y.Velocity * Y.Velocity + Z.Velocity * Z.Velocity);

        /// <summary>Where it is expected at <paramref name="timeS"/>, flown on from the filter's last state at constant velocity.</summary>
        public Plot PredictAt(double timeS)
        {
            double dt = timeS - _filterTimeS;
            return new Plot(X.Predict(dt), Y.Predict(dt), Z.Predict(dt));
        }

        internal double _filterTimeS;

        static int PopCount(uint v)
        {
            int n = 0;
            for (; v != 0; v &= v - 1) n++;
            return n;
        }
    }

    /// <summary>
    /// The track file (ROADMAP Phase 3; Brookner's lifecycle, Meyer's consumer contract), pure C# (P6). Each
    /// <see cref="Scan"/> is one look at the whole volume: plots associate to the tracks whose gates they fall in
    /// (nearest first, never by target identity), the rest start tentative tracks.
    /// <list type="bullet">
    /// <item>Tentative → Confirmed: M hits within its first N looks; dropped as soon as it can no longer make them.</item>
    /// <item>Confirmed → Coasting: a missed look; the filters fly the prediction on.</item>
    /// <item>Coasting → Confirmed: a hit in the (widening) gate. Coasting → Dropped: the settings' run of misses.</item>
    /// </list>
    /// A second hit initialises the velocity from the two plots; from the third, the α-β filters take over.
    /// Deterministic: ties break by track id, then by plot order.
    /// </summary>
    public sealed class TrackFile
    {
        readonly TrackFileSettings _settings;
        readonly List<RadarTrack> _tracks = new List<RadarTrack>();
        readonly List<Pair> _pairs = new List<Pair>();
        readonly List<bool> _plotTaken = new List<bool>();
        readonly HashSet<int> _trackTaken = new HashSet<int>();
        double _lastScanS = double.NegativeInfinity;
        int _nextId = 1;

        /// <summary>Raised on every change of state, with the state it left; Dropped is raised once, then the track leaves the file.</summary>
        public event Action<RadarTrack, TrackState> StateChanged;

        public TrackFile(in TrackFileSettings settings)
        {
            if (settings.ConfirmLooks < 1 || settings.ConfirmLooks > 32) throw new ArgumentOutOfRangeException(nameof(settings.ConfirmLooks), settings.ConfirmLooks, "1 ≤ N ≤ 32");
            if (settings.ConfirmHits < 1 || settings.ConfirmHits > settings.ConfirmLooks) throw new ArgumentOutOfRangeException(nameof(settings.ConfirmHits), settings.ConfirmHits, "1 ≤ M ≤ N");
            if (settings.DropAfterMisses < 1) throw new ArgumentOutOfRangeException(nameof(settings.DropAfterMisses), settings.DropAfterMisses, "at least one miss");
            if (!(settings.MeasurementSigmaM > 0.0) || !(settings.GateSigmas > 0.0)) throw new ArgumentOutOfRangeException(nameof(settings), "noise and gate must be positive");
            if (settings.MaxSpeedMps < 0.0 || settings.MaxAccelMps2 < 0.0) throw new ArgumentOutOfRangeException(nameof(settings), "speed and acceleration bounds must not be negative");
            _settings = settings;
        }

        /// <summary>Every live track (Tentative, Confirmed or Coasting), in the order they were born.</summary>
        public IReadOnlyList<RadarTrack> Tracks => _tracks;

        public bool TryGet(int id, out RadarTrack track)
        {
            foreach (RadarTrack t in _tracks)
                if (t.Id == id) { track = t; return true; }
            track = null;
            return false;
        }

        /// <summary>
        /// The confirmed or coasting track within <paramref name="halfWidthDeg"/> of <paramref name="bearingDeg"/> as seen
        /// from the origin (bearing clockwise from +z, north, toward +x, east): highest quality first, then nearest.
        /// </summary>
        public RadarTrack BestTrack(double originX, double originZ, double bearingDeg, double halfWidthDeg)
        {
            RadarTrack best = null;
            double bestRange = 0.0;
            foreach (RadarTrack t in _tracks)
            {
                if (t.State != TrackState.Confirmed && t.State != TrackState.Coasting) continue;
                double dx = t.X.Position - originX, dz = t.Z.Position - originZ;
                double brg = Math.Atan2(dx, dz) * (180.0 / Math.PI);
                double off = Math.Abs(((brg - bearingDeg) % 360.0 + 540.0) % 360.0 - 180.0);
                if (off > halfWidthDeg) continue;
                double range = Math.Sqrt(dx * dx + dz * dz);
                if (best == null || t.Quality > best.Quality || (t.Quality == best.Quality && range < bestRange))
                {
                    best = t;
                    bestRange = range;
                }
            }
            return best;
        }

        /// <summary>The gate's half-width for <paramref name="track"/> at <paramref name="timeS"/>, metres.</summary>
        public double GateM(RadarTrack track, double timeS)
        {
            double dt = timeS - track._filterTimeS;
            // Both the plot and the prediction carry measurement noise: σ√2 per axis.
            double noise = _settings.GateSigmas * _settings.MeasurementSigmaM * Math.Sqrt(2.0);
            return track.HasVelocity
                ? noise + 0.5 * _settings.MaxAccelMps2 * dt * dt
                : noise + _settings.MaxSpeedMps * dt;
        }

        /// <summary>One look at <paramref name="timeS"/> seconds with its <paramref name="plots"/> (possibly none).</summary>
        public void Scan(double timeS, IReadOnlyList<Plot> plots)
        {
            if (!(timeS > _lastScanS)) throw new ArgumentOutOfRangeException(nameof(timeS), timeS, "scans must move forward in time");
            _lastScanS = timeS;

            // 1. Every plot-track pair inside its gate, nearest first (ties: track id, then plot order).
            _pairs.Clear();
            for (int ti = 0; ti < _tracks.Count; ti++)
            {
                RadarTrack t = _tracks[ti];
                Plot p0 = t.PredictAt(timeS);
                double gate = GateM(t, timeS);
                for (int pi = 0; pi < plots.Count; pi++)
                {
                    Plot p = plots[pi];
                    double dx = p.X - p0.X, dy = p.Y - p0.Y, dz = p.Z - p0.Z;
                    double d2 = dx * dx + dy * dy + dz * dz;
                    if (d2 <= gate * gate) _pairs.Add(new Pair(d2, t.Id, ti, pi));
                }
            }
            _pairs.Sort();

            // 2. Greedy assignment: each plot to at most one track, each track at most one plot.
            _plotTaken.Clear();
            for (int i = 0; i < plots.Count; i++) _plotTaken.Add(false);
            _trackTaken.Clear();
            foreach (Pair pair in _pairs)
            {
                if (_plotTaken[pair.Plot] || _trackTaken.Contains(pair.TrackId)) continue;
                _plotTaken[pair.Plot] = true;
                _trackTaken.Add(pair.TrackId);
                Hit(_tracks[pair.Track], timeS, plots[pair.Plot]);
            }

            // 3. Every track without a plot missed this look.
            int count = _tracks.Count;
            for (int ti = 0; ti < count; ti++)
                if (!_trackTaken.Contains(_tracks[ti].Id)) Miss(_tracks[ti], timeS);

            // 4. Drop the dead, then start a tentative track on every unclaimed plot.
            for (int ti = _tracks.Count - 1; ti >= 0; ti--)
                if (_tracks[ti].State == TrackState.Dropped) _tracks.RemoveAt(ti);
            for (int pi = 0; pi < plots.Count; pi++)
                if (!_plotTaken[pi]) _tracks.Add(new RadarTrack(_nextId++, timeS, plots[pi], _settings.ConfirmLooks) { _filterTimeS = timeS });
        }

        void Hit(RadarTrack t, double timeS, in Plot p)
        {
            double dt = timeS - t._filterTimeS;
            if (t.Hits == 1)
            {
                // Two-point initiation: the second plot gives the velocity.
                double span = timeS - t.BornS;
                t.X = new AlphaBetaFilter(p.X, (p.X - t.FirstPlot.X) / span);
                t.Y = new AlphaBetaFilter(p.Y, (p.Y - t.FirstPlot.Y) / span);
                t.Z = new AlphaBetaFilter(p.Z, (p.Z - t.FirstPlot.Z) / span);
            }
            else
            {
                t.X.Update(_settings.Gains, p.X, dt);
                t.Y.Update(_settings.Gains, p.Y, dt);
                t.Z.Update(_settings.Gains, p.Z, dt);
            }
            t._filterTimeS = timeS;
            t.Hits++;
            t.Misses = 0;
            t.LastHitS = timeS;
            t.Looks++;
            t.History = (t.History << 1) | 1u;

            if (t.State == TrackState.Coasting) SetState(t, TrackState.Confirmed);
            else if (t.State == TrackState.Tentative && t.Hits >= _settings.ConfirmHits) SetState(t, TrackState.Confirmed);
        }

        void Miss(RadarTrack t, double timeS)
        {
            double dt = timeS - t._filterTimeS;
            if (t.HasVelocity)
            {
                t.X.Coast(dt);
                t.Y.Coast(dt);
                t.Z.Coast(dt);
                t._filterTimeS = timeS;
            }
            t.Misses++;
            t.Looks++;
            t.History <<= 1;

            switch (t.State)
            {
                case TrackState.Tentative:
                    // M of its first N looks, or never: drop once the looks left cannot make up the hits.
                    if (t.Hits + (_settings.ConfirmLooks - t.Looks) < _settings.ConfirmHits) SetState(t, TrackState.Dropped);
                    break;
                case TrackState.Confirmed:
                    SetState(t, TrackState.Coasting);
                    if (t.Misses >= _settings.DropAfterMisses) SetState(t, TrackState.Dropped);
                    break;
                case TrackState.Coasting:
                    if (t.Misses >= _settings.DropAfterMisses) SetState(t, TrackState.Dropped);
                    break;
            }
        }

        void SetState(RadarTrack t, TrackState next)
        {
            TrackState from = t.State;
            if (from == next) return;
            t.State = next;
            StateChanged?.Invoke(t, from);
        }

        readonly struct Pair : IComparable<Pair>
        {
            public readonly double D2;
            public readonly int TrackId, Track, Plot;
            public Pair(double d2, int trackId, int track, int plot) { D2 = d2; TrackId = trackId; Track = track; Plot = plot; }

            public int CompareTo(Pair other)
            {
                int c = D2.CompareTo(other.D2);
                if (c != 0) return c;
                c = TrackId.CompareTo(other.TrackId);
                return c != 0 ? c : Plot.CompareTo(other.Plot);
            }
        }
    }
}
