using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Simulation;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core.Cic
{
    /// <summary>Radar tuning (P1); baked from the loadout asset.</summary>
    public struct RadarProfileData
    {
        /// <summary>Instrumented range, m: the farthest the radar reports anything.</summary>
        public float InstrumentedRangeM;
        /// <summary>Antenna height above the waterline, m: sets the radar horizon.</summary>
        public float AntennaHeightM;
        /// <summary>Ticks between sweeps (the radar's Watch).</summary>
        public int SweepPeriodTicks;
    }

    /// <summary>One sensor report of one target on one sweep.</summary>
    public struct Detection
    {
        public int TargetId;
        public TargetKind Kind;
        public Identity Identity;
        public Vector3 Position;
        public Vector3 Velocity;
        public float RangeM;
    }

    /// <summary>A sensor element. Each level is one implementation (BLUEPRINT §4).</summary>
    public interface ISensorModel
    {
        FidelityLevel Level { get; }
        Watch Watch { get; }
        bool Online { get; set; }

        /// <summary>Report what the sensor sees this sweep. Called only on ticks its Watch is due.</summary>
        void Sweep(in OwnShipState own, IReadOnlyList<TruthTarget> world, List<Detection> output);
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

        public void Sweep(in OwnShipState own, IReadOnlyList<TruthTarget> world, List<Detection> output)
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

    public static class SensorFactory
    {
        public static ISensorModel CreateRadar(FidelityLevel requested, in RadarProfileData profile, out FidelityResolution resolution)
        {
            resolution = FidelityLadder.Resolve(ElementKind.Radar, requested);
            switch (resolution.Resolved)
            {
                default: return new RadarL0(profile);
            }
        }
    }
}
