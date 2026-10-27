// Unity_RunCommand body: record a Dogfight smoke, once every few seconds of fixed time, after Play has started.
// Before: open Dogfight and add the setup unsaved, e.g.
//   EditorSceneManager.OpenScene("Assets/_SCENES/Dogfight.unity", OpenSceneMode.Single);
//   DeepWater.Aviator.Editor.AviatorTools.AddFourVersusFour();   // or AddAiWingman(), AddAiJet()
// then Unity_ManageEditor Play, then this. Wait from the shell: scripts in unity-mcp-workflow,
//   wait_file.sh <csv> 600 '^# end'
// After: Unity_ManageEditor Stop; PlayerSettings.runInBackground = false; reopen the scene that was open before.
// Edit the marked parts: the events (cycle the seat, order home) and the row.
using System.Linq;
using UnityEngine;
using UnityEditor;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.Aviator.Modules;

internal class CommandScript : IRunCommand
{
    const string Csv = "__OUT_CSV__";
    const float EventAt = 120f, Every = 10f, Duration = 240f;
    static AirWingRunner s_runner;
    static float s_start, s_last, s_next;
    static bool s_event;
    static System.Text.StringBuilder s_sb;

    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) { result.LogError("not playing yet"); return; }
        Application.runInBackground = true;   // the Editor throttles an unfocused player loop
        s_runner = Object.FindAnyObjectByType<AirWingRunner>();
        s_start = Time.fixedTime; s_last = -1f; s_next = 0f; s_event = false;
        s_sb = new System.Text.StringBuilder("t,player," + string.Join(",", s_runner.Flight.Aircraft.Select(a => $"seat{a.Id},ft{a.Id}")) + "\n");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        result.Log($"recording {s_runner.Flight.Aircraft.Count} aircraft");
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        float t = Time.fixedTime;
        if (t == s_last) return;
        s_last = t;
        float rel = t - s_start;
        var core = s_runner.Flight;
        // ── EDIT: the event ──
        if (!s_event && rel >= EventAt) { s_sb.AppendLine($"# cycle at {rel:F0}: player now in {s_runner.Roster.Cycle()}"); s_event = true; }
        // Object.FindAnyObjectByType<AviatorOrders>().GoHomeAll();   // the go-home order
        if (rel >= s_next)
        {
            s_next += Every;
            // ── EDIT: the row ──
            s_sb.AppendLine(System.FormattableString.Invariant($"{rel:F0},{s_runner.PlayerAircraftId},") +
                            string.Join(",", core.Aircraft.Select(a => $"{s_runner.Roster.Describe(a.Id).Replace(",", " ")},{a.State.Position.y / 0.3048f:F0}")));
        }
        if (rel >= Duration)
        {
            foreach (var a in core.Aircraft) s_sb.AppendLine($"# aircraft {a.Id}: {a.State.Status} {a.CrashReason}");
            foreach (var p in Object.FindObjectsByType<AviatorPilot>(FindObjectsSortMode.None).OrderBy(p => p.GetComponent<AircraftView>().AircraftId))
                s_sb.AppendLine($"# rules {p.GetComponent<AircraftView>().AircraftId}: " + string.Join(" | ", p.Log.Firings.Select(f => $"{f.Tick / 50f:F0}s {f.Rule}").Take(20)));
            s_sb.AppendLine("# end");
            System.IO.File.WriteAllText(Csv, s_sb.ToString());
            EditorApplication.update -= Tick;
        }
    }
}
