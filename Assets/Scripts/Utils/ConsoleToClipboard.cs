using UnityEngine;
using System.Text;
using System.Collections.Generic;
using UnityEditor;

#if UNITY_EDITOR
[InitializeOnLoad]
public class ConsoleToClipboard : EditorWindow
{
    private static List<LogEntry> logEntries = new List<LogEntry>();
    private bool includeWarnings = true;
    private bool includeInfo = false;
    private bool includeStackTrace = true;
    private bool autoScrollToBottom = true;
    private Vector2 scrollPosition;
    private string searchFilter = "";
    private bool useRegex = false;
    private float timeStampWidth = 100f;
    private float typeWidth = 60f;

    private class LogEntry
    {
        public string Message;
        public string StackTrace;
        public LogType Type;
        public double TimeStamp;

        public LogEntry(string message, string stackTrace, LogType type)
        {
            Message = message;
            StackTrace = stackTrace;
            Type = type;
            TimeStamp = EditorApplication.timeSinceStartup;
        }
    }

    [MenuItem("Window/Debug/Console To Clipboard")]
    public static void ShowWindow()
    {
        GetWindow<ConsoleToClipboard>("Console Copier");
    }

    static ConsoleToClipboard()
    {
        Application.logMessageReceived += LogMessageReceived;
    }

    private static void LogMessageReceived(string message, string stackTrace, LogType type)
    {
        logEntries.Add(new LogEntry(message, stackTrace, type));
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        
        // Filter options
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        EditorGUILayout.LabelField("Filter Options", EditorStyles.boldLabel);
        
        includeWarnings = EditorGUILayout.Toggle("Include Warnings", includeWarnings);
        includeInfo = EditorGUILayout.Toggle("Include Info", includeInfo);
        includeStackTrace = EditorGUILayout.Toggle("Include Stack Trace", includeStackTrace);
        autoScrollToBottom = EditorGUILayout.Toggle("Auto-scroll", autoScrollToBottom);
        
        EditorGUILayout.Space();
        
        // Search
        EditorGUILayout.LabelField("Search", EditorStyles.boldLabel);
        searchFilter = EditorGUILayout.TextField("Filter", searchFilter);
        useRegex = EditorGUILayout.Toggle("Use Regex", useRegex);
        
        EditorGUILayout.Space();
        
        // Actions
        if (GUILayout.Button("Copy All to Clipboard"))
        {
            CopyToClipboard();
        }
        
        if (GUILayout.Button("Clear Console"))
        {
            logEntries.Clear();
        }
        
        EditorGUILayout.EndVertical();

        // Log display
        EditorGUILayout.BeginVertical();
        
        // Header
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("Time", GUILayout.Width(timeStampWidth));
        EditorGUILayout.LabelField("Type", GUILayout.Width(typeWidth));
        EditorGUILayout.LabelField("Message");
        EditorGUILayout.EndHorizontal();

        // Log entries
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        foreach (var entry in FilterEntries())
        {
            DrawLogEntry(entry);
        }

        if (autoScrollToBottom && Event.current.type == EventType.Repaint)
        {
            scrollPosition.y = float.MaxValue;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLogEntry(LogEntry entry)
    {
        EditorGUILayout.BeginVertical(GetLogEntryStyle(entry.Type));
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{entry.TimeStamp:F1}s", GUILayout.Width(timeStampWidth));
        EditorGUILayout.LabelField(entry.Type.ToString(), GUILayout.Width(typeWidth));
        EditorGUILayout.LabelField(entry.Message);
        EditorGUILayout.EndHorizontal();

        if (includeStackTrace && !string.IsNullOrEmpty(entry.StackTrace))
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(entry.StackTrace, EditorStyles.wordWrappedMiniLabel);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private GUIStyle GetLogEntryStyle(LogType type)
    {
        GUIStyle style = new GUIStyle();
        style.margin = new RectOffset(0, 0, 1, 1);
        style.padding = new RectOffset(5, 5, 5, 5);
        
        switch (type)
        {
            case LogType.Error:
            case LogType.Exception:
                style.normal.background = MakeTexture(2, 2, new Color(0.8f, 0.2f, 0.2f, 0.1f));
                break;
            case LogType.Warning:
                style.normal.background = MakeTexture(2, 2, new Color(0.8f, 0.8f, 0.2f, 0.1f));
                break;
            case LogType.Log:
                style.normal.background = MakeTexture(2, 2, new Color(0.8f, 0.8f, 0.8f, 0.1f));
                break;
        }
        
        return style;
    }

    private Texture2D MakeTexture(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        Texture2D texture = new Texture2D(width, height);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private IEnumerable<LogEntry> FilterEntries()
    {
        foreach (var entry in logEntries)
        {
            // Filter by type
            if (entry.Type == LogType.Warning && !includeWarnings) continue;
            if (entry.Type == LogType.Log && !includeInfo) continue;

            // Filter by search
            if (!string.IsNullOrEmpty(searchFilter))
            {
                if (useRegex)
                {
                    try
                    {
                        if (!System.Text.RegularExpressions.Regex.IsMatch(entry.Message, searchFilter, 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            continue;
                    }
                    catch
                    {
                        // Invalid regex, skip filtering
                    }
                }
                else
                {
                    if (!entry.Message.ToLower().Contains(searchFilter.ToLower()))
                        continue;
                }
            }

            yield return entry;
        }
    }

    private void CopyToClipboard()
    {
        StringBuilder sb = new StringBuilder();
        
        foreach (var entry in FilterEntries())
        {
            // Add timestamp and type
            sb.AppendLine($"[{entry.TimeStamp:F1}s] [{entry.Type}] {entry.Message}");
            
            // Add stack trace if enabled and available
            if (includeStackTrace && !string.IsNullOrEmpty(entry.StackTrace))
            {
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(entry.StackTrace);
            }
            
            sb.AppendLine();
        }

        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("Console entries copied to clipboard!");
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= LogMessageReceived;
    }
}
#endif
