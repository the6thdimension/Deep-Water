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
    /// System for validating scenes and checking for issues
    /// </summary>
    public static class SceneValidationSystem
    {
        /// <summary>
        /// Performs a full validation of a scene
        /// </summary>
        public static List<ValidationResult> ValidateScene(SceneItem sceneItem)
        {
            var results = new List<ValidationResult>();
            
            if (sceneItem == null || string.IsNullOrEmpty(sceneItem.Path))
            {
                results.Add(new ValidationResult(false, "Invalid scene item", ValidationSeverity.Error));
                return results;
            }

            // Check if scene exists
            if (!File.Exists(sceneItem.Path))
            {
                results.Add(new ValidationResult(false, "Scene file does not exist", ValidationSeverity.Error));
                return results;
            }

            // Load scene in background for validation
            var scene = EditorSceneManager.OpenScene(sceneItem.Path, OpenSceneMode.Additive);
            
            try
            {
                // Check for missing prefabs
                results.AddRange(CheckForMissingPrefabs(scene));
                
                // Check for missing scripts
                results.AddRange(CheckForMissingScripts(scene));
                
                // Check for lighting status
                results.AddRange(CheckLightingStatus(scene, sceneItem));
                
                // Check for performance issues
                results.AddRange(CheckPerformanceIssues(scene, sceneItem));
                
                // Update scene item with validation results
                sceneItem.ValidationResults.Clear();
                sceneItem.ValidationResults.AddRange(results);
                sceneItem.LastValidated = DateTime.Now;
            }
            catch (Exception ex)
            {
                results.Add(new ValidationResult(false, $"Validation error: {ex.Message}", ValidationSeverity.Error));
            }
            finally
            {
                // Close the scene
                EditorSceneManager.CloseScene(scene, true);
            }
            
            return results;
        }

        /// <summary>
        /// Checks for missing prefabs in the scene
        /// </summary>
        private static List<ValidationResult> CheckForMissingPrefabs(UnityEngine.SceneManagement.Scene scene)
        {
            var results = new List<ValidationResult>();
            var rootObjects = scene.GetRootGameObjects();
            
            // Check for missing prefabs in the scene
            foreach (var rootObject in rootObjects)
            {
                CheckGameObjectForMissingPrefabs(rootObject, results);
            }
            
            return results;
        }

        /// <summary>
        /// Recursively checks a GameObject and its children for missing prefabs
        /// </summary>
        private static void CheckGameObjectForMissingPrefabs(GameObject obj, List<ValidationResult> results)
        {
            // Check if this is a missing prefab
            if (PrefabUtility.GetPrefabAssetType(obj) == PrefabAssetType.MissingAsset)
            {
                results.Add(new ValidationResult(false, 
                    $"Missing prefab reference: {obj.name}", 
                    ValidationSeverity.Warning));
            }
            
            // Check children
            foreach (Transform child in obj.transform)
            {
                CheckGameObjectForMissingPrefabs(child.gameObject, results);
            }
        }

        /// <summary>
        /// Checks for missing scripts in the scene
        /// </summary>
        private static List<ValidationResult> CheckForMissingScripts(UnityEngine.SceneManagement.Scene scene)
        {
            var results = new List<ValidationResult>();
            var rootObjects = scene.GetRootGameObjects();
            int missingScriptCount = 0;
            
            // Check for missing scripts in the scene
            foreach (var rootObject in rootObjects)
            {
                missingScriptCount += CheckGameObjectForMissingScripts(rootObject);
            }
            
            if (missingScriptCount > 0)
            {
                results.Add(new ValidationResult(false, 
                    $"Found {missingScriptCount} missing script references", 
                    ValidationSeverity.Warning));
            }
            else
            {
                results.Add(new ValidationResult(true, 
                    "No missing script references found", 
                    ValidationSeverity.Info));
            }
            
            return results;
        }

        /// <summary>
        /// Recursively checks a GameObject and its children for missing scripts
        /// </summary>
        private static int CheckGameObjectForMissingScripts(GameObject obj)
        {
            int count = 0;
            Component[] components = obj.GetComponents<Component>();
            
            // Check for null components (missing scripts)
            foreach (var component in components)
            {
                if (component == null)
                {
                    count++;
                }
            }
            
            // Check children
            foreach (Transform child in obj.transform)
            {
                count += CheckGameObjectForMissingScripts(child.gameObject);
            }
            
            return count;
        }

        /// <summary>
        /// Checks the lighting status of the scene
        /// </summary>
        private static List<ValidationResult> CheckLightingStatus(UnityEngine.SceneManagement.Scene scene, SceneItem sceneItem)
        {
            var results = new List<ValidationResult>();
            
            // Check if lighting is baked
            bool isLightingBaked = LightmapSettings.lightmaps != null && LightmapSettings.lightmaps.Length > 0;
            sceneItem.HasLightingBaked = isLightingBaked;
            
            if (!isLightingBaked)
            {
                results.Add(new ValidationResult(false, 
                    "Scene lighting is not baked", 
                    ValidationSeverity.Info));
            }
            else
            {
                results.Add(new ValidationResult(true, 
                    "Scene lighting is baked", 
                    ValidationSeverity.Info));
            }
            
            return results;
        }

        /// <summary>
        /// Checks for potential performance issues in the scene
        /// </summary>
        private static List<ValidationResult> CheckPerformanceIssues(UnityEngine.SceneManagement.Scene scene, SceneItem sceneItem)
        {
            var results = new List<ValidationResult>();
            var rootObjects = scene.GetRootGameObjects();
            
            // Count total objects in scene
            int totalObjectCount = 0;
            int lightCount = 0;
            int cameraCount = 0;
            int particleSystemCount = 0;
            
            foreach (var rootObject in rootObjects)
            {
                CountSceneObjects(rootObject, ref totalObjectCount, ref lightCount, 
                    ref cameraCount, ref particleSystemCount);
            }
            
            // Estimate memory usage based on object count (very rough estimate)
            float estimatedMemoryMB = totalObjectCount * 0.1f; // Very rough estimate
            sceneItem.EstimatedMemoryUsage = estimatedMemoryMB;
            
            // Estimate load time based on object count (very rough estimate)
            float estimatedLoadTime = totalObjectCount * 0.001f; // Very rough estimate
            sceneItem.EstimatedLoadTime = estimatedLoadTime;
            
            // Check for excessive objects
            if (totalObjectCount > 1000)
            {
                results.Add(new ValidationResult(false, 
                    $"High object count: {totalObjectCount} objects", 
                    ValidationSeverity.Warning));
            }
            
            // Check for excessive lights
            if (lightCount > 50)
            {
                results.Add(new ValidationResult(false, 
                    $"High light count: {lightCount} lights", 
                    ValidationSeverity.Warning));
            }
            
            // Check for excessive cameras
            if (cameraCount > 5)
            {
                results.Add(new ValidationResult(false, 
                    $"High camera count: {cameraCount} cameras", 
                    ValidationSeverity.Info));
            }
            
            // Check for excessive particle systems
            if (particleSystemCount > 20)
            {
                results.Add(new ValidationResult(false, 
                    $"High particle system count: {particleSystemCount} systems", 
                    ValidationSeverity.Warning));
            }
            
            return results;
        }

        /// <summary>
        /// Recursively counts objects in the scene
        /// </summary>
        private static void CountSceneObjects(GameObject obj, ref int totalCount, ref int lightCount, 
            ref int cameraCount, ref int particleSystemCount)
        {
            totalCount++;
            
            if (obj.GetComponent<Light>() != null)
                lightCount++;
                
            if (obj.GetComponent<Camera>() != null)
                cameraCount++;
                
            if (obj.GetComponent<ParticleSystem>() != null)
                particleSystemCount++;
            
            // Count children
            foreach (Transform child in obj.transform)
            {
                CountSceneObjects(child.gameObject, ref totalCount, ref lightCount, 
                    ref cameraCount, ref particleSystemCount);
            }
        }

        /// <summary>
        /// Captures a thumbnail for a scene
        /// </summary>
        public static void CaptureSceneThumbnail(SceneItem sceneItem)
        {
            if (sceneItem == null || string.IsNullOrEmpty(sceneItem.Path))
                return;

            // Ensure thumbnail folder exists
            string thumbnailFolder = "Assets/RH Utilities/Editor/Resources/SceneThumbnails/";
            if (!Directory.Exists(thumbnailFolder))
            {
                Directory.CreateDirectory(thumbnailFolder);
            }

            // Generate a unique filename for the thumbnail
            string thumbnailName = $"{Path.GetFileNameWithoutExtension(sceneItem.Path)}_{DateTime.Now.Ticks}.png";
            string thumbnailPath = Path.Combine(thumbnailFolder, thumbnailName);

            // Load the scene if not already loaded
            var currentScene = EditorSceneManager.GetActiveScene();
            bool needToLoadScene = currentScene.path != sceneItem.Path;
            UnityEngine.SceneManagement.Scene sceneToCapture = currentScene;
            
            if (needToLoadScene)
            {
                sceneToCapture = EditorSceneManager.OpenScene(sceneItem.Path, OpenSceneMode.Additive);
            }

            try
            {
                // Make the scene active
                if (needToLoadScene)
                {
                    EditorSceneManager.SetActiveScene(sceneToCapture);
                }

                // Find the main camera or any camera
                Camera camera = null;
                if (Camera.main != null)
                {
                    camera = Camera.main;
                }
                else
                {
                    camera = GameObject.FindObjectOfType<Camera>();
                }

                if (camera != null)
                {
                    // Create a render texture
                    RenderTexture renderTexture = new RenderTexture(256, 256, 24);
                    RenderTexture previousRT = camera.targetTexture;
                    RenderTexture.active = renderTexture;
                    camera.targetTexture = renderTexture;

                    // Render the camera view
                    camera.Render();

                    // Create a texture2D and read the render texture
                    Texture2D screenshot = new Texture2D(256, 256, TextureFormat.RGB24, false);
                    screenshot.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                    screenshot.Apply();

                    // Reset the camera
                    camera.targetTexture = previousRT;
                    RenderTexture.active = null;
                    GameObject.DestroyImmediate(renderTexture);

                    // Save the texture to a file
                    byte[] bytes = screenshot.EncodeToPNG();
                    File.WriteAllBytes(thumbnailPath, bytes);
                    AssetDatabase.Refresh();

                    // Update the scene item
                    sceneItem.ThumbnailPath = thumbnailPath;
                }
                else
                {
                    Debug.LogWarning($"No camera found in scene {sceneItem.Name} for thumbnail capture");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error capturing thumbnail for scene {sceneItem.Name}: {ex.Message}");
            }
            finally
            {
                // Restore the original scene
                if (needToLoadScene)
                {
                    EditorSceneManager.CloseScene(sceneToCapture, true);
                    EditorSceneManager.SetActiveScene(currentScene);
                }
            }
        }
    }
}
