using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RH.Testing
{
    /// <summary>
    /// Handles execution of tests
    /// </summary>
    public static class TestRunner
    {
        // Event that fires when a test completes
        public static event Action<TestItem, TestResult> OnTestCompleted;
        
        // Event that fires when all tests complete
        public static event Action<List<TestResult>> OnAllTestsCompleted;
        
        // List to store all test results
        private static List<TestResult> allTestResults = new List<TestResult>();
        
        /// <summary>
        /// Runs a single test
        /// </summary>
        public static void RunTest(TestItem test)
        {
            if (test == null || test.TestAction == null)
            {
                Debug.LogError("Cannot run test: Test or TestAction is null");
                return;
            }
            
            // Check if we need to switch to play mode
            if (test.TestMode == TestMode.PlayMode && !EditorApplication.isPlaying)
            {
                // Queue the test to run after entering play mode
                QueueTestForPlayMode(test);
                return;
            }
            
            // Run the test and measure execution time
            float startTime = Time.realtimeSinceStartup;
            bool success = false;
            string message = "";
            
            try
            {
                success = test.TestAction();
                message = success ? "Test passed" : "Test failed";
            }
            catch (Exception e)
            {
                success = false;
                message = $"Test threw an exception: {e.Message}";
                Debug.LogException(e);
            }
            
            float duration = Time.realtimeSinceStartup - startTime;
            
            // Create and store the test result
            TestResult result = new TestResult(test.Name, success, message)
            {
                ExecutionDuration = duration
            };
            
            test.LastResult = result;
            allTestResults.Add(result);
            
            // Log the result
            if (success)
            {
                Debug.Log($"Test '{test.Name}' passed in {duration:F3} seconds");
            }
            else
            {
                Debug.LogError($"Test '{test.Name}' failed in {duration:F3} seconds: {message}");
            }
            
            // Fire the test completed event
            OnTestCompleted?.Invoke(test, result);
        }
        
        /// <summary>
        /// Runs multiple tests
        /// </summary>
        public static void RunTests(List<TestItem> tests)
        {
            if (tests == null || tests.Count == 0)
            {
                Debug.LogWarning("No tests to run");
                return;
            }
            
            allTestResults.Clear();
            
            // Separate tests by mode
            var editModeTests = tests.Where(t => t.TestMode == TestMode.EditMode || t.TestMode == TestMode.Both).ToList();
            var playModeTests = tests.Where(t => t.TestMode == TestMode.PlayMode).ToList();
            
            // Run edit mode tests first
            foreach (var test in editModeTests)
            {
                RunTest(test);
            }
            
            // If we have play mode tests, queue them to run after entering play mode
            if (playModeTests.Count > 0)
            {
                if (!EditorApplication.isPlaying)
                {
                    Debug.Log($"Entering play mode to run {playModeTests.Count} play mode tests");
                    QueueTestsForPlayMode(playModeTests);
                }
                else
                {
                    // If already in play mode, run the play mode tests
                    foreach (var test in playModeTests)
                    {
                        RunTest(test);
                    }
                    
                    // Fire the all tests completed event
                    OnAllTestsCompleted?.Invoke(allTestResults);
                }
            }
            else
            {
                // If no play mode tests, fire the all tests completed event now
                OnAllTestsCompleted?.Invoke(allTestResults);
            }
        }
        
        /// <summary>
        /// Queues a test to run after entering play mode
        /// </summary>
        private static void QueueTestForPlayMode(TestItem test)
        {
            Debug.Log($"Queueing test '{test.Name}' to run in play mode");
            
            // Set up a callback to run when entering play mode
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            
            // Store the test to run
            pendingPlayModeTests.Clear();
            pendingPlayModeTests.Add(test);
            
            // Enter play mode
            EditorApplication.isPlaying = true;
        }
        
        /// <summary>
        /// Queues multiple tests to run after entering play mode
        /// </summary>
        private static void QueueTestsForPlayMode(List<TestItem> tests)
        {
            Debug.Log($"Queueing {tests.Count} tests to run in play mode");
            
            // Set up a callback to run when entering play mode
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            
            // Store the tests to run
            pendingPlayModeTests.Clear();
            pendingPlayModeTests.AddRange(tests);
            
            // Enter play mode
            EditorApplication.isPlaying = true;
        }
        
        // List to store tests that are pending to run in play mode
        private static List<TestItem> pendingPlayModeTests = new List<TestItem>();
        
        /// <summary>
        /// Callback that runs when the play mode state changes
        /// </summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && pendingPlayModeTests.Count > 0)
            {
                // Run the pending tests
                Debug.Log($"Running {pendingPlayModeTests.Count} queued play mode tests");
                
                // Use EditorApplication.delayCall to ensure we're fully in play mode
                EditorApplication.delayCall += () =>
                {
                    foreach (var test in pendingPlayModeTests)
                    {
                        RunTest(test);
                    }
                    
                    // Fire the all tests completed event
                    OnAllTestsCompleted?.Invoke(allTestResults);
                    
                    // Clear the pending tests
                    pendingPlayModeTests.Clear();
                };
                
                // Remove the callback
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }
        }
        
        /// <summary>
        /// Gets all test results
        /// </summary>
        public static List<TestResult> GetAllTestResults()
        {
            return allTestResults;
        }
        
        /// <summary>
        /// Clears all test results
        /// </summary>
        public static void ClearAllTestResults()
        {
            allTestResults.Clear();
        }
    }
}
