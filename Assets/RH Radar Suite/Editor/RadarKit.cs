using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RHRadarSuite.Examples;

namespace RHRadarSuite.EditorTools
{
    /// <summary>
    /// One-click radar outfitting (ROADMAP Phase 1). Programmatic asset builder
    /// in the Guided Fury pattern: default profiles are created as assets on
    /// demand, and any selected GameObject can be fitted with a fully configured
    /// radar (controller + default-LOD module + diagnostics) in one menu action.
    /// Attach is idempotent — running it twice never duplicates components, and
    /// Inspector tuning on existing modules survives.
    /// </summary>
    public static class RadarKit
    {
        public const string ProfileFolder = "Assets/RH Radar Suite/ScriptableObjects/Profiles";
        public const string ExampleScenePath = "Assets/RH Radar Suite/Examples/RadarKitExample.unity";

        // ----- Profile presets -------------------------------------------------

        /// <summary>
        /// Load the default profile asset for a role, creating it (and its
        /// folder) if missing.
        /// </summary>
        public static RadarProfileSO EnsureProfile(RadarRole role)
        {
            string assetPath = $"{ProfileFolder}/{role}Profile.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RadarProfileSO>(assetPath);
            if (existing != null) return existing;

            EnsureFolder(ProfileFolder);

            RadarProfileSO profile = ScriptableObject.CreateInstance<RadarProfileSO>();
            ConfigurePreset(profile, role);
            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RadarKit] Created default profile: {assetPath}");
            return profile;
        }

