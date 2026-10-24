// Unity_RunCommand body: leave play mode and undo what play-mode measurement changes.
// Project rule: restore PlayerSettings.runInBackground to false after any play session that set
// Application.runInBackground. On 2026-10-23 a plain Stop already left it false, so this is cheap
// insurance, not a proven leak. Then confirm with Unity_ManageEditor GetState that IsPlaying is
// false before editing any .cs file: edits made during play freeze compilation.
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        bool wasPlaying = EditorApplication.isPlaying;
        EditorApplication.ExitPlaymode();
        bool rib = PlayerSettings.runInBackground;
        PlayerSettings.runInBackground = false;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        result.Log($"was playing {wasPlaying}; runInBackground was {rib}, now {PlayerSettings.runInBackground}; scene {scene.path} dirty {scene.isDirty}");
    }
}
