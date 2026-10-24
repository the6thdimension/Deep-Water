using System;
using Unity.Collections;
using Unity.Mathematics;
using DeepWater.Sea.Core;

namespace DeepWater.Reckoning.Spike
{
    public enum SpikeVariant { A, B }

    /// <summary>A ship to place at the start: a box hull, floated at its design draft.</summary>
    public struct SpikeSpawn
    {
        public float LengthM, BeamM, HeightM, DraftM;
        public int ProbesAcross, ProbesAlong;
        public float2 PositionXZ;
        public float HeadingDeg;
        /// <summary>Initial heel, degrees (positive = starboard down).</summary>
        public float HeelDeg;
        public float3 Velocity;
    }

    /// <summary>
    /// One spike world on one clock, in a fixed order per tick: sea → sea loads → ships → Fix.
    /// </summary>
    public sealed class SpikeRunner : IDisposable
    {
        public const float Dt = 0.02f, WaterDensity = 1025f, Gravity = 9.81f;

        public readonly SeaSimulation Sea;
        public readonly ISpikeWorld World;
        public readonly NativeArray<SpikeHull> Hulls;
        public ulong RunningFix { get; private set; } = 14695981039346656037UL;
        public long Ticks { get; private set; }
        /// <summary>Apply probe forces at the column centre, as Sea State does today (comparison only).</summary>
        public bool SeaStateApplicationPoint { get; set; }

        readonly NativeArray<BuoyancyProbe> _probes;
        readonly SpikeSeaLoads _loads;

        public SpikeRunner(SpikeVariant variant, in SeaBakeInput sea, SpikeSpawn[] spawns)
        {
            Sea = new SeaSimulation(0f, 4);
            Sea.Apply(sea, default, 0f);

            var probes = new NativeList<BuoyancyProbe>(Allocator.Temp);
            Hulls = new NativeArray<SpikeHull>(spawns.Length, Allocator.Persistent);
            var initial = new NativeArray<ShipState>(spawns.Length, Allocator.Temp);
            for (int i = 0; i < spawns.Length; i++)
            {
                SpikeSpawn s = spawns[i];
                SpikeHull hull = SpikeHull.Box(s.LengthM, s.BeamM, s.HeightM, s.DraftM, s.ProbesAcross, s.ProbesAlong,
                    WaterDensity, Gravity, probes);
                Hulls[i] = hull;
                // Heading: yaw about +Y (0 = bow toward +Z). Heel: roll about the bow axis.
                quaternion rot = math.mul(quaternion.RotateY(math.radians(s.HeadingDeg)),
                                          quaternion.RotateZ(-math.radians(s.HeelDeg)));
                initial[i] = new ShipState
                {
                    Position = new float3(s.PositionXZ.x, hull.RestHeight, s.PositionXZ.y),
                    Rotation = rot,
                    LinearVelocity = s.Velocity,
                };
            }
            _probes = new NativeArray<BuoyancyProbe>(probes.AsArray(), Allocator.Persistent);
            probes.Dispose();
            _loads = new SpikeSeaLoads(Hulls, _probes);
            World = variant == SpikeVariant.A
                ? new SpikeWorldA(Hulls, initial, Dt, Gravity)
                : (ISpikeWorld)new SpikeWorldB(Hulls, initial);
            initial.Dispose();
        }

        public void Step()
        {
            Sea.Step(Dt);
            _loads.Compute(Sea, World.Ships, WaterDensity, Gravity, SeaStateApplicationPoint);
            World.Step(_loads, Dt, Gravity);
            RunningFix = Fix.Of(World.Ships, RunningFix);
            Ticks++;
        }

        /// <summary>Heel of ship <paramref name="i"/> about its bow axis, degrees (starboard down positive).</summary>
        public float HeelDeg(int i)
        {
            float3 starboard = math.mul(World.Ships[i].Rotation, new float3(1f, 0f, 0f));
            return -math.degrees(math.asin(math.clamp(starboard.y, -1f, 1f)));
        }

        public void Dispose()
        {
            World.Dispose();
            _loads.Dispose();
            if (_probes.IsCreated) _probes.Dispose();
            if (Hulls.IsCreated) Hulls.Dispose();
            Sea.Dispose();
        }
    }
}
