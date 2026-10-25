#if UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace DeepWater.FullSpeed.Editor
{
    /// <summary>
    /// Keeps the Unity Editor and every process it starts out of Windows 11 power throttling (EcoQoS).
    /// - With the Editor in the background, Windows runs it and its children on a hybrid CPU's efficiency cores.
    ///   On the i9-12900H that halved the speed of reloads, compiles, tests and shader builds (METHODOLOGY AP27).
    /// - This sets each process's own power-throttling switch (the one Task Manager's "Efficiency mode" flips).
    ///   It changes no Windows setting and ends with each process.
    /// - A background timer rescans the process tree, so children started during a build, while Unity's main thread
    ///   is blocked, are caught too.
    /// Preferences > Deep Water > Full Speed turns it off, or allows it on battery.
    /// </summary>
    [InitializeOnLoad]
    public static class FullSpeed
    {
        // Per-machine preferences (EditorPrefs): whether full speed suits this machine is not a project decision.
        internal const string EnabledPref = "DeepWater.FullSpeed.Enabled";
        internal const string OnBatteryPref = "DeepWater.FullSpeed.OnBattery";
        internal const string IntervalPref = "DeepWater.FullSpeed.IntervalSeconds";
        internal const bool EnabledByDefault = true;
        internal const bool OnBatteryByDefault = false;   // a laptop on battery keeps Windows' battery saving
        internal const float DefaultIntervalSeconds = 1f;  // a scan takes ~1 ms; short-lived csc / ILPP processes live a few seconds
        internal const float MinIntervalSeconds = 0.25f, MaxIntervalSeconds = 10f;

        static readonly object Gate = new object();
        static readonly HashSet<int> FullSpeedIds = new HashSet<int>();         // processes set to full speed and still alive
        static readonly Dictionary<int, string> Names = new Dictionary<int, string>();
        static readonly HashSet<string> ReportedFailures = new HashSet<string>(); // one warning per executable, not per scan
        static readonly Queue<(LogType type, string text)> PendingLogs = new Queue<(LogType, string)>();
        static Timer _timer;
        static int _editorId;
        static bool _throttledByBattery;
        static int _scans, _lastTreeSize;

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPref, EnabledByDefault);
            set { EditorPrefs.SetBool(EnabledPref, value); Apply(); }
        }

        public static bool OnBattery
        {
            get => EditorPrefs.GetBool(OnBatteryPref, OnBatteryByDefault);
            set { EditorPrefs.SetBool(OnBatteryPref, value); Apply(); }
        }

        public static float IntervalSeconds
        {
            get => Mathf.Clamp(EditorPrefs.GetFloat(IntervalPref, DefaultIntervalSeconds), MinIntervalSeconds, MaxIntervalSeconds);
            set { EditorPrefs.SetFloat(IntervalPref, Mathf.Clamp(value, MinIntervalSeconds, MaxIntervalSeconds)); Apply(); }
        }

        /// <summary>Processes currently held at full speed (the Editor included).</summary>
        public static int Count { get { lock (Gate) return FullSpeedIds.Count; } }

        public static bool Running => _timer != null;

        static FullSpeed()
        {
            _editorId = System.Diagnostics.Process.GetCurrentProcess().Id;
            AssemblyReloadEvents.beforeAssemblyReload += StopTimer;   // no timer callback into an unloading domain
            EditorApplication.quitting += StopTimer;
            EditorApplication.update += DrainLogs;
            Apply();
        }

        /// <summary>Start or stop to match the preferences. Turning it off hands every process back to Windows.</summary>
        internal static void Apply()
        {
            if (Enabled) StartTimer();
            else
            {
                StopTimer();
                RestoreAll("turned off in Preferences");
            }
            DrainLogs();   // Apply runs on the main thread: print now, before a reload can drop the queue
        }

        static void StartTimer()
        {
            int ms = Mathf.RoundToInt(IntervalSeconds * 1000f);
            if (_timer != null) { _timer.Change(0, ms); return; }
            _timer = new Timer(_ => Scan(), null, 0, ms);
            Log(LogType.Log, $"[Full Speed] on: Unity (pid {_editorId}) and the processes it starts run outside Windows power throttling, rescanned every {IntervalSeconds:0.##} s. Preferences > Deep Water > Full Speed.");
        }

        static void StopTimer()
        {
            if (_timer == null) return;
            _timer.Dispose();
            _timer = null;
        }

        /// <summary>Scan now, on the calling thread, instead of waiting for the timer.</summary>
        public static void ScanNow() => Scan();

        /// <summary>One pass: find the Editor's process tree, set new members to full speed, forget processes that have exited.</summary>
        static void Scan()
        {
            try
            {
                List<ProcessEntry> snapshot = WindowsProcesses.Snapshot();
                HashSet<int> tree = ProcessTree.WithDescendants(_editorId, snapshot);
                bool onAc = WindowsProcesses.OnAcPower();
                lock (Gate)
                {
                    _scans++;
                    _lastTreeSize = tree.Count;
                    FullSpeedIds.IntersectWith(tree);   // ids of exited processes may be reused by unrelated ones
                    if (!onAc && !OnBatteryCached)
                    {
                        if (!_throttledByBattery) { _throttledByBattery = true; RestoreAllLocked("on battery"); }
                        return;
                    }
                    if (_throttledByBattery) { _throttledByBattery = false; Log(LogType.Log, "[Full Speed] on AC power again: back to full speed."); }

                    var newNames = new List<string>();
                    foreach (ProcessEntry e in snapshot)
                    {
                        if (!tree.Contains(e.Id) || FullSpeedIds.Contains(e.Id)) continue;
                        string error = WindowsProcesses.SetFullSpeed(e.Id);
                        if (error == null) { FullSpeedIds.Add(e.Id); Names[e.Id] = e.Name; newNames.Add(e.Name); }
                        else if (ReportedFailures.Add(e.Name))
                            Log(LogType.Warning, $"[Full Speed] could not set {e.Name} (pid {e.Id}) to full speed: {error}. It stays under Windows power throttling.");
                    }
                    // Only the first scan reports what it set; afterwards the Report menu has the live table.
                    if (_scans == 1 && newNames.Count > 0)
                        Log(LogType.Log, $"[Full Speed] {newNames.Count} processes set to full speed: {Summarise(newNames)}.");
                }
            }
            catch (Exception ex)
            {
                // Not swallowed: reported once per message, and the next scan tries again.
                lock (Gate)
                    if (ReportedFailures.Add(ex.Message)) Log(LogType.Error, "[Full Speed] scan failed: " + ex);
            }
        }

        // EditorPrefs is main-thread only, so the timer reads a copy refreshed on the main thread.
        static bool OnBatteryCached = OnBatteryByDefault;

        static void RestoreAll(string why)
        {
            lock (Gate) RestoreAllLocked(why);
        }

        static void RestoreAllLocked(string why)
        {
            int n = 0;
            foreach (int id in FullSpeedIds)
                if (WindowsProcesses.SetWindowsDefault(id) == null) n++;
            FullSpeedIds.Clear();
            if (n > 0) Log(LogType.Log, $"[Full Speed] {why}: {n} processes handed back to Windows power management.");
        }

        static void DrainLogs()
        {
            OnBatteryCached = OnBattery;
            lock (Gate)
                while (PendingLogs.Count > 0)
                {
                    (LogType type, string text) = PendingLogs.Dequeue();
                    if (type == LogType.Error) Debug.LogError(text);
                    else if (type == LogType.Warning) Debug.LogWarning(text);
                    else Debug.Log(text);
                }
        }

        // Unity's Debug API is main-thread only: the timer queues, DrainLogs prints.
        static void Log(LogType type, string text)
        {
            lock (Gate) PendingLogs.Enqueue((type, text));
        }

        static string Summarise(IEnumerable<string> names) =>
            string.Join(", ", names.GroupBy(n => n).OrderByDescending(g => g.Count()).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key));

        /// <summary>A text table of the Editor's process tree and each process's power-throttling state.</summary>
        public static string Report()
        {
            var sb = new StringBuilder();
            List<ProcessEntry> snapshot = WindowsProcesses.Snapshot();
            HashSet<int> tree = ProcessTree.WithDescendants(_editorId, snapshot);
            sb.AppendLine($"[Full Speed] {(Running ? "on" : "off")}; {(WindowsProcesses.OnAcPower() ? "AC power" : "battery")}; Editor pid {_editorId}; {tree.Count} processes in its tree; {Count} held at full speed; {_scans} scans.");
            foreach (var g in snapshot.Where(e => tree.Contains(e.Id)).GroupBy(e => e.Name).OrderBy(g => g.Key))
            {
                int full = g.Count(e => { var s = WindowsProcesses.GetState(e.Id); return s.error == null && (s.control & WindowsProcesses.ExecutionSpeed) != 0 && (s.state & WindowsProcesses.ExecutionSpeed) == 0; });
                sb.AppendLine($"  {g.Key,-32} {g.Count(),3} running, {full,3} at full speed");
            }
            return sb.ToString();
        }

        [MenuItem("Deep Water/Full Speed/Report")]
        static void ReportMenu() => Debug.Log(Report());

        [SettingsProvider]
        static SettingsProvider Preferences() => new SettingsProvider("Preferences/Deep Water/Full Speed", SettingsScope.User)
        {
            keywords = new[] { "Full Speed", "EcoQoS", "power throttling", "efficiency cores", "build speed" },
            guiHandler = _ =>
            {
                EditorGUILayout.HelpBox("Keeps Unity and the processes it starts (shader compilers, csc, Bee, Burst) out of Windows power throttling, so a background Editor is not held on the efficiency cores. It changes no Windows setting and ends with each process.", MessageType.Info);
                bool enabled = EditorGUILayout.Toggle("Enabled", Enabled);
                if (enabled != Enabled) Enabled = enabled;
                bool onBattery = EditorGUILayout.Toggle("Also on battery", OnBattery);
                if (onBattery != OnBattery) OnBattery = onBattery;
                float interval = EditorGUILayout.Slider("Rescan every (s)", IntervalSeconds, MinIntervalSeconds, MaxIntervalSeconds);
                if (!Mathf.Approximately(interval, IntervalSeconds)) IntervalSeconds = interval;
                EditorGUILayout.LabelField("Status", $"{(Running ? "on" : "off")}, {Count} processes at full speed, tree of {_lastTreeSize}");
                if (GUILayout.Button("Report to Console", GUILayout.Width(160))) ReportMenu();
            }
        };
    }
}
#endif
