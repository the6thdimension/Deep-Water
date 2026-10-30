using System;
using RHRadarSuite.Detection;

namespace DeepWater.AirWing.Core.Radar
{
    /// <summary>
    /// What a jet's radar is doing (SENSORS-CONCEPT J2, J6). Two search modes, two tracking modes and off:
    /// <list type="bullet">
    /// <item><see cref="RangeWhileSearch"/>: a wide sweep that finds contacts and their range; the long-range picture.</item>
    /// <item><see cref="TrackWhileScan"/>: a narrower, faster sweep that keeps full tracks (with velocity) on several
    /// contacts; the targets only hear a search radar.</item>
    /// <item><see cref="Dogfight"/>: a narrow, tall beam ahead, short range, fast; the first contact it finds is locked.</item>
    /// <item><see cref="SingleTarget"/>: the beam on one contact, precise; the target hears it track.</item>
    /// </list>
    /// </summary>
    public enum RadarMode : byte { Off = 0, RangeWhileSearch = 1, TrackWhileScan = 2, Dogfight = 3, SingleTarget = 4 }

    /// <summary>One search mode's volume and pace, in the jet's body frame (azimuth right of the nose, elevation up).</summary>
    [Serializable]
    public struct RadarModeProfile
    {
        /// <summary>Half-width either side of the nose, degrees.</summary>
        public float AzimuthHalfDeg;
        /// <summary>The elevation band, degrees from the nose's plane (down negative).</summary>
        public float ElevationMinDeg, ElevationMaxDeg;
        /// <summary>The farthest the mode looks, metres (its display range; detection itself falls off before it).</summary>
        public float RangeM;
        /// <summary>One sweep of the volume, seconds: each aircraft in it is looked at once per frame.</summary>
        public float FrameS;

        public void Validate(string mode)
        {
            if (!(AzimuthHalfDeg > 0f && AzimuthHalfDeg <= 180f)) throw new ArgumentException($"radar {mode}: azimuth half-width 0 … 180°");
            if (!(ElevationMinDeg < ElevationMaxDeg && ElevationMinDeg >= -90f && ElevationMaxDeg <= 90f)) throw new ArgumentException($"radar {mode}: elevation band −90 … 90°, min below max");
            if (!(RangeM > 0f) || !(FrameS > 0f)) throw new ArgumentException($"radar {mode}: range and frame must be positive");
        }
    }

    /// <summary>
    /// A jet's radar (SENSORS-CONCEPT J1, J2, J6, J16), baked from <c>AircraftProfileSO</c>: a generic game radar, every
    /// number AUTHORED, none from a real set. Detection and measurement use the radar suite's public textbook maths
    /// (<see cref="RadarEquation"/>, <see cref="Clutter"/>, <see cref="MeasurementErrors"/>); tracks are its
    /// <c>TrackFile</c>.
    /// </summary>
    public struct RadarProfile
    {
        public string Name;
        /// <summary>What it puts out and how it listens (the search look; a single-target look adds <see cref="SingleTargetGainDb"/>).</summary>
        public RadarTerms Terms;
        /// <summary>Beamwidths and error floors.</summary>
        public MeasurementTerms Measurement;
        /// <summary>Chance a look at empty sky reports a contact; sets the detection threshold (no false tracks are made).</summary>
        public double FalseAlarmProbability;
        public RadarModeProfile RangeWhileSearch, TrackWhileScan, Dogfight;
        /// <summary>How far off the nose the antenna can hold a single-target track, degrees.</summary>
        public float GimbalHalfDeg;
        /// <summary>Single-target track: its farthest range, metres; seconds between looks; extra gain over a search look, dB.</summary>
        public float SingleTargetRangeM, SingleTargetLookS, SingleTargetGainDb;
        /// <summary>Consecutive missed single-target looks after which the lock is lost.</summary>
        public int SingleTargetDropMisses;
        /// <summary>The track file: a plot's position noise for gating, one sigma, metres; the fastest and hardest-turning target to expect.</summary>
        public float TrackSigmaM, TrackMaxSpeedMps, TrackMaxAccelMps2;
        /// <summary>M-of-N confirmation and the misses after which a coasting track drops.</summary>
        public int ConfirmHits, ConfirmLooks, DropAfterMisses;
        /// <summary>The mode it starts in, and returns to after a reset.</summary>
        public RadarMode StartMode;

        public bool IsSet => !string.IsNullOrEmpty(Name);

        public void Validate()
        {
            if (!IsSet) throw new ArgumentException("radar: no name");
            if (!(Terms.PeakPowerW > 0.0 && Terms.FrequencyHz > 0.0 && Terms.BandwidthHz > 0.0)) throw new ArgumentException("radar: power, frequency and bandwidth must be positive");
            if (!(Measurement.AzimuthBeamwidthDeg > 0.0 && Measurement.ElevationBeamwidthDeg > 0.0 && Measurement.MonopulseSlope > 0.0))
                throw new ArgumentException("radar: beamwidths and monopulse slope must be positive");
            if (!(FalseAlarmProbability > 0.0 && FalseAlarmProbability < 1e-2)) throw new ArgumentException("radar: false-alarm probability 0 … 0.01");
            RangeWhileSearch.Validate("range-while-search");
            TrackWhileScan.Validate("track-while-scan");
            Dogfight.Validate("dogfight");
            if (!(GimbalHalfDeg > 0f && GimbalHalfDeg <= 90f)) throw new ArgumentException("radar: gimbal 0 … 90°");
            if (!(SingleTargetRangeM > 0f && SingleTargetLookS > 0f && SingleTargetGainDb >= 0f && SingleTargetDropMisses >= 1))
                throw new ArgumentException("radar: single-target range and look positive, gain zero or more, at least one miss to drop");
            if (!(TrackSigmaM > 0f && TrackMaxSpeedMps > 0f && TrackMaxAccelMps2 >= 0f)) throw new ArgumentException("radar: track sigma and speed positive, acceleration zero or more");
            if (!(ConfirmLooks >= 1 && ConfirmLooks <= 32 && ConfirmHits >= 1 && ConfirmHits <= ConfirmLooks && DropAfterMisses >= 1))
                throw new ArgumentException("radar: 1 ≤ M ≤ N ≤ 32 to confirm, at least one miss to drop");
            if (StartMode == RadarMode.SingleTarget) throw new ArgumentException("radar: it cannot start locked");
        }

        /// <summary>The volume and pace of a search mode (null for off and single-target).</summary>
        public RadarModeProfile? Volume(RadarMode mode) => mode switch
        {
            RadarMode.RangeWhileSearch => RangeWhileSearch,
            RadarMode.TrackWhileScan => TrackWhileScan,
            RadarMode.Dogfight => Dogfight,
            _ => (RadarModeProfile?)null,
        };
    }

    /// <summary>
    /// How big an aircraft looks to a radar (J1): its radar cross-section from the nose, the beam and the tail, dBsm,
    /// AUTHORED for a game. Unset: radars do not see it (as an unset combat profile lets rounds pass).
    /// </summary>
    public struct RadarSignatureProfile
    {
        public AspectRcs Rcs;
        public bool IsSet;
    }

    /// <summary>The world a radar looks through (J16): the sea under the fight, or none (no clutter).</summary>
    public struct RadarEnvironment
    {
        /// <summary>True: the surface is sea, with <see cref="SeaBeaufort"/>; false: no surface clutter.</summary>
        public bool Sea;
        /// <summary>The wind force on the Beaufort scale, 0 … 12 (the sea's roughness for clutter).</summary>
        public double SeaBeaufort;
    }
}