        private static void ConfigurePreset(RadarProfileSO p, RadarRole role)
        {
            p.role = role;
            p.targetLayers = ~0;
            p.detectionThreshold = 0.1f;

            switch (role)
            {
                case RadarRole.GroundSearch:
                    p.notes = "Ground-based surveillance radar. Medium range, steady rotation, surface-to-low-air coverage.";
                    p.maxDetectionRangeM = 8000f;
                    p.radarPower = 1.5f;
                    p.beamWidthDeg = 20f;
                    p.rotationRpm = 12f;
                    p.fullRotation = true;
                    p.minElevationDeg = 0f;
                    p.maxElevationDeg = 30f;
                    p.updateInterval = 0.25f;
                    p.rangeAccuracyM = 15f;
                    p.defaultLOD = RadarLOD.LOD2_BasicRadar;
                    break;

                case RadarRole.AirSearch:
                    p.notes = "Ground-based air search radar. Long range, slow rotation, high elevation coverage, Doppler for movers.";
                    p.maxDetectionRangeM = 20000f;
                    p.radarPower = 3f;
                    p.beamWidthDeg = 10f;
                    p.rotationRpm = 6f;
                    p.fullRotation = true;
                    p.minElevationDeg = 0f;
                    p.maxElevationDeg = 75f;
                    p.updateInterval = 0.5f;
                    p.rangeAccuracyM = 30f;
                    p.defaultLOD = RadarLOD.LOD3_DopplerRadar;
                    break;

                case RadarRole.NavalSurfaceSearch:
                    p.notes = "Shipboard surface search radar. Sea-surface picture, fast rotation, low elevation fan.";
                    p.maxDetectionRangeM = 12000f;
                    p.radarPower = 2f;
                    p.beamWidthDeg = 15f;
                    p.rotationRpm = 20f;
                    p.fullRotation = true;
                    p.minElevationDeg = -5f;
                    p.maxElevationDeg = 20f;
                    p.updateInterval = 0.2f;
                    p.rangeAccuracyM = 10f;
                    p.defaultLOD = RadarLOD.LOD2_BasicRadar;
                    break;

                case RadarRole.FireControl:
                    p.notes = "Fire-control/track radar. Narrow beam, sector stare, tight accuracy, 3D tracking.";
                    p.maxDetectionRangeM = 15000f;
                    p.radarPower = 4f;
                    p.beamWidthDeg = 3f;
                    p.rotationRpm = 30f;
                    p.fullRotation = false;
                    p.sectorSizeDeg = 60f;
                    p.sectorCenterDeg = 0f;
                    p.minElevationDeg = -5f;
                    p.maxElevationDeg = 80f;
                    p.updateInterval = 0.05f;
                    p.rangeAccuracyM = 3f;
                    p.defaultLOD = RadarLOD.LOD4_3DTracking;
                    break;

                case RadarRole.AirborneIntercept:
                    p.notes = "Nose-mounted airborne intercept radar. Forward sector only, body-relative, Doppler for look-down movers.";
                    p.maxDetectionRangeM = 18000f;
                    p.radarPower = 2.5f;
                    p.beamWidthDeg = 8f;
                    p.rotationRpm = 25f;
                    p.fullRotation = false;
                    p.sectorSizeDeg = 120f;
                    p.sectorCenterDeg = 0f;
                    p.minElevationDeg = -60f;
                    p.maxElevationDeg = 40f;
                    p.updateInterval = 0.1f;
                    p.rangeAccuracyM = 20f;
                    p.defaultLOD = RadarLOD.LOD3_DopplerRadar;
                    break;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ----- Attach ----------------------------------------------------------

        /// <summary>
        /// Fit a GameObject with a configured radar: controller + the profile's
        /// default LOD module + diagnostics gizmos, profile applied. Idempotent.
        /// </summary>
        public static RadarSuiteController AttachRadar(GameObject go, RadarProfileSO profile)
        {
            if (go == null || profile == null) return null;

            RadarSuiteController controller = GetOrAdd<RadarSuiteController>(go);
            EnsureModuleFor(go, profile.defaultLOD);
            GetOrAdd<RadarDiagnostics>(go);

            Undo.RecordObject(controller, "Apply Radar Profile");
            controller.ApplyProfile(profile);
            EditorUtility.SetDirty(controller);
            foreach (var module in go.GetComponents<RadarLODModuleBase>())
            {
                EditorUtility.SetDirty(module);
            }

            Debug.Log($"[RadarKit] {go.name} fitted with {profile.role} radar ({profile.maxDetectionRangeM:F0} m, LOD {profile.defaultLOD})");
            return controller;
        }

        private static void EnsureModuleFor(GameObject go, RadarLOD lod)
        {
            switch (lod)
            {
                case RadarLOD.LOD1_PassiveDetection: GetOrAdd<PassiveDetectionModule>(go); break;
                case RadarLOD.LOD2_BasicRadar: GetOrAdd<BasicRadarModule>(go); break;
                case RadarLOD.LOD3_DopplerRadar: GetOrAdd<DopplerRadarModule>(go); break;
                case RadarLOD.LOD4_3DTracking: GetOrAdd<ThreeDTrackingModule>(go); break;
                case RadarLOD.LOD5_HighFidelity: GetOrAdd<HighFidelityModule>(go); break;
            }
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }

        // ----- Menu items ------------------------------------------------------

        [MenuItem("RH Navy Sims/Radar Suite/Create Default Profiles")]
        public static void CreateDefaultProfiles()
        {
            foreach (RadarRole role in System.Enum.GetValues(typeof(RadarRole)))
            {
                EnsureProfile(role);
            }
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(ProfileFolder));
        }

        private const string AddMenuRoot = "GameObject/RH Navy Sims/Radar Suite/";

        [MenuItem(AddMenuRoot + "Add Ground Search Radar", false, 10)]
        public static void AddGroundSearch() => AttachToSelection(RadarRole.GroundSearch);

        [MenuItem(AddMenuRoot + "Add Air Search Radar", false, 11)]
        public static void AddAirSearch() => AttachToSelection(RadarRole.AirSearch);

        [MenuItem(AddMenuRoot + "Add Naval Surface Search Radar", false, 12)]
        public static void AddNavalSurfaceSearch() => AttachToSelection(RadarRole.NavalSurfaceSearch);

        [MenuItem(AddMenuRoot + "Add Fire Control Radar", false, 13)]
        public static void AddFireControl() => AttachToSelection(RadarRole.FireControl);

        [MenuItem(AddMenuRoot + "Add Airborne Intercept Radar", false, 14)]
        public static void AddAirborneIntercept() => AttachToSelection(RadarRole.AirborneIntercept);

        [MenuItem(AddMenuRoot + "Add Ground Search Radar", true)]
        [MenuItem(AddMenuRoot + "Add Air Search Radar", true)]
        [MenuItem(AddMenuRoot + "Add Naval Surface Search Radar", true)]
        [MenuItem(AddMenuRoot + "Add Fire Control Radar", true)]
        [MenuItem(AddMenuRoot + "Add Airborne Intercept Radar", true)]
        public static bool ValidateAttach() => Selection.activeGameObject != null;

        private static void AttachToSelection(RadarRole role)
        {
            GameObject go = Selection.activeGameObject;
            if (go == null) return;
            AttachRadar(go, EnsureProfile(role));
        }

        // ----- Example scene ---------------------------------------------------

        [MenuItem("RH Navy Sims/Radar Suite/Build Example Scene")]
        public static void BuildExampleScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Ground
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(400f, 1f, 400f); // 4 km x 4 km

            // Ground search radar on a tower
            GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Ground Radar Tower";
            tower.transform.position = new Vector3(0f, 10f, 0f);
            tower.transform.localScale = new Vector3(4f, 20f, 4f);
            AttachRadar(tower, EnsureProfile(RadarRole.GroundSearch));
            SetAutoActivate(tower);

            // Naval surface search radar on a "ship"
            GameObject ship = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ship.name = "Picket Ship";
            ship.transform.position = new Vector3(900f, 8f, 600f);
            ship.transform.localScale = new Vector3(20f, 16f, 90f);
            AttachRadar(ship, EnsureProfile(RadarRole.NavalSurfaceSearch));
            SetAutoActivate(ship);

            // Airborne intercept radar on an orbiting aircraft
            GameObject aircraft = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            aircraft.name = "Patrol Aircraft";
            aircraft.transform.position = new Vector3(-800f, 300f, -400f);
            aircraft.transform.localScale = new Vector3(4f, 4f, 12f);
            CircleFlyer flyer = aircraft.AddComponent<CircleFlyer>();
            flyer.radiusM = 700f;
            flyer.degreesPerSecond = 6f;
            AttachRadar(aircraft, EnsureProfile(RadarRole.AirborneIntercept));
            SetAutoActivate(aircraft);

            // Targets with varied signatures
            CreateTarget("Truck Convoy Lead", new Vector3(1200f, 2f, -300f), 2f, RadarMaterialType.Metal, 0f, false);
            CreateTarget("Fishing Boat", new Vector3(1500f, 2f, 1400f), 1.5f, RadarMaterialType.Wood, 0f, false);
            CreateTarget("Stealth Drone", new Vector3(-500f, 250f, 900f), 0.6f, RadarMaterialType.StealthMaterial, 0.8f, false);
            GameObject jammer = CreateTarget("EW Jammer Site", new Vector3(-1400f, 4f, -1100f), 3f, RadarMaterialType.Metal, 0f, true);

            // Orbiting air target so every radar has something moving to watch
            GameObject bandit = CreateTarget("Bandit", new Vector3(400f, 350f, -900f), 2.5f, RadarMaterialType.Metal, 0.1f, false);
            CircleFlyer banditFlyer = bandit.AddComponent<CircleFlyer>();
            banditFlyer.radiusM = 1200f;
            banditFlyer.degreesPerSecond = -5f;

            EnsureFolder("Assets/RH Radar Suite/Examples");
            EditorSceneManager.SaveScene(scene, ExampleScenePath);
            Debug.Log($"[RadarKit] Example scene built at {ExampleScenePath}. Enter Play mode and watch the Scene view gizmos. Jammer '{jammer.name}' is visible to the passive LOD.");
        }

        private static void SetAutoActivate(GameObject radarGo)
        {
            var so = new SerializedObject(radarGo.GetComponent<RadarSuiteController>());
            so.FindProperty("activateOnStart").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateTarget(
            string name, Vector3 position, float size,
            RadarMaterialType material, float stealthCoating, bool jamming)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * Mathf.Max(1f, size * 2f);

            RadarSignature sig = go.AddComponent<RadarSignature>();
            sig.Size = size;
            sig.MaterialType = material;
            sig.StealthCoating = stealthCoating;
            if (jamming)
            {
                sig.IsJamming = true;
                sig.JammingStrength = 0.6f;
                sig.JammingType = JammingType.Noise;
            }
            return go;
        }
    }
}
