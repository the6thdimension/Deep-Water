// Unity_RunCommand body: attribute GC allocations to the deepest allocating profiler samples over
// the last ~60 recorded frames. First record: ProfilerDriver.ClearAllFrames(); profileEditor = false;
// enabled = true; let the scenario run a few seconds in play mode; then send this.
// Reports bytes per frame per sample path, largest first.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var totals = new Dictionary<string, double>();
        int frames = 0;
        for (int f = ProfilerDriver.lastFrameIndex - 60; f <= ProfilerDriver.lastFrameIndex - 1; f++)
        {
            using (var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnGcMemory, false))
            {
                if (view == null || !view.valid) continue;
                frames++;
                var stack = new List<int> { view.GetRootItemID() };
                var children = new List<int>();
                while (stack.Count > 0)
                {
                    int id = stack[stack.Count - 1]; stack.RemoveAt(stack.Count - 1);
                    view.GetItemChildren(id, children);
                    bool anyChildAlloc = false;
                    foreach (var c in children)
                        if (view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnGcMemory) > 0) { stack.Add(c); anyChildAlloc = true; }
                    if (!anyChildAlloc && id != view.GetRootItemID())
                    {
                        string path = view.GetItemPath(id);
                        totals[path] = (totals.TryGetValue(path, out var v) ? v : 0) + view.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnGcMemory);
                    }
                }
            }
        }
        result.Log($"frames analysed: {frames}, total {totals.Values.Sum() / System.Math.Max(frames, 1):F0} B/frame");
        foreach (var kv in totals.OrderByDescending(k => k.Value).Take(10))
            result.Log($"{kv.Value / System.Math.Max(frames, 1),9:F0} B/frame  {kv.Key}");
    }
}
