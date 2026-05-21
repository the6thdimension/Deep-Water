#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Examples;
using GuidedFury.ScriptableObjects.Profiles;

namespace GuidedFury.Examples.Editor
{
    /// <summary>
    /// Editor menu command that generates the Missile Range test scene from scratch.
    ///
    /// Why this is an editor script and not a hand-authored `.unity` file: scene YAML is
    /// fragile (GUIDs everywhere), and the scene we want is purely composed of primitives,
    /// the test runner, and the missile system components — Unity itself can produce a clean
    /// scene file via the EditorSceneManager API.
    ///
    /// **Menu path:** Guided Fury → Build Missile Range Scene
    /// </summary>
    public static class MissileRangeSceneBuilder
    {
        private const string ScenePath = "Assets/Guided Fury/Examples/Scenes/MissileRange.unity";
        private const string ProfilePath = "Assets/Guided Fury/Examples/Profiles/Range_Default.asset";

        // Distance markers along +Z: where to place poles + labels.
        private static readonly float[] MarkerDistances = { 100f, 250f, 500f, 1000f, 2000f, 5000f };

        // Stationary boxes scattered downrange (x, z) pairs.
        private static readonly Vector2[] StaticBoxOffsets =
        {
            new Vector2(  0f,  300f),
            new Vector2( 50f,  600f),
            new Vector2(-80f, 1200f),
            new Vector2(150f, 1800f),
            new Vector2(-60f, 2500f),
        };

        [MenuItem("Guided Fury/Build Missile Range Scene")]
        public static void Build()
        {
            // 1. Ensure the default missile profile exists.
            var profile = EnsureDefaultProfile();

            // 2. Create a fresh empty scene (with default-light skybox), populate it, save it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            PopulateScene(profile);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);

            if (saved)
            {
                Debug.Log($"[GuidedFury] Missile Range scene generated at {ScenePath}. " +
                          "HUD top-left, control panel top-right. " +
                          "[L] fire missile  [K] fire salvo  [1-5] timescale  [P] pause  [R] reload  [F] camera toggle  [Space] cycle missile.");
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ScenePath);
            }
            else
            {
                Debug.LogError($"[GuidedFury] Failed to save scene to {ScenePath}.");
            }
        }

        // ===========================================================================
        // Scene population
        // ===========================================================================

        private static void PopulateScene(MissileProfileSO profile)
        {
            // Tweak the default camera + lighting for a wide test-range view.
            ConfigureCameraAndLight();

            // Build a flat ground plane large enough to host the longest marker (5 km).
            BuildGround();

            // Distance markers along the +Z axis.
            foreach (float distance in MarkerDistances)
                BuildDistanceMarker(distance);

            // Stationary hittable boxes at scattered positions. y = size.y / 2 so they sit on the ground.
            for (int i = 0; i < StaticBoxOffsets.Length; i++)
            {
                Vector2 offset = StaticBoxOffsets[i];
                BuildHittableBox(
                    name: $"Box_{i + 1}_{(int)offset.y}m",
                    position: new Vector3(offset.x, 1.5f, offset.y),
                    size: new Vector3(3f, 3f, 3f),
                    massKg: 200f);
            }

            // A small stack of crates near 750 m to give the missile something interesting to topple.
            BuildCrateStack(new Vector3(20f, 0f, 750f));

            // Moving targets.
            BuildMovingTarget(
                name: "MovingTarget_Crossing_1500m",
                center: new Vector3(0f, 5f, 1500f),
                driftAxis: new Vector3(120f, 0f, 0f),
                speedMps: 35f,
                phase: 0f);

            BuildMovingTarget(
                name: "MovingTarget_Crossing_3000m",
                center: new Vector3(0f, 10f, 3000f),
                driftAxis: new Vector3(200f, 0f, 0f),
                speedMps: 60f,
                phase: 0.5f);

            // Single-missile launcher: empty GameObject at origin with the test runner attached.
            BuildLauncher(profile);

            // LOD comparison launcher: fires one missile per LOD when the scene plays.
            BuildLodComparisonLauncher(profile);

            // HUD overlay + camera controller on the existing Main Camera.
            BuildHudAndCamera();
        }

