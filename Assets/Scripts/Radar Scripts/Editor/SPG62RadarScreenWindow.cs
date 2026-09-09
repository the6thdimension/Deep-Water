using System.Linq;
using UnityEditor;
using UnityEngine;

public class SPG62RadarScreenWindow : EditorWindow
{
    private const float ScreenPadding = 12f;
    private const float MinScreenSize = 280f;
    private const float BlipSize = 6f;
    private const float BlipHitRadius = 10f;

    private SPG62FireControlRadar _radar;
    private Transform _selectedTarget;
    private Vector2 _trackListScroll;

    public static void Open(SPG62FireControlRadar radar)
    {
        var window = GetWindow<SPG62RadarScreenWindow>("SPG-62 Radar");
        window.minSize = new Vector2(520f, 560f);
        window._radar = radar;
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        EditorApplication.update += HandleEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= HandleEditorUpdate;
    }

    private void HandleEditorUpdate()
    {
        if (Application.isPlaying)
            Repaint();
    }

    private void OnGUI()
    {
        DrawHeader();

        if (_radar == null)
        {
            EditorGUILayout.HelpBox("Assign an SPG62FireControlRadar from the scene.", MessageType.Info);
            return;
        }

        DrawTopControls();
        DrawRadarScreen();
        DrawTrackPanel();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(4f);
        _radar = (SPG62FireControlRadar)EditorGUILayout.ObjectField("Radar Source", _radar, typeof(SPG62FireControlRadar), true);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode for live radar updates and hook/fire actions.", MessageType.Warning);
        }
    }

    private void DrawTopControls()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Search")) _radar.SetMode(SPG62FireControlRadar.RadarMode.Search);
            if (GUILayout.Button("TWS")) _radar.SetMode(SPG62FireControlRadar.RadarMode.TrackWhileScan);
            if (GUILayout.Button("STT")) _radar.SetMode(SPG62FireControlRadar.RadarMode.SingleTargetTrack);
            GUI.enabled = true;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Designate Best")) _radar.TryDesignateBestTrack();
            if (GUILayout.Button("Clear Lock")) _radar.ClearLock();
            if (GUILayout.Button("Fire Salvo")) _radar.TryFireCommand();
            GUI.enabled = true;
        }
    }

    private void DrawRadarScreen()
    {
        float side = Mathf.Max(MinScreenSize, Mathf.Min(position.width - ScreenPadding * 2f, 420f));
        Rect rect = GUILayoutUtility.GetRect(side, side, GUILayout.ExpandWidth(false));
        rect.x = (position.width - rect.width) * 0.5f;

        DrawRadarBackdrop(rect);
        DrawSweep(rect);
        DrawTracks(rect);
        HandleRadarClick(rect);
    }

    private void DrawRadarBackdrop(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.03f, 0.07f, 0.08f, 1f));
        Handles.BeginGUI();
        Color old = Handles.color;
        Vector2 center = rect.center;
        float radius = rect.width * 0.5f - 6f;

        Handles.color = new Color(0.1f, 0.35f, 0.35f, 0.8f);
        for (int i = 1; i <= 4; i++)
            Handles.DrawWireDisc(center, Vector3.forward, radius * (i / 4f));

        Handles.DrawLine(new Vector3(center.x, rect.y + 4f), new Vector3(center.x, rect.yMax - 4f));
        Handles.DrawLine(new Vector3(rect.x + 4f, center.y), new Vector3(rect.xMax - 4f, center.y));
        Handles.color = old;
        Handles.EndGUI();
    }

    private void DrawSweep(Rect rect)
    {
        if (_radar == null)
            return;

        float az = _radar.ScanAzimuthDeg * Mathf.Deg2Rad;
        Vector2 center = rect.center;
        float radius = rect.width * 0.5f - 6f;
        Vector2 dir = new Vector2(Mathf.Sin(az), Mathf.Cos(az));
        Vector2 end = center + dir * radius;

        Handles.BeginGUI();
        Color old = Handles.color;
        Handles.color = new Color(0.0f, 1f, 0.8f, 0.9f);
        Handles.DrawAAPolyLine(2f, center, end);
        Handles.color = old;
        Handles.EndGUI();
    }

    private void DrawTracks(Rect rect)
    {
        if (_radar == null || _radar.Tracks == null)
            return;

        Vector2 center = rect.center;
        float radius = rect.width * 0.5f - 8f;
        float maxRange = Mathf.Max(1f, _radar.InstrumentedRange);

        foreach (var track in _radar.Tracks.Values)
        {
            if (track == null || track.Target == null)
                continue;

            float rNorm = Mathf.Clamp01(track.Range / maxRange);
            float az = track.AzimuthDeg * Mathf.Deg2Rad;
            Vector2 pos = center + new Vector2(Mathf.Sin(az), Mathf.Cos(az)) * (radius * rNorm);
            Rect blipRect = new Rect(pos.x - BlipSize * 0.5f, pos.y - BlipSize * 0.5f, BlipSize, BlipSize);

            Color color = track.IsLocked ? new Color(1f, 0.2f, 0.2f, 1f) : new Color(1f, 0.95f, 0.25f, 1f);
            EditorGUI.DrawRect(blipRect, color);

            if (_selectedTarget == track.Target)
            {
                Handles.BeginGUI();
                Color old = Handles.color;
                Handles.color = Color.white;
                Handles.DrawWireDisc(pos, Vector3.forward, 8f);
                Handles.color = old;
                Handles.EndGUI();
            }
        }
    }

    private void HandleRadarClick(Rect rect)
    {
        if (!Application.isPlaying || _radar == null || _radar.Tracks == null)
            return;

        Event evt = Event.current;
        if (evt.type != EventType.MouseDown || evt.button != 0 || !rect.Contains(evt.mousePosition))
            return;

        Transform hit = FindNearestTrackAt(evt.mousePosition, rect);
        if (hit != null)
            _selectedTarget = hit;

        evt.Use();
        Repaint();
    }

    private Transform FindNearestTrackAt(Vector2 mousePos, Rect rect)
    {
        Vector2 center = rect.center;
        float radius = rect.width * 0.5f - 8f;
        float maxRange = Mathf.Max(1f, _radar.InstrumentedRange);
        float best = float.MaxValue;
        Transform bestTarget = null;

        foreach (var track in _radar.Tracks.Values)
        {
            if (track == null || track.Target == null)
                continue;

            float rNorm = Mathf.Clamp01(track.Range / maxRange);
            float az = track.AzimuthDeg * Mathf.Deg2Rad;
            Vector2 pos = center + new Vector2(Mathf.Sin(az), Mathf.Cos(az)) * (radius * rNorm);
            float d = Vector2.Distance(mousePos, pos);
            if (d < BlipHitRadius && d < best)
            {
                best = d;
                bestTarget = track.Target;
            }
        }

        return bestTarget;
    }

    private void DrawTrackPanel()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Tracks", EditorStyles.boldLabel);

        if (_radar == null)
            return;

        EditorGUILayout.LabelField("Mode", _radar.Mode.ToString());
        EditorGUILayout.LabelField("Active Tracks", _radar.ActiveTrackCount.ToString());
        EditorGUILayout.LabelField("Locked", _radar.CurrentLockedTrack?.Target != null ? _radar.CurrentLockedTrack.Target.name : "None");
        EditorGUILayout.LabelField("Fire Cooldown", _radar.SecondsUntilFireAllowed.ToString("0.00") + " s");

        var ordered = _radar.Tracks.Values
            .Where(t => t != null && t.Target != null)
            .OrderBy(t => t.Range);

        _trackListScroll = EditorGUILayout.BeginScrollView(_trackListScroll, GUILayout.Height(190f));
        foreach (var track in ordered)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField(track.Target.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Range: {track.Range:0} m | Q: {track.TrackQuality:0.00} | Az: {track.AzimuthDeg:0.0}");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Hook Track"))
                    {
                        if (Application.isPlaying)
                        {
                            _selectedTarget = track.Target;
                            _radar.SetSTTTarget(track.Target);
                        }
                    }

                    if (GUILayout.Button("Select"))
                    {
                        Selection.activeTransform = track.Target;
                        EditorGUIUtility.PingObject(track.Target);
                    }

                    GUI.enabled = Application.isPlaying;
                    if (GUILayout.Button("Fire"))
                    {
                        _selectedTarget = track.Target;
                        _radar.SetSTTTarget(track.Target);
                        _radar.TryFireCommand(1);
                    }
                    GUI.enabled = true;
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }
}
