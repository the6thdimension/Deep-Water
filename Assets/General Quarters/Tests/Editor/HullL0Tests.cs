using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using DeepWater.GeneralQuarters.Editor;
using DeepWater.GeneralQuarters.Modules.Hull;
using DeepWater.GeneralQuarters.ScriptableObjects;
using DeepWater.Sea.Core;
using DeepWater.Sea.Modules.Buoyancy;
using DeepWater.Sea.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>
    /// Slice 3: the DDG-51 hull from Blender. Hydrostatics against public figures, the Sea State force model
    /// against the hull's own numbers (the hull-stability-check ladder, step 2), and the assembled prefab.
    /// The hull's numbers are measured on a fine column grid (converged); Sea State floats a coarser one, so the
    /// force-model tests check the probe grid's fidelity as well as frame, centroid and centre of mass.
    /// </summary>
    public class HullL0Tests
    {
        const float Rho = 1025f, G = 9.81f;
        const float Heel = 5f * math.PI / 180f;

        static HullLayout Layout() => HullLayout.Parse(File.ReadAllText(Ddg51HullBuilder.LayoutJson));

        [Test]
        public void Layout_HydrostaticsSitInPublicRanges()
        {
            // Public figures: LOA 153.9 m (505 ft), waterline length about 142 m (466 ft), navigational
            // draft about 9.4 m (31 ft, at the sonar dome), full-load displacement about 8,300 t (Flight I)
            // to 9,200 t (Flight IIA). The model is a Flight I/II (no hangar, Harpoon quads aft).
            HullLayout.Hydrostatics h = Layout().hydrostatics;
            Debug.Log($"[GQ] DDG-51 layout: {h.displacement_t:0} t, LWL {h.length_wl_m:0.0} m, beam WL {h.beam_wl_m:0.0} m, " +
                      $"keel draft {h.draft_m:0.00} m, sonar dome {-h.deepest_y:0.00} m, KB {h.kb_m:0.00}, BM {h.bm_m:0.00}, " +
                      $"KG {h.kg_m:0.00}, GM {h.gm_m:0.00} m");
            Assert.That(h.displacement_t, Is.InRange(8000f, 9700f), "displacement, t");
            Assert.That(h.length_wl_m, Is.InRange(136f, 146f), "waterline length, m");
            Assert.That(-h.deepest_y, Is.InRange(9.0f, 9.8f), "navigational draft (sonar dome), m");
            Assert.AreEqual(h.kb_m + h.bm_m - h.gm_m, h.kg_m, 0.01f, "KG = KB + BM − GM");
            Assert.AreEqual(h.mass_kg, h.displacement_t * 1000f, 0.1f * 1000f, "mass is the displacement");
        }

        [Test]
        public void Probes_FloatTheShipLevelAtItsDrawnWaterline()
        {
            HullLayout layout = Layout();
            HullLayout.Hydrostatics h = layout.hydrostatics;
            using (var sim = CalmSea())
            using (var probes = Probes(layout))
            {
                BuoyancyBodyOutput load = BodyLoad(sim, probes, HeeledPose(h, 0f, 0f));
                float weight = h.mass_kg * G;
                // Levers: how far the centre of buoyancy sits from the centre of mass, m. The probe grid's centre of
                // buoyancy is not exactly the hull's, so judge the fore-and-aft lever by the trim it causes:
                // angle = lever / GM_L, with GM_L = KB + BM_L − KG and BM_L from the probes' wet waterplane.
                float trimLever = load.Torque.x / weight, heelLever = load.Torque.z / weight;
                float gmL = LongitudinalBm(layout) + h.kb_m - h.kg_m;
                float trimM = trimLever / gmL * h.length_wl_m;
                Debug.Log($"[GQ] DDG-51 upright: buoyancy {load.Force.y / weight:0.0000} × weight, " +
                          $"trim lever {trimLever * 100f:0.0} cm (GM_L {gmL:0} m: {trimM * 100f:0.0} cm of trim over the waterline), " +
                          $"heel lever {heelLever * 100f:0.00} cm");
                Assert.AreEqual(weight, load.Force.y, 0.01f * weight, "floats at the drawn waterline");
                Assert.Less(math.abs(trimM), 0.25f, "trims under 25 cm over the waterline length");
                Assert.Less(math.abs(heelLever), 0.01f, "symmetric about the centreline");
            }
        }

        [Test]
        public void Heeled5Degrees_RightingMomentGivesTheLayoutGM()
        {
            // Wall-sided formula GZ = sin φ (GM + ½ BM tan² φ), with the hull's GM. The 5 % band holds the probe
            // grid to the hull: the first grid (9 × 4 m) lost 7.6 % here, because columns whose bottom sits just
            // under the waterline count in full in the waterplane but stop lifting after a few centimetres. The
            // column-centre defect (AP15) would make GM about 4 m larger.
            HullLayout layout = Layout();
            HullLayout.Hydrostatics h = layout.hydrostatics;
            using (var sim = CalmSea())
            using (var probes = Probes(layout))
            {
                BuoyancyBodyOutput load = BodyLoad(sim, probes, HeeledPose(h, Heel, 0f));
                float righting = -load.Torque.z;   // the heel lifts starboard (+x); the sea pushes it back
                float expected = h.mass_kg * G * math.sin(Heel) * (h.gm_m + 0.5f * h.bm_m * math.tan(Heel) * math.tan(Heel));
                float gm = righting / (h.mass_kg * G * math.sin(Heel)) - 0.5f * h.bm_m * math.tan(Heel) * math.tan(Heel);
                Debug.Log($"[GQ] DDG-51 at 5°: righting {righting / 1e6f:0.0} MN·m vs theory {expected / 1e6f:0.0} MN·m; " +
                          $"GM from the probes {gm:0.000} m (layout {h.gm_m:0.000} m)");
                Assert.AreEqual(expected, righting, 0.05f * expected);
            }
        }

        [Test]
        public void FreeRoll_PeriodMatchesTheHullProfile()
        {
            // Free roll from 5°, undamped, roll only: I φ'' = τ(φ), τ from Sea State's batched jobs, I = m k².
            // The gate is the hull's own theory 2π k / √(g GM). IMO is context only: it includes added inertia,
            // which Sea State does not model, so a bare probe hull rolls faster (hull-stability-check, traps).
            HullLayout layout = Layout();
            HullLayout.Hydrostatics h = layout.hydrostatics;
            var profile = ScriptableObject.CreateInstance<ShipHullProfileSO>();
            try
            {
                profile.massKg = h.mass_kg;
                profile.gyradiusM = new Vector3(h.gyradius_m.pitch, h.gyradius_m.yaw, h.gyradius_m.roll);
                profile.metacentricHeightM = h.gm_m;
                float inertia = profile.InertiaTensor.z;
                float theory = profile.NaturalRollPeriodS(G);
                float c = 0.373f + 0.023f * (h.beam_wl_m / h.draft_m) - 0.043f * (h.length_wl_m / 100f);
                float imo = 2f * c * h.beam_wl_m / math.sqrt(h.gm_m);

                const float dt = 0.02f;
                using (var sim = CalmSea())
                using (var probes = Probes(layout))
                {
                    float phi = Heel, omega = 0f;
                    var crossings = new List<double>();
                    for (int step = 1; step <= 2500; step++)   // 50 s, about five periods
                    {
                        float torque = BodyLoad(sim, probes, HeeledPose(h, phi, omega)).Torque.z;
                        omega += torque / inertia * dt;
                        float next = phi + omega * dt;
                        if (phi < 0f && next >= 0f) crossings.Add((step - 1 + phi / (phi - next)) * dt);
                        phi = next;
                    }
                    Assert.GreaterOrEqual(crossings.Count, 3, "too few roll cycles to measure");
                    double measured = (crossings[crossings.Count - 1] - crossings[0]) / (crossings.Count - 1);
                    Debug.Log($"[GQ] DDG-51 free roll: {measured:0.00} s over {crossings.Count - 1} cycles; profile theory " +
                              $"{theory:0.00} s (k {h.gyradius_m.roll:0.00} m, GM {h.gm_m:0.00} m); IMO {imo:0.00} s (context)");
                    Assert.AreEqual(theory, measured, 0.03 * theory);
                }
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void ShipHull_AppliesMassCentreAndInertiaFromItsProfile()
        {
            var profile = ScriptableObject.CreateInstance<ShipHullProfileSO>();
            var go = new GameObject("ship under test");
            try
            {
                go.AddComponent<BoxCollider>().size = new Vector3(20f, 12f, 150f);
                var body = go.AddComponent<Rigidbody>();
                var hull = go.AddComponent<ShipHull>();
                profile.massKg = 8.6e6f;
                profile.centerOfMassM = new Vector3(0f, 0.4f, -0.1f);
                profile.gyradiusM = new Vector3(35f, 36.4f, 6.27f);
                var so = new SerializedObject(hull);
                so.FindProperty("profile").objectReferenceValue = profile;
                so.ApplyModifiedPropertiesWithoutUndo();

                hull.Apply();
                Assert.AreEqual(8.6e6f, body.mass, 1f);
                Assert.AreEqual(0.4f, body.centerOfMass.y, 1e-5f);
                Assert.AreEqual(8.6e6f * 6.27f * 6.27f, body.inertiaTensor.z, 1e-3f * body.inertiaTensor.z, "roll");
                Assert.AreEqual(8.6e6f * 35f * 35f, body.inertiaTensor.x, 1e-3f * body.inertiaTensor.x, "pitch");
                Assert.AreEqual(Quaternion.identity, body.inertiaTensorRotation);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Prefab_IsAssembledFromTheBlenderModel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Ddg51HullBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "no prefab: run Deep Water/General Quarters/Build DDG-51 Hull");
            HullLayout layout = Layout();

            var buoyant = prefab.GetComponent<BuoyantBody>();
            var hull = prefab.GetComponent<ShipHull>();
            Assert.IsNotNull(buoyant);
            Assert.IsNotNull(hull);
            Assert.IsNotNull(hull.Profile, "ShipHull has its profile");
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<BuoyancyHullSO>(Ddg51HullBuilder.BuoyancyAsset), hull.Profile.buoyancy);
            Assert.AreEqual(layout.probes.columns.Length, hull.Profile.buoyancy.probes.Length, "one probe per Blender column");
            Assert.AreEqual(layout.hydrostatics.mass_kg, hull.Profile.buoyancy.massKg, 1f, "both mass sources agree");

            Transform points = prefab.transform.Find("Launch Points");
            Assert.AreEqual(32, points.Find("VLS Fore (32)").childCount);
            Assert.AreEqual(64, points.Find("VLS Aft (64)").childCount);
            Assert.AreEqual(8, points.Find("Harpoon (2 × Mk 141)").childCount);

            MeshCollider[] hullColliders = prefab.transform.Find("Collision").GetComponentsInChildren<MeshCollider>(true);
            Assert.AreEqual(17, hullColliders.Length, "compound convex hull pieces");
            Assert.IsTrue(hullColliders.All(c => c.convex && c.sharedMesh != null && !c.isTrigger));
            BoxCollider[] compartments = prefab.transform.Find("Compartments").GetComponentsInChildren<BoxCollider>(true);
            Assert.AreEqual(layout.compartments.Length, compartments.Length);
            Assert.IsTrue(compartments.All(c => c.isTrigger));

            // Articulation: each elevating part sits under its training part, at the pivot the FBX carried.
            Transform visual = prefab.transform.Find("Visual");
            foreach (string mount in new[] { "GQ_Mk45", "GQ_CIWS_Fwd", "GQ_CIWS_Aft" })
            {
                Transform elevate = visual.GetComponentsInChildren<Transform>(true).Single(t => t.name == mount + "_Elevate");
                Assert.AreEqual(mount + "_Train", elevate.parent.name, mount);
            }

            // Materials: the original model's textured HDRP materials, none missing.
            // Hull, Mk 45 ×2, CIWS 2 × 3, Mk 38 ×2, SPG-62 ×3, exhaust, RHIB. Launch points are empties.
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            Assert.AreEqual(16, renderers.Length, "visual parts");
            Material[] materials = renderers.SelectMany(r => r.sharedMaterials).ToArray();
            Assert.IsTrue(materials.All(m => m != null), "a renderer has an empty material slot");
            string[] foreign = materials.Where(m => AssetDatabase.GetAssetPath(m) != Ddg51HullBuilder.OriginalModel)
                .Select(m => m.name).Distinct().ToArray();
            Assert.IsEmpty(foreign, "materials not remapped to the original model's: " + string.Join(", ", foreign));
        }

        // ── helpers (after Sea State's BuoyancyModelTests) ─────────────────────────────────────────────

        static SeaSimulation CalmSea()
        {
            var sim = new SeaSimulation(0f, 4);
            sim.Apply(new SeaBakeInput { MinWavelengthM = 10f, Gravity = G, FoamCalibrationGrid = 32 }, default, 0f);
            return sim;
        }

        /// <summary>BM_L = I_L / V of the probe columns: the wet waterplane's second moment about its own centroid.</summary>
        static float LongitudinalBm(HullLayout layout)
        {
            float a = layout.probes.area_m2;
            var wet = layout.probes.columns.Where(c => c.centre[1] - 0.5f * c.height < 0f && c.centre[1] + 0.5f * c.height > 0f).ToArray();
            float zf = wet.Average(c => c.centre[2]);
            float il = wet.Sum(c => a * (c.centre[2] - zf) * (c.centre[2] - zf));
            float volume = layout.probes.columns.Sum(c => a * math.clamp(0.5f * c.height - c.centre[1], 0f, c.height));
            return il / volume;
        }

        static NativeArray<BuoyancyProbe> Probes(HullLayout layout) =>
            new NativeArray<BuoyancyProbe>(layout.probes.columns.Select(c => new BuoyancyProbe
            {
                LocalPosition = HullLayout.V(c.centre), AreaM2 = layout.probes.area_m2, HeightM = c.height,
            }).ToArray(), Allocator.Persistent);

        /// <summary>
        /// The ship at its drawn waterline (the frame origin sits on it), heeled about the waterline centreline
        /// (starboard up). Heeling about the waterline keeps the displacement constant to first order.
        /// </summary>
        static BuoyancyBodyPose HeeledPose(HullLayout.Hydrostatics h, float heel, float rollRate)
        {
            quaternion rotation = quaternion.RotateZ(heel);
            return new BuoyancyBodyPose
            {
                Position = float3.zero,
                Rotation = rotation,
                AngularVelocity = new float3(0f, 0f, rollRate),
                WorldCenterOfMass = math.mul(rotation, (float3)HullLayout.V(h.centre_of_mass)),
            };
        }

        /// <summary>One body's summed load from the batched jobs (the path BuoyancySystem runs), undamped.</summary>
        static BuoyancyBodyOutput BodyLoad(SeaSimulation sim, NativeArray<BuoyancyProbe> probes, BuoyancyBodyPose pose)
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
    }
}
