#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using DeepWater.Input;

namespace DeepWater.Input.Editor
{
    /// <summary>
    /// One-click scene setup for the flight stick. Finds or creates a `_Input` GameObject in
    /// the active scene, adds <see cref="FlightStickInput"/> + <see cref="FlightStickHud"/> +
    /// <see cref="FlightStickProbe"/>, and assigns the `VelocityOne.inputactions` asset to
    /// the reader via `SerializedObject` (the same path the Inspector uses, so undo + dirty
    /// tracking work properly).
    ///
    /// **Why this is a menu command and not a runtime auto-wire:** the Input Actions asset
    /// reference must be persisted into the scene file (or it's lost on next open). Setting
    /// it from runtime code would leak a dirty asset modification on every Play. The menu
    /// command applies the reference at edit time, marks the scene dirty, and the user
    /// saves normally.
    ///
    /// **Menu paths:**
    /// - `Deep Water/Input/Setup Flight Stick In Current Scene` — wires everything.
    /// </summary>
    public static class FlightStickSceneSetup
    {
        private const string AssetPath = "Assets/Scripts/Input/VelocityOne.inputactions";
        private const string GameObjectName = "_Input";

        [MenuItem("Deep Water/Input/Setup Flight Stick In Current Scene")]
        public static void SetupInCurrentScene()
        {
            // 1. Resolve the action map asset.
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog(
                    "Setup Aborted",
                    $"Could not find the action map asset at:\n  {AssetPath}\n\n" +
                    "Make sure the file exists and Unity has imported it (try right-click → Reimport).",
                    "OK");
                Debug.LogError($"[FlightStickSetup] No InputActionAsset at {AssetPath}.");
                return;
            }

            // 2. Find or create the _Input GameObject in the active scene.
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog(
                    "Setup Aborted",
                    "No active scene. Open a scene first, then run this command again.",
                    "OK");
                return;
            }

            GameObject root = FindRootByName(scene, GameObjectName);
            bool createdNew = false;
            if (root == null)
            {
                root = new GameObject(GameObjectName);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                createdNew = true;
            }

            // 3. Add the reader if missing.
            var reader = root.GetComponent<FlightStickInput>() ?? root.AddComponent<FlightStickInput>();

            // 4. Assign the asset via SerializedObject so the change is properly tracked
            //    (dirties the scene, supports Undo, plays nice with prefab overrides).
            var so = new SerializedObject(reader);
            var prop = so.FindProperty("inputActions");
            if (prop == null)
            {
                Debug.LogError("[FlightStickSetup] Could not find 'inputActions' SerializedProperty on FlightStickInput. " +
                               "Did the field name change?");
                return;
            }
            prop.objectReferenceValue = asset;
            so.ApplyModifiedProperties();

            // 5. Add HUD and Probe so the user can see what's working without further setup.
            if (root.GetComponent<FlightStickHud>() == null)
                root.AddComponent<FlightStickHud>();
            if (root.GetComponent<FlightStickProbe>() == null)
                root.AddComponent<FlightStickProbe>();

            // 6. Mark dirty + select.
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            string verb = createdNew ? "Created" : "Reused";
            Debug.Log(
                $"[FlightStickSetup] ✓ Done. {verb} '{GameObjectName}' GameObject in scene '{scene.name}'.\n" +
                $"  • FlightStickInput: action map asset = {asset.name}\n" +
                $"  • FlightStickHud: live axis overlay (top-left)\n" +
                $"  • FlightStickProbe: raw device overlay (right side)\n" +
                "Save the scene (Ctrl+S), then press Play. The HUD should show your stick axes responding.");
        }

        private static GameObject FindRootByName(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root != null && root.name == name)
                    return root;
            return null;
        }
    }
}
#endif
