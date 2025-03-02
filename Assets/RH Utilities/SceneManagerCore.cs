using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace RH.Utilities
{
    /// <summary>
    /// Core functionality for the RH Scene Manager
    /// </summary>
    public static class SceneManagerCore
    {
        private const string PREF_KEY_GROUPS = "RH_SceneManager_Groups";
        private const string PREF_KEY_MACROS = "RH_SceneManager_Macros";
        private const string PREF_KEY_PREFERENCES = "RH_SceneManager_Preferences";
        private const string PREF_KEY_RECENT_SCENES = "RH_SceneManager_RecentScenes";
        private const string PREF_KEY_SCENE_GROUPS = "RH_SceneManager_SceneGroups";
        private const string PREF_KEY_SCENE_MACROS = "RH_SceneManager_SceneMacros";
        private const string THUMBNAIL_FOLDER = "Assets/RH Utilities/Editor/Resources/SceneThumbnails/";

        private static List<SceneGroup> sceneGroups;
        private static List<SceneItem> recentScenes;
        private static List<SceneMacro> sceneMacros;
        private static UserPreferences preferences;
        private static SceneManagerPreferences sceneManagerPreferences;

        /// <summary>
        /// Gets all scene groups
        /// </summary>
        public static List<SceneGroup> GetSceneGroups()
        {
            if (sceneGroups == null)
            {
                sceneGroups = new List<SceneGroup>();
                
                // Load from EditorPrefs
                if (EditorPrefs.HasKey(PREF_KEY_SCENE_GROUPS))
                {
                    try
                    {
                        string json = EditorPrefs.GetString(PREF_KEY_SCENE_GROUPS);
                        var wrapper = JsonUtility.FromJson<SceneGroupList>(json);
                        sceneGroups = wrapper.Groups;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error loading scene groups: {ex.Message}");
                    }
                }
                
                // If no groups exist, create a default group
                if (sceneGroups.Count == 0)
                {
                    sceneGroups.Add(new SceneGroup("Default", "Default scene group"));
                }
            }
            
            return sceneGroups;
        }

        /// <summary>
        /// Gets all scene macros
        /// </summary>
        public static List<SceneMacro> GetSceneMacros()
        {
            if (sceneMacros == null)
            {
                sceneMacros = new List<SceneMacro>();
                
                // Load from EditorPrefs
                if (EditorPrefs.HasKey(PREF_KEY_SCENE_MACROS))
                {
                    try
                    {
                        string json = EditorPrefs.GetString(PREF_KEY_SCENE_MACROS);
                        var wrapper = JsonUtility.FromJson<SceneMacroList>(json);
                        sceneMacros = wrapper.Macros;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error loading scene macros: {ex.Message}");
                    }
                }
            }
            
            return sceneMacros;
        }

        /// <summary>
        /// Gets the user preferences
        /// </summary>
        public static SceneManagerPreferences GetPreferences()
        {
            if (sceneManagerPreferences == null)
            {
                LoadPreferences();
            }
            return sceneManagerPreferences;
        }

        /// <summary>
        /// Gets recent scenes
        /// </summary>
        public static List<SceneItem> GetRecentScenes()
        {
            if (recentScenes == null)
            {
                recentScenes = new List<SceneItem>();
                
                // Load from EditorPrefs
                if (EditorPrefs.HasKey(PREF_KEY_RECENT_SCENES))
                {
                    try
                    {
                        string json = EditorPrefs.GetString(PREF_KEY_RECENT_SCENES);
                        var wrapper = JsonUtility.FromJson<SceneItemList>(json);
                        recentScenes = wrapper.Scenes;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error loading recent scenes: {ex.Message}");
                    }
                }
            }
            
            return recentScenes;
        }

        /// <summary>
        /// Saves recent scenes to EditorPrefs
        /// </summary>
        public static void SaveRecentScenes()
        {
            if (recentScenes == null)
                return;

            try
            {
                var wrapper = new SceneItemList { Scenes = recentScenes };
                string json = JsonUtility.ToJson(wrapper);
                EditorPrefs.SetString(PREF_KEY_RECENT_SCENES, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving recent scenes: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a scene to recent scenes
        /// </summary>
        public static void AddToRecentScenes(SceneItem scene)
        {
            if (scene == null || string.IsNullOrEmpty(scene.Path))
                return;
                
            var prefs = GetUserPreferences();
            if (!prefs.TrackRecentScenes)
                return;
                
            var scenes = GetRecentScenes();
            
            // Remove if already exists
            scenes.RemoveAll(s => s.Path == scene.Path);
            
            // Add to beginning
            scene.LastOpened = DateTime.Now;
            scenes.Insert(0, scene);
            
            // Trim list if needed
            if (scenes.Count > prefs.MaxRecentScenes)
            {
                scenes.RemoveRange(prefs.MaxRecentScenes, scenes.Count - prefs.MaxRecentScenes);
            }
            
            // Save changes
            SaveRecentScenes();
        }

        /// <summary>
        /// Clears recent scenes
        /// </summary>
        public static void ClearRecentScenes()
        {
            recentScenes = new List<SceneItem>();
            SaveRecentScenes();
        }

        /// <summary>
        /// Loads a scene
        /// </summary>
        public static void LoadScene(SceneItem sceneItem, bool additive = false)
        {
            if (sceneItem == null || string.IsNullOrEmpty(sceneItem.Path))
                return;

            if (GetUserPreferences().ConfirmSceneLoad && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                if (additive || sceneItem.LoadAdditive)
                {
                    EditorSceneManager.OpenScene(sceneItem.Path, OpenSceneMode.Additive);
                }
                else
                {
                    EditorSceneManager.OpenScene(sceneItem.Path);
                }

                // Update access count and last accessed time
                sceneItem.IncrementAccessCount();
                
                // Add to recent scenes
                if (GetUserPreferences().TrackRecentScenes)
                {
                    AddToRecentScenes(sceneItem);
                }

                // Save changes
                SaveSceneGroups();
                SaveRecentScenes();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading scene {sceneItem.Name}: {ex.Message}");
                EditorUtility.DisplayDialog("Scene Load Error", 
                    $"Failed to load scene {sceneItem.Name}. See console for details.", "OK");
            }
        }

        /// <summary>
        /// Executes a scene macro
        /// </summary>
        public static void ExecuteMacro(SceneMacro macro)
        {
            if (macro == null || macro.ScenesInvolved.Count == 0)
                return;

            if (GetPreferences().ConfirmSceneLoad && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            try
            {
                // Load the first scene normally
                var firstScene = macro.ScenesInvolved[0];
                EditorSceneManager.OpenScene(firstScene.Path);
                firstScene.IncrementAccessCount();

                // Load additional scenes additively
                for (int i = 1; i < macro.ScenesInvolved.Count; i++)
                {
                    var scene = macro.ScenesInvolved[i];
                    EditorSceneManager.OpenScene(scene.Path, OpenSceneMode.Additive);
                    scene.IncrementAccessCount();
                }

                // Execute custom action if specified
                if (!string.IsNullOrEmpty(macro.CustomAction))
                {
                    // This would be implemented based on specific project needs
                    Debug.Log($"Executing custom action: {macro.CustomAction}");
                }

                // Save changes
                SaveSceneGroups();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing macro {macro.Name}: {ex.Message}");
                EditorUtility.DisplayDialog("Macro Execution Error", 
                    $"Failed to execute macro {macro.Name}. See console for details.", "OK");
            }
        }

        /// <summary>
        /// Adds a scene to a group
        /// </summary>
        public static void AddSceneToGroup(SceneAsset sceneAsset, string groupName)
        {
            if (sceneAsset == null)
                return;

            var groups = GetSceneGroups();
            var group = groups.FirstOrDefault(g => g.Name == groupName);
            
            if (group == null)
            {
                group = new SceneGroup(groupName);
                groups.Add(group);
            }

            string path = AssetDatabase.GetAssetPath(sceneAsset);
            
            // Check if scene already exists in the group
            if (group.Scenes.Any(s => s.Path == path))
                return;

            var sceneItem = new SceneItem(sceneAsset.name, path);
            group.Scenes.Add(sceneItem);
            
            SaveSceneGroups();
        }

        /// <summary>
        /// Removes a scene from a group
        /// </summary>
        public static void RemoveSceneFromGroup(SceneItem sceneItem, string groupName)
        {
            var groups = GetSceneGroups();
            var group = groups.FirstOrDefault(g => g.Name == groupName);
            
            if (group == null)
                return;

            group.Scenes.RemoveAll(s => s.Path == sceneItem.Path);
            
            SaveSceneGroups();
        }

        /// <summary>
        /// Creates a new scene group
        /// </summary>
        public static SceneGroup CreateSceneGroup(string name, string description = "")
        {
            var groups = GetSceneGroups();
            
            if (groups.Any(g => g.Name == name))
                return groups.First(g => g.Name == name);

            var newGroup = new SceneGroup(name, description);
            groups.Add(newGroup);
            
            SaveSceneGroups();
            return newGroup;
        }

        /// <summary>
        /// Removes a scene group
        /// </summary>
        public static void RemoveSceneGroup(string groupName)
        {
            var groups = GetSceneGroups();
            groups.RemoveAll(g => g.Name == groupName);
            
            SaveSceneGroups();
        }

        /// <summary>
        /// Creates a new scene macro
        /// </summary>
        public static SceneMacro CreateSceneMacro(string name, string description = "")
        {
            var macros = GetSceneMacros();
            
            if (macros.Any(m => m.Name == name))
                return macros.First(m => m.Name == name);

            var newMacro = new SceneMacro(name, description);
            macros.Add(newMacro);
            
            SaveSceneMacros();
            return newMacro;
        }

        /// <summary>
        /// Removes a scene macro
        /// </summary>
        public static void RemoveSceneMacro(string macroName)
        {
            var macros = GetSceneMacros();
            var macro = macros.FirstOrDefault(m => m.Name == macroName);
            
            if (macro != null)
            {
                macros.Remove(macro);
                SaveSceneMacros();
                Debug.Log($"Removed macro: {macroName}");
            }
        }

        /// <summary>
        /// Validates a scene for issues
        /// </summary>
        public static List<ValidationResult> ValidateScene(SceneItem sceneItem)
        {
            var results = new List<ValidationResult>();
            
            if (sceneItem == null || string.IsNullOrEmpty(sceneItem.Path))
                return results;

            // Check if scene exists
            if (!File.Exists(sceneItem.Path))
            {
                results.Add(new ValidationResult(false, "Scene file does not exist", ValidationSeverity.Error));
                return results;
            }

            // Additional validation logic would be implemented here
            // For example:
            // - Check for missing references
            // - Check for lighting bake status
            // - Check for performance issues
            
            return results;
        }

        /// <summary>
        /// Captures a thumbnail for a scene
        /// </summary>
        public static void CaptureSceneThumbnail(SceneItem sceneItem)
        {
            if (sceneItem == null || string.IsNullOrEmpty(sceneItem.Path))
                return;

            // Ensure thumbnail folder exists
            if (!Directory.Exists(THUMBNAIL_FOLDER))
            {
                Directory.CreateDirectory(THUMBNAIL_FOLDER);
            }

            // Generate a unique filename for the thumbnail
            string thumbnailName = $"{Path.GetFileNameWithoutExtension(sceneItem.Path)}_{DateTime.Now.Ticks}.png";
            string thumbnailPath = Path.Combine(THUMBNAIL_FOLDER, thumbnailName);

            // Capture thumbnail logic would be implemented here
            // This is a placeholder as actual implementation would require
            // capturing a screenshot of the scene view or game view
            
            sceneItem.ThumbnailPath = thumbnailPath;
            SaveSceneGroups();
        }

        /// <summary>
        /// Loads scene groups from EditorPrefs
        /// </summary>
        private static void LoadSceneGroups()
        {
            sceneGroups = new List<SceneGroup>();
            
            string json = EditorPrefs.GetString(PREF_KEY_GROUPS, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    sceneGroups = JsonUtility.FromJson<SceneGroupList>(json).Groups;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading scene groups: {ex.Message}");
                }
            }

            // Add default group if none exist
            if (sceneGroups.Count == 0)
            {
                sceneGroups.Add(new SceneGroup("Default", "Default scene group"));
            }
        }

        /// <summary>
        /// Saves scene groups to EditorPrefs
        /// </summary>
        public static void SaveSceneGroups()
        {
            if (sceneGroups == null)
                return;

            try
            {
                var wrapper = new SceneGroupList { Groups = sceneGroups };
                string json = JsonUtility.ToJson(wrapper);
                EditorPrefs.SetString(PREF_KEY_SCENE_GROUPS, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving scene groups: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads scene macros from EditorPrefs
        /// </summary>
        private static void LoadSceneMacros()
        {
            sceneMacros = new List<SceneMacro>();
            
            string json = EditorPrefs.GetString(PREF_KEY_MACROS, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    sceneMacros = JsonUtility.FromJson<SceneMacroList>(json).Macros;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading scene macros: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Saves scene macros to EditorPrefs
        /// </summary>
        public static void SaveSceneMacros()
        {
            if (sceneMacros == null)
                return;

            try
            {
                var wrapper = new SceneMacroList { Macros = sceneMacros };
                string json = JsonUtility.ToJson(wrapper);
                EditorPrefs.SetString(PREF_KEY_SCENE_MACROS, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving scene macros: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads user preferences from EditorPrefs
        /// </summary>
        private static void LoadPreferences()
        {
            sceneManagerPreferences = new SceneManagerPreferences();
            
            string json = EditorPrefs.GetString(PREF_KEY_PREFERENCES, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    sceneManagerPreferences = JsonUtility.FromJson<SceneManagerPreferences>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading preferences: {ex.Message}");
                }
            }

            // Set user identifier if not set
            if (string.IsNullOrEmpty(sceneManagerPreferences.UserIdentifier))
            {
                sceneManagerPreferences.UserIdentifier = Environment.UserName;
                SavePreferences();
            }
        }

        /// <summary>
        /// Saves user preferences to EditorPrefs
        /// </summary>
        private static void SavePreferences()
        {
            if (sceneManagerPreferences == null)
                return;

            try
            {
                string json = JsonUtility.ToJson(sceneManagerPreferences);
                EditorPrefs.SetString(PREF_KEY_PREFERENCES, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving preferences: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads recent scenes from EditorPrefs
        /// </summary>
        private static void LoadRecentScenes()
        {
            recentScenes = new List<SceneItem>();
            
            string json = EditorPrefs.GetString(PREF_KEY_RECENT_SCENES, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    recentScenes = JsonUtility.FromJson<SceneItemList>(json).Scenes;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading recent scenes: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Updates the user preferences
        /// </summary>
        public static void UpdatePreferences(SceneManagerPreferences newPreferences)
        {
            sceneManagerPreferences = newPreferences;
            SavePreferences();
        }

        /// <summary>
        /// Imports scene groups
        /// </summary>
        public static void ImportSceneGroups(List<SceneGroup> groups)
        {
            sceneGroups = groups;
            SaveSceneGroups();
        }
        
        /// <summary>
        /// Imports scene macros
        /// </summary>
        public static void ImportSceneMacros(List<SceneMacro> macros)
        {
            sceneMacros = macros;
            SaveSceneMacros();
        }
        
        /// <summary>
        /// Resets user preferences to defaults
        /// </summary>
        public static void ResetPreferences()
        {
            preferences = new UserPreferences();
            SaveUserPreferences();
        }
        
        /// <summary>
        /// Clears all data
        /// </summary>
        public static void ClearAllData()
        {
            // Backup data if enabled
            if (GetUserPreferences().BackupBeforeClear)
            {
                // TODO: Implement backup functionality
            }
            
            // Clear all data
            sceneGroups = new List<SceneGroup>();
            sceneMacros = new List<SceneMacro>();
            recentScenes = new List<SceneItem>();
            
            // Save empty data
            SaveSceneGroups();
            SaveSceneMacros();
            SaveRecentScenes();
        }

        /// <summary>
        /// Exports settings to a JSON file
        /// </summary>
        public static void ExportSettings(string filePath)
        {
            try
            {
                // Create export data
                var exportData = new SettingsExportData
                {
                    PreferencesJson = JsonUtility.ToJson(sceneManagerPreferences),
                    GroupsJson = JsonUtility.ToJson(new SceneGroupList { Groups = sceneGroups }),
                    MacrosJson = JsonUtility.ToJson(new SceneMacroList { Macros = sceneMacros })
                };

                // Serialize to JSON
                string json = JsonUtility.ToJson(exportData, true);
                File.WriteAllText(filePath, json);

                Debug.Log($"Settings exported to: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error exporting settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Imports settings from a JSON file
        /// </summary>
        public static void ImportSettings(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogError($"File not found: {filePath}");
                    return;
                }

                string json = File.ReadAllText(filePath);
                var importData = JsonUtility.FromJson<SettingsExportData>(json);

                if (importData == null)
                {
                    Debug.LogError("Invalid settings file format.");
                    return;
                }

                // Import preferences
                if (!string.IsNullOrEmpty(importData.PreferencesJson))
                {
                    sceneManagerPreferences = JsonUtility.FromJson<SceneManagerPreferences>(importData.PreferencesJson);
                    SavePreferences();
                }

                // Import groups
                if (!string.IsNullOrEmpty(importData.GroupsJson))
                {
                    var groupsWrapper = JsonUtility.FromJson<SceneGroupList>(importData.GroupsJson);
                    if (groupsWrapper != null && groupsWrapper.Groups != null)
                    {
                        sceneGroups = groupsWrapper.Groups;
                        SaveSceneGroups();
                    }
                }

                // Import macros
                if (!string.IsNullOrEmpty(importData.MacrosJson))
                {
                    var macrosWrapper = JsonUtility.FromJson<SceneMacroList>(importData.MacrosJson);
                    if (macrosWrapper != null && macrosWrapper.Macros != null)
                    {
                        sceneMacros = macrosWrapper.Macros;
                        SaveSceneMacros();
                    }
                }

                Debug.Log($"Settings imported from: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error importing settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets user preferences
        /// </summary>
        public static UserPreferences GetUserPreferences()
        {
            if (preferences == null)
            {
                preferences = new UserPreferences();
                
                // Load from EditorPrefs
                if (EditorPrefs.HasKey(PREF_KEY_PREFERENCES))
                {
                    try
                    {
                        string json = EditorPrefs.GetString(PREF_KEY_PREFERENCES);
                        preferences = JsonUtility.FromJson<UserPreferences>(json);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error loading preferences: {ex.Message}");
                    }
                }
            }
            
            return preferences;
        }
        
        /// <summary>
        /// Saves user preferences
        /// </summary>
        public static void SaveUserPreferences()
        {
            if (preferences == null)
                return;

            try
            {
                string json = JsonUtility.ToJson(preferences);
                EditorPrefs.SetString(PREF_KEY_PREFERENCES, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving preferences: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates user preferences
        /// </summary>
        public static void UpdatePreferences(UserPreferences newPreferences)
        {
            preferences = newPreferences;
            SaveUserPreferences();
        }
    }

    // Wrapper classes for JSON serialization
    [Serializable]
    public class SceneGroupList
    {
        public List<SceneGroup> Groups = new List<SceneGroup>();
    }

    [Serializable]
    public class SceneMacroList
    {
        public List<SceneMacro> Macros = new List<SceneMacro>();
    }

    [Serializable]
    public class SceneItemList
    {
        public List<SceneItem> Scenes = new List<SceneItem>();
    }

    [Serializable]
    public class SettingsExportData
    {
        public string PreferencesJson;
        public string GroupsJson;
        public string MacrosJson;
    }
}
