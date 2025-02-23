using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MissileLauncher))]
public class MissileSuiteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MissileLauncher launcher = (MissileLauncher)target;

        if (GUILayout.Button("Launch Missile"))
        {
            launcher.LaunchMissile();
        }

        if (launcher.CurrentTarget != null)
        {
            EditorGUILayout.LabelField("Target: " + launcher.CurrentTarget.name);
        }
        else
        {
            EditorGUILayout.LabelField("No target assigned.");
        }
    }
}
