using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using DeepWater.Sea.Core;
using DeepWater.Sea.Editor;
using DeepWater.Sea.Modules.Buoyancy;
using DeepWater.Sea.Modules.Hdrp;
using DeepWater.Sea.ScriptableObjects;

namespace DeepWater.Sea.Examples.Editor
{
    /// <summary>
    /// Builds the Sea State demo scene from scratch: HDRP sky and sun from
    /// <c>Assets/_ENVIRONMENT</c>, a sea driven by the Douglas library, four box hulls from
    /// small craft to destroyer length riding it, a review camera and the selector panel.
    /// Idempotent: re-running regenerates the scene and refreshes the supporting assets.
    /// Refuses to run over unsaved scene changes.
    /// </summary>
    public static class SeaStateDemoSceneBuilder
    {
        const string Root = "Assets/Sea State";
        const string ScenePath = Root + "/Examples/Scenes/SeaStateDemo.unity";
        const string LookPath = Root + "/Examples/Settings/HdrpSeaLook.asset";
        const string HullFolder = Root + "/Examples/Hulls";
        const string MaterialFolder = Root + "/Examples/Materials";
        const string DecalShaderPath = Root + "/Modules/Hdrp/Shaders/SeaWaveDecal.shadergraph";
        const string DecalMaterialPath = Root + "/Modules/Hdrp/Materials/SeaWaveDecal.mat";
        const string OceanPrefabPath = "Assets/_ENVIRONMENT/Ocean System.prefab";
        const string SunPrefabPath = "Assets/_ENVIRONMENT/Sun.prefab";
        const string SkyPrefabPath = "Assets/_ENVIRONMENT/Sky and Fog Volume.prefab";
        const float SeawaterDensity = 1025f;

        // name, length, beam, height, design draft, probes across, probes along, position, heading.
        static readonly (string name, float l, float b, float h, float draft, int nx, int nz, Vector3 pos, float headingDeg)[] Hulls =
        {
            ("Box Hull 154 m (destroyer length)", 154f, 20f, 12f, 6.3f, 5, 16, new Vector3(0f, 0f, 0f), 0f),
            ("Box Hull 40 m (patrol craft)",       40f,  8f,  4f, 1.8f, 4, 10, new Vector3(-130f, 0f, 70f), 30f),
            ("Box Hull 11 m (RHIB)",               11f, 3.5f, 1.4f, 0.45f, 3, 6, new Vector3(60f, 0f, -90f), 300f),
            ("Buoy 2 m",                            2f,  2f,  2f, 0.8f, 2, 2, new Vector3(90f, 0f, 40f), 0f),
        };

