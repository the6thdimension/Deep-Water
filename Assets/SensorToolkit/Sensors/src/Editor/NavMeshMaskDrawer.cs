using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Micosmo.SensorToolkit;

namespace Micosmo.SensorToolkit.Editors {

    [CustomPropertyDrawer(typeof(NavMeshMaskAttribute))]
    public class NavMeshMaskDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty serializedProperty, GUIContent label) {

            using (new GUILayout.HorizontalScope()) {

                position = EditorGUI.PrefixLabel(position, label);

                EditorGUI.BeginChangeCheck();

                string[] areaNames = UnityEngine.AI.NavMesh.GetAreaNames();
                string[] completeAreaNames = new string[areaNames.Length];

                foreach (string name in areaNames) {
                    completeAreaNames[UnityEngine.AI.NavMesh.GetAreaFromName(name)] = name;
                }

                int mask = serializedProperty.intValue;

                mask = EditorGUI.MaskField(position, mask, completeAreaNames);
                if (EditorGUI.EndChangeCheck()) {
                    serializedProperty.intValue = mask;
                }
            }
        }
    }

}