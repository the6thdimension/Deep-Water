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

            // Mobile TEL: military truck with the articulated launcher rig parented to its
            // cargo bed. The 'Launcher' GameObject inside it carries the LauncherRig +
            // TestRunner, so existing systems that look up "Launcher" by name still work.
            BuildMobileLauncher(profile);
            BuildSalvoLauncher(profile);
            BuildScenarioPicker();

            // LOD comparison launcher: fires one missile per LOD when the scene plays.
            BuildLodComparisonLauncher(profile);

            // HUD overlay + camera controller on the existing Main Camera.
            BuildHudAndCamera();

            // Flight-stick input pipeline (reader + HUD + probe + adapters), wired to the
            // launcher, test runner, and camera built above. Skipped if the .inputactions
            // asset is missing (so the scene still builds without DeepWater.Input present).
            BuildFlightStickInput();
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

        private static void BuildLauncher(MissileProfileSO profile, Transform parent = null, bool includeBasePlinth = true)
        {
            // Articulated launcher: Base → Turret (yaw) → ElevationArm (pitch) → Rails + Muzzle.
            // Procedurally built from primitives so the scene-builder is reproducible. A real
            // production launcher would be an authored prefab with a proper FBX model, but
            // for the test range this gets the visual and articulation right.
            //
            // When `parent` is supplied, the launcher is mounted to that anchor (e.g. the
            // cargo bed of a TEL truck) at local zero. When `includeBasePlinth` is false the
            // ground plinth is omitted — the truck bed serves as the base.

            var launcher = new GameObject("Launcher");
            if (parent != null)
            {
                launcher.transform.SetParent(parent, false);
                launcher.transform.localPosition = Vector3.zero;
                launcher.transform.localRotation = Quaternion.identity;
            }
            else
            {
                launcher.transform.position = Vector3.zero;
                launcher.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            }

            // -- Base: wide squat plinth on the ground.
            if (includeBasePlinth)
                BuildLauncherCube(launcher.transform, "Base", new Vector3(0f, 0.5f, 0f),
                                  new Vector3(4f, 1f, 4f), LauncherBaseColor);

            // -- Turret: rotates around Y. Visualised as a stout ring + small cab.
            var turret = new GameObject("Turret");
            turret.transform.SetParent(launcher.transform, false);
            turret.transform.localPosition = new Vector3(0f, 1.1f, 0f);

            BuildLauncherCylinder(turret.transform, "TurretRing", Vector3.zero,
                                  new Vector3(2.4f, 0.3f, 2.4f), LauncherTurretColor);
            BuildLauncherCube(turret.transform, "TurretCab", new Vector3(0f, 0.7f, -0.6f),
                              new Vector3(1.4f, 1.2f, 1.6f), LauncherTurretColor);

            // -- Elevation arm: rotates around X relative to the turret. Pivot at REAR so
            //    all visual children extend forward and the arm rotates around its base.
            var elevation = new GameObject("ElevationArm");
            elevation.transform.SetParent(turret.transform, false);
            elevation.transform.localPosition = new Vector3(0f, 0.85f, 0.2f);

            // Cradle visual (between the rails).
            BuildLauncherCube(elevation.transform, "Cradle", new Vector3(0f, 0f, 1.2f),
                              new Vector3(0.6f, 0.4f, 2.4f), LauncherArmColor);

            // Two parallel rails (slim cylinders along +Z), offset to either side.
            BuildLauncherRail(elevation.transform, "Rail_Left",  new Vector3(-0.45f, 0.25f, 1.6f), 3.4f);
            BuildLauncherRail(elevation.transform, "Rail_Right", new Vector3( 0.45f, 0.25f, 1.6f), 3.4f);

            // Muzzle anchor — empty transform at the forward end of the rails. Missiles
            // spawn here with this rotation; the rig keeps the muzzle aimed.
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(elevation.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.25f, 3.3f);
            muzzle.transform.localRotation = Quaternion.identity;

            // -- LauncherRig component: configure it with the just-built child transforms.
            var rig = launcher.AddComponent<LauncherRig>();
            var soRig = new SerializedObject(rig);
            soRig.FindProperty("turret").objectReferenceValue = turret.transform;
            soRig.FindProperty("elevation").objectReferenceValue = elevation.transform;
            soRig.FindProperty("muzzle").objectReferenceValue = muzzle.transform;
            soRig.FindProperty("startTraverseDeg").floatValue = 0f;
            soRig.FindProperty("startElevationDeg").floatValue = 10f;
            soRig.ApplyModifiedPropertiesWithoutUndo();

            // -- TestRunner: bound to the muzzle so trigger fires in whatever direction the
            //    launcher is aimed.
            var runner = launcher.AddComponent<GuidedFury_TestRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.FindProperty("lod").enumValueIndex = (int)MissileLod.L1_PointMass3Dof;
            so.FindProperty("muzzleTransform").objectReferenceValue = muzzle.transform;
            so.FindProperty("launchDelay").floatValue = 1f;
            // Disable auto-launch — user fires via trigger or the in-play control panel.
            so.FindProperty("autoLaunchOnStart").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // -- Launcher color palette (military gray-green, distinguishes parts visually) ----
        private static readonly Color LauncherBaseColor   = new Color(0.30f, 0.35f, 0.30f);
        private static readonly Color LauncherTurretColor = new Color(0.40f, 0.45f, 0.40f);
        private static readonly Color LauncherArmColor    = new Color(0.55f, 0.55f, 0.45f);
        private static readonly Color LauncherRailColor   = new Color(0.20f, 0.20f, 0.20f);

        private static GameObject BuildLauncherCube(Transform parent, string name,
                                                    Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // launcher parts don't collide with missiles
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = RangeMaterials.MakeColored(color);
            return go;
        }

        private static GameObject BuildLauncherCylinder(Transform parent, string name,
                                                        Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = RangeMaterials.MakeColored(color);
            return go;
        }

        /// <summary>
        /// Build a slim cylinder oriented along +Z (a rail). Unity primitives are Y-oriented
        /// by default, so we rotate 90° around X to lay it along Z.
        /// </summary>
        private static GameObject BuildLauncherRail(Transform parent, string name,
                                                    Vector3 localPos, float lengthM)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            // Cylinder's local Y is its long axis. Rotate so long axis points along +Z.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Cylinder default is 2 m tall (in its own Y). Scale to (diameter, half-length, diameter).
            go.transform.localScale    = new Vector3(0.18f, lengthM * 0.5f, 0.18f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = RangeMaterials.MakeColored(LauncherRailColor);
            return go;
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

        private static void BuildSalvoLauncher(MissileProfileSO profile)
        {
            // Sister to LodComparisonLauncher — same place idea, different launcher. Spawns
            // a salvo of N missiles at one LOD with staggered timing. Disabled by default so
            // pressing Play doesn't fire two demos at once.
            var launcher = new GameObject("SalvoLauncher");
            launcher.transform.position = new Vector3(-15f, 0f, 0f);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "LauncherMarker";
            marker.transform.SetParent(launcher.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            marker.transform.localScale = new Vector3(3f, 1f, 3f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            var rend = marker.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = RangeMaterials.MakeColored(new Color(0.6f, 0.4f, 0.3f));

            var salvo = launcher.AddComponent<SalvoRunner>();
            var so = new SerializedObject(salvo);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.FindProperty("lod").enumValueIndex = (int)MissileLod.L2_RateLimited3Dof;
            so.FindProperty("count").intValue = 4;
            so.FindProperty("interShotDelayS").floatValue = 0.4f;
            so.FindProperty("launchPosition").vector3Value = new Vector3(-15f, 1.5f, 2f);
            so.FindProperty("launchDirection").vector3Value = new Vector3(0f, 0.15f, 1f);
            so.FindProperty("launchDelay").floatValue = 2.5f;
            so.FindProperty("lateralSpacingM").floatValue = 1.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            launcher.SetActive(false);
        }

        private static void BuildScenarioPicker()
        {
            // Single picker for the scene — drives target swap from the control panel.
            // Uses the main Launcher GameObject as anchor so scenarios spawn relative to it.
            var existing = GameObject.Find("ScenarioPicker");
            if (existing != null) return;

            var pickerGo = new GameObject("ScenarioPicker");
            pickerGo.transform.position = Vector3.zero;
            var scenarios = pickerGo.AddComponent<EngagementScenarios>();

            // Anchor on the main Launcher if present.
            var mainLauncher = GameObject.Find("Launcher");
            if (mainLauncher != null)
            {
                var so = new SerializedObject(scenarios);
                so.FindProperty("anchor").objectReferenceValue = mainLauncher.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void BuildHudAndCamera()
        {
            var camGo = GameObject.Find("Main Camera");
            if (camGo == null) return;

            // HUD overlay — single component on the camera; no Canvas required.
            if (camGo.GetComponent<MissileHud>() == null)
                camGo.AddComponent<MissileHud>();

            // Camera controller — chase + overview (in-flight missile camera).
            if (camGo.GetComponent<MissileCameraController>() == null)
                camGo.AddComponent<MissileCameraController>();

            // Control panel — in-play-mode fire buttons and timescale slider.
            if (camGo.GetComponent<MissileControlPanel>() == null)
                camGo.AddComponent<MissileControlPanel>();

            // Comparison stats overlay — bottom-left, tracks per-missile telemetry.
            if (camGo.GetComponent<LodComparisonStats>() == null)
                camGo.AddComponent<LodComparisonStats>();

            // Target selector + bounding box renderer + launcher-camera controller.
            // These compose into the "operator's station" view: pick a target, see a red
            // box around it, view it from the launcher in any of 5 modes, zoom in.
            var selector = camGo.GetComponent<TargetSelector>() ?? camGo.AddComponent<TargetSelector>();
            if (camGo.GetComponent<TargetBoundingBoxRenderer>() == null)
                camGo.AddComponent<TargetBoundingBoxRenderer>();
            var launcherCam = camGo.GetComponent<LauncherCameraController>() ?? camGo.AddComponent<LauncherCameraController>();

            // Bind the launcher transform on the controller so it knows where to anchor.
            var launcher = GameObject.Find("Launcher");
            if (launcher != null)
            {
                var so = new SerializedObject(launcherCam);
                so.FindProperty("launcher").objectReferenceValue = launcher.transform;
                so.FindProperty("controlledCamera").objectReferenceValue = camGo.GetComponent<Camera>();
                so.FindProperty("targetSelector").objectReferenceValue = selector;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ===========================================================================
        // Mobile TEL (Transporter Erector Launcher)
        // ===========================================================================

        // Military olive-drab palette — keeps the truck visually distinct from the
        // gray-green launcher parts while reading as "field vehicle."
        private static readonly Color TruckBodyColor   = new Color(0.27f, 0.32f, 0.21f);
        private static readonly Color TruckCabColor    = new Color(0.23f, 0.28f, 0.18f);
        private static readonly Color TruckBedColor    = new Color(0.22f, 0.25f, 0.17f);
        private static readonly Color TruckTireColor   = new Color(0.08f, 0.08f, 0.08f);
        private static readonly Color TruckGlassColor  = new Color(0.10f, 0.15f, 0.20f);

        /// <summary>
        /// Build a procedural military TEL truck and mount the articulated launcher on its
        /// cargo bed. The truck is a static stand-in (no driving) so the launcher has the
        /// visual of "missile system on a mobile platform" without dragging in the vehicle
        /// physics. Geometry: chassis + cab + hood + cargo bed + 6 wheels (2-axle cab, 4-axle
        /// rear bogie — i.e. 3 axles total, 6 wheels — typical TEL silhouette).
        ///
        /// **Parenting:** the existing `Launcher` GameObject (with LauncherRig + TestRunner)
        /// is created as a child of `LauncherMount` on the truck bed. Other systems that
        /// `GameObject.Find("Launcher")` still resolve to it (Find searches all roots).
        /// </summary>
        private static void BuildMobileLauncher(MissileProfileSO profile)
        {
            var truck = new GameObject("MobileLauncher");
            truck.transform.position = Vector3.zero;
            truck.transform.rotation = Quaternion.identity;

            // Dimensions (rough Oshkosh HEMTT / MAZ-543 silhouette):
            //   total length ~10 m, width ~2.5 m, cab forward, long flat bed behind.
            // Origin is at the rear axle so the launcher (mounted on bed) sits near the
            // scene's world origin — keeps existing markers/targets aligned downrange.
            const float wheelRadius = 0.55f;
            const float chassisY    = wheelRadius + 0.35f; // top of chassis above ground
            const float bedTopY     = chassisY + 0.30f;    // top of cargo bed
            const float cabLengthZ  = 2.4f;
            const float bedLengthZ  = 6.0f;
            const float widthX      = 2.5f;

            // -- Chassis: long flat box that ties cab and bed together.
            BuildLauncherCube(truck.transform, "Chassis",
                              new Vector3(0f, chassisY, 0.5f),
                              new Vector3(widthX - 0.2f, 0.30f, cabLengthZ + bedLengthZ + 0.5f),
                              TruckBodyColor);

            // -- Cab: forward of the bed. Truck "forward" is -Z so the launcher fires
            //    over the rear of the vehicle, which matches the +Z target field.
            // Wait — easier to align launcher forward with truck forward. We point the
            // truck forward = +Z (cab forward, launcher fires forward over the cab).
            // Actually, real TELs typically erect missile straight up; here the launcher
            // can traverse 360° so cab orientation doesn't matter much. We put the cab at
            // +Z (downrange) so the operator sees "truck driving toward targets."
            float cabCenterZ = (bedLengthZ * 0.5f) + (cabLengthZ * 0.5f) + 0.4f;
            BuildLauncherCube(truck.transform, "Cab",
                              new Vector3(0f, chassisY + 0.95f, cabCenterZ),
                              new Vector3(widthX, 1.7f, cabLengthZ),
                              TruckCabColor);

            // Cab windshield (visual only) — a darker panel on the front.
            BuildLauncherCube(truck.transform, "Windshield",
                              new Vector3(0f, chassisY + 1.4f, cabCenterZ + cabLengthZ * 0.5f - 0.05f),
                              new Vector3(widthX - 0.2f, 0.8f, 0.05f),
                              TruckGlassColor);

            // Hood: short box in front of the cab (housing the engine).
            BuildLauncherCube(truck.transform, "Hood",
                              new Vector3(0f, chassisY + 0.55f, cabCenterZ + cabLengthZ * 0.5f + 0.6f),
                              new Vector3(widthX - 0.4f, 0.9f, 1.2f),
                              TruckCabColor);

            // -- Cargo bed: the deck the launcher sits on.
            float bedCenterZ = -bedLengthZ * 0.0f + 0.2f - 0.5f; // centred slightly behind origin
            BuildLauncherCube(truck.transform, "CargoBed",
                              new Vector3(0f, bedTopY, bedCenterZ),
                              new Vector3(widthX, 0.20f, bedLengthZ),
                              TruckBedColor);

            // Low side walls along the bed — visual "this carries a payload."
            BuildLauncherCube(truck.transform, "BedSide_Left",
                              new Vector3(-(widthX * 0.5f - 0.10f), bedTopY + 0.30f, bedCenterZ),
                              new Vector3(0.10f, 0.50f, bedLengthZ),
                              TruckBedColor);
            BuildLauncherCube(truck.transform, "BedSide_Right",
                              new Vector3( (widthX * 0.5f - 0.10f), bedTopY + 0.30f, bedCenterZ),
                              new Vector3(0.10f, 0.50f, bedLengthZ),
                              TruckBedColor);

            // -- Wheels: 3 axles × 2 sides = 6 wheels. One axle forward (under cab), two
            //    axles in a rear bogie (under bed). Z positions chosen to match cab/bed.
            float[] axleZ = {
                cabCenterZ - 0.2f,                         // front axle (under cab)
                bedCenterZ + bedLengthZ * 0.5f - 1.0f,     // rear axle 1 (forward end of bed)
                bedCenterZ - bedLengthZ * 0.5f + 1.0f,     // rear axle 2 (aft end of bed)
            };
            float wheelX = widthX * 0.5f - 0.05f;
            int wheelIdx = 0;
            foreach (float z in axleZ)
            {
                BuildTruckWheel(truck.transform, $"Wheel_{++wheelIdx}_L", new Vector3(-wheelX, wheelRadius, z), wheelRadius);
                BuildTruckWheel(truck.transform, $"Wheel_{++wheelIdx}_R", new Vector3( wheelX, wheelRadius, z), wheelRadius);
            }

            // -- Launcher mount: anchor on the bed where the launcher attaches. We give it
            //    a clear name so scene editors can see what's load-bearing.
            var mount = new GameObject("LauncherMount");
            mount.transform.SetParent(truck.transform, false);
            mount.transform.localPosition = new Vector3(0f, bedTopY + 0.10f, bedCenterZ);
            mount.transform.localRotation = Quaternion.identity;

            // -- Build the launcher rig as a child of the mount (no base plinth — the bed
            //    is the base). The existing BuildLauncher creates a GameObject named
            //    "Launcher" which is what other scene systems look up by name.
            BuildLauncher(profile, parent: mount.transform, includeBasePlinth: false);
        }

        private static void BuildTruckWheel(Transform parent, string name, Vector3 localPos, float radius)
        {
            // Unity's Cylinder is Y-aligned and 2 m tall. Rotate 90° around Z so the wheel's
            // axis runs along X (the truck's left-right). Scale to (radius, half-width, radius).
            var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = name;
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = localPos;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // localScale.y is the half-length along the cylinder's own Y (= world X after the rotation).
            wheel.transform.localScale = new Vector3(radius * 2f, 0.30f, radius * 2f);
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
            var rend = wheel.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = RangeMaterials.MakeColored(TruckTireColor);
        }

        // ===========================================================================
        // Flight-stick input pipeline
        // ===========================================================================

        private const string FlightStickInputActionsPath = "Assets/Scripts/Input/VelocityOne.inputactions";

        /// <summary>
        /// Auto-build the VelocityOne input pipeline: one `_Input` GameObject hosting the
        /// reader, HUD, probe, and every adapter (test runner / launcher rig / launcher
        /// camera). Skipped silently if the `DeepWater.Input` assembly or the
        /// `.inputactions` asset isn't present — the scene still builds, the user just
        /// doesn't get stick input.
        ///
        /// **Why reflection and not direct types:** keeping the editor asmdef independent of
        /// DeepWater.Input would mean no compile-time dependency. We *do* have the asmdef
        /// reference, but reflection on the asset path lets us no-op gracefully when the
        /// asset has been moved or deleted, instead of throwing in the build pipeline.
        /// </summary>
        private static void BuildFlightStickInput()
        {
            var actionsAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                FlightStickInputActionsPath);
            if (actionsAsset == null)
            {
                Debug.LogWarning(
                    $"[GuidedFury] Flight-stick auto-setup skipped — no InputActionAsset at " +
                    $"'{FlightStickInputActionsPath}'. Build the scene anyway; wire input manually if you want stick control.");
                return;
            }

            var root = new GameObject("_Input");
            root.transform.position = Vector3.zero;

            // Reader: the one source of truth for stick state.
            var reader = root.AddComponent<DeepWater.Input.FlightStickInput>();
            var soReader = new SerializedObject(reader);
            soReader.FindProperty("inputActions").objectReferenceValue = actionsAsset;
            soReader.ApplyModifiedPropertiesWithoutUndo();

            // Diagnostic overlays — cheap, leave them on by default so first-time users can
            // see at a glance whether the stick is being captured. They draw nothing if the
            // reader isn't enabled.
            root.AddComponent<DeepWater.Input.FlightStickHud>();
            root.AddComponent<DeepWater.Input.FlightStickProbe>();

            // Adapters — each one self-discovers the reader + its target on enable.
            root.AddComponent<DeepWater.Input.Adapters.FlightStickToTestRunner>();
            root.AddComponent<DeepWater.Input.Adapters.FlightStickToLauncherRig>();
            root.AddComponent<DeepWater.Input.Adapters.FlightStickToLauncherCamera>();

            Debug.Log("[GuidedFury] Flight-stick input pipeline created on '_Input' GameObject.");
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
