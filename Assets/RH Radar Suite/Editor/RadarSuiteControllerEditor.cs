using UnityEditor;
using UnityEngine;

namespace RHRadarSuite
{
    [CustomEditor(typeof(RadarSuiteController))]
    public class RadarSuiteControllerEditor : Editor
    {
        private bool showLODSettings = true;
        private bool showContactList = true;
        private Vector2 contactsScrollPosition;

        public override void OnInspectorGUI()
        {
            RadarSuiteController controller = (RadarSuiteController)target;

            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            // Status section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Radar Status", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = false;
            EditorGUILayout.Toggle("Initialized", controller.IsInitialized, GUILayout.Width(200));
            EditorGUILayout.Toggle("Active", controller.IsActive, GUILayout.Width(200));
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Current LOD: {controller.CurrentLOD}");

            if (Application.isPlaying)
            {
                EditorGUILayout.BeginHorizontal();
                
                if (!controller.IsInitialized)
                {
                    if (GUILayout.Button("Initialize", GUILayout.Width(100)))
                    {
                        controller.Initialize();
                    }
                }
                
                if (controller.IsInitialized && !controller.IsActive)
                {
                    if (GUILayout.Button("Activate", GUILayout.Width(100)))
                    {
                        controller.Activate();
                    }
                }
                else if (controller.IsActive)
                {
                    if (GUILayout.Button("Deactivate", GUILayout.Width(100)))
                    {
                        controller.Deactivate();
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // LOD Settings
            showLODSettings = EditorGUILayout.Foldout(showLODSettings, "LOD Settings", true);
            if (showLODSettings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.LabelField("Change LOD", EditorStyles.boldLabel);
                
                if (Application.isPlaying && controller.IsInitialized)
                {
                    RadarLOD newLOD = (RadarLOD)EditorGUILayout.EnumPopup("Select LOD", controller.CurrentLOD);
                    if (newLOD != controller.CurrentLOD)
                    {
                        controller.SetLOD(newLOD);
                    }
                    
                    EditorGUILayout.Space(5);
                    
                    EditorGUILayout.LabelField("LOD Description", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(GetLODDescription(controller.CurrentLOD), MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Enter Play Mode and initialize the radar to change LOD settings.", MessageType.Info);
                }
                
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            // Contacts List
            if (Application.isPlaying && controller.IsActive)
            {
                showContactList = EditorGUILayout.Foldout(showContactList, $"Detected Contacts ({controller.ActiveContacts.Count})", true);
                if (showContactList)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    if (controller.ActiveContacts.Count > 0)
                    {
                        contactsScrollPosition = EditorGUILayout.BeginScrollView(contactsScrollPosition, GUILayout.Height(200));
                        
                        foreach (var contact in controller.ActiveContacts)
                        {
                            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                            
                            EditorGUILayout.LabelField($"Contact: {contact.TargetName}", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField($"Range: {contact.Range:F1}m, Azimuth: {contact.Azimuth:F1}°, Elevation: {contact.Elevation:F1}°");
                            EditorGUILayout.LabelField($"Signal Strength: {contact.SignalStrength:P1}, Classification: {contact.Classification}");
                            
                            if (contact.Speed > 0.1f)
                            {
                                EditorGUILayout.LabelField($"Speed: {contact.Speed:F1} m/s, Radial Velocity: {contact.RadialVelocity:F1} m/s");
                            }
                            
                            EditorGUILayout.EndVertical();
                            
                            EditorGUILayout.Space(2);
                        }
                        
                        EditorGUILayout.EndScrollView();
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No contacts detected.", MessageType.Info);
                    }
                    
                    EditorGUILayout.EndVertical();
                }
            }

            // Apply changes
            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }

        private string GetLODDescription(RadarLOD lod)
        {
            switch (lod)
            {
                case RadarLOD.LOD1_PassiveDetection:
                    return "Passive Detection: Detects and identifies signals or emissions from external sources without transmitting. Provides basic directional awareness.";
                
                case RadarLOD.LOD2_BasicRadar:
                    return "Basic Radar: Actively transmits pulses and measures range to targets. Provides simple target detection and tracking.";
                
                case RadarLOD.LOD3_DopplerRadar:
                    return "Doppler Radar: Includes Doppler processing for radial velocity. Can identify moving vs. stationary targets.";
                
                case RadarLOD.LOD4_3DTracking:
                    return "3D Tracking: Determines 3D spatial location: range, velocity, azimuth/elevation. Uses phased arrays/antenna patterns for angle measurement.";
                
                case RadarLOD.LOD5_HighFidelity:
                    return "High-Fidelity Active Tracking: SAR/ISAR-like features for detailed imaging. Fine resolution in both range and velocity. Advanced clutter, interference, and ECCM modeling.";
                
                default:
                    return "Unknown LOD";
            }
        }
    }
}
