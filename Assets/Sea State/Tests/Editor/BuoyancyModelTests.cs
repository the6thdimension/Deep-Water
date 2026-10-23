using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using DeepWater.Sea.Core;

namespace DeepWater.Sea.Tests
{
    /// <summary>Probe buoyancy math and a 1-D floating-box sanity check.</summary>
    public class BuoyancyModelTests
    {
        const float Rho = 1025f, G = 9.81f;
        static readonly BuoyancyProbe Probe = new BuoyancyProbe { AreaM2 = 2f, HeightM = 1f };
        static readonly BuoyancyDamping NoDamping = default;

        [Test]
        public void DryProbe_HasNoForce()
        {
            var f = BuoyancyModel.ProbeForce(Probe, NoDamping, Rho, G, 0f, new float3(0f, 2f, 0f), float3.zero, float3.zero);
            Assert.AreEqual(float3.zero, f);
        }

        [Test]
        public void FullySubmergedProbe_DisplacesItsVolume()
        {
            var f = BuoyancyModel.ProbeForce(Probe, NoDamping, Rho, G, 0f, new float3(0f, -5f, 0f), float3.zero, float3.zero);
            Assert.AreEqual(Rho * G * 2f * 1f, f.y, 1e-2f);
        }

        [Test]
        public void HalfSubmergedProbe_GivesHalfTheForce()
        {
            var f = BuoyancyModel.ProbeForce(Probe, NoDamping, Rho, G, 0f, new float3(0f, 0f, 0f), float3.zero, float3.zero);
            Assert.AreEqual(Rho * G * 2f * 0.5f, f.y, 1e-2f);
        }

        [Test]
        public void Drag_OpposesMotionRelativeToTheWater()
        {
            var damping = new BuoyancyDamping { LinearMs = 1f, QuadraticCd = 1f };
            var still = BuoyancyModel.ProbeForce(Probe, damping, Rho, G, 0f, float3.zero, new float3(1f, 0f, 0f), new float3(1f, 0f, 0f));
            var moving = BuoyancyModel.ProbeForce(Probe, damping, Rho, G, 0f, float3.zero, new float3(2f, 0f, 0f), new float3(1f, 0f, 0f));
            Assert.AreEqual(0f, still.x, 1e-4f, "moving with the water: no drag");
            Assert.Less(moving.x, 0f, "faster than the water: drag pushes back");
        }

        [Test]
        public void BatchedJob_EqualsPerProbeForcesAtTheirPoints()
        {
            // ΣF and Σ r×F from the batched job must equal applying each probe force at its probe.
            using (var sim = new SeaSimulation(0f, 4))
            {
                sim.Apply(SeaTestFixtures.Douglas5(), default, 0f);
                for (int i = 0; i < 77; i++) sim.Step(0.02);

                var pose = new BuoyancyBodyPose
                {
                    Position = new float3(3f, -0.4f, 7f),
                    Rotation = quaternion.EulerXYZ(0.05f, 0.7f, -0.08f),
                    LinearVelocity = new float3(0.5f, 0.2f, -0.1f),
                    AngularVelocity = new float3(0.01f, 0.02f, -0.03f),
                    WorldCenterOfMass = new float3(3f, -0.6f, 7.1f),
                };
                var damping = new BuoyancyDamping { LinearMs = 1.2f, QuadraticCd = 0.5f };

                const int n = 12;
                var probes = new NativeArray<BuoyancyProbe>(n, Allocator.TempJob);
                var probeBody = new NativeArray<int>(n, Allocator.TempJob);
                var poses = new NativeArray<BuoyancyBodyPose>(1, Allocator.TempJob);
                var dampings = new NativeArray<BuoyancyDamping>(1, Allocator.TempJob);
                var probeOut = new NativeArray<BuoyancyProbeOutput>(n, Allocator.TempJob);
                var ranges = new NativeArray<int2>(1, Allocator.TempJob);
                var bodyOut = new NativeArray<BuoyancyBodyOutput>(1, Allocator.TempJob);
                try
                {
                    for (int i = 0; i < n; i++)
                        probes[i] = new BuoyancyProbe { LocalPosition = new float3((i % 3 - 1) * 1.5f, 0f, (i / 3 - 1.5f) * 3f), AreaM2 = 4f, HeightM = 2f };
                    poses[0] = pose;
                    dampings[0] = damping;
                    ranges[0] = new int2(0, n);

                    new BuoyancyProbeJob
                    {
                        Groups = sim.PhysicsGroups, GroupCount = sim.PhysicsGroupCount, SeaLevel = 0f, InverseIterations = 4,
                        WaterDensity = Rho, Gravity = G, Probes = probes, ProbeBody = probeBody, Poses = poses,
                        Damping = dampings, Output = probeOut,
                    }.Run(n);
                    new BuoyancySumJob { BodyProbeRange = ranges, Probes = probeOut, Output = bodyOut }.Run(1);

                    float3 f = float3.zero, t = float3.zero;
                    for (int i = 0; i < n; i++)
                    {
                        float3 world = pose.Position + math.mul(pose.Rotation, probes[i].LocalPosition);
                        SeaSample s = sim.Sample(world);
                        float3 arm = world - pose.WorldCenterOfMass;
                        float3 pv = pose.LinearVelocity + math.cross(pose.AngularVelocity, arm);
                        float3 fi = BuoyancyModel.ProbeForce(probes[i], damping, Rho, G, s.SurfaceHeight, world, pv, s.Velocity);
                        f += fi;
                        t += math.cross(arm, fi);
                    }

                    Assert.Greater(math.length(f), 1f, "test must actually float something");
                    Assert.Less(math.length(bodyOut[0].Force - f), 1e-4f * math.length(f), $"force {bodyOut[0].Force} vs {f}");
                    Assert.Less(math.length(bodyOut[0].Torque - t), 1e-4f * math.max(math.length(t), 1f), $"torque {bodyOut[0].Torque} vs {t}");
                }
                finally
                {
                    bodyOut.Dispose(); ranges.Dispose(); probeOut.Dispose(); dampings.Dispose();
                    poses.Dispose(); probeBody.Dispose(); probes.Dispose();
                }
            }
        }

        [Test]
        public void FloatingBox_SettlesAtArchimedesDraft()
        {
            // A 1 m-tall column of 2 m² with mass for a 0.4 m draft, heave only, damped.
            float mass = Rho * Probe.AreaM2 * 0.4f;
            var damping = new BuoyancyDamping { LinearMs = 2f * 0.3f * math.sqrt(G * 0.4f), QuadraticCd = 0f };
            float y = 1f, v = 0f;
            const float dt = 0.02f;
            for (int i = 0; i < 3000; i++)
            {
                float3 f = BuoyancyModel.ProbeForce(Probe, damping, Rho, G, 0f, new float3(0f, y, 0f), new float3(0f, v, 0f), float3.zero);
                v += (f.y / mass - G) * dt;
                y += v * dt;
            }
            float draft = 0.5f - y;   // column bottom is 0.5 m below its centre
            Assert.AreEqual(0.4f, draft, 0.01f);
        }
    }
}
