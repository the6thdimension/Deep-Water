using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DeepWater.Missiles;

public static class BurkeVLSCellGenerator
{
    private const string EssmAssetPath = "Assets/Scripts/Missile Scripts/Missiles/ESSM.asset";

    private const float CellPitchMeters = 0.7f;
    private const float DefaultForeOffsetZ = 18f;
    private const float DefaultAftOffsetZ = -22f;
    private const float DefaultDeckY = 2f;

    private const int ForeRows = 4;
    private const int ForeCols = 8;
    private const int AftRows = 8;
    private const int AftCols = 8;

    [MenuItem("Tools/Deep Water/Wire SPG62 Radars to BurkeVLSSystem")]
    public static void WireRadarsToVLS()
    {
        BurkeVLSSystem vls = Object.FindFirstObjectByType<BurkeVLSSystem>();
        if (vls == null)
        {
            EditorUtility.DisplayDialog(
                "Wire SPG62 Radars",
                "No BurkeVLSSystem found in any open scene. Add one first.",
                "OK");
            return;
        }

        SPG62FireControlRadar[] radars = Object.FindObjectsByType<SPG62FireControlRadar>(FindObjectsSortMode.None);
        if (radars.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Wire SPG62 Radars",
                "No SPG62FireControlRadar components found in any open scene.",
                "OK");
            return;
        }

        MissileData essm = AssetDatabase.LoadAssetAtPath<MissileData>(EssmAssetPath);
        int wired = 0;
        foreach (var radar in radars)
        {
            Undo.RecordObject(radar, "Wire SPG62 Radar");
            radar.VLS = vls;
            if (essm != null && radar.MissileToFire == null)
                radar.MissileToFire = essm;
            EditorUtility.SetDirty(radar);
            wired++;
        }

        Debug.Log($"[BurkeVLSCellGenerator] Wired {wired} SPG62FireControlRadar component(s) to BurkeVLSSystem on '{vls.name}'.");
    }

    [MenuItem("Tools/Deep Water/Generate Burke VLS Cells (Flight IIA, 96) - Auto-find")]
    public static void GenerateFlightIIAAutoFind()
    {
        BurkeVLSSystem found = Object.FindFirstObjectByType<BurkeVLSSystem>();
        if (found == null)
        {
            EditorUtility.DisplayDialog(
                "Burke VLS Generator",
                "No BurkeVLSSystem found in any open scene. Add one to a GameObject first, then re-run.",
                "OK");
            return;
        }
        GenerateOn(found.gameObject);
    }

    [MenuItem("Tools/Deep Water/Generate Burke VLS Cells (Flight IIA, 96)")]
    public static void GenerateFlightIIA()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog(
                "Burke VLS Generator",
                "Select the GameObject that has (or should have) the BurkeVLSSystem component, then re-run.",
                "OK");
            return;
        }
        GenerateOn(selected);
    }

    private static void GenerateOn(GameObject selected)
    {

        BurkeVLSSystem vls = selected.GetComponent<BurkeVLSSystem>();
        if (vls == null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Burke VLS Generator",
                    $"'{selected.name}' has no BurkeVLSSystem. Add one now?",
                    "Add component",
                    "Cancel"))
            {
                return;
            }

            Undo.AddComponent<BurkeVLSSystem>(selected);
            vls = selected.GetComponent<BurkeVLSSystem>();
        }

        Undo.RegisterFullObjectHierarchyUndo(selected, "Generate Burke VLS Cells");

        Transform foreModule = EnsureModule(selected.transform, "VLS_Forward", new Vector3(0f, DefaultDeckY, DefaultForeOffsetZ));
        Transform aftModule = EnsureModule(selected.transform, "VLS_Aft", new Vector3(0f, DefaultDeckY, DefaultAftOffsetZ));

        List<BurkeVLSSystem.VLSCellSocket> sockets = new List<BurkeVLSSystem.VLSCellSocket>(96);

        int foreCount = PopulateGrid(foreModule, "F", ForeRows, ForeCols, sockets);
        int aftCount = PopulateGrid(aftModule, "A", AftRows, AftCols, sockets);

        SerializedObject so = new SerializedObject(vls);
        SerializedProperty variantProp = so.FindProperty("Variant");
        variantProp.intValue = (int)BurkeVLSSystem.BurkeVariant.FlightIIA_96Cells;

        SerializedProperty cellsProp = so.FindProperty("Cells");
        cellsProp.ClearArray();
        for (int i = 0; i < sockets.Count; i++)
        {
            cellsProp.InsertArrayElementAtIndex(i);
            SerializedProperty element = cellsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("LaunchPoint").objectReferenceValue = sockets[i].LaunchPoint;
            element.FindPropertyRelative("HatchAnimator").objectReferenceValue = null;
        }

        MissileData essm = AssetDatabase.LoadAssetAtPath<MissileData>(EssmAssetPath);
        SerializedProperty loadoutProp = so.FindProperty("InitialLoadout");
        loadoutProp.ClearArray();
        if (essm != null)
        {
            loadoutProp.InsertArrayElementAtIndex(0);
            SerializedProperty entry = loadoutProp.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("Missile").objectReferenceValue = essm;
            entry.FindPropertyRelative("Quantity").intValue = sockets.Count;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(vls);

        Debug.Log(
            $"[BurkeVLSCellGenerator] Generated {foreCount} fore + {aftCount} aft cells on '{selected.name}'. " +
            (essm != null
                ? $"Initial loadout = {sockets.Count}x ESSM."
                : $"ESSM asset not found at {EssmAssetPath}; loadout left empty."));
    }

    private static Transform EnsureModule(Transform parent, string name, Vector3 fallbackLocalPos)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            for (int i = existing.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(existing.GetChild(i).gameObject);
            }
            return existing;
        }

        GameObject moduleGO = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(moduleGO, "Create VLS Module");
        moduleGO.transform.SetParent(parent, false);
        moduleGO.transform.localPosition = fallbackLocalPos;
        moduleGO.transform.localRotation = Quaternion.identity;
        return moduleGO.transform;
    }

    private static int PopulateGrid(
        Transform module,
        string prefix,
        int rows,
        int cols,
        List<BurkeVLSSystem.VLSCellSocket> outSockets)
    {
        float halfWidth = (cols - 1) * CellPitchMeters * 0.5f;
        float halfDepth = (rows - 1) * CellPitchMeters * 0.5f;

        int created = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int oneBased = created + 1;
                GameObject cellGO = new GameObject($"Cell_{prefix}_{oneBased:000}");
                Undo.RegisterCreatedObjectUndo(cellGO, "Create VLS Cell");
                cellGO.transform.SetParent(module, false);
                cellGO.transform.localPosition = new Vector3(
                    c * CellPitchMeters - halfWidth,
                    0f,
                    r * CellPitchMeters - halfDepth);
                // Forward (Z+) points up — matches BurkeVLSSystem fallback launch rotation.
                cellGO.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                outSockets.Add(new BurkeVLSSystem.VLSCellSocket
                {
                    LaunchPoint = cellGO.transform,
                    HatchAnimator = null
                });
                created++;
            }
        }
        return created;
    }
}
