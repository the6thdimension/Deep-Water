#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using CarrierOps.Core.Carrier;
using CarrierOps.ScriptableObjects.Profiles;

namespace CarrierOps.Examples.Editor
{
    /// <summary>
    /// CVN-78 carrier migration toolkit. Two operations:
    ///
    ///  1. **Migrate Carrier to New Framework** — modifies the CVN-78 prefab in place:
    ///     - Adds `CarrierBehaviour` to the prefab root.
    ///     - Reads the legacy `CarrierController`'s field references (Elevator1..3, BS1..4,
    ///       Shuttle1..4, Aircraft1) via reflection (no compile-time dep on legacy code).
    ///     - Populates the new behaviour's `catapultSlots` and `elevatorSlots` arrays from
    ///       the extracted references.
    ///     - Creates generated child GameObjects under the prefab root for the things the
    ///       legacy didn't have but the new system needs: shuttle end-points (one per cat),
    ///       FLOLS reference point, wire centerlines (one per wire). Generated transforms
    ///       are placed at reasonable defaults and named `_Generated_*` so you can find and
    ///       reposition them.
    ///     - Ensures a `CarrierProfileSO` asset exists at the standard path; creates a
    ///       Ford-class one if not.
    ///     - Removes the legacy `CarrierController` from the prefab. Does NOT delete or
    ///       archive the script file — that's the second command, run after verification.
    ///
    ///  2. **Archive Legacy CarrierController** — separate destructive operation. Moves the
    ///     legacy `CarrierController.cs` (and editor file) to `Assets/_Archive/Carrier_legacy/`,
    ///     preserving .meta GUIDs. Run only after verifying the migrated prefab works.
    ///
    /// **Idempotent.** Re-running migration on an already-migrated prefab will skip the
    /// per-step work (the behaviour is already there, the generated transforms already
    /// exist). Safe to re-run if you tweak the legacy and want to re-sync.
    /// </summary>
    public static class CVN78MigrationHelper
    {
        private const string PrefabPath      = "Assets/FDS Assets/Naval Vessels/CVN-78/Prefabs/CVN78.prefab";
        private const string ProfilePath     = "Assets/Carrier Ops/ScriptableObjects/Profiles/CVN78_Default.asset";
        private const string LegacyScriptDir = "Assets/FDS Assets/Naval Vessels/CVN-78/Scripts/Carrier Controller";
        private const string ArchiveDir      = "Assets/_Archive/Carrier_legacy/Carrier Controller";

        private const string GeneratedRootName = "_CarrierOps_Generated";

        // ===========================================================================
        // Menu 1 — Migrate
        // ===========================================================================

        [MenuItem("Deep Water/CVN-78/Migrate Carrier to New Framework")]
        public static void Migrate()
        {
            if (!File.Exists(PrefabPath))
            {
                Debug.LogError($"[CVN-78 Migration] Prefab not found at {PrefabPath}. Aborting.");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "CVN-78 Carrier Migration",
                "This will modify the CVN-78 prefab in place:\n" +
                "  • Add CarrierBehaviour configured from the legacy fields\n" +
                "  • Create _Generated_* child transforms for FLOLS, wire centerlines, and catapult shuttle endpoints\n" +
                "  • Remove the legacy CarrierController component\n\n" +
                "Recommended: commit your project to source control first.\n\n" +
                "Proceed?",
                "Migrate",
                "Cancel");
            if (!proceed) return;

            // 1. Ensure profile asset exists.
            CarrierProfileSO profile = EnsureProfileAsset();

            // 2. Open the prefab for editing.
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError("[CVN-78 Migration] Could not load prefab contents.");
                return;
            }

