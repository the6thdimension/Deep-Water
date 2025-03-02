using UnityEngine;

namespace RH.Testing.Examples
{
    /// <summary>
    /// Example of a script that can be tested with the RH Testing Suite
    /// </summary>
    [Testable(TestType.Unit, TestMode.Both, TestCategory.Script, "Example testable script that demonstrates how to implement the ITestable interface")]
    public class ExampleTestableScript : MonoBehaviour, ITestable
    {
        [SerializeField] private float someValue = 5f;
        [SerializeField] private string someText = "Hello World";
        
        /// <summary>
        /// Runs the test for this component
        /// </summary>
        public bool RunTest()
        {
            // Example test logic
            bool test1 = TestSomeValue();
            bool test2 = TestSomeText();
            
            // Log results
            Debug.Log($"ExampleTestableScript.TestSomeValue: {(test1 ? "Passed" : "Failed")}");
            Debug.Log($"ExampleTestableScript.TestSomeText: {(test2 ? "Passed" : "Failed")}");
            
            // Return true if all tests pass
            return test1 && test2;
        }
        
        /// <summary>
        /// Returns a description of the test
        /// </summary>
        public string GetTestDescription()
        {
            return "Tests the ExampleTestableScript component by validating someValue and someText properties";
        }
        
        /// <summary>
        /// Tests the someValue property
        /// </summary>
        [Testable(TestType.Unit, TestMode.Both, TestCategory.Script, "Tests the someValue property")]
        private bool TestSomeValue()
        {
            // Example test logic
            return someValue > 0f && someValue <= 10f;
        }
        
        /// <summary>
        /// Tests the someText property
        /// </summary>
        [Testable(TestType.Unit, TestMode.Both, TestCategory.Script, "Tests the someText property")]
        private bool TestSomeText()
        {
            // Example test logic
            return !string.IsNullOrEmpty(someText);
        }
        
        /// <summary>
        /// Example of a method that can be tested individually
        /// </summary>
        [Testable(TestType.Unit, TestMode.EditMode, TestCategory.Script, "Tests the AddNumbers method")]
        public bool TestAddNumbers()
        {
            // Test the AddNumbers method
            int result = AddNumbers(2, 3);
            return result == 5;
        }
        
        /// <summary>
        /// Example method to test
        /// </summary>
        public int AddNumbers(int a, int b)
        {
            return a + b;
        }
    }
}
