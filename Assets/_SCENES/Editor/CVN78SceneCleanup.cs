#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepWater.SceneTooling.Editor
{
    /// <summary>
    /// One-shot cleanup pass for the CVN-78 FORD scene. Performs the destructive structural
    /// changes from the scene audit (delete _RCCSceneManager, delete broken VR hierarchy and
    /// orphaned Cube, reorganize root hierarchy into _Environment / _Carrier / _AirWing /
    /// _Nav / _Camera / _Systems).
    ///
    /// **Why a menu command instead of editing the .unity YAML directly:** Unity manages
    /// fileIDs and SceneRoots correctly when you use the scene API. Hand-edited YAML risks
    /// dangling references, duplicate fileIDs, and SceneRoots drift — fragile. This script
    /// is idempotent (safe to re-run; missing objects are skipped) and auditable.
    ///
    /// **Menu path:** Deep Water → CVN-78 → Run Cleanup Pass
    ///
    /// What it does (matches the audit items the user agreed to):
    ///  - #1 Delete `_RCCSceneManager` (irrelevant car-physics scene manager)
    ///  - #2/3 Delete the entire `VR` hierarchy (HurricaneVR refs are broken; also catches
    ///        the inactive `Cube` placeholder which lives under VR)
    ///  - #7 Reorganize the flat root list into 6 logical parents
    ///
    /// Direct field edits already applied to the scene YAML (no script needed):
    ///  - #4 Rename "Cat 1" → "Waypoint_Approach"
    ///  - #5 Main Camera HDR + TAA enabled
    ///  - #6 F-18 idle audio spatialized (Spatialize=1, spatial blend = 1)
    ///  - #15 CarrierController Animator triggers cached as StringToHash
    ///  - #27 Sun shadow resolution bumped 512 → 2048
    ///  - #8 Scenes README updated
    /// </summary>
    public static class CVN78SceneCleanup
    {
        private const string ScenePath = "Assets/_SCENES/CVN-78 FORD.unity";

        // The reorganization plan — name of new parent → list of root GameObject names to
        // re-parent under it. Names that don't exist in the scene are silently skipped.
        // Keep this table the single source of truth; if you add a new root in the scene
        // that needs a logical home, add it here.
        private static readonly Dictionary<string, string[]> ReorganizationPlan = new()
        {
            { "_Environment", new[] { "Sun", "Sky and Fog Volume", "StaticLightingSky", "Ocean", "Ocean (1)" } },
            { "_Carrier",     new[] { "Scene Objects" /* legacy holder of CVN78 prefab */ } },
            // F-18E prefab instances filled in dynamically by name-independent prefab-source lookup
            // further down in ReorganizeHierarchy. Start with an empty slot for them here.
            { "_AirWing",     new string[0] },
            { "_Nav",         new[] { "FlightWaypoints", "FlightWaypoint", "TestWaypoint" } },
            { "_Camera",      new[] { "Main Camera" } },
            { "_Systems",     new[] { "LaunchController", "BurstCollisionWorld" } },
        };

        [MenuItem("Deep Water/CVN-78/Run Cleanup Pass")]
        public static void RunCleanup()
        {
            Scene scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                Debug.LogError($"[CVN78 Cleanup] Could not open {ScenePath}. Aborting.");
                return;
            }

            int deleted = 0;
            int reparented = 0;

            // -- Pass 1: deletions ------------------------------------------------
            deleted += DeleteByName(scene, "_RCCSceneManager");
            // VR was an empty root with a disabled subtree of broken HurricaneVR prefabs +
            // the orphan Cube. Killing the root takes everything with it.
            deleted += DeleteByName(scene, "VR");

            // -- Pass 2: reorganization -----------------------------------------
            reparented += ReorganizeHierarchy(scene);

            // -- Save -------------------------------------------------------------
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            if (!saved)
            {
                Debug.LogError("[CVN78 Cleanup] Scene save FAILED. Changes are in the editor but not persisted.");
                return;
            }

            Debug.Log($"[CVN78 Cleanup] Done. Deleted {deleted} root(s); reparented {reparented} root(s) into logical groups.");
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static Scene EnsureSceneOpen()
        {
            Scene active = EditorSceneManager.GetActiveScene();
            if (active.IsValid() && active.path == ScenePath)
                return active;

            // Save anything currently open before swapping scenes.
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                bool ok = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                if (!ok)
                {
                    Debug.LogWarning("[CVN78 Cleanup] User declined to save current scene; aborting.");
                    return default;
                }
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static int DeleteByName(Scene scene, string rootName)
        {
            GameObject go = FindRootByName(scene, rootName);
            if (go == null)
            {
                Debug.Log($"[CVN78 Cleanup] Root '{rootName}' not found — skipping (already cleaned, or never present).");
                return 0;
            }
            Object.DestroyImmediate(go);
            Debug.Log($"[CVN78 Cleanup] Deleted '{rootName}' and its children.");
            return 1;
        }

        /// <summary>
        /// Create the 6 logical-group parents (if absent) and re-parent existing roots into
        /// them per <see cref="ReorganizationPlan"/>. F-18 prefab instances are auto-detected
        /// (their names vary based on prefab modifications) and routed to _AirWing.
        /// </summary>
        private static int ReorganizeHierarchy(Scene scene)
        {
            // Cache existing roots so we don't iterate the live tree mid-mutation.
            GameObject[] roots = scene.GetRootGameObjects();

            // 1. Create the 6 parents (or reuse if they already exist from a previous run).
            var parents = new Dictionary<string, Transform>();
            foreach (var kv in ReorganizationPlan)
            {
                var existing = FindRootByName(scene, kv.Key);
                if (existing != null)
                {
                    parents[kv.Key] = existing.transform;
                    continue;
                }
                var go = new GameObject(kv.Key);
                SceneManager.MoveGameObjectToScene(go, scene);
                parents[kv.Key] = go.transform;
            }

            // 2. Re-parent named roots from the plan.
            int count = 0;
            foreach (var kv in ReorganizationPlan)
            {
                Transform parent = parents[kv.Key];
                foreach (string name in kv.Value)
                {
                    GameObject child = FindRootByName(scene, name);
                    if (child == null) continue;
                    if (child.transform.parent == parent) continue; // already there
                    Undo.SetTransformParent(child.transform, parent, $"Reparent {name} → {kv.Key}");
                    child.transform.SetParent(parent, true);
                    count++;
                }
            }

            // 3. Auto-detect F-18 prefab instances by checking the prefab source of each
            //    remaining root. They're recognised by the F-18E prefab GUID.
            const string F18EPrefabPath = "Assets/FDS Assets/Aircraft/F-18E.prefab";
            var f18ePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(F18EPrefabPath);
            if (f18ePrefab != null)
            {
                Transform airWing = parents["_AirWing"];
                foreach (GameObject root in roots)
                {
                    if (root == null) continue;
                    if (root.transform.parent != null) continue; // already moved
                    var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
                    if (src == null) continue;
                    if (AssetDatabase.GetAssetPath(src) != F18EPrefabPath) continue;
                    Undo.SetTransformParent(root.transform, airWing, $"Reparent F-18E → _AirWing");
                    root.transform.SetParent(airWing, true);
                    count++;
                }
            }
            else
            {
                Debug.LogWarning($"[CVN78 Cleanup] Could not find {F18EPrefabPath} — F-18 instances not auto-grouped.");
            }

            return count;
        }

        private static GameObject FindRootByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root != null && root.name == name)
                    return root;
            return null;
        }
    }
}
#endif
