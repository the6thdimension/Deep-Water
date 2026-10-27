using System.Collections.Generic;
using UnityEngine;
using RHRadarSuite.Detection;
using RHRadarSuite.Tracking;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Simulation;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core.Cic
{
    /// <summary>Radar tuning (P1); baked from the loadout asset.</summary>
    [System.Serializable]
    public struct RadarProfileData
    {
        /// <summary>Instrumented range, m: the farthest the radar reports anything.</summary>
        public float InstrumentedRangeM;
        /// <summary>Antenna height above the waterline, m: sets the radar horizon.</summary>
        public float AntennaHeightM;
        /// <summary>Ticks between sweeps (the radar's Watch).</summary>
        public int SweepPeriodTicks;
        /// <summary>Radar L1's data: the generic array, its calibration, its clutter processing and the target RCS. Unused at L0.</summary>
        public RadarL1Data L1;
    }

    /// <summary>
    /// Radar L1's data (G62–G67): one generic S-band search-and-track array in SPY-1's role. Every number is AUTHORED
    /// (G63, standing constraint 8) except the one public range figure the power is solved from. The loadout's provenance
    /// names each source.
    /// </summary>
    [System.Serializable]
    public struct RadarL1Data
    {
        /// <summary>Carrier frequency, GHz (S band is 2–4 GHz).</summary>
        public float FrequencyGHz;
        /// <summary>Antenna gain, dBi.</summary>
        public float GainDbi;
        /// <summary>Receiver noise bandwidth, MHz: sets the range resolution c/2B and the range error.</summary>
        public float BandwidthMHz;
        public float NoiseFigureDb;
        /// <summary>System losses, dB (positive).</summary>
        public float LossesDb;
        /// <summary>Pulses integrated coherently per look.</summary>
        public int PulsesIntegrated;
        /// <summary>Two-way 3 dB beamwidths, degrees: the angle errors and the sea-clutter cell.</summary>
        public float AzimuthBeamwidthDeg, ElevationBeamwidthDeg;
        /// <summary>Monopulse slope k_m (about 1.6 for a sum-difference feed, Barton).</summary>
        public float MonopulseSlope;
        /// <summary>The errors that do not shrink with SNR, one sigma.</summary>
        public float RangeFloorM, AngleFloorDeg;
        /// <summary>The elevation band searched, degrees, all round in azimuth.</summary>
        public float MinElevationDeg, MaxElevationDeg;
        /// <summary>Probability of false alarm per look and cell: sets the detection threshold (G66). No false plots are made at L1.</summary>
        public float Pfa;
        /// <summary>
        /// Clutter improvement factor, dB: how far the radar's MTI or Doppler processing lowers sea clutter. 0 gives G67's
        /// constant-γ clutter as the model has it. PROPOSED, not in G61–G68 (BLUEPRINT G70): see the loadout's provenance.
        /// </summary>
        public float ClutterImprovementDb;
        /// <summary>
        /// The calibration (G63): a target of <see cref="CalibrationRcsDbsm"/> is seen with probability
        /// <see cref="CalibrationPd"/> in one look at <see cref="CalibrationRangeM"/>. The peak power is solved from it.
        /// </summary>
        public float CalibrationRangeM, CalibrationRcsDbsm, CalibrationPd;
        /// <summary>One radar cross-section per <see cref="TargetKind"/>, dBsm (G65). Aspect waits for the suite's Phase 2.</summary>
        public float UnknownRcsDbsm, AircraftRcsDbsm, AntiShipMissileRcsDbsm, SurfaceShipRcsDbsm;

        /// <summary>False until the loadout fills it in (an L0 loadout leaves it empty).</summary>
        public bool IsSet => FrequencyGHz > 0f && BandwidthMHz > 0f && CalibrationRangeM > 0f && AzimuthBeamwidthDeg > 0f;

        public float RcsDbsm(TargetKind kind)
        {
            switch (kind)
            {
                case TargetKind.Aircraft: return AircraftRcsDbsm;
                case TargetKind.AntiShipMissile: return AntiShipMissileRcsDbsm;
                case TargetKind.SurfaceShip: return SurfaceShipRcsDbsm;
                default: return UnknownRcsDbsm;
            }
        }
    }

    /// <summary>One sensor report of one target on one sweep.</summary>
    public struct Detection
    {
        /// <summary>The truth object the report came from. Identity is taken from truth at L0 and L1 (G68); association never reads it.</summary>
        public int TargetId;
        public TargetKind Kind;
        public Identity Identity;
        /// <summary>Where the sensor puts it: truth at L0, a noisy plot at L1.</summary>
        public Vector3 Position;
        /// <summary>Truth at L0. Zero at L1: the radar measures position only, and Tracks L1 estimates velocity.</summary>
        public Vector3 Velocity;
        public float RangeM;
        /// <summary>Signal to clutter plus noise, dB, of the look that made it (L1; 0 at L0).</summary>
        public float ScnrDb;
        /// <summary>The draw that decided the detection (L1; none at L0).</summary>
        public ChanceRoot Chance;
    }

    /// <summary>A sensor element. Each level is one implementation (BLUEPRINT §4).</summary>
    public interface ISensorModel
    {
        FidelityLevel Level { get; }
        Watch Watch { get; }
        bool Online { get; set; }

        /// <summary>Report what the sensor sees this sweep. Called only on ticks its Watch is due.</summary>
        void Sweep(in OwnShipState own, in SeaEnvironment sea, IReadOnlyList<TruthTarget> world, List<Detection> output);
    }

    public static class RadarMath
    {
        /// <summary>
        /// 4/3-earth radar horizon, m, between an antenna at <paramref name="antennaM"/> and a target at
        /// <paramref name="targetM"/> above the sea: d ≈ 4.12 km × (√h_antenna + √h_target), heights in metres.
        /// The standard 4/3-earth-radius refraction rule of thumb for a normal atmosphere.
        /// </summary>
        public static float HorizonM(float antennaM, float targetM) =>
            4120f * (Mathf.Sqrt(Mathf.Max(antennaM, 0f)) + Mathf.Sqrt(Mathf.Max(targetM, 0f)));
    }

    /// <summary>
    /// Radar L0: an honest floor. It detects every live target within instrumented range and above the
    /// radar horizon, every sweep, with no noise, no missed detections and perfect identification from
    /// truth. What it does NOT model (L1+): the radar equation, RCS, clutter, Pd/Pfa, beam scheduling.
    /// </summary>
    public sealed class RadarL0 : ISensorModel
    {
        readonly RadarProfileData _profile;

        public RadarL0(in RadarProfileData profile)
        {
            _profile = profile;
            Watch = new Watch(Mathf.Max(1, profile.SweepPeriodTicks));
            Online = true;
        }

        public FidelityLevel Level => FidelityLevel.L0;
        public Watch Watch { get; }
        public bool Online { get; set; }

        public void Sweep(in OwnShipState own, in SeaEnvironment sea, IReadOnlyList<TruthTarget> world, List<Detection> output)
        {
            Vector3 antenna = own.Position + Vector3.up * _profile.AntennaHeightM;
            for (int i = 0; i < world.Count; i++)
            {
                TruthTarget t = world[i];
                if (!t.Alive) continue;
                float range = Vector3.Distance(antenna, t.Position);
                float horizon = RadarMath.HorizonM(_profile.AntennaHeightM, t.Position.y);
                if (range > _profile.InstrumentedRangeM || range > horizon) continue;
                output.Add(new Detection
                {
                    TargetId = t.Id, Kind = t.Kind, Identity = t.Identity,
                    Position = t.Position, Velocity = t.Velocity, RangeM = range,
                });
            }
        }
    }

    /// <summary>
    /// Radar L1 (G61–G68): every look at every live target inside instrumented range runs RH Radar Suite's pure core
    /// (<see cref="RadarLook"/>, G61): the 4/3-Earth horizon, the searched volume, the radar equation with one dBsm per
    /// kind (G65), constant-γ sea clutter with γ from the sea state (G67), a Swerling 1 detection draw at a fixed Pfa
    /// (G66), and Barton's range and angle errors on what it detects. The draws come from the run's own generator, in
    /// truth-feed order, so a seed replays every look and the Fix shows it. Identity is copied from truth (G68).
    /// The peak power is not authored: it is solved so that the calibration target is seen with the calibration Pd at
    /// the calibration range (G63).
    /// What it does NOT model (L2+): beam and energy scheduling (every target gets one look per sweep), false alarms,
    /// aspect RCS, Doppler or range-rate measurement, multipath and ducting, and the sea clutter's fall-off below the
    /// critical grazing angle.
    /// </summary>
    public sealed class RadarL1 : ISensorModel
    {
        readonly RadarProfileData _profile;
        readonly RadarL1Data _data;
        readonly RadarTerms _terms;
        readonly MeasurementTerms _measure;
        readonly Coverage _coverage;
        readonly SeededRandom _random;

        public RadarL1(in RadarProfileData profile, DeterministicRng rng)
        {
            RadarL1Data d = profile.L1;
            if (!d.IsSet)
                throw new System.ArgumentException("Radar L1 needs its profile data (RadarProfileData.L1): frequency, bandwidth, beamwidths and the calibration figure", nameof(profile));
            if (!(d.Pfa > 0f && d.Pfa < 1f) || !(d.CalibrationPd > d.Pfa && d.CalibrationPd < 1f))
                throw new System.ArgumentException($"Radar L1 needs 0 < Pfa < calibration Pd < 1 (Pfa {d.Pfa}, Pd {d.CalibrationPd})", nameof(profile));
            _profile = profile;
            _data = d;
            _random = new SeededRandom(rng);
            Watch = new Watch(Mathf.Max(1, profile.SweepPeriodTicks));
            Online = true;

            var terms = new RadarTerms
            {
                PeakPowerW = 1.0,
                GainDbi = d.GainDbi,
                FrequencyHz = d.FrequencyGHz * 1e9,
                BandwidthHz = d.BandwidthMHz * 1e6,
                NoiseFigureDb = d.NoiseFigureDb,
                LossesDb = d.LossesDb,
                PulsesIntegrated = Mathf.Max(1, d.PulsesIntegrated),
                ClutterImprovementDb = d.ClutterImprovementDb,
            };
            RequiredSnrDb = RadarEquation.RequiredSnrSwerling1Db(d.CalibrationPd, d.Pfa);
            terms.PeakPowerW = RadarEquation.PeakPowerForSnrW(terms, d.CalibrationRcsDbsm, d.CalibrationRangeM, RequiredSnrDb);
            _terms = terms;
            _measure = new MeasurementTerms
            {
                AzimuthBeamwidthDeg = d.AzimuthBeamwidthDeg, ElevationBeamwidthDeg = d.ElevationBeamwidthDeg,
                MonopulseSlope = d.MonopulseSlope, RangeFloorM = d.RangeFloorM, AngleFloorDeg = d.AngleFloorDeg,
            };
            _coverage = Coverage.AllRound(d.MinElevationDeg, d.MaxElevationDeg);
        }

        public FidelityLevel Level => FidelityLevel.L1;
        public Watch Watch { get; }
        public bool Online { get; set; }

        /// <summary>The terms the looks use, with the calibrated peak power.</summary>
        public RadarTerms Terms => _terms;
        public MeasurementTerms Measurement => _measure;
        public Coverage Coverage => _coverage;
        public RadarL1Data Data => _data;
        /// <summary>The single-look SNR, dB, the calibration Pd needs at the profile's Pfa (Swerling 1, exact).</summary>
        public double RequiredSnrDb { get; }

        /// <summary>The sea's γ, dB, at this radar's wavelength for <paramref name="sea"/> (G67; <see cref="Clutter.SeaGammaDb"/>).</summary>
        public double SeaGammaDb(in SeaEnvironment sea) =>
            Clutter.SeaGammaDb(Clutter.BeaufortFromWindSpeed(sea.WindSpeedMps), RadarEquation.WavelengthM(_terms.FrequencyHz));

        /// <summary>The antenna as the radar core sees it: x and z in the world, y above the sea.</summary>
        public Plot Antenna(in OwnShipState own) => new Plot(own.Position.x, own.Position.y + _profile.AntennaHeightM, own.Position.z);

        public void Sweep(in OwnShipState own, in SeaEnvironment sea, IReadOnlyList<TruthTarget> world, List<Detection> output)
        {
            Plot antenna = Antenna(own);
            var antennaWorld = new Vector3((float)antenna.X, (float)antenna.Y, (float)antenna.Z);
            SurfaceTerms? clutter = new SurfaceTerms { GammaDb = SeaGammaDb(sea) };
            double instrumentedSq = (double)_profile.InstrumentedRangeM * _profile.InstrumentedRangeM;
            for (int i = 0; i < world.Count; i++)
            {
                TruthTarget t = world[i];
                if (!t.Alive) continue;
                var target = new Plot(t.Position.x, t.Position.y, t.Position.z);
                double dx = target.X - antenna.X, dy = target.Y - antenna.Y, dz = target.Z - antenna.Z;
                if (dx * dx + dy * dy + dz * dz > instrumentedSq) continue;   // beyond instrumented range: no look, no draw

                long draw = _random.Rng.Draws + 1;
                LookResult look = RadarLook.Look(_terms, _measure, _coverage, clutter, _data.Pfa, antenna, target, _data.RcsDbsm(t.Kind), _random);
                if (!look.Detected) continue;
                var plot = new Vector3((float)look.Plot.X, (float)look.Plot.Y, (float)look.Plot.Z);
                output.Add(new Detection
                {
                    TargetId = t.Id, Kind = t.Kind, Identity = t.Identity,
                    Position = plot, Velocity = Vector3.zero, RangeM = Vector3.Distance(antennaWorld, plot),
                    ScnrDb = (float)look.ScnrDb, Chance = new ChanceRoot(_random.Rng.Seed, draw),
                });
            }
        }
    }

    public static class SensorFactory
    {
        public static ISensorModel CreateRadar(FidelityLevel requested, in RadarProfileData profile, DeterministicRng rng, out FidelityResolution resolution)
        {
            resolution = FidelityLadder.Resolve(ElementKind.Radar, requested);
            switch (resolution.Resolved)
            {
                case FidelityLevel.L1: return new RadarL1(profile, rng);
                default: return new RadarL0(profile);
            }
        }
    }
}
