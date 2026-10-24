using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using DeepWater.Sea.Core;
using DeepWater.Sea.ScriptableObjects;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DeepWater.Sea.Tests
{
    /// <summary>Probe buoyancy math, the batched job, and hydrostatic stability of a heeled box.</summary>
    public class BuoyancyModelTests
    {
        const float Rho = 1025f, G = 9.81f;
        static readonly BuoyancyProbe Probe = new BuoyancyProbe { AreaM2 = 2f, HeightM = 1f };
        static readonly BuoyancyDamping NoDamping = default;

        [Test]
        public void DryProbe_HasNoForce()
        {
            var f = BuoyancyModel.ProbeForce(Probe, NoDamping, Rho, G, 0f, new float3(0f, 2f, 0f), float3.zero, float3.zero, quaternion.identity);
            Assert.AreEqual(float3.zero, f);
        }

        [Test]
        public void FullySubmergedProbe_DisplacesItsVolume()
        {
            var f = BuoyancyModel.ProbeForce(Probe, NoDamping, Rho, G, 0f, new float3(0f, -5f, 0f), float3.zero, float3.zero, quaternion.identity);
            Assert.AreEqual(Rho * G * 2f * 1f, f.y, 1e-2f);
        }

        [Test]
        public void HalfSubmergedProbe_GivesHalfTheForce()
        {
            var f = BuoyancyModel.ProbeForce(Probe, NoDamping, Rho, G, 0f, new float3(0f, 0f, 0f), float3.zero, float3.zero, quaternion.identity);
            Assert.AreEqual(Rho * G * 2f * 0.5f, f.y, 1e-2f);
        }

        [Test]
        public void SubmergedCentroid_IsHalfwayUpTheWetPart()
        {
            // A 2 m column centred at y = 0 with the surface at +0.5: 1.5 m wet, from -1 to +0.5.
            float submerged = BuoyancyModel.SubmergedHeight(0.5f, 0f, 2f);
            float3 c = BuoyancyModel.SubmergedCentroid(float3.zero, new float3(0f, 1f, 0f), submerged, 2f);
            Assert.AreEqual(1.5f, submerged, 1e-6f);
            Assert.AreEqual(-0.25f, c.y, 1e-6f);

            // Tilted column: the point moves along the column's own axis.
            float3 up = math.mul(quaternion.RotateZ(0.3f), new float3(0f, 1f, 0f));
            float3 t = BuoyancyModel.SubmergedCentroid(float3.zero, up, submerged, 2f);
            Assert.Less(math.length(t - up * -0.25f), 1e-6f);

            // Upside down: the body's up points down, but the wet part still starts at the lower end.
            float3 inverted = BuoyancyModel.SubmergedCentroid(float3.zero, new float3(0f, -1f, 0f), submerged, 2f);
            Assert.AreEqual(-0.25f, inverted.y, 1e-6f);
        }

        [Test]
        public void Drag_OpposesMotionRelativeToTheWater()
        {
            var damping = new BuoyancyDamping { LinearMs = 1f, QuadraticCd = 1f };
            var still = BuoyancyModel.ProbeForce(Probe, damping, Rho, G, 0f, float3.zero, new float3(1f, 0f, 0f), new float3(1f, 0f, 0f), quaternion.identity);
            var moving = BuoyancyModel.ProbeForce(Probe, damping, Rho, G, 0f, float3.zero, new float3(2f, 0f, 0f), new float3(1f, 0f, 0f), quaternion.identity);
            Assert.AreEqual(0f, still.x, 1e-4f, "moving with the water: no drag");
            Assert.Less(moving.x, 0f, "faster than the water: drag pushes back");
        }

        [Test]
        public void PerAxisDrag_ActsInTheHullFrame()
        {
            // A ship with its own manoeuvring model keeps heave drag only (x sway, z surge off). Yawed 30°
            // and rolled 10°, motion along the hull's forward axis meets no probe drag; motion along its
            // up axis meets the full drag, along that axis.
            var damping = new BuoyancyDamping { LinearMs = 1f, QuadraticCd = 0.5f, PerAxis = true, AxisScale = new float3(0f, 1f, 0f) };
            quaternion rot = math.mul(quaternion.RotateY(math.radians(30f)), quaternion.RotateZ(math.radians(10f)));
            float3 forward = math.mul(rot, new float3(0f, 0f, 1f));
            float3 up = math.mul(rot, new float3(0f, 1f, 0f));
            float3 deep = new float3(0f, -5f, 0f);   // fully submerged, so buoyancy is a known vertical term
            float3 buoyancy = new float3(0f, Rho * G * Probe.AreaM2 * Probe.HeightM, 0f);

            float3 surging = BuoyancyModel.ProbeForce(Probe, damping, Rho, G, 0f, deep, 3f * forward, float3.zero, rot);
            float3 heaving = BuoyancyModel.ProbeForce(Probe, damping, Rho, G, 0f, deep, 3f * up, float3.zero, rot);
            var isotropic = new BuoyancyDamping { LinearMs = 1f, QuadraticCd = 0.5f };
            float3 heavingIso = BuoyancyModel.ProbeForce(Probe, isotropic, Rho, G, 0f, deep, 3f * up, float3.zero, rot);

            Assert.Less(math.length(surging - buoyancy), 1e-3f, "surge along the hull meets no probe drag");
            Assert.Less(math.length((heaving - buoyancy) - (heavingIso - buoyancy)), 1e-3f, "heave drag is unchanged");
            Assert.Greater(math.length(heaving - buoyancy), 1f, "and it is not zero");
        }

        [TestCase(false)]
        [TestCase(true)]   // per-axis drag in the hull frame goes through the same path
        public void BatchedJob_EqualsPerProbeForcesAtTheirPoints(bool perAxis)
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
                var damping = new BuoyancyDamping { LinearMs = 1.2f, QuadraticCd = 0.5f, PerAxis = perAxis, AxisScale = new float3(0.3f, 1f, 0f) };

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
                    float3 up = math.mul(pose.Rotation, new float3(0f, 1f, 0f));
                    for (int i = 0; i < n; i++)
                    {
                        float3 world = pose.Position + math.mul(pose.Rotation, probes[i].LocalPosition);
                        SeaSample s = sim.Sample(world);
                        float submerged = BuoyancyModel.SubmergedHeight(s.SurfaceHeight, world.y, probes[i].HeightM);
                        float3 arm = BuoyancyModel.SubmergedCentroid(world, up, submerged, probes[i].HeightM) - pose.WorldCenterOfMass;
                        float3 pv = pose.LinearVelocity + math.cross(pose.AngularVelocity, arm);
                        float3 fi = BuoyancyModel.ProbeForce(probes[i], damping, Rho, G, s.SurfaceHeight, world, pv, s.Velocity, pose.Rotation);
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

        // ── Hydrostatic stability of a heeled box ─────────────────────────────
        // The demo's destroyer-length box: 154 × 20 × 12 m, design draft 6.3 m, centre of mass
        // at the box centre unless offset. Box theory: KB = d/2, BM = B²/(12 d), KG = H/2 + offset,
        // GM = KB + BM − KG = 2.44 m. With n columns across, the probe waterplane's Σx² is
        // B² (n² − 1) / (12 n²), so 20 columns keep BM within 0.25 % of the continuous box.

        const float BoxL = 154f, BoxB = 20f, BoxH = 12f, BoxD = 6.3f;
        const int BoxAcross = 20, BoxAlong = 24;

        static float BoxMass => Rho * BoxL * BoxB * BoxD;

        /// <summary>GM of the probe box: KB + BM (of its 20-column waterplane) − KG.</summary>
        static float BoxMetacentricHeight(float comOffsetY) => MetacentricHeight(BoxB, BoxH, BoxD, BoxAcross, comOffsetY);

        /// <summary>
        /// GM of a box on <paramref name="across"/> probe columns: KB + BM − KG, with BM from the
        /// probe waterplane (Σx² = B² (n² − 1) / (12 n²)) and KG = H/2 + the centre-of-mass offset.
        /// </summary>
        static float MetacentricHeight(float beam, float height, float draft, int across, float comOffsetY) =>
            0.5f * draft + beam * beam / (12f * draft) * (across * across - 1f) / (across * across)
            - (0.5f * height + comOffsetY);

        [TestCase(0f)]
        [TestCase(-1.5f)]   // ballasted: centre of mass 1.5 m lower, so GM 1.5 m larger
        public void HeeledBox_RightingMoment_MatchesWallSidedTheory(float comOffsetY)
        {
            // Wall-sided formula: GZ = sin φ (GM + ½ BM tan² φ). Applying the force at the column
            // centre instead of the submerged centroid makes GM = BM here (5.29 m instead of
            // 2.44 m), so the 2 % band catches a return of that defect.
            const float heel = 5f * math.PI / 180f;
            using (var sim = new SeaSimulation(0f, 4))
            {
                sim.Apply(SeaTestFixtures.Calm(), default, 0f);
                var probes = BoxProbes(BoxL, BoxB, BoxH, BoxAcross, BoxAlong);
                try
                {
                    BuoyancyBodyOutput load = BodyLoad(sim, probes, HeeledBoxPose(heel, 0f, comOffsetY), NoDamping);
                    float bm = BoxB * BoxB / (12f * BoxD);
                    float gm = BoxMetacentricHeight(comOffsetY);
                    float expected = BoxMass * G * math.sin(heel) * (gm + 0.5f * bm * math.tan(heel) * math.tan(heel));
                    float righting = -load.Torque.z;   // the heel lifts starboard (+x); the sea pushes it back
                    Debug.Log($"[Buoyancy] 154 m box at 5°, KG offset {comOffsetY:0.0} m: righting {righting / 1e6f:0.0} MN·m, " +
                              $"wall-sided theory {expected / 1e6f:0.0} MN·m (GM {gm:0.00} m)");
                    Assert.AreEqual(BoxMass * G, load.Force.y, 1e-3f * BoxMass * G, "the box should float at its design draft");
                    Assert.AreEqual(expected, righting, 0.02f * expected);
                }
                finally { probes.Dispose(); }
            }
        }

        [Test]
        public void InvertedBox_RightsLikeAnUprightBox()
        {
            // With its centre of mass at the box centre, a box turned 180° about its length is the
            // same shape in the water, so 185° must load it exactly like 5°. A centroid taken along
            // the body's up without flipping it puts the force above the wet part and makes the
            // inverted box spuriously stiff (seen in the demo: the 2 m buoy floated stable upside down).
            const float heel = 5f * math.PI / 180f;
            using (var sim = new SeaSimulation(0f, 4))
            {
                sim.Apply(SeaTestFixtures.Calm(), default, 0f);
                var probes = BoxProbes(BoxL, BoxB, BoxH, BoxAcross, BoxAlong);
                try
                {
                    float upright = BodyLoad(sim, probes, HeeledBoxPose(heel, 0f, 0f), NoDamping).Torque.z;
                    float inverted = BodyLoad(sim, probes, HeeledBoxPose(math.PI + heel, 0f, 0f), NoDamping).Torque.z;
                    Assert.AreEqual(upright, inverted, 1e-3f * math.abs(upright));
                }
                finally { probes.Dispose(); }
            }
        }

        [Test]
        public void CubeBuoy_UniformTipsOver_BallastedRightsItself()
        {
            // The demo's 2 m buoy: a 2 m cube at 0.8 m draft on 2 × 2 probes. Uniform, its centre
            // of mass sits 1.0 m above the keel and GM = 0.40 + 0.31 − 1.00 < 0: heeled 5° it is
            // pushed further over. Ballasted 0.7 m lower (the demo's value), GM = 0.41 m.
            const float size = 2f, draft = 0.8f, ballast = -0.7f;
            const int across = 2;
            const float heel = 5f * math.PI / 180f;
            float mass = Rho * size * size * draft;
            using (var sim = new SeaSimulation(0f, 4))
            {
                sim.Apply(SeaTestFixtures.Calm(), default, 0f);
                var probes = BoxProbes(size, size, size, across, across);
                try
                {
                    float uniform = -BodyLoad(sim, probes, HeeledPose(size, draft, heel, 0f, 0f), NoDamping).Torque.z;
                    float ballasted = -BodyLoad(sim, probes, HeeledPose(size, draft, heel, 0f, ballast), NoDamping).Torque.z;
                    float gm = MetacentricHeight(size, size, draft, across, ballast);
                    float expected = mass * G * math.sin(heel) * gm;
                    Debug.Log($"[Buoyancy] 2 m cube buoy at 5°: uniform {uniform:0} N·m (GM {MetacentricHeight(size, size, draft, across, 0f):0.00} m), " +
                              $"ballasted {ballasted:0} N·m vs m g GM sin φ {expected:0} (GM {gm:0.00} m)");
                    Assert.Less(uniform, 0f, "a uniform cube this light should be unstable upright");
                    Assert.AreEqual(expected, ballasted, 0.02f * expected);
                }
                finally { probes.Dispose(); }
            }
        }

        [Test]
        public void HullAsset_AppliesItsCentreOfMass_OnlyWhenItOverridesIt()
        {
            var hull = ScriptableObject.CreateInstance<BuoyancyHullSO>();
            var go = new GameObject("hull under test");
            try
            {
                go.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 2f);
                var body = go.AddComponent<Rigidbody>();
                hull.massKg = 3280f;

                // No override: a centre of mass set by hand on the Rigidbody survives.
                body.centerOfMass = new Vector3(0f, 0.25f, 0f);
                hull.ApplyMassProperties(body);
                Assert.AreEqual(3280f, body.mass, 1e-3f);
                Assert.AreEqual(0.25f, body.centerOfMass.y, 1e-6f);

                hull.overrideCenterOfMass = true;
                hull.centerOfMassM = new Vector3(0f, -0.7f, 0f);
                hull.ApplyMassProperties(body);
                Assert.AreEqual(-0.7f, body.centerOfMass.y, 1e-6f);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(hull);
            }
        }

        [Test]
        public void HeeledBox_RollPeriod_MatchesBoxTheory()
        {
            // Free roll from 5°, undamped, roll only: I φ'' = τ(φ), τ from the batched jobs.
            // Box theory: T = 2π √(I / (m g GM)), I = m (B² + H²) / 12 (Sea State has no added inertia).
            const float dt = 0.02f;
            float inertia = BoxMass * (BoxB * BoxB + BoxH * BoxH) / 12f;
            double theory = 2.0 * math.PI * math.sqrt(inertia / (BoxMass * G * BoxMetacentricHeight(0f)));
            float c = 0.373f + 0.023f * (BoxB / BoxD) - 0.043f * (BoxL / 100f);
            double imo = 2f * c * BoxB / math.sqrt(BoxMetacentricHeight(0f));

            using (var sim = new SeaSimulation(0f, 4))
            {
                sim.Apply(SeaTestFixtures.Calm(), default, 0f);
                var probes = BoxProbes(BoxL, BoxB, BoxH, BoxAcross, BoxAlong);
                try
                {
                    float phi = 5f * math.PI / 180f, omega = 0f;
                    var crossings = new List<double>();
                    for (int step = 1; step <= 2000; step++)   // 40 s
                    {
                        float torque = BodyLoad(sim, probes, HeeledBoxPose(phi, omega, 0f), NoDamping).Torque.z;
                        omega += torque / inertia * dt;
                        float next = phi + omega * dt;
                        if (phi < 0f && next >= 0f) crossings.Add((step - 1 + phi / (phi - next)) * dt);
                        phi = next;
                    }
                    Assert.GreaterOrEqual(crossings.Count, 3, "too few roll cycles to measure");
                    double measured = (crossings[crossings.Count - 1] - crossings[0]) / (crossings.Count - 1);
                    Debug.Log($"[Buoyancy] 154 m box free roll: {measured:0.00} s over {crossings.Count - 1} cycles; " +
                              $"box theory {theory:0.00} s; IMO (real ships, with added inertia) {imo:0.00} s");
                    Assert.AreEqual(theory, measured, 0.02 * theory);
                }
                finally { probes.Dispose(); }
            }
        }

        /// <summary>A box hull's probes in BuoyancyHullSO.FillBoxGrid's layout: full-height columns at y = 0.</summary>
        static NativeArray<BuoyancyProbe> BoxProbes(float length, float beam, float height, int nx, int nz)
        {
            var probes = new NativeArray<BuoyancyProbe>(nx * nz, Allocator.Persistent);
            float area = length * beam / (nx * nz);
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
                probes[iz * nx + ix] = new BuoyancyProbe
                {
                    LocalPosition = new float3(((ix + 0.5f) / nx - 0.5f) * beam, 0f, ((iz + 0.5f) / nz - 0.5f) * length),
                    AreaM2 = area,
                    HeightM = height,
                };
            return probes;
        }

        /// <summary>The 154 m box at its design draft, heeled by <paramref name="heel"/> about its length (starboard up).</summary>
        static BuoyancyBodyPose HeeledBoxPose(float heel, float rollRate, float comOffsetY) =>
            HeeledPose(BoxH, BoxD, heel, rollRate, comOffsetY);

        /// <summary>A box of the given height at its design draft, heeled about its length (starboard up).</summary>
        static BuoyancyBodyPose HeeledPose(float height, float draft, float heel, float rollRate, float comOffsetY)
        {
            var position = new float3(0f, 0.5f * height - draft, 0f);
            quaternion rotation = quaternion.RotateZ(heel);
            return new BuoyancyBodyPose
            {
                Position = position,
                Rotation = rotation,
                AngularVelocity = new float3(0f, 0f, rollRate),
                WorldCenterOfMass = position + math.mul(rotation, new float3(0f, comOffsetY, 0f)),
            };
        }

        /// <summary>One body's summed load from the batched jobs (the path BuoyancySystem runs).</summary>
        static BuoyancyBodyOutput BodyLoad(SeaSimulation sim, NativeArray<BuoyancyProbe> probes, BuoyancyBodyPose pose, BuoyancyDamping damping)
        {
            int n = probes.Length;
            var probeBody = new NativeArray<int>(n, Allocator.TempJob);
            var poses = new NativeArray<BuoyancyBodyPose>(1, Allocator.TempJob);
            var dampings = new NativeArray<BuoyancyDamping>(1, Allocator.TempJob);
            var probeOut = new NativeArray<BuoyancyProbeOutput>(n, Allocator.TempJob);
            var ranges = new NativeArray<int2>(1, Allocator.TempJob);
            var bodyOut = new NativeArray<BuoyancyBodyOutput>(1, Allocator.TempJob);
            try
            {
                poses[0] = pose;
                dampings[0] = damping;
                ranges[0] = new int2(0, n);
                new BuoyancyProbeJob
                {
                    Groups = sim.PhysicsGroups, GroupCount = sim.PhysicsGroupCount, SeaLevel = sim.SeaLevel,
                    InverseIterations = sim.InverseIterations, WaterDensity = Rho, Gravity = G,
                    Probes = probes, ProbeBody = probeBody, Poses = poses, Damping = dampings, Output = probeOut,
                }.Run(n);
                new BuoyancySumJob { BodyProbeRange = ranges, Probes = probeOut, Output = bodyOut }.Run(1);
                return bodyOut[0];
            }
            finally
            {
                bodyOut.Dispose(); ranges.Dispose(); probeOut.Dispose(); dampings.Dispose();
                poses.Dispose(); probeBody.Dispose();
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
                float3 f = BuoyancyModel.ProbeForce(Probe, damping, Rho, G, 0f, new float3(0f, y, 0f), new float3(0f, v, 0f), float3.zero, quaternion.identity);
                v += (f.y / mass - G) * dt;
                y += v * dt;
            }
            float draft = 0.5f - y;   // column bottom is 0.5 m below its centre
            Assert.AreEqual(0.4f, draft, 0.01f);
        }
    }
}
