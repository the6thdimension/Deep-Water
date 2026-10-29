using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeepWater.AirWing.Modules.World;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Sea.Core;

namespace DeepWater.AirWing.Editor
{
    /// <summary>
    /// Deep Water → Air Wing → Build Dogfight (slice 1e; W12, W13, W14). Overwrites <c>Assets/_SCENES/Dogfight.unity</c>
    /// in place, so its GUID survives (C24), with Air Wing's test bed:
    /// <list type="bullet">
    /// <item>the Sea State sea, sky and sun (the HDRP water the old scene had is gone);</item>
    /// <item>land at the sea's edge, south of the start, with a runway, 09/27, along its coast. Both are truth-feed
    /// ground (<see cref="FlatGround"/>); at L0 meeting either is a crash, so the runway waits for L1 gear (W29);</item>
    /// <item>four gates flown in order, from <c>Examples/Dogfight/Dogfight_Gates.asset</c>;</item>
    /// <item>the DDG-51 model as a still landmark under the second gate (the model only: no scripts, no physics);</item>
    /// <item>the shared flight rig: the player seat, the runner, the F/A-18E airborne at 5,000 ft and 400 kt heading
    /// north toward the first gate, the chase camera and the HUD with the course.</item>
    /// </list>
    /// The pre-Air Wing scene is kept in <c>Assets/_Archive/Dogfight_legacy/</c> (P8); the builder refuses to run until
    /// that copy exists. Every layout number here is AUTHORED scenery for a test bed.
    /// </summary>
    public static class DogfightBuilder
    {
        public const string ScenePath = "Assets/_SCENES/Dogfight.unity";
        public const string LegacyCopyPath = "Assets/_Archive/Dogfight_legacy/Dogfight.unity";
        public const string CoursePath = AirWingBuilders.Root + "/Examples/Dogfight/Dogfight_Gates.asset";
        const string MaterialsDir = AirWingBuilders.Root + "/Examples/Materials";
        const string DdgModelPath = "Assets/General Quarters/Prefabs/Models/DDG51_GQ.fbx";

        // AUTHORED layout, metres. The sea datum is y = 0; +Z is north.
        static readonly Vector3 JetStart = new Vector3(0f, 1524f, -2500f);          // 5,000 ft, 2.5 km off the coast, heading 000
        const float LandTopM = 8f;
        static readonly Vector3 LandCentre = new Vector3(0f, LandTopM, -6500f);     // its north edge (the coast) at z = -4,500
        static readonly Vector2 LandSizeM = new Vector2(8000f, 4000f);              // east-west, north-south
        static readonly Vector3 RunwayCentre = new Vector3(0f, LandTopM + 0.05f, -5500f);
        const float RunwayLengthM = 2438f;                                          // 8,000 ft
        const float RunwayWidthM = 61f;                                             // 200 ft
        static readonly Vector3 DdgPosition = new Vector3(3800f, 0f, 6000f);        // the model's origin is its waterline amidships
        const float DdgHeadingDeg = 90f;

        [MenuItem("Deep Water/Air Wing/Build Dogfight")]
        public static void Build()
        {
            if (AirWingBuilders.AnyDirtyScene()) return;
            if (File.Exists(ScenePath) && !File.Exists(LegacyCopyPath))
            {
                Debug.LogError($"[Air Wing] {ScenePath} has no archived copy at {LegacyCopyPath} yet (P8): copy it there first. Nothing was built.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AirWingBuilders.PrefabPath) == null) AirWingBuilders.BuildPrefab();

            // Load assets only after the scene switch (AP12).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AirWingBuilders.RigAssetsPresent()) return;
            GateCourseSO course = EnsureCourse();
            Material gate = EnsureMaterial("Gate", "HDRP/Lit", new Color(0.85f, 0.85f, 0.85f), 0.4f);
            Material nextGate = EnsureMaterial("Gate Next", "HDRP/Lit", new Color(1f, 0.45f, 0.05f), 0.4f);
            FlatGround runwayGround = BuildWorld(out SeaStateDirector sea);
            AirWingBuilders.Rig rig = AirWingBuilders.BuildRig(sea, JetStart, 0f);
            // The runway start is one setting on the jet (W14): Start Mode → Runway. Airborne stays the default.
            var jetView = Object.FindAnyObjectByType<DeepWater.AirWing.Modules.Aircraft.AircraftView>();
            if (jetView != null) AirWingBuilders.Set(jetView, "startRunway", runwayGround);

            var courseGo = new GameObject("Gate Course");
            var view = courseGo.AddComponent<GateCourseView>();
            AirWingBuilders.Set(view, "runner", rig.Runner);
            AirWingBuilders.Set(view, "course", course);
            AirWingBuilders.Set(view, "gateMaterial", gate);
            AirWingBuilders.Set(view, "nextGateMaterial", nextGate);
            AirWingBuilders.Set(rig.Hud, "gates", view);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Air Wing] Built {ScenePath}: Sea State sea, land and runway 09/27, {course.gates.Length} gates, DDG-51 landmark, " +
                      "one F/A-18E at 5,000 ft and 400 kt heading 000, chase camera, HUD.");
        }

