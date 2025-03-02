using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using RH.Utilities;

namespace RH.Utilities
{
    public class RH_SceneManagerWindow : EditorWindow
    {
        // UI State
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private bool showSettings = false;
        private bool showGroups = true;
        private bool showRecent = true;
        private bool showMacros = true;
        private int selectedTab = 0;
        private RH.Utilities.SceneGroup selectedGroup;
        private RH.Utilities.SceneItem selectedScene;
        private RH.Utilities.SceneMacro selectedMacro;
        
        // UI Styles
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle sceneButtonStyle;
        private GUIStyle groupHeaderStyle;
        private GUIStyle tabStyle;
        private GUIStyle searchBoxStyle;
        private Texture2D logoTexture;
        private Texture2D defaultThumbnail;
        
        // Tab names
        private readonly string[] tabNames = { "Scenes", "Recent", "Macros", "Settings" };
        
        // Constants
        private const float THUMBNAIL_SIZE = 64f;
        
        // Field to track which macro we're adding a scene to
        private RH.Utilities.SceneMacro currentMacroForSceneAdd;
        
        [MenuItem("Window/REALMHAUS/Scene Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<RH_SceneManagerWindow>("RH Scene Manager");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }
        
        private void OnEnable()
        {
            // Initialize styles
            InitializeStyles();
            
            // Load resources
            LoadResources();
            
            // Load default thumbnail
            defaultThumbnail = EditorGUIUtility.FindTexture("SceneAsset Icon") as Texture2D;
            if (defaultThumbnail == null)
            {
                defaultThumbnail = EditorGUIUtility.FindTexture("DefaultAsset Icon") as Texture2D;
            }
        }
        
        private void InitializeStyles()
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(0, 0, 10, 10)
            };
            
            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(0, 0, 5, 5)
            };
            
            sceneButtonStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(5, 5, 5, 5),
                margin = new RectOffset(0, 0, 2, 2)
            };
            
            groupHeaderStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };
            
            tabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fixedHeight = 30,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            
            searchBoxStyle = new GUIStyle(EditorStyles.toolbarSearchField)
            {
                fixedHeight = 22,
                margin = new RectOffset(5, 5, 5, 5)
            };
        }
        
        private void LoadResources()
        {
            // Try to load logo
            logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RH Utilities/Editor/Resources/SceneManagerLogo.png");
            
            // Load default thumbnail
            //defaultThumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RH Utilities/Editor/Resources/DefaultSceneThumbnail.png");
            
            // Create default thumbnail if it doesn't exist
            //if (defaultThumbnail == null)
            //{
            //    CreateDefaultThumbnail();
            //}
        }
        
        private void CreateDefaultThumbnail()
        {
            string resourcesPath = "Assets/RH Utilities/Editor/Resources";
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(resourcesPath))
            {
                Directory.CreateDirectory(resourcesPath);
            }
            
            // Create a simple texture
            Texture2D texture = new Texture2D(128, 128);
            Color[] colors = new Color[128 * 128];
            
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color(0.3f, 0.3f, 0.3f);
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            
            // Save the texture
            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(resourcesPath, "DefaultSceneThumbnail.png"), bytes);
            
            AssetDatabase.Refresh();
            
            // Load the texture
            defaultThumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RH Utilities/Editor/Resources/DefaultSceneThumbnail.png");
        }
        
        private void OnGUI()
        {
            // Initialize styles if needed
            if (headerStyle == null)
            {
                InitializeStyles();
            }
            
            // Draw header
            DrawHeader();
            
            // Draw tabs
            DrawTabs();
            
            EditorGUILayout.Space(10);
            
            // Draw selected tab content
            switch (selectedTab)
            {
                case 0:
                    DrawScenesTab();
                    break;
                case 1:
                    DrawRecentScenesTab();
                    break;
                case 2:
                    DrawMacrosTab();
                    break;
                case 3:
                    DrawSettingsTab();
                    break;
            }
            
            // Process events for object picker
            if (Event.current != null && Event.current.commandName == "ObjectSelectorClosed")
            {
                HandleObjectPickerClosed();
            }
        }
        
        private void HandleObjectPickerClosed()
        {
            var selectedObject = EditorGUIUtility.GetObjectPickerObject();
            if (selectedObject != null && selectedObject is SceneAsset sceneAsset)
            {
                // Handle the selected scene asset based on the current context
                if (currentMacroForSceneAdd != null)
                {
                    AddSceneToMacro(sceneAsset, currentMacroForSceneAdd);
                    currentMacroForSceneAdd = null;
                }
            }
        }
        
        private void AddSceneToMacro(SceneAsset sceneAsset, RH.Utilities.SceneMacro macro)
        {
            string path = AssetDatabase.GetAssetPath(sceneAsset);
            
            // Check if scene already exists in the macro
            if (!macro.ScenesInvolved.Any(s => s.Path == path))
            {
                var sceneItem = new RH.Utilities.SceneItem(sceneAsset.name, path);
                macro.ScenesInvolved.Add(sceneItem);
                SceneManagerCore.SaveSceneMacros();
                
                EditorUtility.DisplayDialog("Add to Macro", 
                    $"Added '{sceneAsset.name}' to macro '{macro.Name}'.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Add to Macro", 
                    "This scene is already in the macro.", "OK");
            }
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            
            // Draw logo if available
            if (logoTexture != null)
            {
                Rect logoRect = EditorGUILayout.GetControlRect(false, 80);
                logoRect.x = (position.width - 80) / 2;
                logoRect.width = 80;
                GUI.DrawTexture(logoRect, logoTexture, ScaleMode.ScaleToFit);
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.LabelField("RH Scene Manager", headerStyle);
            EditorGUILayout.Space(5);
        }
        
        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            
            for (int i = 0; i < tabNames.Length; i++)
            {
                bool isSelected = selectedTab == i;
                GUI.backgroundColor = isSelected ? Color.gray : Color.white;
                
                if (GUILayout.Toggle(isSelected, tabNames[i], tabStyle))
                {
                    selectedTab = i;
                }
                
                GUI.backgroundColor = Color.white;
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);
        }
        
        private void DrawScenesTab()
        {
            EditorGUILayout.BeginVertical();
            
            // Search bar
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            string newSearchFilter = EditorGUILayout.TextField(searchFilter, searchBoxStyle);
            
            if (newSearchFilter != searchFilter)
            {
                searchFilter = newSearchFilter;
            }
            
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                searchFilter = "";
                GUI.FocusControl(null);
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Add scene field
            EditorGUILayout.BeginHorizontal();
            SceneAsset sceneToAdd = (SceneAsset)EditorGUILayout.ObjectField("Add Scene", null, typeof(SceneAsset), false);
            
            if (sceneToAdd != null)
            {
                AddSceneToSelectedGroup(sceneToAdd);
            }
            
            if (GUILayout.Button("Add Current", GUILayout.Width(100)))
            {
                AddCurrentSceneToSelectedGroup();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Group selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Group:", GUILayout.Width(50));
            
            // Get all groups
            var groups = SceneManagerCore.GetSceneGroups();
            string[] groupNames = groups.Select(g => g.Name).ToArray();
            
            // Find index of selected group
            int selectedIndex = 0;
            if (selectedGroup != null)
            {
                selectedIndex = Array.IndexOf(groupNames, selectedGroup.Name);
                if (selectedIndex < 0) selectedIndex = 0;
            }
            
            // Group dropdown
            int newSelectedIndex = EditorGUILayout.Popup(selectedIndex, groupNames);
            if (newSelectedIndex != selectedIndex || selectedGroup == null)
            {
                selectedGroup = groups[newSelectedIndex];
            }
            
            // Add group button
            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                ShowAddGroupDialog();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Scene list
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            if (groups.Count > 0)
            {
                foreach (var group in groups)
                {
                    DrawSceneGroup(group);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No scene groups found. Create a new group to get started.", MessageType.Info);
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSceneGroup(RH.Utilities.SceneGroup group)
        {
            // Skip empty groups if searching
            if (!string.IsNullOrEmpty(searchFilter) && 
                !group.Scenes.Any(s => s.Name.ToLower().Contains(searchFilter.ToLower())))
            {
                return;
            }
            
            // Group header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // Group color
            EditorGUI.BeginChangeCheck();
            Color newColor = EditorGUILayout.ColorField(GUIContent.none, group.Color, false, false, false, GUILayout.Width(20));
            if (EditorGUI.EndChangeCheck())
            {
                group.Color = newColor;
                SceneManagerCore.GetSceneGroups(); // Save changes
            }
            
            // Group foldout
            bool wasExpanded = group.IsExpanded;
            group.IsExpanded = EditorGUILayout.Foldout(group.IsExpanded, group.Name, true, groupHeaderStyle);
            
            if (wasExpanded != group.IsExpanded)
            {
                SceneManagerCore.GetSceneGroups(); // Save changes
            }
            
            // Group options
            if (GUILayout.Button("...", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                ShowGroupOptionsMenu(group);
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Scene list
            if (group.IsExpanded)
            {
                EditorGUI.indentLevel++;
                
                // Filter scenes based on search
                var filteredScenes = string.IsNullOrEmpty(searchFilter) 
                    ? group.Scenes 
                    : group.Scenes.Where(s => s.Name.ToLower().Contains(searchFilter.ToLower())).ToList();
                
                if (filteredScenes.Count > 0)
                {
                    foreach (var scene in filteredScenes)
                    {
                        DrawSceneItem(scene, group);
                    }
                }
                else if (group.Scenes.Count > 0)
                {
                    EditorGUILayout.LabelField("No scenes match the search filter.");
                }
                else
                {
                    EditorGUILayout.LabelField("No scenes in this group.");
                }
                
                EditorGUI.indentLevel--;
            }
        }
        
        private void DrawSceneItem(RH.Utilities.SceneItem scene, RH.Utilities.SceneGroup group)
        {
            var preferences = SceneManagerCore.GetUserPreferences();
            
            // Scene item container
            EditorGUILayout.BeginHorizontal(sceneButtonStyle);
            
            // Thumbnail
            if (preferences.ShowThumbnails)
            {
                Texture2D thumbnail = null;
                
                if (!string.IsNullOrEmpty(scene.ThumbnailPath))
                {
                    thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(scene.ThumbnailPath);
                }
                
                if (thumbnail == null)
                {
                    thumbnail = defaultThumbnail;
                }
                
                EditorGUILayout.BeginHorizontal(GUILayout.Width(THUMBNAIL_SIZE + 8));
                GUILayout.Box(thumbnail, GUILayout.Width(THUMBNAIL_SIZE), GUILayout.Height(THUMBNAIL_SIZE));
                
                // Add a small button to regenerate the thumbnail
                if (GUILayout.Button("↻", GUILayout.Width(20), GUILayout.Height(20)))
                {
                    GenerateThumbnailForScene(scene);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            // Scene info
            EditorGUILayout.BeginVertical();
            
            // Scene name and favorite
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(scene.Name, EditorStyles.boldLabel);
            
            // Favorite toggle
            EditorGUI.BeginChangeCheck();
            scene.IsFavorite = EditorGUILayout.Toggle(scene.IsFavorite, GUILayout.Width(20));
            if (EditorGUI.EndChangeCheck())
            {
                SceneManagerCore.GetSceneGroups(); // Save changes
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Path
            if (preferences.ShowPaths)
            {
                EditorGUILayout.LabelField(scene.Path, EditorStyles.miniLabel);
            }
            
            // Tags
            if (scene.Tags != null && scene.Tags.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Tags:", GUILayout.Width(40));
                EditorGUILayout.LabelField(string.Join(", ", scene.Tags), EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            
            // Performance metrics
            if (preferences.ShowPerformanceMetrics)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Load: {scene.EstimatedLoadTime:F1}s", EditorStyles.miniLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField($"Memory: {scene.EstimatedMemoryUsage:F1}MB", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            
            // Validation status
            if (preferences.ShowValidationStatus && scene.HasValidationIssues())
            {
                EditorGUILayout.HelpBox("Has validation issues", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
            
            // Buttons
            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            
            // Load button
            if (GUILayout.Button("Load", GUILayout.Height(25)))
            {
                SceneManagerCore.LoadScene(scene);
            }
            
            // Additive load button
            if (GUILayout.Button("+ Additive", GUILayout.Height(25)))
            {
                SceneManagerCore.LoadScene(scene, true);
            }
            
            // Options button
            if (GUILayout.Button("Options", GUILayout.Height(25)))
            {
                ShowSceneOptionsMenu(scene, group);
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }
        
        private void GenerateThumbnailForScene(RH.Utilities.SceneItem scene)
        {
            if (EditorUtility.DisplayDialog("Generate Thumbnail", 
                $"Generate a new thumbnail for {scene.Name}? This will temporarily open the scene.", 
                "Generate", "Cancel"))
            {
                EditorUtility.DisplayProgressBar("Generating Thumbnail", $"Processing {scene.Name}...", 0.5f);
                
                try
                {
                    string thumbnailPath = SceneThumbnailGenerator.GenerateThumbnail(scene);
                    
                    if (!string.IsNullOrEmpty(thumbnailPath))
                    {
                        scene.ThumbnailPath = thumbnailPath;
                        SceneManagerCore.SaveSceneGroups();
                    }
                }
                catch (System.Exception ex)
                {
                    EditorUtility.DisplayDialog("Error", 
                        $"An error occurred while generating the thumbnail: {ex.Message}", "OK");
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }
        
        private void ShowAddGroupDialog()
        {
            string groupName = EditorInputDialog.Show("New Group", "Enter group name:", "");
            
            if (!string.IsNullOrEmpty(groupName))
            {
                var group = SceneManagerCore.CreateSceneGroup(groupName);
                selectedGroup = group;
            }
        }
        
        private void ShowGroupOptionsMenu(RH.Utilities.SceneGroup group)
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Rename Group"), false, () => {
                string newName = EditorInputDialog.Show("Rename Group", "Enter new group name:", group.Name);
                if (!string.IsNullOrEmpty(newName) && newName != group.Name)
                {
                    group.Name = newName;
                    SceneManagerCore.GetSceneGroups(); // Save changes
                }
            });
            
            menu.AddItem(new GUIContent("Edit Description"), false, () => {
                string newDesc = EditorInputDialog.Show("Edit Description", "Enter group description:", group.Description);
                if (newDesc != null) // Allow empty descriptions
                {
                    group.Description = newDesc;
                    SceneManagerCore.GetSceneGroups(); // Save changes
                }
            });
            
            menu.AddItem(new GUIContent("Validate All Scenes"), false, () => {
                ValidateAllScenesInGroup(group);
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Delete Group"), false, () => {
                if (EditorUtility.DisplayDialog("Delete Group", 
                    $"Are you sure you want to delete the group '{group.Name}'?", "Delete", "Cancel"))
                {
                    SceneManagerCore.RemoveSceneGroup(group.Name);
                    selectedGroup = null;
                }
            });
            
            menu.ShowAsContext();
        }
        
        private void ShowSceneOptionsMenu(RH.Utilities.SceneItem scene, RH.Utilities.SceneGroup group)
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Edit Tags"), false, () => {
                string tags = EditorInputDialog.Show("Edit Tags", "Enter tags (comma separated):", 
                    scene.Tags != null ? string.Join(",", scene.Tags) : "");
                
                if (tags != null) // Allow empty tags
                {
                    scene.Tags = new List<string>(tags.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .ToArray());
                    
                    SceneManagerCore.GetSceneGroups(); // Save changes
                }
            });
            
            menu.AddItem(new GUIContent("Edit Notes"), false, () => {
                string notes = EditorInputDialog.Show("Edit Notes", "Enter notes:", scene.Notes ?? "");
                
                if (notes != null) // Allow empty notes
                {
                    scene.Notes = notes;
                    SceneManagerCore.GetSceneGroups(); // Save changes
                }
            });
            
            menu.AddItem(new GUIContent("Capture Thumbnail"), false, () => {
                SceneValidationSystem.CaptureSceneThumbnail(scene);
            });
            
            menu.AddItem(new GUIContent("Validate Scene"), false, () => {
                ValidateScene(scene);
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Set Shortcut Key"), false, () => {
                string key = EditorInputDialog.Show("Set Shortcut Key", "Enter shortcut key:", scene.ShortcutKey ?? "");
                
                if (key != null) // Allow empty key
                {
                    scene.ShortcutKey = key;
                    SceneManagerCore.GetSceneGroups(); // Save changes
                }
            });
            
            menu.AddItem(new GUIContent("Toggle Additive Load"), scene.LoadAdditive, () => {
                scene.LoadAdditive = !scene.LoadAdditive;
                SceneManagerCore.GetSceneGroups(); // Save changes
            });
            
            menu.AddSeparator("");
            
            // Add to macro submenu
            var macros = SceneManagerCore.GetSceneMacros();
            if (macros.Count > 0)
            {
                foreach (var macro in macros)
                {
                    bool isInMacro = macro.ScenesInvolved.Any(s => s.Path == scene.Path);
                    menu.AddItem(new GUIContent($"Add to Macro/{macro.Name}"), isInMacro, () => {
                        if (!isInMacro)
                        {
                            macro.ScenesInvolved.Add(scene);
                            SceneManagerCore.GetSceneMacros(); // Save changes
                        }
                    });
                }
            }
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Remove from Group"), false, () => {
                if (EditorUtility.DisplayDialog("Remove Scene", 
                    $"Remove '{scene.Name}' from the group '{group.Name}'?", "Remove", "Cancel"))
                {
                    SceneManagerCore.RemoveSceneFromGroup(scene, group.Name);
                }
            });
            
            menu.ShowAsContext();
        }
        
        private void AddSceneToSelectedGroup(SceneAsset sceneAsset)
        {
            if (selectedGroup == null)
            {
                if (SceneManagerCore.GetSceneGroups().Count == 0)
                {
                    selectedGroup = SceneManagerCore.CreateSceneGroup("Default");
                }
                else
                {
                    selectedGroup = SceneManagerCore.GetSceneGroups()[0];
                }
            }
            
            SceneManagerCore.AddSceneToGroup(sceneAsset, selectedGroup.Name);
        }
        
        private void AddCurrentSceneToSelectedGroup()
        {
            var currentScene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(currentScene.path))
            {
                EditorUtility.DisplayDialog("Add Current Scene", 
                    "The current scene has not been saved. Please save the scene first.", "OK");
                return;
            }
            
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(currentScene.path);
            if (sceneAsset != null)
            {
                AddSceneToSelectedGroup(sceneAsset);
            }
        }
        
        private void ValidateScene(RH.Utilities.SceneItem scene)
        {
            EditorUtility.DisplayProgressBar("Validating Scene", $"Validating {scene.Name}...", 0.5f);
            
            try
            {
                var results = SceneValidationSystem.ValidateScene(scene);
                
                EditorUtility.ClearProgressBar();
                
                // Show results
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Validation Results for {scene.Name}:");
                sb.AppendLine();
                
                foreach (var result in results)
                {
                    string icon = result.Success ? "✓" : "✗";
                    string severity = result.Success ? "" : $"[{result.Severity}] ";
                    sb.AppendLine($"{icon} {severity}{result.Message}");
                }
                
                EditorUtility.DisplayDialog("Validation Results", sb.ToString(), "OK");
                
                // Save changes
                SceneManagerCore.GetSceneGroups();
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"Error validating scene: {ex.Message}");
                EditorUtility.DisplayDialog("Validation Error", 
                    $"An error occurred while validating the scene. See console for details.", "OK");
            }
        }
        
        private void ValidateAllScenesInGroup(RH.Utilities.SceneGroup group)
        {
            if (group.Scenes.Count == 0)
            {
                EditorUtility.DisplayDialog("Validate Scenes", 
                    "No scenes in this group to validate.", "OK");
                return;
            }
            
            if (!EditorUtility.DisplayDialog("Validate Scenes", 
                $"Validate all {group.Scenes.Count} scenes in group '{group.Name}'?", "Validate", "Cancel"))
            {
                return;
            }
            
            int validatedCount = 0;
            int issuesCount = 0;
            
            try
            {
                for (int i = 0; i < group.Scenes.Count; i++)
                {
                    var scene = group.Scenes[i];
                    float progress = (float)i / group.Scenes.Count;
                    
                    EditorUtility.DisplayProgressBar("Validating Scenes", 
                        $"Validating {scene.Name} ({i+1}/{group.Scenes.Count})...", progress);
                    
                    var results = SceneValidationSystem.ValidateScene(scene);
                    validatedCount++;
                    
                    if (scene.HasValidationIssues())
                    {
                        issuesCount++;
                    }
                }
                
                EditorUtility.ClearProgressBar();
                
                // Save changes
                SceneManagerCore.GetSceneGroups();
                
                EditorUtility.DisplayDialog("Validation Complete", 
                    $"Validated {validatedCount} scenes. {issuesCount} scenes have issues.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"Error validating scenes: {ex.Message}");
                EditorUtility.DisplayDialog("Validation Error", 
                    $"An error occurred while validating scenes. See console for details.", "OK");
            }
        }
        
        private void DrawRecentScenesTab()
        {
            EditorGUILayout.BeginVertical();
            
            // Search bar
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            string newSearchFilter = EditorGUILayout.TextField(searchFilter, searchBoxStyle);
            
            if (newSearchFilter != searchFilter)
            {
                searchFilter = newSearchFilter;
            }
            
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                searchFilter = "";
                GUI.FocusControl(null);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Recent scenes list
            var recentScenes = SceneManagerCore.GetRecentScenes();
            
            if (recentScenes.Count > 0)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                
                // Filter scenes based on search
                var filteredScenes = string.IsNullOrEmpty(searchFilter) 
                    ? recentScenes 
                    : recentScenes.Where(s => s.Name.ToLower().Contains(searchFilter.ToLower())).ToList();
                
                if (filteredScenes.Count > 0)
                {
                    EditorGUILayout.LabelField("Recent Scenes", subHeaderStyle);
                    EditorGUILayout.Space(5);
                    
                    foreach (var scene in filteredScenes)
                    {
                        DrawRecentSceneItem(scene);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No recent scenes match the search filter.", MessageType.Info);
                }
                
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("No recent scenes found. Open some scenes to populate this list.", MessageType.Info);
            }
            
            EditorGUILayout.Space(10);
            
            // Bottom controls
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Clear History", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear History", 
                    "Are you sure you want to clear your scene history?", "Clear", "Cancel"))
                {
                    SceneManagerCore.GetRecentScenes().Clear();
                    SceneManagerCore.SaveRecentScenes();
                }
            }
            
            if (GUILayout.Button("Add Current to Favorites", GUILayout.Height(30)))
            {
                AddCurrentSceneToFavorites();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawRecentSceneItem(RH.Utilities.SceneItem scene)
        {
            var preferences = SceneManagerCore.GetUserPreferences();
            
            // Scene item container
            EditorGUILayout.BeginHorizontal(sceneButtonStyle);
            
            // Thumbnail
            if (preferences.ShowThumbnails)
            {
                Texture2D thumbnail = null;
                
                if (!string.IsNullOrEmpty(scene.ThumbnailPath))
                {
                    thumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(scene.ThumbnailPath);
                }
                
                if (thumbnail == null)
                {
                    thumbnail = defaultThumbnail;
                }
                
                GUILayout.Box(thumbnail, GUILayout.Width(THUMBNAIL_SIZE), GUILayout.Height(THUMBNAIL_SIZE));
            }
            
            // Scene info
            EditorGUILayout.BeginVertical();
            
            // Scene name and last opened
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(scene.Name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Opened: {scene.LastOpened.ToString("g")}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            // Path
            if (preferences.ShowPaths)
            {
                EditorGUILayout.LabelField(scene.Path, EditorStyles.miniLabel);
            }
            
            // Access count
            EditorGUILayout.LabelField($"Times opened: {scene.AccessCount}", EditorStyles.miniLabel);
            
            EditorGUILayout.EndVertical();
            
            // Buttons
            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            
            // Load button
            if (GUILayout.Button("Load", GUILayout.Height(25)))
            {
                SceneManagerCore.LoadScene(scene);
            }
            
            // Additive load button
            if (GUILayout.Button("+ Additive", GUILayout.Height(25)))
            {
                SceneManagerCore.LoadScene(scene, true);
            }
            
            // Add to group button
            if (GUILayout.Button("Add to Group", GUILayout.Height(25)))
            {
                ShowAddToGroupMenu(scene);
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }
        
        private void ShowAddToGroupMenu(RH.Utilities.SceneItem scene)
        {
            GenericMenu menu = new GenericMenu();
            
            var groups = SceneManagerCore.GetSceneGroups();
            
            foreach (var group in groups)
            {
                bool isInGroup = group.Scenes.Any(s => s.Path == scene.Path);
                menu.AddItem(new GUIContent(group.Name), isInGroup, () => {
                    if (!isInGroup)
                    {
                        // Create a copy of the scene item
                        var newScene = new RH.Utilities.SceneItem(scene.Name, scene.Path);
                        group.Scenes.Add(newScene);
                        SceneManagerCore.GetSceneGroups(); // Save changes
                    }
                });
            }
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("New Group..."), false, () => {
                string groupName = EditorInputDialog.Show("New Group", "Enter group name:", "");
                
                if (!string.IsNullOrEmpty(groupName))
                {
                    var group = SceneManagerCore.CreateSceneGroup(groupName);
                    
                    // Create a copy of the scene item
                    var newScene = new RH.Utilities.SceneItem(scene.Name, scene.Path);
                    group.Scenes.Add(newScene);
                    SceneManagerCore.GetSceneGroups(); // Save changes
                }
            });
            
            menu.ShowAsContext();
        }
        
        private void AddCurrentSceneToFavorites()
        {
            var currentScene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(currentScene.path))
            {
                EditorUtility.DisplayDialog("Add to Favorites", 
                    "The current scene has not been saved. Please save the scene first.", "OK");
                return;
            }
            
            // Find or create Favorites group
            var groups = SceneManagerCore.GetSceneGroups();
            var favoritesGroup = groups.FirstOrDefault(g => g.Name == "Favorites");
            
            if (favoritesGroup == null)
            {
                favoritesGroup = SceneManagerCore.CreateSceneGroup("Favorites", "Your favorite scenes");
                favoritesGroup.Color = new Color(1f, 0.8f, 0.2f); // Gold color
            }
            
            // Check if scene already exists in the group
            bool exists = favoritesGroup.Scenes.Any(s => s.Path == currentScene.path);
            
            if (!exists)
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(currentScene.path);
                if (sceneAsset != null)
                {
                    SceneManagerCore.AddSceneToGroup(sceneAsset, "Favorites");
                    EditorUtility.DisplayDialog("Add to Favorites", 
                        $"Added '{sceneAsset.name}' to Favorites.", "OK");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Add to Favorites", 
                    "This scene is already in your Favorites.", "OK");
            }
        }
        
        private void DrawMacrosTab()
        {
            EditorGUILayout.BeginVertical();
            
            // Search bar
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            string newSearchFilter = EditorGUILayout.TextField(searchFilter, searchBoxStyle);
            
            if (newSearchFilter != searchFilter)
            {
                searchFilter = newSearchFilter;
            }
            
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                searchFilter = "";
                GUI.FocusControl(null);
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Create new macro button
            if (GUILayout.Button("Create New Macro", GUILayout.Height(30)))
            {
                CreateNewMacro();
            }
            
            EditorGUILayout.Space(10);
            
            // Macros list
            var macros = SceneManagerCore.GetSceneMacros();
            
            if (macros.Count > 0)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                
                // Filter macros based on search
                var filteredMacros = string.IsNullOrEmpty(searchFilter) 
                    ? macros 
                    : macros.Where(m => m.Name.ToLower().Contains(searchFilter.ToLower()) || 
                                       m.Description.ToLower().Contains(searchFilter.ToLower())).ToList();
                
                if (filteredMacros.Count > 0)
                {
                    EditorGUILayout.LabelField("Scene Macros", subHeaderStyle);
                    EditorGUILayout.Space(5);
                    
                    foreach (var macro in filteredMacros)
                    {
                        DrawMacroItem(macro);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No macros match the search filter.", MessageType.Info);
                }
                
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("No macros found. Create a new macro to get started.", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawMacroItem(RH.Utilities.SceneMacro macro)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Macro header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(macro.Name, EditorStyles.boldLabel);
            
            // Shortcut key
            if (!string.IsNullOrEmpty(macro.ShortcutKey))
            {
                EditorGUILayout.LabelField($"Shortcut: {macro.ShortcutKey}", EditorStyles.miniLabel);
            }
            
            // Edit button
            if (GUILayout.Button("Edit", GUILayout.Width(60)))
            {
                EditMacro(macro);
            }
            
            // Delete button
            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Delete Macro", 
                    $"Are you sure you want to delete the macro '{macro.Name}'?", "Delete", "Cancel"))
                {
                    SceneManagerCore.RemoveSceneMacro(macro.Name);
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Description
            if (!string.IsNullOrEmpty(macro.Description))
            {
                EditorGUILayout.LabelField(macro.Description, EditorStyles.wordWrappedLabel);
            }
            
            // Scenes involved
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Scenes:", EditorStyles.boldLabel);
            
            if (macro.ScenesInvolved.Count > 0)
            {
                EditorGUI.indentLevel++;
                
                foreach (var scene in macro.ScenesInvolved)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(scene.Name);
                    
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        macro.ScenesInvolved.Remove(scene);
                        SceneManagerCore.GetSceneMacros(); // Save changes
                        GUIUtility.ExitGUI(); // Prevent GUI errors
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.LabelField("No scenes added to this macro.");
            }
            
            // Options
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            
            // Additive loading toggle
            EditorGUI.BeginChangeCheck();
            macro.LoadAdditive = EditorGUILayout.ToggleLeft("Load Additively", macro.LoadAdditive);
            if (EditorGUI.EndChangeCheck())
            {
                SceneManagerCore.GetSceneMacros(); // Save changes
            }
            
            // Debug mode toggle
            EditorGUI.BeginChangeCheck();
            macro.EnableDebugMode = EditorGUILayout.ToggleLeft("Enable Debug Mode", macro.EnableDebugMode);
            if (EditorGUI.EndChangeCheck())
            {
                SceneManagerCore.GetSceneMacros(); // Save changes
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Execute button
            if (GUILayout.Button("Execute Macro", GUILayout.Height(30)))
            {
                SceneManagerCore.ExecuteMacro(macro);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        private void CreateNewMacro()
        {
            string macroName = EditorInputDialog.Show("New Macro", "Enter macro name:", "");
            
            if (!string.IsNullOrEmpty(macroName))
            {
                string description = EditorInputDialog.Show("Macro Description", "Enter macro description:", "", true);
                
                var macro = SceneManagerCore.CreateSceneMacro(macroName, description);
                EditMacro(macro);
            }
        }
        
        private void EditMacro(RH.Utilities.SceneMacro macro)
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Rename Macro"), false, () => {
                string newName = EditorInputDialog.Show("Rename Macro", "Enter new macro name:", macro.Name);
                if (!string.IsNullOrEmpty(newName) && newName != macro.Name)
                {
                    macro.Name = newName;
                    SceneManagerCore.GetSceneMacros(); // Save changes
                }
            });
            
            menu.AddItem(new GUIContent("Edit Description"), false, () => {
                string newDesc = EditorInputDialog.Show("Edit Description", "Enter macro description:", macro.Description, true);
                if (newDesc != null) // Allow empty descriptions
                {
                    macro.Description = newDesc;
                    SceneManagerCore.GetSceneMacros(); // Save changes
                }
            });
            
            menu.AddItem(new GUIContent("Set Shortcut Key"), false, () => {
                string key = EditorInputDialog.Show("Set Shortcut Key", "Enter shortcut key:", macro.ShortcutKey ?? "");
                
                if (key != null) // Allow empty key
                {
                    macro.ShortcutKey = key;
                    SceneManagerCore.GetSceneMacros(); // Save changes
                }
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Add Scene"), false, () => {
                ShowAddSceneToMacroDialog(macro);
            });
            
            menu.AddItem(new GUIContent("Add Current Scene"), false, () => {
                AddCurrentSceneToMacro(macro);
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Set Custom Action"), false, () => {
                string action = EditorInputDialog.Show("Set Custom Action", "Enter custom action:", macro.CustomAction ?? "");
                
                if (action != null) // Allow empty action
                {
                    macro.CustomAction = action;
                    SceneManagerCore.GetSceneMacros(); // Save changes
                }
            });
            
            menu.ShowAsContext();
        }
        
        private void ShowAddSceneToMacroDialog(RH.Utilities.SceneMacro macro)
        {
            // Store the current macro for later use when the object picker is closed
            currentMacroForSceneAdd = macro;
            
            // Show the object picker
            EditorGUIUtility.ShowObjectPicker<SceneAsset>(null, false, "", 0);
        }
        
        private void AddCurrentSceneToMacro(RH.Utilities.SceneMacro macro)
        {
            var currentScene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(currentScene.path))
            {
                EditorUtility.DisplayDialog("Add to Macro", 
                    "The current scene has not been saved. Please save the scene first.", "OK");
                return;
            }
            
            // Check if scene already exists in the macro
            if (!macro.ScenesInvolved.Any(s => s.Path == currentScene.path))
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(currentScene.path);
                if (sceneAsset != null)
                {
                    var sceneItem = new RH.Utilities.SceneItem(sceneAsset.name, currentScene.path);
                    macro.ScenesInvolved.Add(sceneItem);
                    SceneManagerCore.SaveSceneMacros(); // Save changes
                    
                    EditorUtility.DisplayDialog("Add to Macro", 
                        $"Added '{sceneAsset.name}' to macro '{macro.Name}'.", "OK");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Add to Macro", 
                    "This scene is already in the macro.", "OK");
            }
        }
        
        private void DrawSettingsTab()
        {
            EditorGUILayout.BeginVertical();
            
            EditorGUILayout.LabelField("Scene Manager Settings", headerStyle);
            EditorGUILayout.Space(10);
            
            var preferences = SceneManagerCore.GetUserPreferences();
            bool prefsChanged = false;
            
            // UI Settings
            EditorGUILayout.LabelField("UI Settings", subHeaderStyle);
            EditorGUILayout.Space(5);
            
            EditorGUI.BeginChangeCheck();
            preferences.ShowThumbnails = EditorGUILayout.Toggle("Show Scene Thumbnails", preferences.ShowThumbnails);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            // Thumbnail actions
            if (preferences.ShowThumbnails)
            {
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Generate All Thumbnails", GUILayout.Height(25)))
                {
                    GenerateAllThumbnails();
                }
                
                if (GUILayout.Button("Clear All Thumbnails", GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog("Clear Thumbnails", 
                        "Are you sure you want to delete all scene thumbnails?", "Delete", "Cancel"))
                    {
                        SceneThumbnailGenerator.ClearAllThumbnails();
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUI.BeginChangeCheck();
            preferences.ShowPaths = EditorGUILayout.Toggle("Show Scene Paths", preferences.ShowPaths);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            EditorGUI.BeginChangeCheck();
            preferences.ShowSceneTags = EditorGUILayout.Toggle("Show Scene Tags", preferences.ShowSceneTags);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            EditorGUI.BeginChangeCheck();
            preferences.EnableDarkMode = EditorGUILayout.Toggle("Enable Dark Mode", preferences.EnableDarkMode);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            EditorGUILayout.Space(10);
            
            // Behavior Settings
            EditorGUILayout.LabelField("Behavior Settings", subHeaderStyle);
            EditorGUILayout.Space(5);
            
            EditorGUI.BeginChangeCheck();
            preferences.ConfirmSceneLoad = EditorGUILayout.Toggle("Confirm Scene Load", preferences.ConfirmSceneLoad);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            EditorGUI.BeginChangeCheck();
            preferences.TrackRecentScenes = EditorGUILayout.Toggle("Track Recent Scenes", preferences.TrackRecentScenes);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            EditorGUI.BeginChangeCheck();
            preferences.MaxRecentScenes = EditorGUILayout.IntSlider("Max Recent Scenes", preferences.MaxRecentScenes, 5, 30);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            EditorGUI.BeginChangeCheck();
            preferences.AutoValidateOnLoad = EditorGUILayout.Toggle("Auto Validate On Load", preferences.AutoValidateOnLoad);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            EditorGUILayout.Space(10);
            
            // Validation Settings
            EditorGUILayout.LabelField("Validation Settings", subHeaderStyle);
            EditorGUILayout.Space(5);
            
            EditorGUI.BeginChangeCheck();
            preferences.ValidateMissingReferences = EditorGUILayout.Toggle("Check Missing References", preferences.ValidateMissingReferences);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            EditorGUI.BeginChangeCheck();
            preferences.ValidatePerformance = EditorGUILayout.Toggle("Check Performance Issues", preferences.ValidatePerformance);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            EditorGUI.BeginChangeCheck();
            preferences.ValidateLighting = EditorGUILayout.Toggle("Check Lighting Setup", preferences.ValidateLighting);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            EditorGUILayout.Space(10);
            
            // Data Management
            EditorGUILayout.LabelField("Data Management", subHeaderStyle);
            EditorGUILayout.Space(5);
            
            EditorGUI.BeginChangeCheck();
            preferences.AutoSaveGroups = EditorGUILayout.Toggle("Auto Save Groups", preferences.AutoSaveGroups);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            EditorGUI.BeginChangeCheck();
            preferences.BackupBeforeClear = EditorGUILayout.Toggle("Backup Before Clear", preferences.BackupBeforeClear);
            if (EditorGUI.EndChangeCheck()) prefsChanged = true;
            
            EditorGUILayout.Space(10);
            
            // Data Management Actions
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Export Settings", GUILayout.Height(30)))
            {
                ExportSettings();
            }
            
            if (GUILayout.Button("Import Settings", GUILayout.Height(30)))
            {
                ImportSettings();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Reset All Settings", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Reset Settings", 
                    "Are you sure you want to reset all settings to defaults?", "Reset", "Cancel"))
                {
                    ResetSettings();
                }
            }
            
            if (GUILayout.Button("Clear All Data", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear All Data", 
                    "Are you sure you want to clear all scene groups, macros, and recent scenes? This cannot be undone.", 
                    "Clear", "Cancel"))
                {
                    ClearAllData();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Save preferences if changed
            if (prefsChanged)
            {
                SceneManagerCore.SaveUserPreferences();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void GenerateAllThumbnails()
        {
            if (EditorUtility.DisplayDialog("Generate Thumbnails", 
                "This will generate thumbnails for all scenes in your scene groups. This may take some time and will temporarily open each scene. Continue?", 
                "Generate", "Cancel"))
            {
                EditorUtility.DisplayProgressBar("Generating Thumbnails", "Preparing...", 0f);
                
                try
                {
                    var groups = SceneManagerCore.GetSceneGroups();
                    SceneThumbnailGenerator.GenerateThumbnailsForGroups(groups);
                    
                    EditorUtility.DisplayDialog("Generate Thumbnails", 
                        "Thumbnails generated successfully.", "OK");
                }
                catch (System.Exception ex)
                {
                    EditorUtility.DisplayDialog("Error", 
                        $"An error occurred while generating thumbnails: {ex.Message}", "OK");
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }
        
        private void ExportSettings()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Scene Manager Settings",
                "",
                "RHSceneManagerSettings.json",
                "json");
                
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    // Export settings
                    var settings = new SettingsExportData
                    {
                        PreferencesJson = JsonUtility.ToJson(SceneManagerCore.GetUserPreferences()),
                        GroupsJson = JsonUtility.ToJson(new SceneGroupList { Groups = SceneManagerCore.GetSceneGroups() }),
                        MacrosJson = JsonUtility.ToJson(new SceneMacroList { Macros = SceneManagerCore.GetSceneMacros() })
                    };
                    
                    string json = JsonUtility.ToJson(settings, true);
                    File.WriteAllText(path, json);
                    
                    EditorUtility.DisplayDialog("Export Settings", 
                        "Settings exported successfully.", "OK");
                }
                catch (System.Exception ex)
                {
                    EditorUtility.DisplayDialog("Export Error", 
                        $"Failed to export settings: {ex.Message}", "OK");
                }
            }
        }
        
        private void ImportSettings()
        {
            string path = EditorUtility.OpenFilePanel(
                "Import Scene Manager Settings",
                "",
                "json");
                
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var importData = JsonUtility.FromJson<SettingsExportData>(json);
                    
                    if (importData == null)
                    {
                        EditorUtility.DisplayDialog("Import Error", 
                            "Invalid settings file format.", "OK");
                        return;
                    }
                    
                    // Import preferences
                    if (!string.IsNullOrEmpty(importData.PreferencesJson))
                    {
                        var prefs = JsonUtility.FromJson<RH.Utilities.UserPreferences>(importData.PreferencesJson);
                        if (prefs != null)
                        {
                            SceneManagerCore.UpdatePreferences(prefs);
                        }
                    }
                    
                    // Import scene groups
                    if (!string.IsNullOrEmpty(importData.GroupsJson))
                    {
                        var groupsWrapper = JsonUtility.FromJson<SceneGroupList>(importData.GroupsJson);
                        if (groupsWrapper != null && groupsWrapper.Groups != null)
                        {
                            SceneManagerCore.ImportSceneGroups(groupsWrapper.Groups);
                        }
                    }
                    
                    // Import macros
                    if (!string.IsNullOrEmpty(importData.MacrosJson))
                    {
                        var macrosWrapper = JsonUtility.FromJson<SceneMacroList>(importData.MacrosJson);
                        if (macrosWrapper != null && macrosWrapper.Macros != null)
                        {
                            SceneManagerCore.ImportSceneMacros(macrosWrapper.Macros);
                        }
                    }
                    
                    EditorUtility.DisplayDialog("Import Settings", 
                        "Settings imported successfully.", "OK");
                }
                catch (System.Exception ex)
                {
                    EditorUtility.DisplayDialog("Import Error", 
                        $"Failed to import settings: {ex.Message}", "OK");
                }
            }
        }
        
        private void ResetSettings()
        {
            SceneManagerCore.ResetPreferences();
            EditorUtility.DisplayDialog("Reset Settings", 
                "Settings have been reset to defaults.", "OK");
        }
        
        private void ClearAllData()
        {
            SceneManagerCore.ClearAllData();
            EditorUtility.DisplayDialog("Clear All Data", 
                "All scene manager data has been cleared.", "OK");
        }
        
        private void DrawSceneValidationStatus(RH.Utilities.SceneItem scene)
        {
            GUILayout.BeginVertical();
            
            if (scene.ValidationResults != null && scene.ValidationResults.Count > 0)
            {
                bool hasIssues = scene.HasValidationIssues();
                
                if (hasIssues)
                {
                    EditorGUILayout.LabelField("⚠️ Validation Issues", EditorStyles.boldLabel);
                    
                    foreach (var result in scene.ValidationResults.Where(r => !r.Success))
                    {
                        string severityIcon = "";
                        switch (result.Severity)
                        {
                            case ValidationSeverity.Error:
                                severityIcon = "🔴";
                                break;
                            case ValidationSeverity.Warning:
                                severityIcon = "🟠";
                                break;
                            case ValidationSeverity.Info:
                                severityIcon = "🔵";
                                break;
                        }
                        
                        EditorGUILayout.LabelField($"{severityIcon} {result.Message}");
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("✅ Validation Passed", EditorStyles.boldLabel);
                }
                
                if (scene.LastValidated != DateTime.MinValue)
                {
                    EditorGUILayout.LabelField($"Last validated: {scene.LastValidated.ToString("g")}");
                }
            }
            else
            {
                EditorGUILayout.LabelField("Not validated yet");
            }
            
            if (GUILayout.Button("Validate Scene"))
            {
                var results = SceneValidationSystem.ValidateScene(scene);
                
                string message = results.Any(r => !r.Success) 
                    ? "Scene validation completed with issues." 
                    : "Scene validation passed successfully.";
                    
                EditorUtility.DisplayDialog("Validation Results", message, "OK");
            }
            
            GUILayout.EndVertical();
        }
    }
}

[System.Serializable]
public class SettingsExportData
{
    public string PreferencesJson;
    public string GroupsJson;
    public string MacrosJson;
}

[System.Serializable]
public class SceneGroupList
{
    public List<RH.Utilities.SceneGroup> Groups;
}

[System.Serializable]
public class SceneMacroList
{
    public List<RH.Utilities.SceneMacro> Macros;
}
