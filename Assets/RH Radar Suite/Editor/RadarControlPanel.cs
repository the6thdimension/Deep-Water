using UnityEditor;
using UnityEngine;

namespace RHRadarSuite
{
    /// <summary>
    /// Custom Unity Editor Window for controlling and debugging the radar suite.
    /// </summary>
    public class RadarControlPanel : EditorWindow
    {
        private RadarSuiteController radarController;
        private Vector2 scrollPosition;

        [MenuItem("RH Navy Sims/Radar Suite/Radar Control Panel")]
        public static void ShowWindow()
        {
            GetWindow<RadarControlPanel>("Radar Control Panel");
        }

        private void OnGUI()
        {
            GUILayout.Label("Radar Control Panel", EditorStyles.boldLabel);

            // Radar Controller Selection
            radarController = (RadarSuiteController)EditorGUILayout.ObjectField("Radar Controller", radarController, typeof(RadarSuiteController), true);

            if (radarController == null)
            {
                EditorGUILayout.HelpBox("Please assign a RadarSuiteController to control.", MessageType.Warning);
                return;
            }

            // Display and modify radar parameters
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Radar Parameters", EditorStyles.boldLabel);
            radarController.SetRadarParameter("detectionRange", EditorGUILayout.FloatField("Detection Range", radarController.MaxDetectionRange));
            radarController.SetRadarParameter("radarPower", EditorGUILayout.FloatField("Radar Power", radarController.RadarPower));
            radarController.SetRadarParameter("updateInterval", EditorGUILayout.FloatField("Update Interval", radarController.UpdateInterval));
            radarController.SetRadarParameter("maxTargets", EditorGUILayout.IntField("Max Targets", radarController.MaxTargets));

            EditorGUILayout.Space();

            // LOD Selection
            EditorGUILayout.LabelField("Level of Detail (LOD)", EditorStyles.boldLabel);
            radarController.SetLOD((RadarLOD)EditorGUILayout.EnumPopup("Current LOD", radarController.CurrentLOD));

            EditorGUILayout.Space();

            // Activation Controls
            EditorGUILayout.LabelField("Radar Activation", EditorStyles.boldLabel);
            if (GUILayout.Button(radarController.IsActive ? "Deactivate Radar" : "Activate Radar"))
            {
                if (radarController.IsActive)
                {
                    radarController.Deactivate();
                }
                else
                {
                    radarController.Activate();
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
