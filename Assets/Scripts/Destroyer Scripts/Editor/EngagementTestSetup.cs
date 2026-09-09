using UnityEditor;
using UnityEngine;

public static class EngagementTestSetup
{
    private const int WaypointCount = 6;
    private const float OrbitRadiusMeters = 3500f;
    private const float OrbitForwardOffsetMeters = 6000f;
    private const float OrbitAltitudeMeters = 1200f;
    private const int TargetCount = 3;
    private const float TargetSpeed = 220f;
    private const float TargetVisualScale = 60f;

    [MenuItem("Tools/Deep Water/Setup Engagement Test (3 targets + AutoFire)")]
    public static void Setup()
    {
        BurkeVLSSystem vls = Object.FindFirstObjectByType<BurkeVLSSystem>();
        if (vls == null)
        {
            EditorUtility.DisplayDialog(
                "Engagement Test Setup",
                "No BurkeVLSSystem found. Set up the VLS first.",
                "OK");
            return;
        }

        Vector3 shipPos = vls.transform.position;
        Vector3 shipForward = vls.transform.forward;
        Vector3 shipRight = vls.transform.right;
        Vector3 orbitCenter = shipPos + shipForward * OrbitForwardOffsetMeters + Vector3.up * OrbitAltitudeMeters;

        Transform root = EnsureEmpty("Enemy_Targets", null, Vector3.zero);
        Transform waypointParent = EnsureEmpty("Waypoints", root, Vector3.zero);
        Transform targetParent = EnsureEmpty("Targets", root, Vector3.zero);

        ClearChildren(waypointParent);
        ClearChildren(targetParent);

        Transform[] waypoints = new Transform[WaypointCount];
        for (int i = 0; i < WaypointCount; i++)
        {
            float angle = (i / (float)WaypointCount) * Mathf.PI * 2f;
            Vector3 pos = orbitCenter
                + shipRight * (Mathf.Cos(angle) * OrbitRadiusMeters)
                + shipForward * (Mathf.Sin(angle) * OrbitRadiusMeters);

            GameObject wp = new GameObject($"WP_{i:00}");
            Undo.RegisterCreatedObjectUndo(wp, "Create Waypoint");
            wp.transform.SetParent(waypointParent, false);
            wp.transform.position = pos;
            waypoints[i] = wp.transform;
        }

        for (int t = 0; t < TargetCount; t++)
        {
            int startIdx = (t * (WaypointCount / TargetCount)) % WaypointCount;
            Vector3 startPos = waypoints[startIdx].position;

            GameObject target = new GameObject($"AirTarget_{t + 1}");
            Undo.RegisterCreatedObjectUndo(target, "Create Air Target");
            target.transform.SetParent(targetParent, false);
            target.transform.position = startPos;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(visual, "Create Air Target Visual");
            Object.DestroyImmediate(visual.GetComponent<SphereCollider>());
            visual.name = "Visual";
            visual.transform.SetParent(target.transform, false);
            visual.transform.localScale = Vector3.one * TargetVisualScale;
            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material redMat = new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                redMat.name = "AirTarget_Red";
                redMat.color = new Color(1f, 0.15f, 0.1f);
                if (redMat.HasProperty("_BaseColor"))
                    redMat.SetColor("_BaseColor", new Color(1f, 0.15f, 0.1f));
                if (redMat.HasProperty("_EmissiveColor"))
                    redMat.SetColor("_EmissiveColor", new Color(2f, 0.3f, 0.2f));
                renderer.sharedMaterial = redMat;
            }

            SphereCollider col = target.AddComponent<SphereCollider>();
            col.radius = TargetVisualScale * 0.6f;
            col.isTrigger = false;

            Rigidbody rb = target.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            SimpleAirTarget sat = target.AddComponent<SimpleAirTarget>();
            sat.Waypoints = waypoints;
            sat.Speed = TargetSpeed;
            sat.StartIndex = startIdx;
            sat.ArriveRadius = 250f;
        }

        SPG62FireControlRadar[] radars = Object.FindObjectsByType<SPG62FireControlRadar>(FindObjectsSortMode.None);
        foreach (var r in radars)
        {
            Undo.RecordObject(r, "Enable AutoFireWhenLocked");
            r.AutoFireWhenLocked = true;
            EditorUtility.SetDirty(r);
        }

        Debug.Log(
            $"[EngagementTestSetup] Spawned {TargetCount} targets on a {WaypointCount}-waypoint loop, " +
            $"radius {OrbitRadiusMeters / 1000f}km, altitude {OrbitAltitudeMeters}m around the DDG51. " +
            $"AutoFireWhenLocked enabled on {radars.Length} SPG62 radar(s).");
    }

    private static Transform EnsureEmpty(string name, Transform parent, Vector3 localPosition)
    {
        Transform existing = parent != null ? parent.Find(name) : null;
        if (existing == null && parent == null)
        {
            GameObject scene = GameObject.Find(name);
            if (scene != null) existing = scene.transform;
        }

        if (existing != null)
            return existing;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        if (parent != null)
            go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
    }
}
