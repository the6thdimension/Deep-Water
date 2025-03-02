using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

namespace RH.Testing
{
    /// <summary>
    /// Manages test configurations and settings
    /// </summary>
    public static class TestConfigurationManager
    {
        private const string CONFIG_PREFS_KEY = "RHTestSuite_Configuration";
        
        /// <summary>
        /// Serializable class to store test configuration
        /// </summary>
        [Serializable]
        private class TestConfiguration
        {
            public List<SerializableTestItem> Tests = new List<SerializableTestItem>();
            public TestType DefaultTestType = TestType.Unit;
            public TestMode DefaultTestMode = TestMode.EditMode;
            public bool AutoRefreshTests = true;
            public bool ShowPassedTests = true;
            public bool ShowFailedTests = true;
        }
        
        /// <summary>
        /// Serializable version of TestItem
        /// </summary>
        [Serializable]
        private class SerializableTestItem
        {
            public string Name;
            public string Description;
            public TestType TestType;
            public TestMode TestMode;
            public TestCategory Category;
            public bool IsEnabled;
            public string TargetPath;
            public string TestScriptPath;
            
            public SerializableTestItem(TestItem item)
            {
                Name = item.Name;
                Description = item.Description;
                TestType = item.TestType;
                TestMode = item.TestMode;
                Category = item.Category;
                IsEnabled = item.IsEnabled;
                TargetPath = item.TargetPath;
                TestScriptPath = item.TestScriptPath;
            }
            
            public TestItem ToTestItem()
            {
                return new TestItem(Name, Description, TestType, TestMode, Category, TargetPath, TestScriptPath)
                {
                    IsEnabled = IsEnabled
                };
            }
        }
        
        /// <summary>
        /// Exports the current test configuration to a file
        /// </summary>
        public static void ExportConfiguration(string filePath, List<TestItem> tests)
        {
            try
            {
                TestConfiguration config = new TestConfiguration();
                
                // Convert TestItems to SerializableTestItems
                foreach (var test in tests)
                {
                    config.Tests.Add(new SerializableTestItem(test));
                }
                
                // Serialize to JSON
                string json = JsonUtility.ToJson(config, true);
                
                // Write to file
                File.WriteAllText(filePath, json);
                
                Debug.Log($"Test configuration exported to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error exporting test configuration: {e.Message}");
            }
        }
        
        /// <summary>
        /// Imports a test configuration from a file
        /// </summary>
        public static List<TestItem> ImportConfiguration(string filePath)
        {
            try
            {
                // Read from file
                string json = File.ReadAllText(filePath);
                
                // Deserialize from JSON
                TestConfiguration config = JsonUtility.FromJson<TestConfiguration>(json);
                
                // Convert SerializableTestItems to TestItems
                List<TestItem> tests = new List<TestItem>();
                foreach (var serializableTest in config.Tests)
                {
                    tests.Add(serializableTest.ToTestItem());
                }
                
                Debug.Log($"Test configuration imported from {filePath}");
                
                // Save the configuration to PlayerPrefs
                SaveConfiguration(config);
                
                return tests;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error importing test configuration: {e.Message}");
                return new List<TestItem>();
            }
        }
        
        /// <summary>
        /// Saves the current configuration to PlayerPrefs
        /// </summary>
        private static void SaveConfiguration(TestConfiguration config)
        {
            string json = JsonUtility.ToJson(config);
            PlayerPrefs.SetString(CONFIG_PREFS_KEY, json);
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// Loads the configuration from PlayerPrefs
        /// </summary>
        private static TestConfiguration LoadConfiguration()
        {
            if (PlayerPrefs.HasKey(CONFIG_PREFS_KEY))
            {
                string json = PlayerPrefs.GetString(CONFIG_PREFS_KEY);
                return JsonUtility.FromJson<TestConfiguration>(json);
            }
            
            return new TestConfiguration();
        }
        
        /// <summary>
        /// Resets all settings to default
        /// </summary>
        public static void ResetAllSettings()
        {
            PlayerPrefs.DeleteKey(CONFIG_PREFS_KEY);
            PlayerPrefs.Save();
            
            Debug.Log("All test settings reset to default");
        }
        
        /// <summary>
        /// Updates the enabled state of a test
        /// </summary>
        public static void UpdateTestEnabledState(string testName, bool isEnabled)
        {
            TestConfiguration config = LoadConfiguration();
            
            foreach (var test in config.Tests)
            {
                if (test.Name == testName)
                {
                    test.IsEnabled = isEnabled;
                    break;
                }
            }
            
            SaveConfiguration(config);
        }
        
        /// <summary>
        /// Gets the default test type
        /// </summary>
        public static TestType GetDefaultTestType()
        {
            return LoadConfiguration().DefaultTestType;
        }
        
        /// <summary>
        /// Gets the default test mode
        /// </summary>
        public static TestMode GetDefaultTestMode()
        {
            return LoadConfiguration().DefaultTestMode;
        }
        
        /// <summary>
        /// Sets the default test type
        /// </summary>
        public static void SetDefaultTestType(TestType testType)
        {
            TestConfiguration config = LoadConfiguration();
            config.DefaultTestType = testType;
            SaveConfiguration(config);
        }
        
        /// <summary>
        /// Sets the default test mode
        /// </summary>
        public static void SetDefaultTestMode(TestMode testMode)
        {
            TestConfiguration config = LoadConfiguration();
            config.DefaultTestMode = testMode;
            SaveConfiguration(config);
        }
    }
}
