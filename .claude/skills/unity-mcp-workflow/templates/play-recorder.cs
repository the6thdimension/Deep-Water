// Unity_RunCommand body: record values once per FIXED step in play mode, optionally perturb at a
// set time and take a screenshot, then write a CSV the shell can wait on and analyse.
// Send it AFTER play has started (Unity_ManageEditor Play). Then:
//   scripts/wait_file.sh <csv> 300 '^# end'   and   python -I scripts/periods.py <csv> --after 0
// Afterwards send templates/exit-play.cs, which also restores PlayerSettings.runInBackground.
//
// Edit the four marked regions. Times are seconds of Time.fixedTime from when this command runs.
// Static state survives between EditorApplication.update calls because play mode does not reload
// the domain mid-play. Entering play does reload it, so this must be sent after Play.
using System.Linq;
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    const string Csv = "__OUT_CSV__";           // absolute path, forward slashes, in the scratchpad
    const string Shot = "__OUT_PNG__";          // "" for no screenshot
    const float ShotAt = 12f, PerturbAt = 16f, Duration = 56f;

    static Rigidbody[] s_bodies;
    static float s_start, s_last;
    static bool s_shot, s_perturbed;
    static System.Text.StringBuilder s_sb;

    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) { result.LogError("not playing yet: send Unity_ManageEditor Play first"); return; }
        Application.runInBackground = true;   // the Editor throttles an unfocused player loop otherwise

        // ── EDIT 1: what to record ────────────────────────────────────────
        s_bodies = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None)
            .Where(rb => !rb.isKinematic).OrderBy(rb => rb.name).ToArray();
        // ── EDIT 2: one-off setup (e.g. calm the sea, set a scenario) ─────
        // var director = Object.FindFirstObjectByType<DeepWater.Sea.Core.SeaStateDirector>(); director.SetSeaState(0);

        s_start = Time.fixedTime; s_last = -1f; s_shot = string.IsNullOrEmpty(Shot); s_perturbed = false;
        s_sb = new System.Text.StringBuilder();
        s_sb.AppendLine("t," + string.Join(",", s_bodies.Select(b => b.name.Replace(",", ""))));
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        result.Log($"recording {s_bodies.Length} bodies: " + string.Join("; ", s_bodies.Select(b =>
            $"{b.name} m={b.mass:0} com={b.centerOfMass:F2} I={b.inertiaTensor:F1}")));
    }

    // ── EDIT 3: the value recorded per body per fixed step (here: heel about the body's forward axis, degrees)
    static float Value(Rigidbody rb) => -Mathf.Asin(Mathf.Clamp((rb.rotation * Vector3.right).y, -1f, 1f)) * Mathf.Rad2Deg;

    // ── EDIT 4: the perturbation (here: heel 5 degrees, from rest)
    static void Perturb(Rigidbody rb)
    {
        rb.rotation = Quaternion.AngleAxis(5f, rb.rotation * Vector3.forward) * rb.rotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        float t = Time.fixedTime;
        if (t == s_last) return;   // sample once per fixed step, not per Editor update
        s_last = t;
        float rel = t - s_start;
        if (!s_shot && rel >= ShotAt) { ScreenCapture.CaptureScreenshot(Shot); s_shot = true; }
        if (!s_perturbed && rel >= PerturbAt)
        {
            s_sb.AppendLine("# perturbed at t=" + rel.ToString("0.00"));
            foreach (var rb in s_bodies) Perturb(rb);
            s_perturbed = true;
        }
        if (s_perturbed)
            s_sb.AppendLine($"{rel - PerturbAt:0.000}," + string.Join(",", s_bodies.Select(b => Value(b).ToString("0.0000"))));
        if (rel >= Duration)
        {
            s_sb.AppendLine("# end");
            System.IO.File.WriteAllText(Csv, s_sb.ToString());
            EditorApplication.update -= Tick;
        }
    }
}
