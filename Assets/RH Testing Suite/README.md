# RH Testing Suite

A comprehensive testing framework for Unity projects that allows you to configure and execute different types of tests through a user-friendly GUI window.

## Features

- **Custom Editor Window**: A dedicated Unity Editor window for configuring and executing tests.
- **Multiple Test Types**: Support for Unit Tests, Integration Tests, and System Tests.
- **Multiple Test Categories**: Scene Tests, Script Tests, and Model Tests.
- **Play Mode and Edit Mode Support**: Tests can be executed in both Play Mode and Edit Mode.
- **Dynamic Test Scope Management**: Easy to include/exclude specific GameObjects, scripts, or assets from testing.
- **Simple Implementation**: Implementing a testable script requires only one additional line of code.
- **Extensible & Configurable**: Easy to add new test types and configure existing ones.
- **Test Results Export**: Export test results to JSON, CSV, or HTML formats.

## Getting Started

### Opening the Test Suite Window

1. In Unity, go to the menu bar and select `RH Navy Sims > Testing Suite`.
2. The RH Testing Suite window will open, displaying all available tests.

### Running Tests

1. In the Test Suite window, select the test type (Unit, Integration, or System) and test mode (Edit Mode or Play Mode).
2. Use the category filters to show/hide specific test categories.
3. Enable the tests you want to run by checking the checkbox next to each test.
4. Click "Run Selected Tests" to execute the selected tests.
5. Test results will be displayed in the console.

### Exporting Test Results

1. Click the "Settings" button in the toolbar.
2. Select "Export Test Results" from the dropdown menu.
3. Choose a location and format (JSON, CSV, or HTML) to save the test results.

## Creating Testable Scripts

### Using the Testable Attribute

Add the `[Testable]` attribute to any class or method you want to test:

```csharp
[Testable(TestType.Unit, TestMode.EditMode, TestCategory.Script, "Description of the test")]
public class MyScript : MonoBehaviour
{
    // Your code here
}
```

### Implementing the ITestable Interface

For more control over the testing process, implement the `ITestable` interface:

```csharp
public class MyScript : MonoBehaviour, ITestable
{
    public bool RunTest()
    {
        // Your test logic here
        return true; // Return true if the test passes, false otherwise
    }
    
    public string GetTestDescription()
    {
        return "Description of the test";
    }
}
```

## Test Types

- **Unit Tests**: Test individual components or functions in isolation.
- **Integration Tests**: Test how components work together.
- **System Tests**: Test the entire system or a large part of it.

## Test Categories

- **Scene Tests**: Ensure all scenes load without errors.
- **Script Tests**: Validate that all scripts execute correctly on relevant GameObjects.
- **Model Tests**: Verify that all models have textures and that attached scripts function properly.

## Test Modes

- **Edit Mode**: Tests that run in the Unity Editor without entering Play Mode.
- **Play Mode**: Tests that run in Play Mode to validate runtime functionality.

## Example Usage

See the `Examples` folder for sample implementations of testable scripts, scene tests, and model tests.

## Extending the Test Suite

To add new test types or categories:

1. Add new values to the `TestType`, `TestCategory`, or `TestMode` enums in `TestTypes.cs`.
2. Update the UI in `RHTestSuiteWindow.cs` to display the new options.
3. Implement the necessary logic in `TestDiscovery.cs` and `TestRunner.cs`.

## Configuration

Test configurations can be exported and imported to share test setups between team members or projects.

1. Click the "Settings" button in the toolbar.
2. Select "Export Test Configuration" to save the current configuration.
3. Select "Import Test Configuration" to load a previously saved configuration.

## Troubleshooting

- If tests are not showing up in the Test Suite window, try clicking the "Refresh Tests" button.
- If Play Mode tests are not running, make sure the scene is saved before running the tests.
- If you encounter any issues, check the Unity Console for error messages.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
