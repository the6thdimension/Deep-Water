using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace RH.Testing
{
    public class RHTestSuiteWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private TestType selectedTestType = TestType.Unit;
        private TestMode selectedTestMode = TestMode.EditMode;
        private bool showSceneTests = true;
        private bool showScriptTests = true;
        private bool showModelTests = true;
        private List<TestItem> availableTests = new List<TestItem>();
        private string searchFilter = "";
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private Texture2D logoTexture;

        [MenuItem("RH Navy Sims/Testing Suite")]
        public static void ShowWindow()
        {
            RHTestSuiteWindow window = GetWindow<RHTestSuiteWindow>("RH Testing Suite");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }

        private void OnEnable()
        {
            // Initialize styles and load resources
            InitializeStyles();
            RefreshAvailableTests();
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

            // Try to load logo if available
            logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RH Testing Suite/Editor/Resources/TestSuiteLogo.png");
        }

        private void RefreshAvailableTests()
        {
            availableTests.Clear();
            
            // Find all test classes in the project
            var testableScripts = TestDiscovery.FindAllTestableScripts();
            var sceneTests = TestDiscovery.FindAllSceneTests();
            var modelTests = TestDiscovery.FindAllModelTests();
            
            // Add them to the available tests list
            availableTests.AddRange(testableScripts);
            availableTests.AddRange(sceneTests);
            availableTests.AddRange(modelTests);
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawToolbar();
            DrawTestTypeSelection();
            DrawTestModeSelection();
            DrawCategoryFilters();
            DrawSearchBar();
            DrawTestList();
            DrawBottomControls();
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
            
            EditorGUILayout.LabelField("RH Testing Suite", headerStyle);
            EditorGUILayout.Space(5);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (GUILayout.Button("Refresh Tests", EditorStyles.toolbarButton))
            {
                RefreshAvailableTests();
            }
            
            if (GUILayout.Button("Run All Tests", EditorStyles.toolbarButton))
            {
                RunAllTests();
            }
            
            if (GUILayout.Button("Run Selected Tests", EditorStyles.toolbarButton))
            {
                RunSelectedTests();
            }
            
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton))
            {
                ShowSettingsMenu();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTestTypeSelection()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Test Type", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selectedTestType == TestType.Unit, "Unit Tests", EditorStyles.toolbarButton))
                selectedTestType = TestType.Unit;
            
            if (GUILayout.Toggle(selectedTestType == TestType.Integration, "Integration Tests", EditorStyles.toolbarButton))
                selectedTestType = TestType.Integration;
            
            if (GUILayout.Toggle(selectedTestType == TestType.System, "System Tests", EditorStyles.toolbarButton))
                selectedTestType = TestType.System;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTestModeSelection()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Test Mode", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selectedTestMode == TestMode.EditMode, "Edit Mode", EditorStyles.toolbarButton))
                selectedTestMode = TestMode.EditMode;
            
            if (GUILayout.Toggle(selectedTestMode == TestMode.PlayMode, "Play Mode", EditorStyles.toolbarButton))
                selectedTestMode = TestMode.PlayMode;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCategoryFilters()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Test Categories", subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            showSceneTests = EditorGUILayout.ToggleLeft("Scene Tests", showSceneTests, GUILayout.Width(120));
            showScriptTests = EditorGUILayout.ToggleLeft("Script Tests", showScriptTests, GUILayout.Width(120));
            showModelTests = EditorGUILayout.ToggleLeft("Model Tests", showModelTests, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            searchFilter = EditorGUILayout.TextField(searchFilter);
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                searchFilter = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTestList()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Available Tests", subHeaderStyle);
            
            // Filter tests based on selected criteria
            var filteredTests = availableTests
                .Where(t => (t.TestType == selectedTestType || selectedTestType == TestType.All))
                .Where(t => (t.TestMode == selectedTestMode || t.TestMode == TestMode.Both))
                .Where(t => (
                    (t.Category == TestCategory.Scene && showSceneTests) ||
                    (t.Category == TestCategory.Script && showScriptTests) ||
                    (t.Category == TestCategory.Model && showModelTests)
                ))
                .Where(t => string.IsNullOrEmpty(searchFilter) || 
                      t.Name.ToLower().Contains(searchFilter.ToLower()) || 
                      t.Description.ToLower().Contains(searchFilter.ToLower()))
                .ToList();
            
            EditorGUILayout.LabelField($"Showing {filteredTests.Count} of {availableTests.Count} tests");
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            
            foreach (var test in filteredTests)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                
                test.IsEnabled = EditorGUILayout.Toggle(test.IsEnabled, GUILayout.Width(20));
                
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(test.Name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(test.Description, EditorStyles.wordWrappedLabel);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Type: {test.TestType}", GUILayout.Width(120));
                EditorGUILayout.LabelField($"Category: {test.Category}", GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
                
                if (GUILayout.Button("Run", GUILayout.Width(60), GUILayout.Height(40)))
                {
                    RunTest(test);
                }
                
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawBottomControls()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Select All", GUILayout.Height(30)))
            {
                SelectAllTests(true);
            }
            
            if (GUILayout.Button("Deselect All", GUILayout.Height(30)))
            {
                SelectAllTests(false);
            }
            
            if (GUILayout.Button("Run Selected Tests", GUILayout.Height(30)))
            {
                RunSelectedTests();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void SelectAllTests(bool select)
        {
            foreach (var test in availableTests)
            {
                test.IsEnabled = select;
            }
        }

        private void RunTest(TestItem test)
        {
            Debug.Log($"Running test: {test.Name}");
            TestRunner.RunTest(test);
        }

        private void RunSelectedTests()
        {
            var selectedTests = availableTests.Where(t => t.IsEnabled).ToList();
            Debug.Log($"Running {selectedTests.Count} selected tests");
            TestRunner.RunTests(selectedTests);
        }

        private void RunAllTests()
        {
            Debug.Log($"Running all {availableTests.Count} tests");
            TestRunner.RunTests(availableTests);
        }

        private void ShowSettingsMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Export Test Results"), false, ExportTestResults);
            menu.AddItem(new GUIContent("Import Test Configuration"), false, ImportTestConfiguration);
            menu.AddItem(new GUIContent("Export Test Configuration"), false, ExportTestConfiguration);
            menu.AddItem(new GUIContent("Reset All Settings"), false, ResetAllSettings);
            menu.ShowAsContext();
        }

        private void ExportTestResults()
        {
            string path = EditorUtility.SaveFilePanel("Export Test Results", "", "TestResults", "json");
            if (!string.IsNullOrEmpty(path))
            {
                TestResultExporter.ExportResults(path);
            }
        }

        private void ImportTestConfiguration()
        {
            string path = EditorUtility.OpenFilePanel("Import Test Configuration", "", "json");
            if (!string.IsNullOrEmpty(path))
            {
                TestConfigurationManager.ImportConfiguration(path);
                RefreshAvailableTests();
            }
        }

        private void ExportTestConfiguration()
        {
            string path = EditorUtility.SaveFilePanel("Export Test Configuration", "", "TestConfig", "json");
            if (!string.IsNullOrEmpty(path))
            {
                TestConfigurationManager.ExportConfiguration(path, availableTests);
            }
        }

        private void ResetAllSettings()
        {
            if (EditorUtility.DisplayDialog("Reset All Settings", 
                "Are you sure you want to reset all test settings? This cannot be undone.", 
                "Reset", "Cancel"))
            {
                TestConfigurationManager.ResetAllSettings();
                RefreshAvailableTests();
            }
        }
    }
}
