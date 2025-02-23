using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(F18Controller))]
public class F18ControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {

        F18Controller controller = (F18Controller)target;


        controller.startupAudio = (GameObject)EditorGUILayout.ObjectField("Start Up AudioSource", controller.startupAudio, typeof(GameObject), true);

        GUILayout.BeginHorizontal();

        if(GUILayout.Button("Start Up"))
        {
            controller.StartUp();
        }
        if(GUILayout.Button("Shut Down"))
        {
            // controller.startupAudio();
        }


        GUILayout.EndHorizontal();
    }

}
