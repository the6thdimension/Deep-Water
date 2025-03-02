using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

public class RH_SceneManager : EditorWindow
{
    private List<SceneAsset> sceneList = new List<SceneAsset>();
    private Vector2 scrollPosition;
    private SceneAsset sceneToAdd;
    private string searchFilter = "";
    private bool showPaths = false;
    
    [MenuItem("Window/REALMHAUS/Quick Scene Loader")]
    public static void ShowWindow()
    {
        var window = GetWindow<RH_SceneManager>("Quick Scene Loader");
        window.minSize = new Vector2(300, 200);
    }

    private void OnEnable()
    {
        LoadSavedScenes();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(5);
        
        // Add scene field
        EditorGUILayout.BeginHorizontal();
        sceneToAdd = (SceneAsset)EditorGUILayout.ObjectField("Add Scene", sceneToAdd, typeof(SceneAsset), false);
        if (sceneToAdd != null)
        {
            if (!sceneList.Contains(sceneToAdd))
            {
                sceneList.Add(sceneToAdd);
                SaveSceneList();
            }
            sceneToAdd = null;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Search and options
        EditorGUILayout.BeginHorizontal();
        searchFilter = EditorGUILayout.TextField("Search", searchFilter);
        showPaths = EditorGUILayout.ToggleLeft("Show Paths", showPaths, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Scene list
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        var filteredScenes = string.IsNullOrEmpty(searchFilter) 
            ? sceneList 
            : sceneList.Where(s => s != null && s.name.ToLower().Contains(searchFilter.ToLower())).ToList();

        for (int i = 0; i < filteredScenes.Count; i++)
        {
            var scene = filteredScenes[i];
            if (scene == null) continue;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            // Scene name and path
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(scene.name, EditorStyles.boldLabel);
            if (showPaths)
            {
                EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(scene), EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            // Load button
            if (GUILayout.Button("Load", GUILayout.Width(60)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(scene));
                }
            }

            // Remove button
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("Remove Scene", 
                    $"Remove {scene.name} from the list?", "Yes", "No"))
                {
                    sceneList.RemoveAt(i);
                    SaveSceneList();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        // Bottom buttons
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Clear List"))
        {
            if (EditorUtility.DisplayDialog("Clear Scene List", 
                "Are you sure you want to clear the entire list?", "Yes", "No"))
            {
                sceneList.Clear();
                SaveSceneList();
            }
        }

        if (GUILayout.Button("Add Current Scene"))
        {
            var currentScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                EditorSceneManager.GetActiveScene().path);
            if (currentScene != null && !sceneList.Contains(currentScene))
            {
                sceneList.Add(currentScene);
                SaveSceneList();
            }
        }
        
        EditorGUILayout.EndHorizontal();
    }

    private void SaveSceneList()
    {
        var paths = sceneList.Select(s => AssetDatabase.GetAssetPath(s)).ToArray();
        EditorPrefs.SetString("RH_SceneManager_SceneList", string.Join(";", paths));
    }

    private void LoadSavedScenes()
    {
        sceneList.Clear();
        var savedData = EditorPrefs.GetString("RH_SceneManager_SceneList", "");
        
        if (!string.IsNullOrEmpty(savedData))
        {
            var paths = savedData.Split(';');
            foreach (var path in paths)
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (scene != null)
                {
                    sceneList.Add(scene);
                }
            }
        }
    }
}
