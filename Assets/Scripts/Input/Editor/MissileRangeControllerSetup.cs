#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using GuidedFury.Examples;
using DeepWater.Input.Adapters;

namespace DeepWater.Input.Editor
{
    public static class MissileRangeControllerSetup
    {
        [MenuItem("Deep Water/Input/Setup Missile Range Controllers")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "MissileRange" || EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Open MissileRange in Edit Mode first.");
            var truck = GameObject.Find("MobileLauncher");
            var root = GameObject.Find("_Input");
            if (truck == null || root == null) throw new System.InvalidOperationException("Missing range truck or input root.");
            var profile = AssetDatabase.LoadAssetAtPath<TruckDriveProfile>("Assets/Scripts/Input/MissileRangeTruck.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<TruckDriveProfile>();
                AssetDatabase.CreateAsset(profile, "Assets/Scripts/Input/MissileRangeTruck.asset");
            }
            var body = truck.GetComponent<Rigidbody>();
            if (body == null) body = Undo.AddComponent<Rigidbody>(truck);
            Undo.RecordObject(body, "Configure truck physics");
            body.mass = profile.massKg;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (var name in new[] { "Chassis", "Cab", "CargoBed" })
            {
                var part = truck.transform.Find(name);
                if (part != null && part.GetComponent<BoxCollider>() == null) Undo.AddComponent<BoxCollider>(part.gameObject);
            }
            var driver = truck.GetComponent<XboxTruckDriver>();
            if (driver == null) driver = Undo.AddComponent<XboxTruckDriver>(truck);
            var so = new SerializedObject(driver);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Scripts/Input/TruckDriving.inputactions");
            var wheelProps = so.FindProperty("wheels"); wheelProps.arraySize = 6;
            var visuals = so.FindProperty("wheelVisuals"); visuals.arraySize = 6;
            for (int i = 0; i < 6; i++)
            {
                var visual = truck.transform.Find("Wheel_" + (i + 1) + (i % 2 == 0 ? "_L" : "_R"));
                var wheelName = "DriveWheel_" + (i + 1);
                var holder = truck.transform.Find(wheelName);
                if (holder == null)
                {
                    var go = new GameObject(wheelName); Undo.RegisterCreatedObjectUndo(go, "Create truck wheel");
                    holder = go.transform; holder.SetParent(truck.transform, false);
                }
                // Half travel above the visual center puts the resting spring near its midpoint.
                holder.localPosition = visual.localPosition + Vector3.up * profile.suspensionDistance * 0.5f;
                var wheel = holder.GetComponent<WheelCollider>();
                if (wheel == null) wheel = Undo.AddComponent<WheelCollider>(holder.gameObject);
                Undo.RecordObject(wheel, "Configure wheel");
                wheel.radius = profile.wheelRadius;
                wheel.suspensionDistance = profile.suspensionDistance;
                var spring = wheel.suspensionSpring;
                spring.spring = profile.suspensionSpring; spring.damper = profile.suspensionDamper; spring.targetPosition = 0.5f;
                wheel.suspensionSpring = spring;
                wheelProps.GetArrayElementAtIndex(i).objectReferenceValue = wheel;
                visuals.GetArrayElementAtIndex(i).objectReferenceValue = visual;
            }
            so.ApplyModifiedProperties();
            var reader = root.GetComponent<FlightStickInput>();
            var rig = Object.FindFirstObjectByType<LauncherRig>();
            var selector = Object.FindFirstObjectByType<TargetSelector>();
            var camera = Object.FindFirstObjectByType<LauncherCameraController>();
            Set(reader, "inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Scripts/Input/VelocityOne.inputactions"));
            Set(root.GetComponent<FlightStickToLauncherRig>(), "reader", reader);
            Set(root.GetComponent<FlightStickToLauncherRig>(), "rig", rig);
            Set(root.GetComponent<FlightStickToTestRunner>(), "reader", reader);
            Set(root.GetComponent<FlightStickToTestRunner>(), "targetSelector", selector);
            Set(root.GetComponent<FlightStickToLauncherCamera>(), "reader", reader);
            Set(root.GetComponent<FlightStickToLauncherCamera>(), "targetSelector", selector);
            Set(root.GetComponent<FlightStickToLauncherCamera>(), "launcherCamera", camera);
            Set(camera, "targetSelector", selector);
            Set(camera, "launcher", truck.transform);
            var fire = new SerializedObject(root.GetComponent<FlightStickToTestRunner>());
            fire.FindProperty("holdSecondsToFire").floatValue = 0.25f;
            fire.ApplyModifiedProperties();
            var probe = root.GetComponent<FlightStickProbe>();
            if (probe != null) { Undo.RecordObject(probe, "Disable raw input overlay"); probe.enabled = false; }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("MissileRange: VelocityOne launcher and Xbox six-wheel truck configured and saved.");
        }
        private static void Set(Object target, string field, Object value)
        {
            if (target == null || value == null) throw new System.InvalidOperationException("Missing controller wiring: " + field);
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
#endif

