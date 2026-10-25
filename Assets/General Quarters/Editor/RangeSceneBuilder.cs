using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using DeepWater.GeneralQuarters.Examples;
using DeepWater.GeneralQuarters.Modules.Combat;
using DeepWater.GeneralQuarters.Modules.Hull;
using DeepWater.GeneralQuarters.Modules.Presentation;
using DeepWater.GeneralQuarters.Modules.Propulsion;
using DeepWater.GeneralQuarters.ScriptableObjects;
using DeepWater.Sea.Modules.Buoyancy;
using DeepWater.Sea.Modules.Hdrp;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DeepWater.GeneralQuarters.Editor
{
    /// <summary>
    /// Builds the General Quarters range scene (slice 5, G12) from the Sea State demo: the same sky, sun and sea, with
    /// the demo's box hulls, selector panel and review camera taken out and the DDG, the range and its screen put in.
    /// Copying the demo keeps the range on whatever the sea looks like now, with no code tie to the demo's builder.
    /// Regenerates the scene each time: a copy of the previous one goes to Library/GQ_RangeBackups first.
    /// Refuses to run over unsaved scene changes.
    /// </summary>
    public static class RangeSceneBuilder
    {
        const string DemoScene = "Assets/Sea State/Examples/Scenes/SeaStateDemo.unity";
        public const string RangeScene = "Assets/General Quarters/Examples/Scenes/GQ_Range.unity";
        const string BuildDemoMenu = "Deep Water/Sea State/Build Demo Scene";
        const float StartSpeedKn = 12f;

        [MenuItem("Deep Water/General Quarters/Build Range Scene")]
        public static void BuildMenu() => Debug.Log(Build());

        [MenuItem("Deep Water/General Quarters/Play the Range")]
        public static void Play()
        {
            if (EditorApplication.isPlaying) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RangeScene) == null) { Debug.LogError("[GQ] No range scene yet: run Build Range Scene first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(RangeScene, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        public static string Build()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                    return "[GQ] An open scene has unsaved changes: save it first; nothing was built.";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScene) == null && !EditorApplication.ExecuteMenuItem(BuildDemoMenu))
                return $"[GQ] {DemoScene} is missing and '{BuildDemoMenu}' could not build it.";

            string assets = Ddg51CombatData.Build() + System.Environment.NewLine + CombatEffectsBuilder.Build();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // the old range may be the open scene
            const string folder = "Assets/General Quarters/Examples/Scenes";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/General Quarters/Examples", "Scenes");
            if (File.Exists(RangeScene))
            {
                // Overwrite the file in place, so the scene keeps its GUID (a delete and copy gave it a new one every rebuild).
                BackUpPrevious();
                File.Copy(DemoScene, RangeScene, true);
                AssetDatabase.ImportAsset(RangeScene, ImportAssetOptions.ForceUpdate);
            }
            else if (!AssetDatabase.CopyAsset(DemoScene, RangeScene)) return $"[GQ] Could not copy {DemoScene} to {RangeScene}.";
            var scene = EditorSceneManager.OpenScene(RangeScene, OpenSceneMode.Single);

            // Out: the demo's hulls, its selector (its number keys and Tab would fight the range's), its camera script.
            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name.StartsWith("Box Hull") || root.name.StartsWith("Buoy") || root.name == "Sea State Controls") { Object.DestroyImmediate(root); removed++; }
            var system = Object.FindAnyObjectByType<BuoyancySystem>();
            Camera cam = Object.FindAnyObjectByType<Camera>();
            if (system == null || cam == null) return "[GQ] The demo scene has no BuoyancySystem or camera; nothing was built.";
            if (cam.GetComponent("SeaDemoCamera") is Component demoCamera) Object.DestroyImmediate(demoCamera);

            // In: the DDG, under way at 12 kn, with her combat system.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Ddg51HullBuilder.PrefabPath);
            var helm = AssetDatabase.LoadAssetAtPath<HelmProfileSO>(Ddg51HullBuilder.HelmAsset);
            var loadout = AssetDatabase.LoadAssetAtPath<ShipLoadoutSO>(Ddg51CombatData.LoadoutAsset);
            var ai = AssetDatabase.LoadAssetAtPath<AiCaptainProfileSO>(Ddg51CombatData.AiCaptainAsset);
            if (prefab == null || helm == null) return "[GQ] DDG prefab or Helm profile missing: run Build DDG-51 Hull first.";
            var ship = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            ship.name = "DDG-51 (General Quarters)";
            ship.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Set(ship.GetComponent<BuoyantBody>(), "system", system);
            SetFloat(ship.GetComponent<ShipDrive>(), "startSpeedKn", StartSpeedKn);
            var combat = ship.AddComponent<ShipCombat>();
            Set(combat, "loadout", loadout);
            Set(combat, "aiCaptainProfile", ai);
            ship.AddComponent<ShipFlooding>();   // Damage Control L1: the flood water on the hull, the sea at the openings (G55)

            // In: the range (conn, input, seats, stand-ins) and its screen.
            var rangeGo = new GameObject("General Quarters Range");
            var connInput = rangeGo.AddComponent<ConnInput>();
            var conn = rangeGo.AddComponent<ConnController>();
            Set(conn, "ship", ship.GetComponent<ShipDrive>());
            Set(conn, "helmProfile", helm);
            Set(conn, "combat", combat);
            var rangeInput = rangeGo.AddComponent<RangeInput>();
            var proxies = new GameObject("Stand-ins").AddComponent<RangeProxies>();
            proxies.transform.SetParent(rangeGo.transform, false);
            Set(proxies, "combat", combat);
            Set(proxies, "missileModel", AssetDatabase.LoadAssetAtPath<GameObject>(Ddg51CombatData.EssmModel));
            Set(proxies, "threatMissileModel", AssetDatabase.LoadAssetAtPath<GameObject>(Ddg51CombatData.EssmModel));
            Set(proxies, "aircraftModel", AssetDatabase.LoadAssetAtPath<GameObject>(Ddg51CombatData.AircraftModel));
            Set(proxies, "friendlyMaterial", AssetDatabase.LoadAssetAtPath<Material>(Ddg51CombatData.FriendlyMaterial));
            Set(proxies, "hostileMaterial", AssetDatabase.LoadAssetAtPath<Material>(Ddg51CombatData.HostileMaterial));
            // Measured 2026-10-24: the ESSM model's fins sit at +X, so its nose is -X; the F-16 model is 15.25 m along Z, nose +Z.
            SetVector(proxies, "missileModelEuler", new Vector3(0f, 90f, 0f));
            SetVector(proxies, "threatModelEuler", new Vector3(0f, 90f, 0f));
            SetVector(proxies, "aircraftModelEuler", Vector3.zero);

            var chase = cam.gameObject.AddComponent<ShipChaseCamera>();
            Set(chase, "target", ship.transform);
            Set(chase, "controls", connInput);
            // Motion blur off for the frames after a camera cut (G33).
            var cut = cam.gameObject.AddComponent<Volume>();
            cut.isGlobal = true;
            cut.priority = 100f;
            cut.weight = 0f;
            cut.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(CombatEffectsBuilder.CameraCutProfile);
            Set(chase, "cutVolume", cut);

            // Presentation (slice 6): effects, mounts, hatches, foam and sound, read from the combat log.
            var presentation = rangeGo.AddComponent<CombatPresentation>();
            Set(presentation, "combat", combat);
            Set(presentation, "presentation", AssetDatabase.LoadAssetAtPath<CombatPresentationSO>(CombatEffectsBuilder.PresentationAsset));
            var sea = Object.FindAnyObjectByType<HdrpSeaRenderer>();
            if (sea != null) Set(presentation, "sea", sea); else Debug.LogWarning("[GQ] No HdrpSeaRenderer in the demo copy: splashes and the wake leave no foam.");
            Set(presentation, "listener", cam.transform);
            Set(proxies, "presentation", presentation);
            cam.transform.SetPositionAndRotation(new Vector3(-150f, 40f, -200f), Quaternion.LookRotation(new Vector3(150f, -30f, 200f)));

            var director = rangeGo.AddComponent<RangeDirector>();
            Set(director, "range", AssetDatabase.LoadAssetAtPath<RangeProfileSO>(Ddg51CombatData.RangeAsset));
            Set(director, "combat", combat);
            Set(director, "conn", conn);
            Set(director, "input", rangeInput);
            Set(director, "proxies", proxies);
            Set(director, "chaseCamera", chase);
            Set(director, "viewCamera", cam);

            var screenGo = new GameObject("Range Screen");
            screenGo.transform.SetParent(rangeGo.transform, false);
            var doc = screenGo.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(Ddg51CombatData.PanelSettingsAsset);
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Ddg51CombatData.ScreenUxml);
            if (doc.panelSettings == null || doc.visualTreeAsset == null) return "[GQ] Panel settings or RangeScreen.uxml missing; scene not saved.";
            var screen = screenGo.AddComponent<RangeScreen>();
            Set(screen, "director", director);

            Directory.CreateDirectory(Path.GetDirectoryName(RangeScene));
            EditorSceneManager.SaveScene(scene, RangeScene);
            AssetDatabase.SaveAssets();
            return $"{assets}\n[GQ] Built {RangeScene} from the Sea State demo: removed {removed} demo objects; added the DDG (12 kn, combat system), " +
                   "the range (conn, seats, stand-ins, director) and its App UI screen. Play it: Deep Water → General Quarters → Play the Range.";
        }

        static void BackUpPrevious()
        {
            if (!File.Exists(RangeScene)) return;
            string dir = Path.Combine("Library", "GQ_RangeBackups");
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
            File.Copy(RangeScene, Path.Combine(dir, $"GQ_Range.pre-rebuild-backup-{stamp}.unity"), true);
        }

        // Serialized-field wiring through SerializedObject: a renamed field fails loudly (no reflection, AP1).
        static void Set(Object target, string field, Object value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target), $"no component to set '{field}' on");
            if (value == null) throw new ArgumentNullException(field, $"refusing to wire {target.GetType().Name}.{field} to a missing asset or object");
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetVector(Object target, string field, Vector3 value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'");
            p.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'");
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
