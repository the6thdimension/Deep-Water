using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(RH_RadarModule))]
public class RH_RadarModuleEditor : Editor
{
    private bool showTargets = true;
    private bool showScanSettings = true;
    private bool showAdvancedSettings = true;
    private Vector2 scrollPosition;
    private GUIStyle contactInfoStyle;
    private GUIStyle headerStyle;
    private GUIStyle warningStyle;
    private Color defaultGuiColor;
    private Texture2D signalStrengthTexture;

    private void OnEnable()
    {
        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            margin = new RectOffset(0, 0, 10, 5)
        };

        warningStyle = new GUIStyle(EditorStyles.helpBox)
        {
            normal = { textColor = Color.yellow },
            fontSize = 11,
            padding = new RectOffset(5, 5, 5, 5)
        };

        contactInfoStyle = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 11,
            padding = new RectOffset(5, 5, 5, 5)
        };

        defaultGuiColor = GUI.color;
        CreateSignalStrengthTexture();
    }

    private void CreateSignalStrengthTexture()
    {
        signalStrengthTexture = new Texture2D(100, 1);
        for (int i = 0; i < 100; i++)
        {
            float strength = i / 100f;
            Color color = Color.Lerp(Color.red, Color.green, strength);
            signalStrengthTexture.SetPixel(i, 0, color);
        }
        signalStrengthTexture.Apply();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        RH_RadarModule radar = (RH_RadarModule)target;

        // Radar Status Section
        DrawStatusSection(radar);

        EditorGUILayout.Space(10);

        // Scan Settings Section
        showScanSettings = EditorGUILayout.Foldout(showScanSettings, "Scan Settings", true);
        if (showScanSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawScanSettings();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(5);

        // Advanced Settings Section
        showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);
        if (showAdvancedSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawAdvancedSettings();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(5);

        // Detected Targets Section
        DrawTargetsSection(radar);

        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStatusSection(RH_RadarModule radar)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.LabelField("Radar Status", headerStyle);
        
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = false;
        EditorGUILayout.Toggle("Active", radar.IsActive, GUILayout.Width(200));
        GUI.enabled = true;
        
        if (Application.isPlaying)
        {
            GUI.color = radar.IsActive ? Color.green : Color.red;
            if (!radar.IsActive)
            {
                if (GUILayout.Button("Activate", GUILayout.Width(100)))
                {
                    radar.ActivateModule();
                }
            }
            else
            {
                if (GUILayout.Button("Deactivate", GUILayout.Width(100)))
                {
                    radar.DeactivateModule();
                }
            }
            GUI.color = defaultGuiColor;
        }
        EditorGUILayout.EndHorizontal();

        if (radar.IsActive)
        {
            EditorGUILayout.LabelField($"Current Scan Angle: {radar.CurrentScanAngle:F1}°");
            DrawScanVisualizer(radar);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawScanSettings()
    {
        SerializedProperty scanSpeedProp = serializedObject.FindProperty("scanSpeed");
        SerializedProperty enableRotationProp = serializedObject.FindProperty("enableRotation");
        SerializedProperty sectorSizeProp = serializedObject.FindProperty("sectorSize");
        SerializedProperty sectorCenterProp = serializedObject.FindProperty("sectorCenter");

        EditorGUILayout.PropertyField(scanSpeedProp);
        EditorGUILayout.PropertyField(enableRotationProp);

        if (!enableRotationProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(sectorSizeProp);
            EditorGUILayout.PropertyField(sectorCenterProp);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawAdvancedSettings()
    {
        SerializedProperty detectionThresholdProp = serializedObject.FindProperty("detectionThreshold");
        SerializedProperty interferenceProp = serializedObject.FindProperty("interference");
        SerializedProperty weatherAttenuationProp = serializedObject.FindProperty("weatherAttenuation");
        SerializedProperty enableDopplerProp = serializedObject.FindProperty("enableDopplerTracking");
        SerializedProperty enableECMProp = serializedObject.FindProperty("enableECMDetection");
        SerializedProperty enableTerrainProp = serializedObject.FindProperty("enableTerrainMasking");
        SerializedProperty contactPersistenceTimeProp = serializedObject.FindProperty("contactPersistenceTime");
        SerializedProperty showPersistentContactsProp = serializedObject.FindProperty("showPersistentContacts");

        EditorGUILayout.PropertyField(detectionThresholdProp);
        EditorGUILayout.PropertyField(interferenceProp);
        EditorGUILayout.PropertyField(weatherAttenuationProp);
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.PropertyField(enableDopplerProp);
        EditorGUILayout.PropertyField(enableECMProp);
        EditorGUILayout.PropertyField(enableTerrainProp);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Contact Persistence", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(contactPersistenceTimeProp, new GUIContent("Persistence Time"));
        EditorGUILayout.PropertyField(showPersistentContactsProp, new GUIContent("Show Lost Contacts"));
        EditorGUI.indentLevel--;

        if (interferenceProp.floatValue > 0.5f)
        {
            EditorGUILayout.HelpBox("High interference levels may significantly reduce radar effectiveness!", MessageType.Warning);
        }
    }

    private void DrawTargetsSection(RH_RadarModule radar)
    {
        showTargets = EditorGUILayout.Foldout(showTargets, "Detected Targets", true);
        
        if (showTargets)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (!radar.DetectedTargets.Count.Equals(0))
            {
                EditorGUILayout.LabelField($"Targets Detected: {radar.DetectedTargets.Count}", EditorStyles.boldLabel);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(300));
                
                foreach (GameObject target in radar.DetectedTargets)
                {
                    if (target == null) continue;
                    DrawTargetInfo(radar, target);
                }
                
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.LabelField("No targets detected", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawTargetInfo(RH_RadarModule radar, GameObject target)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // Target Header
        EditorGUILayout.BeginHorizontal();
        
        var contact = radar.GetRadarContact(target);
        if (contact != null && !contact.IsCurrentlyDetected)
        {
            GUI.color = new Color(1f, 0.6f, 0f); // Orange for lost contacts
        }
        
        EditorGUILayout.ObjectField("Target", target, typeof(GameObject), true);
        
        GUI.color = Color.cyan;
        if (GUILayout.Button("Select", GUILayout.Width(60)))
        {
            Selection.activeObject = target;
            SceneView.FrameLastActiveSceneView();
        }
        GUI.color = defaultGuiColor;
        EditorGUILayout.EndHorizontal();

        // Contact Information
        if (Application.isPlaying)
        {
            if (contact != null)
            {
                EditorGUILayout.Space(2);
                DrawContactInfo(contact);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private void DrawContactInfo(RadarContact contact)
    {
        EditorGUILayout.BeginVertical(contactInfoStyle);
        
        // Position and Velocity
        EditorGUI.indentLevel++;
        EditorGUILayout.Vector3Field("Position", contact.Position);
        EditorGUILayout.Vector3Field("Velocity", contact.Velocity);
        
        if (contact.RadialVelocity != 0)
        {
            string approach = contact.RadialVelocity < 0 ? "Approaching" : "Receding";
            EditorGUILayout.LabelField($"Radial Velocity: {Mathf.Abs(contact.RadialVelocity):F1} m/s ({approach})");
        }

        // Distance and Signal Strength
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Distance: {contact.Distance:F1}m", GUILayout.Width(150));
        DrawSignalStrengthBar(contact.SignalStrength);
        EditorGUILayout.EndHorizontal();

        float timeSinceUpdate = Time.realtimeSinceStartup - contact.LastUpdateTime;
        
        if (!contact.IsCurrentlyDetected)
        {
            GUI.color = new Color(1f, 0.6f, 0f); // Orange
            EditorGUILayout.LabelField($"Last Detection: {timeSinceUpdate:F1}s ago", EditorStyles.boldLabel);
            GUI.color = defaultGuiColor;
        }
        else
        {
            EditorGUILayout.LabelField($"Last Updated: {timeSinceUpdate:F2}s ago");
        }

        if (contact.IsJammed)
        {
            GUI.color = Color.yellow;
            EditorGUILayout.LabelField("⚠ Signal Jammed", warningStyle);
            GUI.color = defaultGuiColor;
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
    }

    private void DrawSignalStrengthBar(float strength)
    {
        Rect rect = GUILayoutUtility.GetRect(100, 15);
        rect.y += 2;
        rect.height = 4;
        
        // Background
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
        
        // Signal bar
        rect.width *= strength;
        Color barColor = Color.Lerp(Color.red, Color.green, strength);
        EditorGUI.DrawRect(rect, barColor);
    }

    private void DrawScanVisualizer(RH_RadarModule radar)
    {
        Rect rect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 40, 100);
        rect.x += 10;
        rect.width -= 20;

        // Draw background
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

        if (!radar.enableRotation)
        {
            // Draw sector limits
            float halfSector = radar.sectorSize * 0.5f;
            float minAngle = radar.sectorCenter - halfSector;
            float maxAngle = radar.sectorCenter + halfSector;

            Color sectorColor = new Color(0.3f, 0.6f, 0.3f, 0.2f);
            DrawAngleIndicator(rect, minAngle, maxAngle, sectorColor);
        }

        // Draw current scan angle
        DrawAngleIndicator(rect, radar.CurrentScanAngle - radar.beamWidth * 0.5f, 
                          radar.CurrentScanAngle + radar.beamWidth * 0.5f, 
                          new Color(1f, 1f, 0f, 0.3f));
    }

    private void DrawAngleIndicator(Rect rect, float startAngle, float endAngle, Color color)
    {
        float center = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height).x;
        float radius = Mathf.Min(rect.width, rect.height) * 0.45f;
        
        Vector2 start = new Vector2(
            center + radius * Mathf.Sin(startAngle * Mathf.Deg2Rad),
            rect.y + rect.height - radius * Mathf.Cos(startAngle * Mathf.Deg2Rad)
        );
        
        Vector2 end = new Vector2(
            center + radius * Mathf.Sin(endAngle * Mathf.Deg2Rad),
            rect.y + rect.height - radius * Mathf.Cos(endAngle * Mathf.Deg2Rad)
        );

        Handles.BeginGUI();
        Handles.color = color;
        Handles.DrawSolidArc(new Vector2(center, rect.y + rect.height), 
                            Vector3.forward, 
                            Vector3.right, 
                            -startAngle,
                            radius);
        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void OnDisable()
    {
        if (signalStrengthTexture != null)
        {
            DestroyImmediate(signalStrengthTexture);
        }
    }
}
