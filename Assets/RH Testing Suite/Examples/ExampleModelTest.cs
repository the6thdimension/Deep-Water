using UnityEngine;
using System.Collections.Generic;

namespace RH.Testing.Examples
{
    /// <summary>
    /// Example of a script that tests 3D models for missing textures or scripts
    /// </summary>
    [Testable(TestType.Unit, TestMode.EditMode, TestCategory.Model, "Example model test that validates textures and scripts on 3D models")]
    public class ExampleModelTest : MonoBehaviour, ITestable
    {
        [SerializeField] private GameObject modelToTest;
        [SerializeField] private bool checkTextures = true;
        [SerializeField] private bool checkScripts = true;
        [SerializeField] private bool checkMaterials = true;
        
        /// <summary>
        /// Runs the test for this model
        /// </summary>
        public bool RunTest()
        {
            if (modelToTest == null)
            {
                Debug.LogError("No model assigned to test");
                return false;
            }
            
            List<string> errors = new List<string>();
            
            // Check for missing materials
            if (checkMaterials)
            {
                bool materialsValid = CheckMaterials(errors);
                if (!materialsValid)
                {
                    Debug.LogError($"Model {modelToTest.name} has missing or invalid materials");
                }
            }
            
            // Check for missing textures
            if (checkTextures)
            {
                bool texturesValid = CheckTextures(errors);
                if (!texturesValid)
                {
                    Debug.LogError($"Model {modelToTest.name} has missing textures");
                }
            }
            
            // Check for missing scripts
            if (checkScripts)
            {
                bool scriptsValid = CheckScripts(errors);
                if (!scriptsValid)
                {
                    Debug.LogError($"Model {modelToTest.name} has missing script references");
                }
            }
            
            // Log all errors
            if (errors.Count > 0)
            {
                Debug.LogError($"Model {modelToTest.name} has {errors.Count} errors:");
                foreach (string error in errors)
                {
                    Debug.LogError($"- {error}");
                }
                return false;
            }
            
            Debug.Log($"Model {modelToTest.name} passed all tests");
            return true;
        }
        
        /// <summary>
        /// Returns a description of the test
        /// </summary>
        public string GetTestDescription()
        {
            return $"Tests the {modelToTest?.name ?? "unassigned"} model by validating materials, textures, and script references";
        }
        
        /// <summary>
        /// Checks if all materials on the model are valid
        /// </summary>
        private bool CheckMaterials(List<string> errors)
        {
            bool allValid = true;
            
            // Get all renderers on the model
            Renderer[] renderers = modelToTest.GetComponentsInChildren<Renderer>();
            
            foreach (Renderer renderer in renderers)
            {
                // Check for null materials
                if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0)
                {
                    errors.Add($"Renderer {renderer.name} has no materials assigned");
                    allValid = false;
                    continue;
                }
                
                // Check each material
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    Material material = renderer.sharedMaterials[i];
                    
                    if (material == null)
                    {
                        errors.Add($"Renderer {renderer.name} has a missing material at index {i}");
                        allValid = false;
                    }
                }
            }
            
            return allValid;
        }
        
        /// <summary>
        /// Checks if all textures on the model are valid
        /// </summary>
        private bool CheckTextures(List<string> errors)
        {
            bool allValid = true;
            
            // Get all renderers on the model
            Renderer[] renderers = modelToTest.GetComponentsInChildren<Renderer>();
            
            foreach (Renderer renderer in renderers)
            {
                // Check each material
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue; // Already reported in CheckMaterials
                    }
                    
                    // Check for main texture
                    if (material.mainTexture == null)
                    {
                        // Some materials don't need textures, so this is just a warning
                        Debug.LogWarning($"Material {material.name} on renderer {renderer.name} has no main texture");
                    }
                    
                    // Check for shader
                    if (material.shader == null)
                    {
                        errors.Add($"Material {material.name} on renderer {renderer.name} has no shader assigned");
                        allValid = false;
                    }
                }
            }
            
            return allValid;
        }
        
        /// <summary>
        /// Checks if all script references on the model are valid
        /// </summary>
        private bool CheckScripts(List<string> errors)
        {
            bool allValid = true;
            
            // Get all components on the model
            Component[] components = modelToTest.GetComponentsInChildren<Component>();
            
            foreach (Component component in components)
            {
                if (component == null)
                {
                    // This happens when there's a missing script reference
                    errors.Add($"GameObject {modelToTest.name} has a missing script reference");
                    allValid = false;
                }
            }
            
            return allValid;
        }
    }
}