        [MenuItem("Deep Water/Sea State/Build Demo Scene")]
        public static void Build()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("[SeaStateDemoSceneBuilder] An open scene has unsaved changes — save it first; nothing was built.");
                    return;
                }
            }

            if (AssetDatabase.LoadAssetAtPath<SeaStateLibrarySO>(DouglasProfileBuilder.LibraryPath) == null)
                DouglasProfileBuilder.BuildAll();
            AssetDatabase.SaveAssets();

            // Load assets only AFTER the scene switch: opening a scene in Single mode unloads
            // assets nothing references, which turns earlier-loaded references into dead ones.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var library = AssetDatabase.LoadAssetAtPath<SeaStateLibrarySO>(DouglasProfileBuilder.LibraryPath);
            var look = LoadOrCreate<HdrpSeaLookSO>(LookPath);
            var decalMaterial = EnsureDecalMaterial();
            var hullMaterial = EnsureLitMaterial($"{MaterialFolder}/HullGrey.mat", new Color(0.42f, 0.44f, 0.47f));
            var buoyMaterial = EnsureLitMaterial($"{MaterialFolder}/BuoyOrange.mat", new Color(0.95f, 0.38f, 0.08f));

            InstantiatePrefab(SunPrefabPath);
            InstantiatePrefab(SkyPrefabPath);

            // Sea: HDRP WaterSurface (look copied from the project's Ocean System) + director + renderer.
            var seaGo = new GameObject("Sea");
            var surface = seaGo.AddComponent<WaterSurface>();
            var oceanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OceanPrefabPath);
            var oceanSurface = oceanPrefab != null ? oceanPrefab.GetComponent<WaterSurface>() : null;
            if (oceanSurface != null) EditorUtility.CopySerialized(oceanSurface, surface);
            else Debug.LogWarning($"[SeaStateDemoSceneBuilder] {OceanPrefabPath} not found; using WaterSurface defaults.");

            var director = seaGo.AddComponent<SeaStateDirector>();
            SetField(director, "library", library);

            var renderer = seaGo.AddComponent<HdrpSeaRenderer>();
            SetField(renderer, "director", director);
            SetField(renderer, "look", look);
            SetField(renderer, "waveDecalMaterial", decalMaterial);

            // Hulls.
            Directory.CreateDirectory(HullFolder);
            var bodies = new BuoyantBody[Hulls.Length];
            for (int i = 0; i < Hulls.Length; i++)
                bodies[i] = BuildHull(Hulls[i], director, i == Hulls.Length - 1 ? buoyMaterial : hullMaterial);

            // Camera.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 30000f;
            camGo.AddComponent<HDAdditionalCameraData>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<SeaDemoCamera>();
            camGo.transform.position = new Vector3(240f, 100f, -340f);
            camGo.transform.LookAt(Vector3.zero);

            // Controls.
            var panelGo = new GameObject("Sea State Controls");
            var panel = panelGo.AddComponent<SeaStateSelectorPanel>();
            SetField(panel, "director", director);
            var so = new SerializedObject(panel);
            var hullsProp = so.FindProperty("hulls");
            hullsProp.arraySize = bodies.Length;
            for (int i = 0; i < bodies.Length; i++) hullsProp.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SeaStateDemoSceneBuilder] Built {ScenePath}: sea + {Hulls.Length} hulls + camera + selector.");
        }

        static BuoyantBody BuildHull((string name, float l, float b, float h, float draft, int nx, int nz, Vector3 pos, float headingDeg) spec,
            SeaStateDirector director, Material material)
        {
            string assetName = spec.name.Split(' ')[0] + "_" + spec.l.ToString("0") + "m";
            var hull = LoadOrCreate<BuoyancyHullSO>($"{HullFolder}/{assetName}.asset");
            hull.FillBoxGrid(spec.l, spec.b, spec.h, spec.nx, spec.nz);
            hull.massKg = SeawaterDensity * spec.l * spec.b * spec.draft;   // Archimedes at the design draft
            hull.designDraftM = spec.draft;
            hull.heaveDampingRatio = 0.2f;
            hull.quadraticDragCd = 0.5f;
            EditorUtility.SetDirty(hull);

            var go = new GameObject(spec.name);
            go.transform.SetPositionAndRotation(
                spec.pos + Vector3.up * (0.5f * spec.h - spec.draft),
                Quaternion.Euler(0f, spec.headingDeg, 0f));

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(spec.b, spec.h, spec.l);

            var body = go.AddComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(spec.b, spec.h, spec.l);
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;

            body.mass = hull.massKg;
            var buoyant = go.AddComponent<BuoyantBody>();
            SetField(buoyant, "sea", director);
            SetField(buoyant, "hull", hull);
            return buoyant;
        }

        static Material EnsureDecalMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(DecalShaderPath);
            if (shader == null)
                throw new FileNotFoundException($"Wave decal shader graph missing at {DecalShaderPath}.");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(DecalMaterialPath);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DecalMaterialPath));
                mat = new Material(shader) { name = "SeaWaveDecal" };
                AssetDatabase.CreateAsset(mat, DecalMaterialPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        static Material EnsureLitMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // Pipeline default material (AP5): never Shader.Find("Standard").
                mat = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial);
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void InstantiatePrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning($"[SeaStateDemoSceneBuilder] {path} not found; skipped."); return; }
            PrefabUtility.InstantiatePrefab(prefab);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // Serialized-field assignment through SerializedObject: names are checked at runtime
        // and the call fails loudly if a field is renamed (no reflection, AP1).
        static void SetField(Object target, string field, Object value)
        {
            if (value == null)
                throw new System.ArgumentNullException(field, $"Refusing to wire {target.GetType().Name}.{field} to a null/unloaded object.");
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
                throw new MissingFieldException($"{target.GetType().Name} has no serialized field '{field}'.");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (prop.objectReferenceValue != value)
                throw new MissingFieldException($"{target.GetType().Name}.{field} rejected {value.GetType().Name} '{value.name}'.");
        }

        sealed class MissingFieldException : System.Exception
        {
            public MissingFieldException(string message) : base(message) { }
        }
    }
}
