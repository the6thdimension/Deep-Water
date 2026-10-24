using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeepWater.GeneralQuarters.Modules.Hull;
using DeepWater.GeneralQuarters.ScriptableObjects;
using DeepWater.Sea.Modules.Buoyancy;
using DeepWater.Sea.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Editor
{
    /// <summary>
    /// Builds the DDG-51 hull assets and prefab from the Blender exports (slice 3). Idempotent: rerun it after
    /// re-running <c>ArtSource/General Quarters/author_ddg51_gq.py</c>. Builds in a preview scene, so the open
    /// scene is never touched.
    /// </summary>
    public static class Ddg51HullBuilder
    {
        public const string ModelDir = "Assets/General Quarters/Prefabs/Models";
        public const string VisualFbx = ModelDir + "/DDG51_GQ.fbx";
        public const string CollisionFbx = ModelDir + "/DDG51_GQ_Collision.fbx";
        public const string LayoutJson = ModelDir + "/DDG51_GQ_Layout.json";
        public const string BuoyancyAsset = "Assets/General Quarters/ScriptableObjects/DDG51_GQ_Buoyancy.asset";
        public const string HullAsset = "Assets/General Quarters/ScriptableObjects/DDG51_GQ_Hull.asset";
        public const string PrefabPath = "Assets/General Quarters/Prefabs/DDG51_GQ.prefab";
        /// <summary>The original model import, whose materials (HDRP/Lit, textured) the new model reuses.</summary>
        public const string OriginalModel ="Assets/_SCENES/Rocket/Navy Assets/11-low-poly-us-navy-ddg-51-uss-arleigh-burke/source/DDG51/DDG51.blend";

        // AUTHORED: Sea State's default probe damping (heave damping ratio 0.2 is mid-range for ships, 0.1-0.3).
        const float HeaveDampingRatio = 0.2f;
        const float QuadraticDragCd = 0.5f;

        [MenuItem("Deep Water/General Quarters/Build DDG-51 Hull")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            HullLayout layout = HullLayout.Parse(File.ReadAllText(LayoutJson));
            int remapped = RemapMaterials();
            BuoyancyHullSO buoyancy = BuildBuoyancy(layout);
            ShipHullProfileSO hull = BuildHullProfile(layout, buoyancy);
            string prefab = BuildPrefab(hull, buoyancy);
            AssetDatabase.SaveAssets();
            return $"[GQ] DDG-51 hull built: {buoyancy.probes.Length} buoyancy columns, {layout.hydrostatics.displacement_t:0} t, " +
                   $"GM {hull.metacentricHeightM:0.00} m, roll period {hull.NaturalRollPeriodS():0.0} s (undamped); " +
                   $"{remapped} materials remapped; prefab {prefab}";
        }

        static int RemapMaterials()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(VisualFbx);
            Dictionary<string, Material> originals = AssetDatabase.LoadAllAssetsAtPath(OriginalModel).OfType<Material>()
                .GroupBy(m => m.name).ToDictionary(g => g.Key, g => g.First());
            var existing = importer.GetExternalObjectMap();
            int n = 0;
            foreach (Material m in AssetDatabase.LoadAllAssetsAtPath(VisualFbx).OfType<Material>().ToList())
            {
                if (!originals.TryGetValue(m.name, out Material original)) continue;
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name);
                if (existing.TryGetValue(id, out Object current) && current == original) continue;
                importer.AddRemap(id, original);
                n++;
            }
            if (n > 0) importer.SaveAndReimport();
            return n;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static BuoyancyHullSO BuildBuoyancy(HullLayout layout)
        {
            BuoyancyHullSO so = LoadOrCreate<BuoyancyHullSO>(BuoyancyAsset);
            so.probes = layout.probes.columns.Select(c => new BuoyancyHullSO.Probe
            {
                localPosition = HullLayout.V(c.centre), areaM2 = layout.probes.area_m2, heightM = c.height,
            }).ToArray();
            so.massKg = layout.hydrostatics.mass_kg;
            so.overrideCenterOfMass = true;
            so.centerOfMassM = HullLayout.V(layout.hydrostatics.centre_of_mass);
            so.designDraftM = layout.hydrostatics.draft_m;
            so.heaveDampingRatio = HeaveDampingRatio;
            so.quadraticDragCd = QuadraticDragCd;
            EditorUtility.SetDirty(so);
            return so;
        }

        static ShipHullProfileSO BuildHullProfile(HullLayout layout, BuoyancyHullSO buoyancy)
        {
            HullLayout.Hydrostatics h = layout.hydrostatics;
            ShipHullProfileSO so = LoadOrCreate<ShipHullProfileSO>(HullAsset);
            so.buoyancy = buoyancy;
            so.massKg = h.mass_kg;
            so.centerOfMassM = HullLayout.V(h.centre_of_mass);
            so.gyradiusM = new Vector3(h.gyradius_m.pitch, h.gyradius_m.yaw, h.gyradius_m.roll);
            so.displacementT = h.displacement_t;
            so.designDraftM = h.draft_m;
            so.kbM = h.kb_m;
            so.bmM = h.bm_m;
            so.metacentricHeightM = h.gm_m;
            so.compartments = layout.compartments.Select(c => new ShipHullProfileSO.Compartment
            {
                name = c.name, min = HullLayout.V(c.min), max = HullLayout.V(c.max),
            }).ToArray();
            so.provenance =
                $"Built from {LayoutJson} ({layout.source}).\n" +
                $"{layout.hydrostatics_note}: displacement {h.displacement_t:0} t at the drawn waterline, draft {h.draft_m:0.00} m, " +
                $"KB {h.kb_m:0.00} m, BM {h.bm_m:0.00} m, waterline {h.length_wl_m:0} × {h.beam_wl_m:0.0} m.\n" +
                $"Buoyancy: {buoyancy.probes.Length} columns on a {layout.probes.grid} grid (AUTHORED resolution); on their own they " +
                $"displace {layout.probes.displacement_t:0} t with BM {layout.probes.bm_m:0.00} m.\n" +
                $"GM {h.gm_m:0.00} m: {h.gm_note}.\n" +
                $"Gyradii: {h.gyradius_note}.\n" +
                $"Compartments: {layout.compartments_note}.\n" +
                $"Buoyancy damping: heave ratio {HeaveDampingRatio}, Cd {QuadraticDragCd} (AUTHORED, Sea State defaults).";
            EditorUtility.SetDirty(so);
            return so;
        }

        static string BuildPrefab(ShipHullProfileSO hull, BuoyancyHullSO buoyancy)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("DDG-51 (General Quarters)");
                SceneManager.MoveGameObjectToScene(root, preview);

                var body = root.AddComponent<Rigidbody>();
                body.mass = hull.massKg;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.linearDamping = 0f;
                body.angularDamping = 0f;   // the sea damps the ship (probe damping); no hidden extra damping
                var buoyant = root.AddComponent<BuoyantBody>();
                SetField(buoyant, "hull", buoyancy);
                var shipHull = root.AddComponent<ShipHull>();
                SetField(shipHull, "profile", hull);

                // Visual: unpack the model so the elevating parts can sit under their training parts.
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VisualFbx), preview);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                Transform[] parts = visual.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in parts.Where(t => t.name.EndsWith("_Elevate")))
                {
                    Transform train = parts.FirstOrDefault(p => p.name == t.name.Replace("_Elevate", "_Train"));
                    if (train != null) t.SetParent(train, true);
                }
                Transform points = new GameObject("Launch Points").transform;
                points.SetParent(root.transform, false);
                Group(parts, "GQ_VLS_F_", points, "VLS Fore (32)");
                Group(parts, "GQ_VLS_A_", points, "VLS Aft (64)");
                Group(parts, "GQ_Harpoon_", points, "Harpoon (2 × Mk 141)");

                // Collision: convex mesh colliders, no renderers.
                var collision = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CollisionFbx), preview);
                PrefabUtility.UnpackPrefabInstance(collision, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                collision.name = "Collision";
                collision.transform.SetParent(root.transform, false);
                foreach (MeshFilter mf in collision.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                    Object.DestroyImmediate(mf.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(mf);
                }

                // Compartments: trigger volumes (Damage Control L1 floods them).
                Transform comps = new GameObject("Compartments").transform;
                comps.SetParent(root.transform, false);
                foreach (ShipHullProfileSO.Compartment c in hull.compartments)
                {
                    var go = new GameObject(c.name);
                    go.transform.SetParent(comps, false);
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.center = (c.min + c.max) * 0.5f;
                    box.size = c.max - c.min;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return PrefabPath;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        static void Group(Transform[] parts, string prefix, Transform under, string name)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(under, false);
            foreach (Transform t in parts.Where(p => p.name.StartsWith(prefix)).OrderBy(p => p.name))
                t.SetParent(group, true);
        }

        static void SetField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) throw new System.MissingFieldException(target.GetType().Name, field);
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
