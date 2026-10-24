using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeepWater.Sea.Core
{
    /// <summary>Rigid-body pose and velocities captured at the start of a fixed step.</summary>
    public struct BuoyancyBodyPose
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 LinearVelocity;
        /// <summary>World-space angular velocity, rad/s.</summary>
        public float3 AngularVelocity;
        public float3 WorldCenterOfMass;
    }

    /// <summary>One probe's contribution: force, torque about the body's centre of mass, volumes.</summary>
    public struct BuoyancyProbeOutput
    {
        public float3 Force;
        public float3 Torque;
        public float SubmergedVolume;
        public float Volume;
    }

    /// <summary>One body's summed hydrodynamic load for the step.</summary>
    public struct BuoyancyBodyOutput
    {
        public float3 Force;
        public float3 Torque;
        public float SubmergedFraction;
    }

    /// <summary>
    /// Every probe of every floating body in one parallel job: find the water surface above
    /// the probe (packed SIMD kernel), then buoyancy + drag against the water particle
    /// velocity, then the torque about the body's centre of mass. Each force acts at the
    /// centroid of its column's submerged part (<see cref="BuoyancyModel.SubmergedCentroid"/>);
    /// applying ΣF and Σ r×F per body is physically identical to applying each F there.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
    public struct BuoyancyProbeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SeaComponentGroup> Groups;
        public int GroupCount;
        public float SeaLevel;
        public int InverseIterations;
        public float WaterDensity;
        public float Gravity;

        [ReadOnly] public NativeArray<BuoyancyProbe> Probes;
        [ReadOnly] public NativeArray<int> ProbeBody;
        [ReadOnly] public NativeArray<BuoyancyBodyPose> Poses;
        [ReadOnly] public NativeArray<BuoyancyDamping> Damping;

        [WriteOnly] public NativeArray<BuoyancyProbeOutput> Output;

        public void Execute(int i)
        {
            int body = ProbeBody[i];
            BuoyancyBodyPose pose = Poses[body];
            BuoyancyProbe probe = Probes[i];

            float3 world = pose.Position + math.mul(pose.Rotation, probe.LocalPosition);
            float2 x0 = SeaFieldPacked.InverseHorizontal(Groups, GroupCount, world.xz, InverseIterations);
            SeaFieldPacked.Evaluate(Groups, GroupCount, x0, out float height, out float3 waterVelocity, out _, out _);
            float surfaceY = SeaLevel + height;

            float submerged = BuoyancyModel.SubmergedHeight(surfaceY, world.y, probe.HeightM);
            float3 up = math.mul(pose.Rotation, new float3(0f, 1f, 0f));
            float3 apply = BuoyancyModel.SubmergedCentroid(world, up, submerged, probe.HeightM);

            float3 arm = apply - pose.WorldCenterOfMass;
            float3 pointVelocity = pose.LinearVelocity + math.cross(pose.AngularVelocity, arm);
            float3 force = BuoyancyModel.ProbeForce(probe, Damping[body], WaterDensity, Gravity,
                surfaceY, world, pointVelocity, waterVelocity, pose.Rotation);

            Output[i] = new BuoyancyProbeOutput
            {
                Force = force,
                Torque = math.cross(arm, force),
                SubmergedVolume = probe.AreaM2 * submerged,
                Volume = probe.AreaM2 * probe.HeightM,
            };
        }
    }

    /// <summary>
    /// Sums each body's probes in probe order (a fixed order, so the sum is deterministic).
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
    public struct BuoyancySumJob : IJobParallelFor
    {
        /// <summary>x = first probe index, y = probe count, per body.</summary>
        [ReadOnly] public NativeArray<int2> BodyProbeRange;
        [ReadOnly] public NativeArray<BuoyancyProbeOutput> Probes;
        [WriteOnly] public NativeArray<BuoyancyBodyOutput> Output;

        public void Execute(int body)
        {
            int2 range = BodyProbeRange[body];
            float3 force = float3.zero, torque = float3.zero;
            float submerged = 0f, volume = 0f;
            for (int i = range.x; i < range.x + range.y; i++)
            {
                BuoyancyProbeOutput p = Probes[i];
                force += p.Force;
                torque += p.Torque;
                submerged += p.SubmergedVolume;
                volume += p.Volume;
            }
            Output[body] = new BuoyancyBodyOutput
            {
                Force = force,
                Torque = torque,
                SubmergedFraction = volume > 0f ? submerged / volume : 0f,
            };
        }
    }
}
