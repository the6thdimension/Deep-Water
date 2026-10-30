using System.Collections.Generic;
using DeepWater.AirWing.Core.State;
using UnityEngine;

namespace DeepWater.AirWing.Core.Radar
{
    /// <summary>What a radar was doing when it painted a jet (J3): searching, tracking it, or guiding a missile at it (slice 3).</summary>
    public enum PaintKind : byte { Search = 1, Track = 2, Missile = 3 }

    /// <summary>One radar's beam crossing one jet on one tick: who, at whom, and how. The core's truth; a pilot never reads it.</summary>
    public readonly struct RadarPaint
    {
        /// <summary>The emitting aircraft's id, or minus a radar missile's id (W68).</summary>
        public readonly int EmitterId, TargetId;
        public readonly PaintKind Kind;
        /// <summary>Where the emitter is this tick.</summary>
        public readonly Vector3 From;

        public RadarPaint(int emitterId, int targetId, PaintKind kind, Vector3 from)
        {
            EmitterId = emitterId;
            TargetId = targetId;
            Kind = kind;
            From = from;
        }
    }

    /// <summary>
    /// One radar the warning receiver hears (J3): where it is, relative to the jet's nose (azimuth right, elevation up,
    /// degrees, measured when it last painted the jet), and what it is doing. No range, no identity.
    /// </summary>
    public readonly struct WarningContact
    {
        public readonly float AzimuthDeg, ElevationDeg;
        public readonly PaintKind Kind;
        /// <summary>Seconds since it last painted the jet.</summary>
        public readonly float AgeS;

        public WarningContact(float azimuthDeg, float elevationDeg, PaintKind kind, float ageS)
        {
            AzimuthDeg = azimuthDeg;
            ElevationDeg = elevationDeg;
            Kind = kind;
            AgeS = ageS;
        }

        public override string ToString() => $"{Kind} at {AzimuthDeg:0}°/{ElevationDeg:0}° ({AgeS:0.0} s)";
    }

    /// <summary>
    /// A jet's radar warning receiver (SENSORS-CONCEPT J3), stepped after every radar has looked this tick. It hears each
    /// radar whose beam crossed the jet (the radars' <see cref="RadarPaint"/>s: a search sweep passing over it, whether
    /// or not that radar detected it, or a single-target track on it) and keeps showing it for the hold of its kind.
    /// <see cref="Contacts"/> is what a pilot may read: strongest threat first (missile, then track, then search), then
    /// nearest the nose. A reset of the jet clears it; a jet that is not flying hears nothing.
    /// </summary>
    public sealed class WarningReceiver
    {
        public readonly int AircraftId;
        public readonly WarningReceiverProfile Profile;

        struct Heard
        {
            public long Tick;
            public PaintKind Kind;
            public float AzimuthDeg, ElevationDeg;
        }

        // By emitter id, in insertion order irrelevant: Contacts is sorted on a total order, so it never depends on hashing.
        readonly Dictionary<int, Heard> _heard = new Dictionary<int, Heard>();
        readonly List<int> _expired = new List<int>();
        readonly List<WarningContact> _contacts = new List<WarningContact>();
        readonly List<int> _emitters = new List<int>();
        int _resets;

        public WarningReceiver(int aircraftId, in WarningReceiverProfile profile)
        {
            profile.Validate();
            AircraftId = aircraftId;
            Profile = profile;
        }

        /// <summary>Every radar heard now, strongest threat first.</summary>
        public IReadOnlyList<WarningContact> Contacts => _contacts;

        /// <summary>
        /// The radar behind <see cref="Contacts"/>[<paramref name="index"/>] (an aircraft id, or minus a missile's id): the
        /// core's truth, for tests and logs, not a pilot's.
        /// </summary>
        public int EmitterOf(int index) => _emitters[index];

        /// <summary>The strongest threat heard (null when quiet): what a warning tone would sound for.</summary>
        public PaintKind? Strongest => _contacts.Count == 0 ? (PaintKind?)null : _contacts[0].Kind;

        internal void Step(long tick, double tickS, Aircraft self, IReadOnlyList<Aircraft> all, IReadOnlyList<RadarPaint> paints)
        {
            if (self.Resets != _resets) { _resets = self.Resets; _heard.Clear(); }
            if (self.State.Status != FlightStatus.Flying) { _heard.Clear(); Rebuild(tick, tickS); return; }

            for (int i = 0; i < paints.Count; i++)
            {
                RadarPaint p = paints[i];
                if (p.TargetId != AircraftId) continue;
                JetRadarLook.Bearing(self.State.Position, self.State.Attitude, p.From, out _, out float az, out float el);
                _heard[p.EmitterId] = new Heard { Tick = tick, Kind = p.Kind, AzimuthDeg = az, ElevationDeg = el };
            }
            Rebuild(tick, tickS);
        }

        void Rebuild(long tick, double tickS)
        {
            _expired.Clear();
            foreach (KeyValuePair<int, Heard> kv in _heard)
            {
                double age = (tick - kv.Value.Tick) * tickS;
                double hold = kv.Value.Kind == PaintKind.Search ? Profile.SearchHoldS : Profile.TrackHoldS;
                if (age > hold) _expired.Add(kv.Key);
            }
            foreach (int id in _expired) _heard.Remove(id);

            _emitters.Clear();
            _emitters.AddRange(_heard.Keys);
            _emitters.Sort((a, b) =>
            {
                Heard x = _heard[a], y = _heard[b];
                int c = ((int)y.Kind).CompareTo((int)x.Kind);
                if (c != 0) return c;
                c = Mathf.Abs(x.AzimuthDeg).CompareTo(Mathf.Abs(y.AzimuthDeg));
                return c != 0 ? c : a.CompareTo(b);
            });
            _contacts.Clear();
            foreach (int id in _emitters)
            {
                Heard h = _heard[id];
                _contacts.Add(new WarningContact(h.AzimuthDeg, h.ElevationDeg, h.Kind, (float)((tick - h.Tick) * tickS)));
            }
        }
    }
}
