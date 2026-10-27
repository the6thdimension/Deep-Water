// Unity_RunCommand body: run aircraft tests (or a trace helper) by calling them directly, with no Test Runner.
// Use when the open scene is dirty (the Test Runner would block on a save dialog), or for a quick re-run of one
// test class while tuning. Replace the Run(...) lines. Test classes are public with public [Test] methods.
// Before it: Apply Pilot Profiles runs first so the assets match the tools' numbers.
// Results come back in the command's log; tagged Debug.Log lines also land in Editor.log:
//   grep -a "\[Aviator 3d\]" "$LOCALAPPDATA/Unity/Editor/Editor.log" | tail
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        DeepWater.Aviator.Editor.AviatorTools.ApplyPilotProfiles();
        var sb = new System.Text.StringBuilder();
        var fight = new DeepWater.Aviator.Tests.FightTests();
        Run(sb, "AceRookie", () => fight.TheAce_BeatsTheRookie_InAtLeast15Of20NeutralMerges_AndNobodyCrashes());
        // A trace: one flight, a CSV row a second.
        // sb.AppendLine(DeepWater.Aviator.Tests.HomeTests.Trace(0, "__SCRATCH__/home_trace.csv"));
        // sb.AppendLine(DeepWater.Aviator.Tests.FightTests.TraceMerge(5, "__SCRATCH__/merge_trace.csv"));
        result.Log(sb.ToString());
    }

    // No System.Diagnostics.Stopwatch here: RunCommand's compiler lacks the System assembly (CS1069).
    static void Run(System.Text.StringBuilder sb, string name, System.Action a)
    {
        float t0 = Time.realtimeSinceStartup;
        try { a(); sb.AppendLine($"Passed {name} {Time.realtimeSinceStartup - t0:F1}s"); }
        catch (System.Exception e) { sb.AppendLine($"Failed {name} {Time.realtimeSinceStartup - t0:F1}s: {e.Message}"); }
    }
}
