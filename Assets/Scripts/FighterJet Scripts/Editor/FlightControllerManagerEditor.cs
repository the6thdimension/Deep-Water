using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FlightControllerManager))]
public class FlightControllerManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI(); // Draw the default inspector

        FlightControllerManager manager = (FlightControllerManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Module States", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(true); // Disable editing

        foreach (var module in manager.modules)
        {
            string status = module.currentState.ToString();
            EditorGUILayout.TextField(module.module != null ? module.module.name : "Unnamed Module", status);
        }

        EditorGUI.EndDisabledGroup();
    }
}

