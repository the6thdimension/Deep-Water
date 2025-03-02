using UnityEngine;
using UnityEditor;
using System;

namespace RH.Utilities
{
    /// <summary>
    /// Utility class for displaying input dialogs in the Unity Editor
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private string title;
        private string message;
        private string inputText;
        private string defaultValue;
        private Action<string> onConfirm;
        private bool isCancelled;
        private bool isMultiline;
        private Vector2 scrollPosition;

        /// <summary>
        /// Shows a simple input dialog and returns the input text
        /// </summary>
        public static string Show(string title, string message, string defaultValue = "", bool multiline = false)
        {
            string result = null;

            EditorInputDialog window = CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window.title = title;
            window.message = message;
            window.defaultValue = defaultValue;
            window.inputText = defaultValue;
            window.isMultiline = multiline;
            window.onConfirm = (inputValue) => {
                result = inputValue;
            };

            window.position = new Rect(Screen.width / 2, Screen.height / 2, 400, multiline ? 250 : 150);
            window.ShowModal();

            return result;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(10);

            // Handle keyboard input
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return && !isMultiline)
            {
                if (onConfirm != null)
                {
                    onConfirm.Invoke(inputText);
                }
                Close();
                e.Use();
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                isCancelled = true;
                Close();
                e.Use();
            }

            if (isMultiline)
            {
                EditorGUILayout.LabelField("Input:", EditorStyles.boldLabel);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(100));
                inputText = EditorGUILayout.TextArea(inputText, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Input:", GUILayout.Width(50));
                GUI.SetNextControlName("InputField");
                inputText = EditorGUILayout.TextField(inputText);
                EditorGUILayout.EndHorizontal();
                
                // Focus the text field
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.FocusTextInControl("InputField");
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Cancel", GUILayout.Width(100)))
            {
                isCancelled = true;
                Close();
            }
            
            if (GUILayout.Button("OK", GUILayout.Width(100)))
            {
                if (onConfirm != null)
                {
                    onConfirm.Invoke(inputText);
                }
                Close();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void OnLostFocus()
        {
            // Don't close the window when it loses focus
        }
    }
}
