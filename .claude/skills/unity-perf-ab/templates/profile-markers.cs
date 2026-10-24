// Unity_RunCommand body (play mode): average named profiler markers over N frames after a warm-up,
// then write a report the shell can wait on (`wait_file.sh <out> 300 '^FINISHED'`).
// Fill __OUT__ and the Markers list. GPU markers need ProfilerRecorderOptions.GpuRecorder and a
// device with SystemInfo.supportsGpuRecorder; Memory markers report bytes per frame.
// Run the same command for the A and the B configuration, on the same scenario.
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEditor;

internal static class PerfAb
{
    public static string Path = "__OUT__";
    static readonly (string name, ProfilerCategory cat, bool gpu)[] Markers =
    {
        ("DeepWater.Sea.Step", ProfilerCategory.Scripts, false),
        ("DeepWater.Sea.Buoyancy", ProfilerCategory.Scripts, false),
        ("WaterSurfaceUpdate", ProfilerCategory.Render, true),
        ("GC Allocated In Frame", ProfilerCategory.Memory, false),
        ("Main Thread", ProfilerCategory.Internal, false),
    };
    const int Warmup = 30;
    static ProfilerRecorder[] _rec; static double[] _sum; static int _frames, _target;
    static EditorApplication.CallbackFunction _cb;

    public static void Start(int frames)
    {
        _target = frames; _frames = 0;
        _rec = new ProfilerRecorder[Markers.Length]; _sum = new double[Markers.Length];
        for (int i = 0; i < Markers.Length; i++)
        {
            var opts = ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame;
            if (Markers[i].gpu) opts |= ProfilerRecorderOptions.GpuRecorder;
            _rec[i] = ProfilerRecorder.StartNew(Markers[i].cat, Markers[i].name, 1, opts);
        }
        _cb = () =>
        {
            if (_frames++ < Warmup) return;
            for (int i = 0; i < Markers.Length; i++) if (_rec[i].Valid) _sum[i] += _rec[i].LastValue;
            if (_frames >= _target + Warmup) Finish();
        };
        EditorApplication.update += _cb;
        File.WriteAllText(Path, "RUNNING\n");
    }

    static void Finish()
    {
        EditorApplication.update -= _cb;
        int n = _frames - Warmup;
        var sb = new StringBuilder();
        // Record what changes the number besides the code: resolution, fps, device.
        sb.AppendLine($"frames={n} fps~{1f / Time.smoothDeltaTime:F0} screen={Screen.width}x{Screen.height} gpuRecorder={SystemInfo.supportsGpuRecorder}");
        for (int i = 0; i < Markers.Length; i++)
        {
            bool mem = Markers[i].cat.Equals(ProfilerCategory.Memory);
            double perFrame = _sum[i] / n * (mem ? 1.0 : 1e-6);
            sb.AppendLine($"{Markers[i].name}{(Markers[i].gpu ? " [GPU]" : "")}: {(_rec[i].Valid ? "" : "(no samples) ")}avg {perFrame:F4} {(mem ? "B/frame" : "ms/frame")}");
            _rec[i].Dispose();
        }
        sb.AppendLine("FINISHED");
        File.WriteAllText(Path, sb.ToString());
    }
}

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) { result.LogError("play mode only"); return; }
        Application.runInBackground = true;
        PerfAb.Start(300);
        result.Log("profiling 300 frames after a 30-frame warm-up");
    }
}