        private static void ConfigureCameraAndLight()
        {
            var camGo = GameObject.Find("Main Camera");
            if (camGo != null)
            {
                // Position the camera up and behind the launch point, looking downrange.
                camGo.transform.position = new Vector3(-30f, 20f, -20f);
                camGo.transform.rotation = Quaternion.Euler(15f, 35f, 0f);
                var cam = camGo.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.farClipPlane = 10000f;  // see all the way to the 5 km marker
                    cam.nearClipPlane = 0.3f;
                }
            }

            var lightGo = GameObject.Find("Directional Light");
            if (lightGo != null)
            {
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        private static void BuildGround()
        {
            // 10 km × 10 km plane — Unity's default Plane is 10×10 m, so scale = 1000.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(1000f, 1f, 1000f);
            ground.transform.position = Vector3.zero;

            // Subtle green tint so it doesn't look like a white floor.
            var rend = ground.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = RangeMaterials.MakeColored(new Color(0.35f, 0.45f, 0.30f));
        }

        private static void BuildDistanceMarker(float distanceM)
        {
            var marker = new GameObject($"Marker_{distanceM:0}m");
            marker.transform.position = new Vector3(0f, 0f, distanceM);

            // Pole: a tall thin cylinder.
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(marker.transform, false);
            pole.transform.localPosition = new Vector3(0f, 4f, 0f);
            pole.transform.localScale = new Vector3(0.2f, 4f, 0.2f);
            Object.DestroyImmediate(pole.GetComponent<Collider>()); // no fuze trips on the pole

            // Sign: a small flat cube on top of the pole.
            var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "Sign";
            sign.transform.SetParent(marker.transform, false);
            sign.transform.localPosition = new Vector3(0f, 8.5f, 0f);
            sign.transform.localScale = new Vector3(8f, 2f, 0.15f);
            Object.DestroyImmediate(sign.GetComponent<Collider>());

            // Colour the sign brightly so it's visible at long range. 1 km marks yellow,
            // intermediate marks white.
            var rend = sign.GetComponent<Renderer>();
            if (rend != null)
            {
                Color signColor = (distanceM % 1000f == 0f) ? Color.yellow : Color.white;
                rend.sharedMaterial = RangeMaterials.MakeColored(signColor);
            }

            // Label: built-in TextMesh (no TextMeshPro dependency).
            var label = new GameObject("Label");
            label.transform.SetParent(marker.transform, false);
            label.transform.localPosition = new Vector3(0f, 8.5f, -0.1f);
            // Face the launcher (looking back toward -Z origin).
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var tm = label.AddComponent<TextMesh>();
            tm.text = distanceM >= 1000f ? $"{distanceM / 1000f:0.#} km" : $"{distanceM:0} m";
            tm.fontSize = 80;
            tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.black;
        }

        private static void BuildHittableBox(string name, Vector3 position, Vector3 size, float massKg)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = size;

            var rb = box.AddComponent<Rigidbody>();
            rb.mass = massKg;

            box.AddComponent<HittableBox>();

            // Muted orange tint for static boxes so they're distinct from markers.
            var rend = box.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = RangeMaterials.MakeColored(new Color(0.85f, 0.55f, 0.25f));
        }

