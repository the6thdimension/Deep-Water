using UnityEngine;
using System;

namespace RH.Testing
{
    /// <summary>
    /// Defines the type of test
    /// </summary>
    public enum TestType
    {
        All,
        Unit,
        Integration,
        System
    }

    /// <summary>
    /// Defines the mode in which the test can run
    /// </summary>
    public enum TestMode
    {
        EditMode,
        PlayMode,
        Both
    }

    /// <summary>
    /// Defines the category of the test
    /// </summary>
    public enum TestCategory
    {
        Scene,
        Script,
        Model
    }

    /// <summary>
    /// Represents a test item in the test suite
    /// </summary>
    [Serializable]
    public class TestItem
    {
        public string Name;
        public string Description;
        public TestType TestType;
        public TestMode TestMode;
        public TestCategory Category;
        public bool IsEnabled = true;
        public string TargetPath; // Path to the target object (scene, script, model)
        public string TestScriptPath; // Path to the test script
        
        // For runtime use only (not serialized)
        [NonSerialized]
        public Func<bool> TestAction;
        
        [NonSerialized]
        public TestResult LastResult;

        public TestItem(string name, string description, TestType testType, TestMode testMode, 
                       TestCategory category, string targetPath = "", string testScriptPath = "")
        {
            Name = name;
            Description = description;
            TestType = testType;
            TestMode = testMode;
            Category = category;
            TargetPath = targetPath;
            TestScriptPath = testScriptPath;
        }
    }

    /// <summary>
    /// Represents the result of a test execution
    /// </summary>
    [Serializable]
    public class TestResult
    {
        public string TestName;
        public bool Success;
        public string Message;
        public float ExecutionDuration;
        public DateTime ExecutionTime;
        
        public TestResult(string testName, bool success, string message)
        {
            TestName = testName;
            Success = success;
            Message = message;
            ExecutionTime = DateTime.Now;
        }
    }

    /// <summary>
    /// Attribute to mark a class or method as testable
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class TestableAttribute : Attribute
    {
        public TestType TestType { get; private set; }
        public TestMode TestMode { get; private set; }
        public TestCategory Category { get; private set; }
        public string Description { get; private set; }
        
        public TestableAttribute(TestType testType, TestMode testMode, TestCategory category, string description = "")
        {
            TestType = testType;
            TestMode = testMode;
            Category = category;
            Description = description;
        }
    }

    /// <summary>
    /// Interface for testable components
    /// </summary>
    public interface ITestable
    {
        bool RunTest();
    }
}