            try
            {
                // 3. Find legacy controller via reflection (no compile-time dep on the legacy script).
                MonoBehaviour legacy = FindLegacyController(prefabRoot);

                // Idempotence check: if there's no legacy AND CarrierBehaviour is already present,
                // migration has already happened. Don't overwrite the user's tuned slots with
                // empty-LegacyData null refs.
                bool alreadyMigrated = legacy == null
                                    && prefabRoot.GetComponentInChildren<CarrierBehaviour>(true) != null;
                if (alreadyMigrated)
                {
                    Debug.Log("[CVN-78 Migration] Already migrated — no legacy controller and CarrierBehaviour already present. No changes.");
                    return;
                }

                // 4. Extract legacy field refs (or zeros if missing).
                LegacyData data = legacy != null
                    ? ExtractLegacyData(legacy)
                    : LegacyData.Empty;

                if (legacy == null)
                    Debug.LogWarning("[CVN-78 Migration] No legacy CarrierController found, but no existing CarrierBehaviour either. Adding fresh CarrierBehaviour with default slots — you'll need to wire references manually.");
                else
                    Debug.Log("[CVN-78 Migration] Legacy CarrierController detected. Extracting field references.");

                // 5. Add or find CarrierBehaviour on the root.
                CarrierBehaviour behaviour = prefabRoot.GetComponentInChildren<CarrierBehaviour>(true);
                if (behaviour == null)
                {
                    behaviour = prefabRoot.AddComponent<CarrierBehaviour>();
                    Debug.Log("[CVN-78 Migration] Added CarrierBehaviour to prefab root.");
                }

                // 6. Generate child transforms we need (idempotent — reuses any pre-existing _Generated_*).
                Transform genRoot = EnsureGeneratedRoot(prefabRoot);
                Transform[] shuttleEnds = EnsureShuttleEnds(genRoot, data);
                Transform flolsRef = EnsureFlolsReference(genRoot);
                Transform[] wireCenters = EnsureWireCenterlines(genRoot, profile.wireCount);

                // 7. Configure the behaviour via SerializedObject.
                ConfigureBehaviour(behaviour, profile, data, shuttleEnds, flolsRef, wireCenters);

                // 8. Remove the legacy component (keep the script file for now).
                if (legacy != null)
                {
                    Object.DestroyImmediate(legacy, true);
                    Debug.Log("[CVN-78 Migration] Removed legacy CarrierController component from prefab.");
                }

                // 9. Save the prefab.
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                Debug.Log("[CVN-78 Migration] Migration complete. Saved prefab.");

                Debug.Log(
                    "[CVN-78 Migration] Next steps:\n" +
                    "  1. Open the CVN78 prefab and inspect the new CarrierBehaviour.\n" +
                    $"  2. Reposition the generated transforms under '{GeneratedRootName}' — especially:\n" +
                    "       _Generated_FLOLS_Reference (place at the lens housing, port side of angled deck)\n" +
                    "       _Generated_Wire_1..4 (place across the touchdown zone, ~12m apart)\n" +
                    "       _Generated_Shuttle_End_1..4 (verify they're at the forward end of each catapult)\n" +
                    "  3. Play the scene, exercise the launch panel + TailhookHook to verify behavior.\n" +
                    "  4. Once verified, run Deep Water/CVN-78/Archive Legacy CarrierController to retire the legacy script.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ===========================================================================
        // Menu 2 — Archive legacy script
        // ===========================================================================

        [MenuItem("Deep Water/CVN-78/Archive Legacy CarrierController")]
        public static void ArchiveLegacy()
        {
            if (!Directory.Exists(LegacyScriptDir))
            {
                Debug.LogWarning($"[CVN-78 Archive] No legacy script directory at {LegacyScriptDir} — nothing to archive.");
                return;
            }

            // Sanity check: refuse to archive if the legacy controller is still referenced on the prefab.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                MonoBehaviour legacy = FindLegacyController(prefab);
                if (legacy != null)
                {
                    EditorUtility.DisplayDialog(
                        "Archive Aborted",
                        "The CVN-78 prefab still references CarrierController. Run the Migrate command first, then re-run Archive.",
                        "OK");
                    return;
                }
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Archive Legacy CarrierController",
                $"This will move\n  {LegacyScriptDir}\nto\n  {ArchiveDir}\n\n" +
                "GUIDs are preserved (the .meta files travel with the scripts). Proceed?",
                "Archive",
                "Cancel");
            if (!proceed) return;

            Directory.CreateDirectory(Path.GetDirectoryName(ArchiveDir));
            string err = AssetDatabase.MoveAsset(LegacyScriptDir, ArchiveDir);
            if (!string.IsNullOrEmpty(err))
                Debug.LogError($"[CVN-78 Archive] Move failed: {err}");
            else
                Debug.Log($"[CVN-78 Archive] Moved {LegacyScriptDir} → {ArchiveDir}. Legacy script preserved with GUIDs intact.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ===========================================================================
        // Legacy reflection
        // ===========================================================================

        /// <summary>
        /// Bag of values extracted from the legacy CarrierController. Refs are scoped to
        /// the prefab being edited (so they're prefab-relative, not scene-relative).
        /// </summary>
        private struct LegacyData
        {
            public Transform[]  Elevators;       // 3
            public GameObject[] BS;              // 4
            public GameObject[] Shuttles;        // 4
            public GameObject   Aircraft1;
            public float        ElevatorRange;
            public float        CatapultRange;

            public static LegacyData Empty => new LegacyData
            {
                Elevators = new Transform[3],
                BS = new GameObject[4],
                Shuttles = new GameObject[4],
                Aircraft1 = null,
                ElevatorRange = 8f,
                CatapultRange = 94f,
            };
        }

        private static MonoBehaviour FindLegacyController(GameObject root)
        {
            var all = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var c in all)
            {
                if (c == null) continue;
                if (c.GetType().FullName?.EndsWith("CarrierController") == true)
                    return c;
            }
            return null;
        }

        private static LegacyData ExtractLegacyData(MonoBehaviour legacy)
        {
            var data = LegacyData.Empty;
            var t = legacy.GetType();

            data.Elevators[0] = ReadField<Transform>(legacy, t, "Elevator1");
            data.Elevators[1] = ReadField<Transform>(legacy, t, "Elevator2");
            data.Elevators[2] = ReadField<Transform>(legacy, t, "Elevator3");

            data.BS[0] = ReadField<GameObject>(legacy, t, "BS1");
            data.BS[1] = ReadField<GameObject>(legacy, t, "BS2");
            data.BS[2] = ReadField<GameObject>(legacy, t, "BS3");
            data.BS[3] = ReadField<GameObject>(legacy, t, "BS4");

            data.Shuttles[0] = ReadField<GameObject>(legacy, t, "Shuttle1");
            data.Shuttles[1] = ReadField<GameObject>(legacy, t, "Shuttle2");
            data.Shuttles[2] = ReadField<GameObject>(legacy, t, "Shuttle3");
            data.Shuttles[3] = ReadField<GameObject>(legacy, t, "Shuttle4");

            data.Aircraft1 = ReadField<GameObject>(legacy, t, "Aircraft1");

            float er = ReadValue<float>(legacy, t, "ElevatorRange", 8f);
            data.ElevatorRange = er > 0.1f ? er : 8f;
            float cr = ReadValue<float>(legacy, t, "CatapultRange", 94f);
            data.CatapultRange = cr > 0.1f ? cr : 94f;

            return data;
        }

        private static T ReadField<T>(MonoBehaviour target, System.Type type, string fieldName) where T : Object
        {
            var fi = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi == null) return null;
            return fi.GetValue(target) as T;
        }

        private static T ReadValue<T>(MonoBehaviour target, System.Type type, string fieldName, T fallback) where T : struct
        {
            var fi = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi == null) return fallback;
            object v = fi.GetValue(target);
            return v is T cast ? cast : fallback;
        }