        /// <summary>
        /// Dogfight's world in the open scene: sun, sky, the Sea State sea, the land with runway 09/27, and the DDG
        /// landmark. Shared with the training scene (RH Aviator's training builder) so both fly over the same world.
        /// Call after the scene switch (AP12). Returns the runway.
        /// </summary>
        public static FlatGround BuildWorld(out SeaStateDirector sea)
        {
            Material land = EnsureMaterial("Land", "HDRP/Lit", new Color(0.36f, 0.40f, 0.27f), 0.15f);
            Material runway = EnsureMaterial("Runway", "HDRP/Lit", new Color(0.13f, 0.13f, 0.14f), 0.25f);
            Material marking = EnsureMaterial("Runway Marking", "HDRP/Lit", new Color(0.92f, 0.92f, 0.90f), 0.25f);
            AirWingBuilders.Instantiate(AirWingBuilders.SunPrefabPath);
            AirWingBuilders.Instantiate(AirWingBuilders.SkyPrefabPath);
            sea = AirWingBuilders.BuildSea();
            FlatGround runwayGround = BuildLand(land, runway, marking);
            BuildLandmark();
            return runwayGround;
        }

        static FlatGround BuildLand(Material land, Material runway, Material marking)
        {
            var landGo = new GameObject("Land");
            landGo.transform.position = LandCentre;
            landGo.AddComponent<FlatGround>().SetSize(LandSizeM.y, LandSizeM.x);
            Block("Ground", landGo.transform, new Vector3(0f, -10f, 0f), new Vector3(LandSizeM.x, 20f, LandSizeM.y), land, collide: true);

            // The runway's length runs along its own +Z; turned to 090 it lies east-west.
            var runwayGo = new GameObject("Runway 09-27");
            runwayGo.transform.SetPositionAndRotation(RunwayCentre, Quaternion.Euler(0f, 90f, 0f));
            runwayGo.AddComponent<FlatGround>().SetSize(RunwayLengthM, RunwayWidthM);
            Block("Surface", runwayGo.transform, new Vector3(0f, -0.05f, 0f), new Vector3(RunwayWidthM, 0.1f, RunwayLengthM), runway, collide: true);
            Block("Centreline", runwayGo.transform, new Vector3(0f, 0.005f, 0f), new Vector3(0.9f, 0.01f, RunwayLengthM - 120f), marking);
            foreach (float end in new[] { -1f, 1f })
                Block(end < 0f ? "Threshold 09" : "Threshold 27", runwayGo.transform, new Vector3(0f, 0.005f, end * (0.5f * RunwayLengthM - 20f)),
                      new Vector3(RunwayWidthM - 6f, 0.01f, 6f), marking);
            return runwayGo.GetComponent<FlatGround>();
        }

        static void BuildLandmark()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(DdgModelPath);
            if (model == null) { Debug.LogWarning($"[Air Wing] {DdgModelPath} not found; no landmark."); return; }
            var ddg = (GameObject)PrefabUtility.InstantiatePrefab(model);
            ddg.name = "DDG-51 (landmark)";
            ddg.transform.SetPositionAndRotation(DdgPosition, Quaternion.Euler(0f, DdgHeadingDeg, 0f));
            foreach (Collider c in ddg.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);   // still scenery: nothing to hit at L0
        }

        /// <summary>
        /// A box. The ground boxes keep their colliders: the landing gear finds them by PhysX raycast (W4). The truth
        /// feed (<see cref="FlatGround"/>) still decides crashes. Markings have no collider.
        /// </summary>
        static void Block(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool collide = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>The Dogfight course, seeded once; after that the asset is the source (edit it, then rebuild).</summary>
        static GateCourseSO EnsureCourse()
        {
            var course = AssetDatabase.LoadAssetAtPath<GateCourseSO>(CoursePath);
            if (course != null) return course;
            course = ScriptableObject.CreateInstance<GateCourseSO>();
            course.gates = new[]
            {
                Entry("Gate 1 · 5,000 ft", new Vector3(0f, 1524f, 2500f), 0f, 100f, new Vector2(350f, 450f), new Vector2(4500f, 5500f)),
                Entry("Gate 2 · DDG pass, 1,000 ft", new Vector3(3000f, 305f, 6000f), 90f, 100f, Vector2.zero, new Vector2(800f, 1200f)),
                Entry("Gate 3 · climb to 6,000 ft", new Vector3(6000f, 1829f, -1000f), 180f, 100f, new Vector2(300f, 0f), new Vector2(5500f, 6500f)),
                Entry("Gate 4 · runway pass, 500 ft", new Vector3(0f, 152f, -5500f), 270f, 60f, new Vector2(250f, 350f), new Vector2(300f, 700f)),
            };
            course.provenance =
                "AUTHORED 2026-10-25 for the Dogfight test bed (slice 1e). A loop: north at 5,000 ft; east, down to 1,000 ft, over the " +
                "DDG; south, climbing to 6,000 ft; west along runway 09/27 at 500 ft; then north again. Legs of 5 to 8 km leave room " +
                "for the turns: at 400 kt and 4 G the turn radius is about 1.1 km. Gate 4's ring is 60 m so it stays 84 m clear of " +
                "the runway. The EditMode test 'TheInstructor_FliesTheDogfightCourse' checks the course can be flown at L0.";
            Directory.CreateDirectory(Path.GetDirectoryName(CoursePath));
            AssetDatabase.CreateAsset(course, CoursePath);
            AssetDatabase.SaveAssets();
            return course;
        }

        static GateCourseSO.GateEntry Entry(string name, Vector3 centre, float headingDeg, float radiusM, Vector2 speedKt, Vector2 altitudeFt) =>
            new GateCourseSO.GateEntry { name = name, centreM = centre, headingDeg = headingDeg, radiusM = radiusM, speedBandKt = speedKt, altitudeBandFt = altitudeFt };

        static Material EnsureMaterial(string name, string shaderName, Color colour, float smoothness)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError($"[Air Wing] Shader {shaderName} not found; {name} uses the default material."); return null; }
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            Directory.CreateDirectory(MaterialsDir);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
