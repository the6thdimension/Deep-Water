using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.Modules.Cockpit;
using DeepWater.AirWing.Modules.Input;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Sea.Core;
using DeepWater.Sea.Editor;
using DeepWater.Sea.Modules.Hdrp;
using DeepWater.Sea.ScriptableObjects;

namespace DeepWater.AirWing.Editor
{
    /// <summary>
    /// Builds Air Wing's assets and scenes from code, so they can be regenerated and reviewed (W13).
    /// <list type="bullet">
    /// <item><b>Build F/A-18E Prefab</b>: the Air Wing jet. A new root carrying <see cref="AircraftView"/> and an
    /// instance of the F-18E model, with no vendor script and no Rigidbody (AP8, W30). The model and the FDS prefab
    /// are only read.</item>
    /// <item><b>Build Flight Test Scene</b>: the bare test scene (slice 1d): the Sea State sea, sky and sun, the player
    /// seat, the runner, one jet airborne at 1,500 m and 400 kt, the chase camera and the HUD.</item>
    /// </list>
    /// Each overwrites its file in place, so GUIDs survive (C24). Both refuse to run over unsaved scene changes.
    /// </summary>
    public static class AirWingBuilders
    {
        internal const string Root = "Assets/Air Wing";
        const string ModelPath = "Assets/_AIRCRAFT/F-18E/Blender/F-18E.blend";
        internal const string PrefabPath = Root + "/Prefabs/FA18E_AirWing.prefab";
        const string VisualsPath = Root + "/ScriptableObjects/FA18E_Visuals.asset";
        const string ProfilePath = Root + "/ScriptableObjects/FA18E_AirWing.asset";
        const string SheetPath = Root + "/ScriptableObjects/FA18E_FidelitySheet.asset";
        const string SeatSettingsPath = Root + "/ScriptableObjects/PlayerSeatSettings.asset";
        const string ActionsPath = Root + "/Modules/Input/AirWingFlight.inputactions";
        const string FlightTestScenePath = Root + "/Examples/Scenes/AirWing_FlightTest.unity";

        // The Sea State demo's sea (Assets/Sea State/Examples/Editor/SeaStateDemoSceneBuilder.cs), reused as it is.
        const string OceanPrefabPath = "Assets/_ENVIRONMENT/Ocean System.prefab";
        internal const string SunPrefabPath = "Assets/_ENVIRONMENT/Sun.prefab";
        internal const string SkyPrefabPath = "Assets/_ENVIRONMENT/Sky and Fog Volume.prefab";
        const string SeaLookPath = "Assets/Sea State/Examples/Settings/HdrpSeaLook.asset";
        const string SeaDecalMaterialPath = "Assets/Sea State/Modules/Hdrp/Materials/SeaWaveDecal.mat";
        const string SeaFoamComputePath = "Assets/Sea State/Modules/Hdrp/Shaders/SeaFoam.compute";

        [MenuItem("Deep Water/Air Wing/Build F-A-18E Prefab")]
        public static void BuildPrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) { Debug.LogError($"[Air Wing] {ModelPath} not found; nothing built."); return; }
            AircraftVisualsSO visuals = EnsureVisuals();

            var root = new GameObject("FA18E_AirWing");
            try
            {
                var view = root.AddComponent<AircraftView>();
                Set(view, "visuals", visuals);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = "Model";
                instance.transform.SetParent(root.transform, false);

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) { Debug.LogError($"[Air Wing] Saving {PrefabPath} failed."); return; }
                Debug.Log($"[Air Wing] Built {PrefabPath}: AircraftView + the F-18E model ({model.GetComponentsInChildren<Renderer>(true).Length} renderers), no vendor scripts.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [MenuItem("Deep Water/Air Wing/Build Flight Test Scene")]
        public static void BuildFlightTestScene()
        {
            if (AnyDirtyScene()) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) BuildPrefab();

            // Load assets only after the scene switch (AP12).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!RigAssetsPresent()) return;

            Instantiate(SunPrefabPath);
            Instantiate(SkyPrefabPath);
            SeaStateDirector sea = BuildSea();
            BuildRig(sea, new Vector3(0f, 1500f, 0f), 0f);

            Directory.CreateDirectory(Path.GetDirectoryName(FlightTestScenePath));
            EditorSceneManager.SaveScene(scene, FlightTestScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Air Wing] Built {FlightTestScenePath}: Sea State sea, player seat, runner, one F/A-18E at 1,500 m and 400 kt, chase camera, HUD.");
        }

        /// <summary>The flight rig every Air Wing scene shares, and what it returns to the scene builder.</summary>
        internal struct Rig
        {
            public AirWingRunner Runner;
            public FlightHud Hud;
        }

