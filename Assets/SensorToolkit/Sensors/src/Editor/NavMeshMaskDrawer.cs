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

#if UNITY_6000_0_OR_NEWER
                string[] areaNames = UnityEngine.AI.NavMesh.GetAreaNames();
#else
                string[] areaNames = GameObjectUtility.GetNavMeshAreaNames();
#endif
                List<string> completeAreaNames = new List<string>();

                foreach (string name in areaNames) {
#if UNITY_6000_0_OR_NEWER
                    var id = UnityEngine.AI.NavMesh.GetAreaFromName(name);
#else
                    var id = GameObjectUtility.GetNavMeshAreaFromName(name);
#endif
                    while (id >= completeAreaNames.Count) {
                        completeAreaNames.Add("");
                    }
                    completeAreaNames[id] = name;
                }

                int mask = serializedProperty.intValue;

                mask = EditorGUI.MaskField(position, mask, completeAreaNames.ToArray());
                if (EditorGUI.EndChangeCheck()) {
                    serializedProperty.intValue = mask;
                }
            }
        }
    }

}