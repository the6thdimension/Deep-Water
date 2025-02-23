using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BaseAirplane_Input))]
public class BaseAirplane_Input_Editor : Editor
{

    #region Variables
    private BaseAirplane_Input targetInput;
    #endregion

    #region Bultin Methods
    void OnEnable()
    {
        targetInput = (BaseAirplane_Input)target;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        string debugInfo = "";
        debugInfo += "Pitch = " + targetInput.Pitch + "\n";
        debugInfo += "Roll = " + targetInput.Roll + "\n";
        debugInfo += "Yaw = " + targetInput.Yaw + "\n";
        debugInfo += "Throttle = " + targetInput.Throttle + "\n";
        debugInfo += "Brake = " + targetInput.Brake + "\n";
        debugInfo += "Flaps = " + targetInput.Flaps + "\n";

        //Custom Editor Code
        GUILayout.Space(20);
        EditorGUILayout.TextArea(debugInfo, GUILayout.Height(100));
        GUILayout.Space(20);

        Repaint();
    }

    #endregion
}
