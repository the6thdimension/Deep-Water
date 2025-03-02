using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;

namespace RH.Utilities
{
    /// <summary>
    /// Utility class for generating and managing scene thumbnails
    /// </summary>
    public static class SceneThumbnailGenerator
    {
        private const string THUMBNAILS_FOLDER = "Assets/RH Utilities/Thumbnails";
        private const int THUMBNAIL_SIZE = 256;

        /// <summary>
        /// Generates a thumbnail for the specified scene
        /// </summary>
        public static string GenerateThumbnail(SceneItem sceneItem)
        {
            if (sceneItem == null || string.IsNullOrEmpty(sceneItem.Path))
                return null;

            // Ensure thumbnails folder exists
            if (!Directory.Exists(THUMBNAILS_FOLDER))
            {
                Directory.CreateDirectory(THUMBNAILS_FOLDER);
                AssetDatabase.Refresh();
            }

            // Generate thumbnail filename based on scene path
            string sceneName = Path.GetFileNameWithoutExtension(sceneItem.Path);
            string thumbnailPath = $"{THUMBNAILS_FOLDER}/{sceneName}_thumbnail.png";

            // Check if we need to open the scene to generate the thumbnail
            bool wasSceneOpen = false;
            var openScene = EditorSceneManager.GetSceneByPath(sceneItem.Path);
            
            if (!openScene.isLoaded)
            {
                // We need to open the scene to generate a thumbnail
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    openScene = EditorSceneManager.OpenScene(sceneItem.Path, OpenSceneMode.Single);
                    wasSceneOpen = true;
                }
                else
                {
                    // User canceled, use default thumbnail
                    return null;
                }
            }

            try
            {
                // Find a camera to use for the thumbnail
                Camera camera = FindSuitableCamera();
                
                if (camera == null)
                {
                    Debug.LogWarning($"No suitable camera found in scene {sceneName} for thumbnail generation.");
                    return null;
                }

                // Create a render texture for the thumbnail
                RenderTexture renderTexture = new RenderTexture(THUMBNAIL_SIZE, THUMBNAIL_SIZE, 24);
                RenderTexture previousRenderTexture = camera.targetTexture;
                RenderTexture.active = renderTexture;
                camera.targetTexture = renderTexture;

                // Render the scene from the camera
                camera.Render();

                // Create a texture2D and read the render texture
                Texture2D thumbnail = new Texture2D(THUMBNAIL_SIZE, THUMBNAIL_SIZE, TextureFormat.RGB24, false);
                thumbnail.ReadPixels(new Rect(0, 0, THUMBNAIL_SIZE, THUMBNAIL_SIZE), 0, 0);
                thumbnail.Apply();

                // Reset camera
                camera.targetTexture = previousRenderTexture;
                RenderTexture.active = null;
                Object.DestroyImmediate(renderTexture);

                // Save the thumbnail as a PNG
                byte[] bytes = thumbnail.EncodeToPNG();
                File.WriteAllBytes(thumbnailPath, bytes);
                AssetDatabase.ImportAsset(thumbnailPath);

                // Update texture import settings
                TextureImporter importer = AssetImporter.GetAtPath(thumbnailPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.SaveAndReimport();
                }

                // Clean up
                Object.DestroyImmediate(thumbnail);

                // Return the path to the thumbnail
                return thumbnailPath;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error generating thumbnail for {sceneName}: {ex.Message}");
                return null;
            }
            finally
            {
                // Restore the previous scene if we opened one
                if (wasSceneOpen)
                {
                    EditorSceneManager.OpenScene(EditorSceneManager.GetActiveScene().path);
                }
            }
        }

        /// <summary>
        /// Finds a suitable camera for thumbnail generation
        /// </summary>
        private static Camera FindSuitableCamera()
        {
            // Try to find the main camera first
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                return mainCamera;

            // If no main camera, try to find any camera
            Camera[] allCameras = Object.FindObjectsOfType<Camera>();
            if (allCameras.Length > 0)
                return allCameras[0];

            // No cameras found
            return null;
        }

        /// <summary>
        /// Generates thumbnails for all scenes in the specified groups
        /// </summary>
        public static void GenerateThumbnailsForGroups(List<SceneGroup> groups)
        {
            if (groups == null)
                return;

            // Track processed scenes to avoid duplicates
            HashSet<string> processedScenes = new HashSet<string>();
            
            foreach (var group in groups)
            {
                foreach (var scene in group.Scenes)
                {
                    if (!processedScenes.Contains(scene.Path))
                    {
                        processedScenes.Add(scene.Path);
                        
                        // Generate thumbnail
                        string thumbnailPath = GenerateThumbnail(scene);
                        
                        // Update scene item with thumbnail path
                        if (!string.IsNullOrEmpty(thumbnailPath))
                        {
                            scene.ThumbnailPath = thumbnailPath;
                        }
                    }
                }
            }
            
            // Save changes
            SceneManagerCore.SaveSceneGroups();
        }

        /// <summary>
        /// Clears all generated thumbnails
        /// </summary>
        public static void ClearAllThumbnails()
        {
            if (Directory.Exists(THUMBNAILS_FOLDER))
            {
                string[] files = Directory.GetFiles(THUMBNAILS_FOLDER, "*_thumbnail.png");
                
                foreach (string file in files)
                {
                    AssetDatabase.DeleteAsset(file);
                }
                
                AssetDatabase.Refresh();
            }
        }
    }
}