        private static void BuildCrateStack(Vector3 basePosition)
        {
            // 3 × 2 × 2 stack of small crates.
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 pos = basePosition + new Vector3(x * 2.1f, 1f + y * 2.1f, z * 2.1f);
                BuildHittableBox(
                    name: $"Crate_{x}{y}{z}",
                    position: pos,
                    size: new Vector3(2f, 2f, 2f),
                    massKg: 50f);
            }
        }

        private static void BuildMovingTarget(string name, Vector3 center, Vector3 driftAxis, float speedMps, float phase)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.transform.position = center;
            target.transform.localScale = new Vector3(4f, 4f, 4f);

            var rb = target.AddComponent<Rigidbody>();
            rb.mass = 500f;
            rb.isKinematic = true;   // moved procedurally, not by physics

            target.AddComponent<HittableBox>();

            var mover = target.AddComponent<MovingTarget>();
            // We can't access private SerializeFields from here without reflection, so we
            // configure via a small editor-side reflective set. This is acceptable in editor
            // tooling — the goal is a one-shot generated scene, not runtime sets.
            var so = new SerializedObject(mover);
            so.FindProperty("driftAxis").vector3Value = driftAxis;
            so.FindProperty("speedMps").floatValue = speedMps;
            so.FindProperty("phaseOffset").floatValue = phase;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Bright cyan so moving targets are obvious.
            var rend = target.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = RangeMaterials.MakeColored(new Color(0.2f, 0.8f, 0.95f));
        }

        private static void BuildLauncher(MissileProfileSO profile)
        {
            var launcher = new GameObject("Launcher");
            launcher.transform.position = Vector3.zero;
            launcher.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            // Small reference marker so you can see the launcher in the scene view.
            // Sits on the ground at the launcher position.
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "LauncherMarker";
            marker.transform.SetParent(launcher.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            marker.transform.localScale = new Vector3(1.5f, 1f, 3f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            var rend = marker.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = RangeMaterials.MakeColored(Color.gray);

            // Attach the test runner and configure it via SerializedObject.
            var runner = launcher.AddComponent<GuidedFury_TestRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.FindProperty("lod").enumValueIndex = (int)MissileLod.L1_PointMass3Dof;
            so.FindProperty("launchPosition").vector3Value = new Vector3(0f, 1.5f, 2f);
            so.FindProperty("launchDirection").vector3Value = new Vector3(0f, 0.15f, 1f);
            so.FindProperty("launchDelay").floatValue = 1f;
            // target left null intentionally — user can drag a target Transform in if they
            // want homing. Default behavior is "fly straight downrange and impact whatever
            // gets in the way."
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildLodComparisonLauncher(MissileProfileSO profile)
        {
            // Sits beside the main launcher. Disabled by default so the scene doesn't fire
            // 4 extra missiles every time you press Play — user enables it when they want
            // the comparison demo.
            var launcher = new GameObject("LodComparisonLauncher");
            launcher.transform.position = new Vector3(15f, 0f, 0f);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "LauncherMarker";
            marker.transform.SetParent(launcher.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            marker.transform.localScale = new Vector3(3f, 1f, 3f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            var rend = marker.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = RangeMaterials.MakeColored(new Color(0.5f, 0.4f, 0.6f));

            var runner = launcher.AddComponent<LodComparisonRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.FindProperty("launchPosition").vector3Value = new Vector3(15f, 1.5f, 2f);
            so.FindProperty("launchDirection").vector3Value = new Vector3(0f, 0.15f, 1f);
            so.FindProperty("launchDelay").floatValue = 2.0f;
            so.FindProperty("spacingM").floatValue = 4f;
            so.FindProperty("includeL0").boolValue = true;
            so.FindProperty("includeL1").boolValue = true;
            so.FindProperty("includeL2").boolValue = true;
            so.FindProperty("includeL3").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Disabled by default — enable in Inspector to run the comparison.
            launcher.SetActive(false);
        }

        private static void BuildHudAndCamera()
        {
            var camGo = GameObject.Find("Main Camera");
            if (camGo == null) return;

            // HUD overlay — single component on the camera; no Canvas required.
            if (camGo.GetComponent<MissileHud>() == null)
                camGo.AddComponent<MissileHud>();

            // Camera controller — chase + overview.
            if (camGo.GetComponent<MissileCameraController>() == null)
                camGo.AddComponent<MissileCameraController>();

            // Control panel — in-play-mode fire buttons and timescale slider.
            if (camGo.GetComponent<MissileControlPanel>() == null)
                camGo.AddComponent<MissileControlPanel>();
        }

        // ===========================================================================
        // Default profile
        // ===========================================================================

        private static MissileProfileSO EnsureDefaultProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MissileProfileSO>(ProfilePath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));

            var profile = ScriptableObject.CreateInstance<MissileProfileSO>();
            profile.missileId        = "RANGE-1";
            profile.displayName      = "Range Default";
            profile.description      = "Generic test-range missile. Modest thrust, ProNav, ~3s burn.";
            profile.dryMassKg        = 80f;
            profile.propellantMassKg = 40f;
            profile.boostThrustN     = 22000f;
            profile.boostDurationS   = 3f;
            profile.cruiseSpeedMps   = 250f;
            profile.l0UseGravity     = false;
            profile.dragCoefficient  = 0.25f;
            profile.referenceAreaM2  = 0.03f;
            profile.guidanceLaw      = GuidanceLawKind.ProportionalNavigation;
            profile.navigationGain   = 3f;
            profile.maxLifetimeS     = 20f;
            profile.fuzeProximityRadiusM = 8f;
            profile.fuzeArmDelayS = 0.5f;

            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GuidedFury] Created default profile at {ProfilePath}.");
            return profile;
        }
    }
}
#endif
