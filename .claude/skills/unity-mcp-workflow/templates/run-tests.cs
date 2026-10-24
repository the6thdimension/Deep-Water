// Unity_RunCommand body: run EditMode tests and stream results to a file the shell can wait on.
// Replace __OUT__ with an absolute path (forward slashes) in the session scratchpad, and
// __ASSEMBLIES__ with the test assembly names, e.g. "DeepWater.Sea.Tests.Editor".
// Then: `scripts/wait_file.sh <out> 300 '^FINISHED'`.
//
// Rules learned the hard way:
// - Delete an old results file from the SHELL first. File.Delete inside RunCommand is refused
//   ("User interactions are not supported for MCP tool calls").
// - Compile first, in a separate command. Reimporting a test file in the same command that
//   starts the run triggers a domain reload that discards the callback.
// - The active scene must be clean, or the Test Runner pops a "save scene?" dialog and waits.
// - Test Debug.Log lines land in Editor.log; tag them (e.g. "[SPIKE]") and grep for the tag.
using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

internal class CommandScript : IRunCommand
{
    class Cb : ICallbacks
    {
        public string Path;
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder();
        public void RunStarted(ITestAdaptor t) { _sb.AppendLine($"started {System.DateTime.Now:HH:mm:ss}"); }
        public void RunFinished(ITestResultAdaptor r)
        {
            _sb.AppendLine($"FINISHED pass {r.PassCount} fail {r.FailCount} skip {r.SkipCount} inconclusive {r.InconclusiveCount} in {r.Duration:0.0}s");
            System.IO.File.WriteAllText(Path, _sb.ToString());
        }
        public void TestStarted(ITestAdaptor t) { }
        public void TestFinished(ITestResultAdaptor r)
        {
            if (r.HasChildren) return;
            _sb.AppendLine($"{r.TestStatus,-7} {r.Duration,6:0.00}s {r.Test.FullName}");
            if (r.TestStatus == TestStatus.Failed) _sb.AppendLine("    " + r.Message?.Replace("\n", "\n    "));
        }
    }

    public void Execute(ExecutionResult result)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty) { result.LogError($"{scene.path} has unsaved changes: the Test Runner would block on a save dialog"); return; }
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Cb { Path = "__OUT__" });
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { __ASSEMBLIES__ } }));
        result.Log("tests started");
    }
}
