using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FixedWingController))]
public class FixedWingController_Editor : Editor
{

    #region Variables
    private FixedWingController targetInput;
    #endregion

    #region Bultin Methods
    void OnEnable()
    {
        targetInput = (FixedWingController)target;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        string debugInfo = "";
        debugInfo += "Pitch = " + targetInput.pitch + "\n";
        debugInfo += "Roll = " + targetInput.roll + "\n";
        debugInfo += "Yaw = " + targetInput.yaw + "\n";
        debugInfo += "Throttle = " + targetInput.throttle + "\n";
        debugInfo += "Brake = " + targetInput.brake + "\n";
        
        //Custom Editor Code
        GUILayout.Space(20);
        EditorGUILayout.TextArea(debugInfo, GUILayout.Height(100));
        GUILayout.Space(20);

        Repaint();
    }
    #endregion

}