        /// <summary>True when the prefab, profile, fidelity sheet, seat settings and input actions all load; logs otherwise.</summary>
        internal static bool RigAssetsPresent()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null && AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(ProfilePath) != null &&
                AssetDatabase.LoadAssetAtPath<FidelitySheetSO>(SheetPath) != null && AssetDatabase.LoadAssetAtPath<PlayerSeatSettingsSO>(SeatSettingsPath) != null &&
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath) != null)
                return true;
            Debug.LogError("[Air Wing] A required asset is missing (prefab, profile, fidelity sheet, seat settings or input actions); nothing saved.");
            return false;
        }

        /// <summary>
        /// The player seat, the runner on the sea, one F/A-18E airborne at <paramref name="jetPosition"/> on
        /// <paramref name="headingDeg"/> (its view's start speed), the chase camera and the HUD. Call after the scene
        /// switch (AP12) and after <see cref="RigAssetsPresent"/>.
        /// </summary>
        internal static Rig BuildRig(SeaStateDirector sea, Vector3 jetPosition, float headingDeg)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var inputGo = new GameObject("Player Seat");
            var seat = inputGo.AddComponent<PlayerSeatInput>();
            Set(seat, "actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            Set(seat, "settings", AssetDatabase.LoadAssetAtPath<PlayerSeatSettingsSO>(SeatSettingsPath));

            var runnerGo = new GameObject("Air Wing");
            var runner = runnerGo.AddComponent<AirWingRunner>();
            Set(runner, "profile", AssetDatabase.LoadAssetAtPath<AircraftProfileSO>(ProfilePath));
            Set(runner, "fidelitySheet", AssetDatabase.LoadAssetAtPath<FidelitySheetSO>(SheetPath));
            Set(runner, "playerSeat", seat);
            Set(runner, "sea", sea);

            Quaternion heading = Quaternion.Euler(0f, headingDeg, 0f);
            var jet = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            jet.transform.SetPositionAndRotation(jetPosition, heading);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 30000f;
            camGo.AddComponent<HDAdditionalCameraData>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.SetPositionAndRotation(jetPosition + heading * new Vector3(0f, 6f, -26f), heading);
            var chase = camGo.AddComponent<ChaseCamera>();
            Set(chase, "runner", runner);
            var hud = camGo.AddComponent<FlightHud>();
            Set(hud, "runner", runner);
            Set(hud, "viewCamera", cam);
            return new Rig { Runner = runner, Hud = hud };
        }

        /// <summary>The F/A-18E's moving parts, copied from the FDS prefab's AerialArcade surface settings (read only).</summary>
        static AircraftVisualsSO EnsureVisuals()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<AircraftVisualsSO>(VisualsPath);
            if (visuals != null) return visuals;
            visuals = ScriptableObject.CreateInstance<AircraftVisualsSO>();
            visuals.surfaces = new[]
            {
                Surface("Aileron_Left", SurfaceDriver.Roll, new Vector3(0f, 0f, -1f), 10f, 2f),
                Surface("Aileron_Right", SurfaceDriver.Roll, new Vector3(0f, 0f, 1f), 10f, 2f),
                Surface("ELEVATOR_LEFT_1", SurfaceDriver.Pitch, new Vector3(1f, 0f, 0f), 10f, 5f),
                Surface("ELEVATOR_RIGHT_1", SurfaceDriver.Pitch, new Vector3(1f, 0f, 0f), 10f, 5f),
                Surface("rudder_left", SurfaceDriver.Pedals, new Vector3(0f, 0f, -1f), 30f, 2f),
                Surface("rudder_right", SurfaceDriver.Pedals, new Vector3(0f, 0f, -1f), 30f, 2f),
                Surface("Flaps_Left", SurfaceDriver.Flaps, new Vector3(0f, 0f, 1f), 15f, 1f),
                Surface("Flaps_Right", SurfaceDriver.Flaps, new Vector3(0f, 0f, 1f), 15f, 1f),
            };
            visuals.provenance = "Copied 2026-10-25 from Assets/FDS Assets/Aircraft/F-18E.prefab's AerialArcade ControlSurface settings " +
                                 "(transform names, axes, max angles, smoothing). Rudder travel AUTHORED at 30° (the FDS prefab used 50°). " +
                                 "Presentation only; the flight core never reads it.";
            Directory.CreateDirectory(Path.GetDirectoryName(VisualsPath));
            AssetDatabase.CreateAsset(visuals, VisualsPath);
            AssetDatabase.SaveAssets();
            return visuals;
        }

        static AircraftVisualsSO.Surface Surface(string name, SurfaceDriver driver, Vector3 axis, float maxDeg, float smoothing) =>
            new AircraftVisualsSO.Surface { transformName = name, driver = driver, axis = axis, maxAngleDeg = maxDeg, smoothing = smoothing };

        /// <summary>The Sea State demo's sea: HDRP water with the Ocean System's look, the director and the renderer.</summary>
        internal static SeaStateDirector BuildSea()
        {
            var seaGo = new GameObject("Sea");
            var surface = seaGo.AddComponent<WaterSurface>();
            var ocean = AssetDatabase.LoadAssetAtPath<GameObject>(OceanPrefabPath);
            var oceanSurface = ocean != null ? ocean.GetComponent<WaterSurface>() : null;
            if (oceanSurface != null) EditorUtility.CopySerialized(oceanSurface, surface);
            else Debug.LogWarning($"[Air Wing] {OceanPrefabPath} not found; using WaterSurface defaults.");

            var director = seaGo.AddComponent<SeaStateDirector>();
            var library = AssetDatabase.LoadAssetAtPath<SeaStateLibrarySO>(DouglasProfileBuilder.LibraryPath);
            if (library == null) Debug.LogError($"[Air Wing] {DouglasProfileBuilder.LibraryPath} not found: run Deep Water → Sea State → Build Douglas Profiles.");
            Set(director, "library", library);

            var renderer = seaGo.AddComponent<HdrpSeaRenderer>();
            Set(renderer, "director", director);
            Set(renderer, "look", AssetDatabase.LoadAssetAtPath<HdrpSeaLookSO>(SeaLookPath));
            Set(renderer, "waveDecalMaterial", AssetDatabase.LoadAssetAtPath<Material>(SeaDecalMaterialPath));
            Set(renderer, "foamCompute", AssetDatabase.LoadAssetAtPath<ComputeShader>(SeaFoamComputePath));
            return director;
        }

        internal static void Instantiate(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning($"[Air Wing] {path} not found; skipped."); return; }
            PrefabUtility.InstantiatePrefab(prefab);
        }

        internal static bool AnyDirtyScene()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("[Air Wing] An open scene has unsaved changes; save it first. Nothing was built.");
                    return true;
                }
            return false;
        }

        internal static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Air Wing] {target.GetType().Name} has no field '{field}'."); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
