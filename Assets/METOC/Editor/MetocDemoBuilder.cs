using System;
using System.IO;
using System.Linq;
using DeepWater.Metoc.Examples;
using DeepWater.Metoc.Hdrp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DeepWater.Metoc.Editor
{
    /// <summary>
    /// Builds the METOC demo (E34): a copy of the Sea State demo (the shared sky prefab, the Sun and the sea) with the
    /// METOC director, the HDRP renderer, a Moon light and the App UI panel. No existing scene is changed. The copy is
    /// opened additively and closed after saving, so whatever scene is open, saved or not, is left as it is.
    /// The settings assets are made once and then left alone: a value set by hand is never overwritten.
    /// </summary>
    public static class MetocDemoBuilder
    {
        const string SeaDemo = "Assets/Sea State/Examples/Scenes/SeaStateDemo.unity";
        public const string DemoScene = "Assets/METOC/Examples/Scenes/MetocDemo.unity";
        const string SettingsDir = "Assets/METOC/Examples/Settings";
        public const string ScenarioAsset = SettingsDir + "/MetocDemoScenario.asset";
        public const string IlluminationAsset = SettingsDir + "/ClearSkyIllumination.asset";
        public const string LookAsset = SettingsDir + "/HdrpMetocLook.asset";
        const string PanelSettingsAsset = SettingsDir + "/MetocPanelSettings.asset";
        const string WeatherDir = SettingsDir + "/Weather";
        const string PresetProvenance =
            "AUTHORED: a game-level archetype after typical marine weather, not an observation. METAR cover classes and the " +
            "WMO fog and mist limits are from WMO No. 306 and No. 8 (from memory, verify).";

        // The six presets of slice 2 (E30, E51): name, cover, base, thickness, high cloud, visibility, obscuration top.
        static readonly (string name, double cover, double baseM, double thickM, double high, double visM, double topM)[] Presets =
        {
            ("Clear", 0, 1500, 1000, 0, 30000, 1000),
            ("Scattered", 3, 900, 1200, 1, 25000, 900),
            ("Broken", 6, 750, 1500, 2, 15000, 900),
            ("Overcast", 8, 450, 900, 0, 8000, 450),
            ("Haze", 1, 1500, 800, 3, 4000, 1200),
            ("Fog bank", 0, 1500, 1000, 0, 300, 120),
        };
        const string AppUiTheme = "Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI.tss";

        [MenuItem("Deep Water/METOC/Build Demo Scene")]
        static void BuildMenu() => Debug.Log(Build());

        [MenuItem("Deep Water/METOC/Play the Demo")]
        static void Play()
        {
            if (EditorApplication.isPlaying) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScene) == null) { Debug.LogError("[METOC] No demo scene yet: run Build Demo Scene first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "[METOC] Leave play mode first; nothing was built.";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SeaDemo) == null) return $"[METOC] {SeaDemo} is missing: run Deep Water → Sea State → Build Demo Scene first.";
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == DemoScene)
                    return "[METOC] The METOC demo is open: open another scene, then rebuild. Nothing was built.";

            var scenario = LoadOrCreate<MetocScenarioSO>(ScenarioAsset, out _);
            MetocWeatherSO[] presets = EnsureWeatherPresets(scenario);
            var illumination = LoadOrCreate<IlluminationProfileSO>(IlluminationAsset, out _);
            var look = LoadOrCreate<HdrpMetocLookSO>(LookAsset, out _);
            var panelSettings = BuildPanelSettings();

            Directory.CreateDirectory(Path.GetDirectoryName(DemoScene));
            AssetDatabase.Refresh();
            if (File.Exists(DemoScene))
            {
                // Overwrite in place so the scene keeps its GUID; the previous build is kept in Library.
                BackUpPrevious();
                File.Copy(SeaDemo, DemoScene, true);
                AssetDatabase.ImportAsset(DemoScene, ImportAssetOptions.ForceUpdate);
            }
            else if (!AssetDatabase.CopyAsset(SeaDemo, DemoScene)) return $"[METOC] Could not copy {SeaDemo} to {DemoScene}.";

            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);   // new objects go into the demo, not the scene the user has open
                Light sun = scene.GetRootGameObjects()
                    .SelectMany(g => g.GetComponentsInChildren<Light>(true))
                    .Where(l => l.type == LightType.Directional && l.GetComponent<HDAdditionalLightData>() != null)
                    .OrderByDescending(l => l.name.Contains("Sun"))
                    .FirstOrDefault();
                if (sun == null) return "[METOC] The Sea State demo has no HDRP directional light to drive; scene not saved.";

                var metocGo = new GameObject("METOC");
                var director = metocGo.AddComponent<MetocDirector>();
                Set(director, "scenario", scenario);
                Set(director, "illumination", illumination);

                var moonGo = new GameObject("Moon");
                moonGo.transform.SetParent(metocGo.transform, false);
                HDAdditionalLightData moonHd = moonGo.AddHDLight(LightType.Directional);
                Light moon = moonGo.GetComponent<Light>();
                moon.intensity = 0.3f;   // replaced every frame from the report; a plausible value for edit mode
                moon.shadows = LightShadows.None;
                moonHd.celestialBodyShadingSource = HDAdditionalLightData.CelestialBodyShadingSource.ReflectSunLight;
                moonHd.sunLightOverride = sun;
                moonHd.angularDiameter = 0.52f;

                var renderer = metocGo.AddComponent<HdrpMetocRenderer>();
                Set(renderer, "director", director);
                Set(renderer, "sun", sun);
                Set(renderer, "moon", moon);
                Set(renderer, "look", look);

                var panelGo = new GameObject("METOC Panel");
                panelGo.transform.SetParent(metocGo.transform, false);
                var doc = panelGo.AddComponent<UIDocument>();
                doc.panelSettings = panelSettings;
                var panel = panelGo.AddComponent<MetocPanel>();
                Set(panel, "director", director);
                Set(panel, "metocRenderer", renderer);
                SetArray(panel, "weatherPresets", presets);

                EditorSceneManager.SaveScene(scene, DemoScene);
                AssetDatabase.SaveAssets();
                return $"[METOC] Built {DemoScene} from the Sea State demo: director ({ScenarioAsset}), renderer driving '{sun.name}' " +
                       "and a new Moon light, overlay exposure and the App UI panel. Play it: Deep Water → METOC → Play the Demo.";
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// Makes the weather presets that are missing (one never made is filled in; one that exists is left alone) and
        /// gives the scenario a start weather if it has none.
        /// </summary>
        public static MetocWeatherSO[] EnsureWeatherPresets(MetocScenarioSO scenario)
        {
            var assets = new MetocWeatherSO[Presets.Length];
            for (int i = 0; i < Presets.Length; i++)
            {
                var p = Presets[i];
                var so = LoadOrCreate<MetocWeatherSO>($"{WeatherDir}/{p.name.Replace(" ", "")}.asset", out bool created);
                if (created)
                {
                    so.displayName = p.name;
                    so.coverOktas = p.cover;
                    so.cloudBaseM = p.baseM;
                    so.cloudThicknessM = p.thickM;
                    so.highCloudOktas = p.high;
                    so.visibilityM = p.visM;
                    so.obscurationTopM = p.topM;
                    so.provenance = PresetProvenance;
                    EditorUtility.SetDirty(so);
                }
                assets[i] = so;
            }
            if (scenario.startWeather == null)
            {
                scenario.startWeather = assets[0];
                EditorUtility.SetDirty(scenario);
            }
            AssetDatabase.SaveAssets();
            return assets;
        }

        static PanelSettings BuildPanelSettings()
        {
            var ps = LoadOrCreate<PanelSettings>(PanelSettingsAsset, out bool created);
            if (!created) return ps;
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AppUiTheme);
            if (theme == null) throw new FileNotFoundException("App UI theme missing (is com.unity.dt.app-ui installed?)", AppUiTheme);
            ps.themeStyleSheet = theme;
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.clearColor = false;
            EditorUtility.SetDirty(ps);
            return ps;
        }

        static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (!created) return asset;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.Refresh();
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void BackUpPrevious()
        {
            string dir = Path.Combine("Library", "MetocDemoBackups");
            Directory.CreateDirectory(dir);
            File.Copy(DemoScene, Path.Combine(dir, $"MetocDemo.pre-rebuild-backup-{DateTime.Now:yyyyMMdd-HHmm}.unity"), true);
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

        static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'");
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i] != null ? values[i] : throw new ArgumentNullException(field, $"preset {i} is missing");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
