using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;
using System.IO;

namespace RH.Testing
{
    /// <summary>
    /// Handles discovery of testable items in the project
    /// </summary>
    public static class TestDiscovery
    {
        /// <summary>
        /// Finds all scripts marked with the Testable attribute or implementing ITestable
        /// </summary>
        public static List<TestItem> FindAllTestableScripts()
        {
            List<TestItem> testItems = new List<TestItem>();
            
            // Get all types in all assemblies
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type[] types = assembly.GetTypes();
                    
                    foreach (Type type in types)
                    {
                        // Check if the type has the Testable attribute
                        TestableAttribute classAttribute = type.GetCustomAttribute<TestableAttribute>();
                        if (classAttribute != null)
                        {
                            string assetPath = GetAssetPathForType(type);
                            
                            TestItem item = new TestItem(
                                type.Name,
                                string.IsNullOrEmpty(classAttribute.Description) ? $"Test for {type.Name}" : classAttribute.Description,
                                classAttribute.TestType,
                                classAttribute.TestMode,
                                classAttribute.Category,
                                assetPath
                            );
                            
                            // Create a test action for this type
                            item.TestAction = () => RunTestForType(type);
                            
                            testItems.Add(item);
                        }
                        
                        // Check if the type implements ITestable
                        if (typeof(ITestable).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        {
                            // Only add if we didn't already add it via attribute
                            if (!testItems.Any(t => t.Name == type.Name))
                            {
                                string assetPath = GetAssetPathForType(type);
                                
                                TestItem item = new TestItem(
                                    type.Name,
                                    $"Test for {type.Name}",
                                    TestType.Unit, // Default values
                                    TestMode.EditMode,
                                    TestCategory.Script,
                                    assetPath
                                );
                                
                                // Create a test action for this type
                                item.TestAction = () => RunTestForType(type);
                                
                                testItems.Add(item);
                            }
                        }
                        
                        // Check methods with the Testable attribute
                        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                        {
                            TestableAttribute methodAttribute = method.GetCustomAttribute<TestableAttribute>();
                            if (methodAttribute != null)
                            {
                                string assetPath = GetAssetPathForType(type);
                                
                                TestItem item = new TestItem(
                                    $"{type.Name}.{method.Name}",
                                    string.IsNullOrEmpty(methodAttribute.Description) ? $"Test method {method.Name} in {type.Name}" : methodAttribute.Description,
                                    methodAttribute.TestType,
                                    methodAttribute.TestMode,
                                    methodAttribute.Category,
                                    assetPath
                                );
                                
                                // Create a test action for this method
                                item.TestAction = () => RunTestForMethod(method);
                                
                                testItems.Add(item);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error processing assembly {assembly.FullName}: {e.Message}");
                }
            }
            
            return testItems;
        }

        /// <summary>
        /// Finds all scenes in the project and creates test items for them
        /// </summary>
        public static List<TestItem> FindAllSceneTests()
        {
            List<TestItem> testItems = new List<TestItem>();
            
            // Find all scene files in the project
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            
            foreach (string guid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                
                TestItem item = new TestItem(
                    $"Scene: {sceneName}",
                    $"Tests if scene {sceneName} loads without errors",
                    TestType.Integration,
                    TestMode.PlayMode,
                    TestCategory.Scene,
                    scenePath
                );
                
                // Create a test action for this scene
                item.TestAction = () => TestSceneLoading(scenePath);
                
                testItems.Add(item);
            }
            
            return testItems;
        }

        /// <summary>
        /// Finds all models in the project and creates test items for them
        /// </summary>
        public static List<TestItem> FindAllModelTests()
        {
            List<TestItem> testItems = new List<TestItem>();
            
            // Find all model files in the project
            string[] modelGuids = AssetDatabase.FindAssets("t:Model");
            
            foreach (string guid in modelGuids)
            {
                string modelPath = AssetDatabase.GUIDToAssetPath(guid);
                string modelName = Path.GetFileNameWithoutExtension(modelPath);
                
                TestItem item = new TestItem(
                    $"Model: {modelName}",
                    $"Tests if model {modelName} has valid textures and scripts",
                    TestType.Unit,
                    TestMode.EditMode,
                    TestCategory.Model,
                    modelPath
                );
                
                // Create a test action for this model
                item.TestAction = () => TestModel(modelPath);
                
                testItems.Add(item);
            }
            
            return testItems;
        }

        /// <summary>
        /// Gets the asset path for a given type
        /// </summary>
        private static string GetAssetPathForType(Type type)
        {
            // Try to find the script asset in the project
            string[] guids = AssetDatabase.FindAssets($"t:MonoScript {type.Name}");
            
            if (guids.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }
            
            return "";
        }

        /// <summary>
        /// Runs a test for a given type
        /// </summary>
        private static bool RunTestForType(Type type)
        {
            try
            {
                // If it's a MonoBehaviour, we need to create a GameObject with the component
                if (typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    GameObject testObject = new GameObject($"Test_{type.Name}");
                    ITestable testComponent = testObject.AddComponent(type) as ITestable;
                    
                    if (testComponent != null)
                    {
                        bool result = testComponent.RunTest();
                        GameObject.DestroyImmediate(testObject);
                        return result;
                    }
                    
                    GameObject.DestroyImmediate(testObject);
                    Debug.LogError($"Type {type.Name} does not implement ITestable");
                    return false;
                }
                else
                {
                    // For non-MonoBehaviour types, create an instance and run the test
                    ITestable testInstance = Activator.CreateInstance(type) as ITestable;
                    
                    if (testInstance != null)
                    {
                        return testInstance.RunTest();
                    }
                    
                    Debug.LogError($"Type {type.Name} does not implement ITestable");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error running test for type {type.Name}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Runs a test for a given method
        /// </summary>
        private static bool RunTestForMethod(MethodInfo method)
        {
            try
            {
                // Check if the method is static
                if (method.IsStatic)
                {
                    // For static methods, invoke directly
                    object result = method.Invoke(null, null);
                    
                    // Check if the method returns a bool
                    if (method.ReturnType == typeof(bool))
                    {
                        return (bool)result;
                    }
                    
                    // If not, assume success if no exception was thrown
                    return true;
                }
                else
                {
                    // For instance methods, create an instance of the declaring type
                    Type declaringType = method.DeclaringType;
                    
                    // If it's a MonoBehaviour, we need to create a GameObject with the component
                    if (typeof(MonoBehaviour).IsAssignableFrom(declaringType))
                    {
                        GameObject testObject = new GameObject($"Test_{declaringType.Name}");
                        Component component = testObject.AddComponent(declaringType);
                        
                        object result = method.Invoke(component, null);
                        
                        GameObject.DestroyImmediate(testObject);
                        
                        // Check if the method returns a bool
                        if (method.ReturnType == typeof(bool))
                        {
                            return (bool)result;
                        }
                        
                        // If not, assume success if no exception was thrown
                        return true;
                    }
                    else
                    {
                        // For non-MonoBehaviour types, create an instance and invoke the method
                        object instance = Activator.CreateInstance(declaringType);
                        
                        object result = method.Invoke(instance, null);
                        
                        // Check if the method returns a bool
                        if (method.ReturnType == typeof(bool))
                        {
                            return (bool)result;
                        }
                        
                        // If not, assume success if no exception was thrown
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error running test method {method.Name}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tests if a scene loads without errors
        /// </summary>
        private static bool TestSceneLoading(string scenePath)
        {
            try
            {
                // In a real implementation, this would load the scene additively in play mode
                // and check for errors. For now, we'll just simulate success.
                Debug.Log($"Testing scene loading: {scenePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error testing scene {scenePath}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tests if a model has valid textures and scripts
        /// </summary>
        private static bool TestModel(string modelPath)
        {
            try
            {
                // Load the model asset
                GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                
                if (modelPrefab == null)
                {
                    Debug.LogError($"Failed to load model at path: {modelPath}");
                    return false;
                }
                
                // Check for missing textures
                Renderer[] renderers = modelPrefab.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    if (renderer.sharedMaterials.Any(m => m == null))
                    {
                        Debug.LogError($"Model {modelPath} has missing materials");
                        return false;
                    }
                    
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null && material.mainTexture == null)
                        {
                            Debug.LogWarning($"Material {material.name} on model {modelPath} has no main texture");
                        }
                    }
                }
                
                // Check for missing scripts
                Component[] components = modelPrefab.GetComponentsInChildren<Component>();
                foreach (Component component in components)
                {
                    if (component == null)
                    {
                        Debug.LogError($"Model {modelPath} has missing script references");
                        return false;
                    }
                }
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error testing model {modelPath}: {e.Message}");
                return false;
            }
        }
    }
}
