using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SPG62FireControlRadar))]
public class SPG62FireControlRadarEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var radar = (SPG62FireControlRadar)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Developer Control Surface", EditorStyles.boldLabel);

        DrawModeControls(radar);
        DrawLockControls(radar);
        DrawFireControls(radar);
        DrawRadarScreenButton(radar);
        DrawRuntimeStatus(radar);

        if (Application.isPlaying)
            Repaint();
    }

    private static void DrawModeControls(SPG62FireControlRadar radar)
    {
        EditorGUILayout.LabelField("Mode", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Search"))
            SetMode(radar, SPG62FireControlRadar.RadarMode.Search);
        if (GUILayout.Button("TWS"))
            SetMode(radar, SPG62FireControlRadar.RadarMode.TrackWhileScan);
        if (GUILayout.Button("STT"))
            SetMode(radar, SPG62FireControlRadar.RadarMode.SingleTargetTrack);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawLockControls(SPG62FireControlRadar radar)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Track / Lock", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Designate Best"))
        {
            if (Application.isPlaying)
                radar.TryDesignateBestTrack();
        }
        if (GUILayout.Button("STT Manual Target"))
        {
            if (Application.isPlaying && radar.ManualSTTTarget != null)
                radar.SetSTTTarget(radar.ManualSTTTarget);
        }
        if (GUILayout.Button("Clear Lock"))
        {
            if (Application.isPlaying)
                radar.ClearLock();
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Clear Tracks"))
        {
            if (Application.isPlaying)
                radar.ClearTracks();
        }
    }

    private static void DrawFireControls(SPG62FireControlRadar radar)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fire Commands", EditorStyles.miniBoldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Fire Single"))
            {
                if (Application.isPlaying)
                    radar.TryFireCommand(1);
            }

            if (GUILayout.Button("Fire Salvo"))
            {
                if (Application.isPlaying)
                    radar.TryFireCommand();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Critical Salvo"))
            {
                if (Application.isPlaying)
                    radar.TryFireCommand(-1, BurkeVLSSystem.LaunchPriority.Critical);
            }
            if (GUILayout.Button("High Single"))
            {
                if (Application.isPlaying)
                    radar.TryFireCommand(1, BurkeVLSSystem.LaunchPriority.High);
            }
        }
    }

    private static void DrawRuntimeStatus(SPG62FireControlRadar radar)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Status", EditorStyles.miniBoldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use fire/lock commands and live telemetry.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Mode", radar.Mode.ToString());
        EditorGUILayout.LabelField("Active Tracks", radar.ActiveTrackCount.ToString());
        EditorGUILayout.LabelField("Fire Cooldown", radar.SecondsUntilFireAllowed.ToString("0.00") + " s");

        if (radar.VLS != null)
        {
            EditorGUILayout.LabelField("VLS Ready Cells", radar.VLS.ReadyCells.ToString());
            EditorGUILayout.LabelField("VLS Total Cells", radar.VLS.TotalConfiguredCells.ToString());
        }
        else
        {
            EditorGUILayout.LabelField("VLS", "Not assigned");
        }

        if (radar.CurrentLockedTrack != null)
        {
            var t = radar.CurrentLockedTrack;
            string targetName = t.Target != null ? t.Target.name : "<null>";
            EditorGUILayout.LabelField("Locked Target", targetName);
            EditorGUILayout.LabelField("Range", t.Range.ToString("0") + " m");
            EditorGUILayout.LabelField("Track Quality", t.TrackQuality.ToString("0.00"));
            EditorGUILayout.LabelField("Is Locked", t.IsLocked ? "Yes" : "No");
        }
        else
        {
            EditorGUILayout.LabelField("Locked Target", "None");
        }
    }

    private static void DrawRadarScreenButton(SPG62FireControlRadar radar)
    {
        EditorGUILayout.Space();
        if (GUILayout.Button("Open Radar Screen Window"))
        {
            SPG62RadarScreenWindow.Open(radar);
        }
    }

    private static void SetMode(SPG62FireControlRadar radar, SPG62FireControlRadar.RadarMode mode)
    {
        Undo.RecordObject(radar, "Set SPG62 Mode");
        radar.SetMode(mode);
        EditorUtility.SetDirty(radar);
    }
}
