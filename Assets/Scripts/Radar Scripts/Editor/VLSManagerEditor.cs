using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(VLSManager))]
public class VLSManagerEditor : Editor
{
    private VLSManager vlsManager;
    private bool showEnvironmental = true;
    private bool showCellStatus = true;
    private bool showLaunchQueue = true;
    private Vector2 cellScrollPosition;
    private Vector2 queueScrollPosition;
    private GUIStyle headerStyle;
    private GUIStyle cellStyle;
    private GUIStyle warningStyle;
    private Color defaultGuiColor;
    private Texture2D cellBackgroundTexture;

    private void OnEnable()
    {
        vlsManager = (VLSManager)target;
        defaultGuiColor = GUI.color;
        CreateStyles();
    }

    private void CreateStyles()
    {
        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            margin = new RectOffset(0, 0, 10, 5)
        };

        cellStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 10, 10),
            margin = new RectOffset(5, 5, 5, 5)
        };

        warningStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = Color.yellow },
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };

        // Create cell background texture
        cellBackgroundTexture = new Texture2D(1, 1);
        cellBackgroundTexture.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.2f, 0.5f));
        cellBackgroundTexture.Apply();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Basic configuration
        EditorGUILayout.Space(10);
        DrawConfigurationSection();

        // Environmental conditions
        EditorGUILayout.Space(10);
        DrawEnvironmentalSection();

        if (Application.isPlaying)
        {
            // System status
            EditorGUILayout.Space(10);
            DrawSystemStatus();

            // Cell status
            EditorGUILayout.Space(10);
            DrawCellStatus();

            // Launch queue
            EditorGUILayout.Space(10);
            DrawLaunchQueue();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawConfigurationSection()
    {
        EditorGUILayout.LabelField("VLS Configuration", headerStyle);
        EditorGUI.indentLevel++;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("missilePrefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("totalCells"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cellsPerModule"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("launchInterval"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("missileConfigurations"), true);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("System References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("radar"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("launchPoint"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("launchSound"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("launchEffects"));

        EditorGUI.indentLevel--;
    }

    private void DrawEnvironmentalSection()
    {
        showEnvironmental = EditorGUILayout.Foldout(showEnvironmental, "Environmental Conditions", true);
        if (showEnvironmental)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxWindSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxRollAngle"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxPitchAngle"));

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Current Conditions:", EditorStyles.boldLabel);
                
                // Display current wind vector
                Vector3 windVector = vlsManager.transform.InverseTransformDirection(Vector3.right * 10f);
                EditorGUILayout.Vector3Field("Wind Direction", windVector.normalized);
                
                // Display ship orientation
                Vector3 rotation = vlsManager.transform.rotation.eulerAngles;
                float roll = rotation.z > 180 ? rotation.z - 360 : rotation.z;
                float pitch = rotation.x > 180 ? rotation.x - 360 : rotation.x;
                
                EditorGUILayout.LabelField($"Ship Roll: {roll:F1}°");
                EditorGUILayout.LabelField($"Ship Pitch: {pitch:F1}°");
            }

            EditorGUI.indentLevel--;
        }
    }

    private void DrawSystemStatus()
    {
        EditorGUILayout.LabelField("System Status", headerStyle);
        EditorGUI.indentLevel++;

        // Display available missiles
        GUI.color = vlsManager.AvailableMissiles > 0 ? Color.green : Color.red;
        EditorGUILayout.LabelField($"Available Missiles: {vlsManager.AvailableMissiles}/{vlsManager.totalCells}");
        GUI.color = defaultGuiColor;

        // Display launch status
        if (vlsManager.IsLaunching)
        {
            GUI.color = Color.yellow;
            EditorGUILayout.LabelField("⚠ Launch Sequence in Progress", warningStyle);
            GUI.color = defaultGuiColor;
        }

        EditorGUI.indentLevel--;
    }

    private void DrawCellStatus()
    {
        showCellStatus = EditorGUILayout.Foldout(showCellStatus, "VLS Cell Status", true);
        if (!showCellStatus) return;

        cellScrollPosition = EditorGUILayout.BeginScrollView(cellScrollPosition, GUILayout.MaxHeight(300));
        
        int moduleCount = Mathf.CeilToInt((float)vlsManager.totalCells / vlsManager.cellsPerModule);
        
        for (int module = 0; module < moduleCount; module++)
        {
            EditorGUILayout.BeginVertical(cellStyle);
            EditorGUILayout.LabelField($"Module {module + 1}", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            for (int cell = 0; cell < vlsManager.cellsPerModule; cell++)
            {
                int globalIndex = module * vlsManager.cellsPerModule + cell;
                if (globalIndex >= vlsManager.totalCells) break;

                var vlsCell = vlsManager.Cells[globalIndex];
                DrawCellGUI(vlsCell);

                if ((cell + 1) % 4 == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();

        // Reload button
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reload All Empty Cells"))
        {
            vlsManager.ReloadAllEmptyCells();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCellGUI(VLSCell cell)
    {
        // Cell background
        Rect cellRect = EditorGUILayout.BeginVertical(GUILayout.Width(60), GUILayout.Height(60));
        GUI.DrawTexture(cellRect, cellBackgroundTexture);

        // Cell status color
        Color statusColor = GetStatusColor(cell.Status);
        GUI.color = statusColor;

        // Cell content
        GUILayout.Label($"Cell {cell.CellIndex + 1}", EditorStyles.centeredGreyMiniLabel);
        
        // Status indicator
        GUILayout.FlexibleSpace();
        GUILayout.Label(GetStatusSymbol(cell.Status), EditorStyles.centeredGreyMiniLabel);
        
        // Reload button for empty cells
        if (cell.Status == CellStatus.Empty)
        {
            if (GUILayout.Button("Reload", EditorStyles.miniButton))
            {
                vlsManager.ReloadCell(cell.Index);
            }
        }

        EditorGUILayout.EndVertical();
        GUI.color = defaultGuiColor;

        // Tooltip
        EditorGUIUtility.AddCursorRect(cellRect, MouseCursor.Link);
        if (cellRect.Contains(Event.current.mousePosition))
        {
            string tooltip = $"Cell {cell.Index + 1}\nStatus: {cell.Status}";
            if (cell.Status == CellStatus.Ready)
            {
                tooltip += "\nReady for launch";
            }
            EditorGUI.LabelField(new Rect(Event.current.mousePosition, new Vector2(150, 40)), tooltip, EditorStyles.helpBox);
        }
    }

    private void DrawLaunchQueue()
    {
        showLaunchQueue = EditorGUILayout.Foldout(showLaunchQueue, "Launch Queue", true);
        if (!showLaunchQueue) return;

        queueScrollPosition = EditorGUILayout.BeginScrollView(queueScrollPosition, GUILayout.MaxHeight(150));
        
        // TODO: Display launch queue when we have access to it
        EditorGUILayout.LabelField("Launch queue information will be displayed here");
        
        EditorGUILayout.EndScrollView();
    }

    private Color GetStatusColor(CellStatus status)
    {
        switch (status)
        {
            case CellStatus.Ready:
                return Color.green;
            case CellStatus.Launching:
                return Color.yellow;
            case CellStatus.Reloading:
                return new Color(1f, 0.6f, 0f); // Orange
            case CellStatus.Malfunction:
                return Color.red;
            case CellStatus.Empty:
            default:
                return Color.gray;
        }
    }

    private string GetStatusSymbol(CellStatus status)
    {
        switch (status)
        {
            case CellStatus.Ready:
                return "✓";
            case CellStatus.Launching:
                return "↑";
            case CellStatus.Reloading:
                return "⟳";
            case CellStatus.Malfunction:
                return "⚠";
            case CellStatus.Empty:
            default:
                return "∅";
        }
    }

    private void OnDisable()
    {
        if (cellBackgroundTexture != null)
        {
            DestroyImmediate(cellBackgroundTexture);
        }
    }
}
