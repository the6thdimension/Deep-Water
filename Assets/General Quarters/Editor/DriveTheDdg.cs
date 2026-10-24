using UnityEditor;
using UnityEngine;
using DeepWater.GeneralQuarters.Examples;
using DeepWater.GeneralQuarters.Modules.Propulsion;
using DeepWater.GeneralQuarters.ScriptableObjects;
using DeepWater.Sea.Modules.Buoyancy;

namespace DeepWater.GeneralQuarters.Editor
{
    /// <summary>
    /// Deep Water → General Quarters → Drive the DDG. In a scene with a Sea State sea (the Sea State demo), it
    /// enters play mode and puts the DDG in the water under way at 12 knots, with the conn (keyboard, mouse,
    /// Xbox and generic gamepads, through the project-wide "Conn" action map), the conn panel and a chase camera.
    /// Nothing is saved to the scene: it all goes when play stops. A stand-in until slice 5's range scene.
    /// </summary>
    [InitializeOnLoad]
    public static class DriveTheDdg
    {
        const string PendingKey = "DeepWater.GeneralQuarters.DriveTheDdg.Pending";
        public const float StartSpeedKn = 12f;
        static readonly Vector3 SpawnPosition = new Vector3(-600f, 0f, -900f);   // clear of the demo's hulls, heading north

        static DriveTheDdg() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("Deep Water/General Quarters/Drive the DDG")]
        public static void Drive()
        {
            if (Object.FindFirstObjectByType<BuoyancySystem>() == null)
            {
                Debug.LogError("[GQ] Drive the DDG needs a scene with a Sea State sea (a BuoyancySystem). " +
                               "Open Assets/Sea State/Examples/Scenes/SeaStateDemo.unity first.");
                return;
            }
            if (EditorApplication.isPlaying) { Spawn(); return; }
            SessionState.SetBool(PendingKey, true);   // survives the domain reload on entering play mode
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.EraseBool(PendingKey);
            Spawn();
        }

        /// <summary>Spawn the DDG under the conn. Play mode only. Returns the ship; if one is already under the conn, that one.</summary>
        public static GameObject Spawn()
        {
            // One conn at a time: a second would read the same controls and drive a second ship with them.
            var existing = Object.FindFirstObjectByType<ConnController>();
            if (existing != null && existing.Ship != null)
            {
                Selection.activeGameObject = existing.Ship.gameObject;
                Debug.Log("[GQ] Already driving the DDG; selected her.");
                return existing.Ship.gameObject;
            }

            var system = Object.FindFirstObjectByType<BuoyancySystem>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Ddg51HullBuilder.PrefabPath);
            var helm = AssetDatabase.LoadAssetAtPath<HelmProfileSO>(Ddg51HullBuilder.HelmAsset);
            if (system == null || prefab == null || helm == null)
            {
                Debug.LogError($"[GQ] Drive the DDG: sea {(system != null)}, prefab {(prefab != null)}, helm profile {(helm != null)}; run Build DDG-51 Hull first.");
                return null;
            }

            // Spawn inactive so the buoyancy system and the start speed are set before OnEnable and Start run.
            var hold = new GameObject("GQ spawn hold");
            hold.SetActive(false);
            GameObject ship = Object.Instantiate(prefab, SpawnPosition, Quaternion.identity, hold.transform);
            ship.name = "DDG-51 (General Quarters)";
            var buoyant = ship.GetComponent<BuoyantBody>();
            buoyant.Configure(system, buoyant.Hull);
            var drive = ship.GetComponent<ShipDrive>();
            var so = new SerializedObject(drive);
            so.FindProperty("startSpeedKn").floatValue = StartSpeedKn;
            so.ApplyModifiedPropertiesWithoutUndo();
            ship.transform.SetParent(null, true);
            Object.Destroy(hold);

            var conn = new GameObject("Conn");
            var input = conn.AddComponent<ConnInput>();
            var controller = conn.AddComponent<ConnController>();
            controller.Bind(drive, helm);
            conn.AddComponent<ConnPanel>();

            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                if (cam.GetComponent("SeaDemoCamera") is Behaviour demoCamera) demoCamera.enabled = false;   // it would fight the chase camera
                var chase = cam.GetComponent<ShipChaseCamera>() ?? cam.gameObject.AddComponent<ShipChaseCamera>();
                chase.Follow(ship.transform, input);
            }
            Selection.activeGameObject = ship;
            Debug.Log($"[GQ] DDG-51 under way at {StartSpeedKn:0} kn, steering 000. Conn: keyboard, mouse, Xbox and generic pads " +
                      "(map 'Conn' in Project Settings → Input System Package). Let go of the rudder and she steadies.");
            return ship;
        }
    }
}
