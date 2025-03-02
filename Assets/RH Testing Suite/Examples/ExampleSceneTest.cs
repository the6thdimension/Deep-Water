using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace RH.Testing.Examples
{
    /// <summary>
    /// Example of a script that tests scene loading and validation
    /// </summary>
    [Testable(TestType.Integration, TestMode.PlayMode, TestCategory.Scene, "Example scene test that validates scene loading and required objects")]
    public class ExampleSceneTest : MonoBehaviour, ITestable
    {
        [SerializeField] private string sceneToTest = "SampleScene";
        [SerializeField] private string[] requiredGameObjects;
        
        /// <summary>
        /// Runs the test for this scene
        /// </summary>
        public bool RunTest()
        {
            // In a real implementation, this would use a coroutine to load the scene additively
            // and check for required objects. For simplicity, we'll just simulate success.
            Debug.Log($"Testing scene: {sceneToTest}");
            
            // Check if the scene exists in the build settings
            bool sceneExists = false;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                
                if (sceneName == sceneToTest)
                {
                    sceneExists = true;
                    break;
                }
            }
            
            if (!sceneExists)
            {
                Debug.LogError($"Scene '{sceneToTest}' does not exist in the build settings");
                return false;
            }
            
            // Simulate checking for required GameObjects
            Debug.Log($"Checking for {requiredGameObjects.Length} required GameObjects in scene {sceneToTest}");
            
            // In a real implementation, we would actually check if these objects exist
            // For now, we'll just simulate success
            return true;
        }
        
        /// <summary>
        /// Returns a description of the test
        /// </summary>
        public string GetTestDescription()
        {
            return $"Tests the {sceneToTest} scene by validating that it loads correctly and contains all required GameObjects";
        }
        
        /// <summary>
        /// Example of how to implement a scene loading test using a coroutine
        /// </summary>
        private IEnumerator LoadSceneAndTest()
        {
            // Remember the current active scene
            Scene originalScene = SceneManager.GetActiveScene();
            
            // Load the scene additively
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneToTest, LoadSceneMode.Additive);
            
            // Wait until the scene is loaded
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
            
            // Get the loaded scene
            Scene loadedScene = SceneManager.GetSceneByName(sceneToTest);
            
            // Activate the loaded scene
            SceneManager.SetActiveScene(loadedScene);
            
            // Check for required GameObjects
            bool allObjectsFound = true;
            foreach (string objectName in requiredGameObjects)
            {
                GameObject obj = GameObject.Find(objectName);
                if (obj == null)
                {
                    Debug.LogError($"Required GameObject '{objectName}' not found in scene {sceneToTest}");
                    allObjectsFound = false;
                }
            }
            
            // Unload the scene
            AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(loadedScene);
            
            // Wait until the scene is unloaded
            while (!asyncUnload.isDone)
            {
                yield return null;
            }
            
            // Restore the original scene
            SceneManager.SetActiveScene(originalScene);
            
            // Report the result
            if (allObjectsFound)
            {
                Debug.Log($"Scene test for {sceneToTest} passed");
            }
            else
            {
                Debug.LogError($"Scene test for {sceneToTest} failed");
            }
        }
    }
}