        // ===========================================================================
        // Asset + scene plumbing
        // ===========================================================================

        private static CarrierProfileSO EnsureProfileAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CarrierProfileSO>(ProfilePath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
            var profile = ScriptableObject.CreateInstance<CarrierProfileSO>();
            profile.carrierId   = "CVN-78";
            profile.displayName = "USS Gerald R. Ford";
            // All other fields keep the Ford-class defaults from the SO's field initializers.
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CVN-78 Migration] Created default profile at {ProfilePath}.");
            return profile;
        }

        private static Transform EnsureGeneratedRoot(GameObject prefabRoot)
        {
            var existing = prefabRoot.transform.Find(GeneratedRootName);
            Transform t;
            if (existing != null)
            {
                t = existing;
            }
            else
            {
                var go = new GameObject(GeneratedRootName);
                go.transform.SetParent(prefabRoot.transform, false);
                t = go.transform;
            }

            // Force identity local transform. Required because EnsureShuttleEnds computes
            // positions in prefab-root local space and stores them as localPosition under
            // genRoot — that only equates correctly when genRoot is identity-under-root.
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;
            return t;
        }

        /// <summary>
        /// Find or create one ShuttleEnd transform per catapult. Position is computed from
        /// the legacy Shuttle{i}.position (start) + +Z × CatapultRange (end).
        /// </summary>
        private static Transform[] EnsureShuttleEnds(Transform genRoot, LegacyData data)
        {
            var results = new Transform[4];
            float stroke = data.CatapultRange > 0.1f ? data.CatapultRange : 94f;

            for (int i = 0; i < 4; i++)
            {
                string name = $"_Generated_Shuttle_End_{i + 1}";
                var existing = genRoot.Find(name);
                Transform t;
                if (existing != null)
                {
                    t = existing;
                }
                else
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(genRoot, false);
                    t = go.transform;
                }

                // Place at legacy Shuttle{i}.position + forward * stroke. If no shuttle ref,
                // place at a stub default along the X axis so they're at least distinguishable.
                if (data.Shuttles[i] != null)
                {
                    Vector3 start = genRoot.parent.InverseTransformPoint(data.Shuttles[i].transform.position);
                    Vector3 end = start + Vector3.forward * stroke;
                    t.localPosition = end;
                }
                else
                {
                    // Fallback layout: 4 catapults at slightly different X offsets.
                    t.localPosition = new Vector3((i - 1.5f) * 5f, 18f, stroke * 0.5f);
                }

                results[i] = t;
            }
            return results;
        }

        private static Transform EnsureFlolsReference(Transform genRoot)
        {
            const string Name = "_Generated_FLOLS_Reference";
            var existing = genRoot.Find(Name);
            if (existing != null) return existing;

            var go = new GameObject(Name);
            go.transform.SetParent(genRoot, false);
            // Default: port-side of the angled deck, ~20m above water, slightly aft of midships.
            // These are GUESSES — the user will reposition.
            go.transform.localPosition = new Vector3(-10f, 20f, -20f);
            return go.transform;
        }

        private static Transform[] EnsureWireCenterlines(Transform genRoot, int count)
        {
            count = Mathf.Clamp(count, 1, 4);
            var results = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                string name = $"_Generated_Wire_{i + 1}";
                var existing = genRoot.Find(name);
                Transform t;
                if (existing != null)
                {
                    t = existing;
                }
                else
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(genRoot, false);
                    t = go.transform;
                    // Default: aft of midships, on the angled deck, 12m apart.
                    // GUESSES — user will reposition.
                    t.localPosition = new Vector3(0f, 17f, -40f - i * 12f);
                }
                results[i] = t;
            }
            return results;
        }

        // ===========================================================================
        // Configure the new CarrierBehaviour via SerializedObject
        // ===========================================================================

        private static void ConfigureBehaviour(
            CarrierBehaviour behaviour,
            CarrierProfileSO profile,
            LegacyData data,
            Transform[] shuttleEnds,
            Transform flolsRef,
            Transform[] wireCenters)
        {
            var so = new SerializedObject(behaviour);

            // Profile
            so.FindProperty("profile").objectReferenceValue = profile;

            // Helm — leave at 0/0 (all stop, rudder amidships).
            so.FindProperty("throttleNormalized").floatValue = 0f;
            so.FindProperty("rudderNormalized").floatValue = 0f;

            // Catapult slots (4)
            var catProp = so.FindProperty("catapultSlots");
            catProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var slot = catProp.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("ShuttleAnimator").objectReferenceValue =
                    data.Shuttles[i] != null ? data.Shuttles[i].GetComponent<Animator>() : null;
                slot.FindPropertyRelative("JbdAnimator").objectReferenceValue =
                    data.BS[i] != null ? data.BS[i].GetComponent<Animator>() : null;
                slot.FindPropertyRelative("ShuttleStart").objectReferenceValue =
                    data.Shuttles[i] != null ? data.Shuttles[i].transform : null;
                slot.FindPropertyRelative("ShuttleEnd").objectReferenceValue = shuttleEnds[i];
                // Only cat 1 had a legacy aircraft reference. `??` doesn't work cleanly on
                // Unity component lookups (fake-null semantics) — do the fallback explicitly.
                Rigidbody aircraftRb = null;
                if (i == 0 && data.Aircraft1 != null)
                {
                    aircraftRb = data.Aircraft1.GetComponent<Rigidbody>();
                    if (aircraftRb == null)
                        aircraftRb = data.Aircraft1.GetComponentInChildren<Rigidbody>();
                }
                slot.FindPropertyRelative("AttachedAircraft").objectReferenceValue = aircraftRb;
            }

            // Elevator slots (3)
            var elevProp = so.FindProperty("elevatorSlots");
            elevProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var slot = elevProp.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("LiftTransform").objectReferenceValue = data.Elevators[i];
                slot.FindPropertyRelative("StowedLocalPosition").vector3Value =
                    data.Elevators[i] != null ? data.Elevators[i].localPosition : Vector3.zero;
                // Default deployed offset: elevator goes DOWN to hangar by ElevatorRange.
                slot.FindPropertyRelative("DeployedLocalOffset").vector3Value =
                    new Vector3(0f, -data.ElevatorRange, 0f);
            }

            // FLOLS slots
            so.FindProperty("flolsReference").objectReferenceValue = flolsRef;
            // flolsBallTransform / flolsCutLights — leave null; designer wires these up.

            // Wire slots
            var wireProp = so.FindProperty("wireSlots");
            wireProp.arraySize = wireCenters.Length;
            for (int i = 0; i < wireCenters.Length; i++)
            {
                var slot = wireProp.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("WireCenterline").objectReferenceValue = wireCenters[i];
                slot.FindPropertyRelative("WireVisual").objectReferenceValue = null;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
