// Unity_RunCommand body: force Unity to pick up edited scripts and compile them.
// Before sending: run `scripts/wait_build.sh mark`. After: `scripts/wait_build.sh <Assembly> <NewSymbol>`.
// Replace __PATHS__ with the edited files or folders, e.g. "Assets/Sea State/Core/Buoyancy", "Assets/Sea State/Tests/Editor".
// If this command itself returns COMPILATION_IN_PROGRESS, Unity is already compiling: skip it and just wait.
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // Unity defers every compile while playing; edits made during play sit until play stops.
        if (EditorApplication.isPlaying) { result.LogError("still in play mode: stop play first (Unity_ManageEditor Stop)"); return; }
        foreach (var path in new[] { __PATHS__ })
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        result.Log($"reimported; compile requested; isCompiling {EditorApplication.isCompiling}; scene {scene.path} dirty {scene.isDirty}");
    }
}
