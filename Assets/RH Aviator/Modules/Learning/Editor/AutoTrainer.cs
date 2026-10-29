using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.ScriptableObjects;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace DeepWater.Aviator.Learning.Editor
{
    /// <summary>A trained brain's record (M19, M34): written beside the latest brain after each session, kept with a kept one.</summary>
    [Serializable]
    public sealed class BrainInfo
    {
        public string run, config, trainedUtc, keptUtc;
        public int steps = -1, sensesVersion, sensesSize;
        /// <summary>Who flew each side in the session that trained it last.</summary>
        public string scenario;
        public string rewardWeights, learnedPilot, configText;
        /// <summary>How the brain scores against a held-out set: not measured until the gate is built (TRAINING-CONCEPT).</summary>
        public string scorecard = "not measured: the held-out gate is not built";
    }

    /// <summary>
    /// Auto-train on Play (A18; TRAINING-CONCEPT M31–M34, SIX 2026-10-29). With it on and Dogfight Training open, a trainer
    /// (<c>mlagents-learn</c> from the venv, M28) is kept running in the background, ready for Play: one rolling run per
    /// config, resumed every session, so each Play continues the same brain. Play while it is still starting is held and
    /// pressed again when it is ready. When Play ends the trainer saves the brain and stops; the newest brain is copied to
    /// <see cref="BrainLibrary.LatestPath"/> with its record, and the next trainer starts, resuming. Self-play when both
    /// sides have learning jets, the one-sided config otherwise; nothing when no jet learns.
    /// <para>The trainer's output goes to a log file under <c>Training/results/auto-trainer-logs</c>, not to the Editor,
    /// so a recompile does not cut it off: its process id is kept in SessionState and found again after a domain reload.
    /// It is stopped when the Editor closes. Nothing here is deleted: a run that cannot resume is renamed aside.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class AutoTrainer
    {
        public enum State { Off = 0, Starting = 1, Listening = 2, Training = 3, Saving = 4, Failed = 5 }

        public const string MenuPath = "Deep Water/RH Aviator/Auto-Train on Play";
        const string OnKey = "DeepWater.Aviator.AutoTrain.On";
        const string VenvKey = "DeepWater.Aviator.AutoTrain.Venv";
        // SessionState outlives a domain reload, not an Editor restart (when the trainer is stopped anyway).
        const string StateKey = "DeepWater.Aviator.AutoTrain.State", WhyKey = "DeepWater.Aviator.AutoTrain.Why";
        const string PidKey = "DeepWater.Aviator.AutoTrain.Pid", ModeKey = "DeepWater.Aviator.AutoTrain.Mode";
        const string LogKey = "DeepWater.Aviator.AutoTrain.Log", SinceKey = "DeepWater.Aviator.AutoTrain.Since";
        const string ResumedKey = "DeepWater.Aviator.AutoTrain.Resumed", EnterKey = "DeepWater.Aviator.AutoTrain.EnterWhenReady";
        const string SessionKey = "DeepWater.Aviator.AutoTrain.SessionInfo", SessionStartKey = "DeepWater.Aviator.AutoTrain.SessionStart";
        /// <summary>AUTHORED: the trainer loads PyTorch and the run in ~10–30 s on this machine; three minutes means it is stuck.</summary>
        const double StartTimeoutS = 180, SaveTimeoutS = 180;

        static Process _process;
        static double _nextLook;

        static AutoTrainer()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.quitting += Quitting;
            EditorApplication.delayCall += () => Menu.SetChecked(MenuPath, On);
        }

        /// <summary>Closing the Editor: a trainer still saving the last session gets a moment to finish; then it goes.</summary>
        static void Quitting()
        {
            if (Now == State.Saving && Alive()) _process.WaitForExit(20000);
            Stop("the Editor is closing");
        }

        /// <summary>The switch (menu and Fighter Brains window); kept per machine in EditorPrefs.</summary>
        public static bool On
        {
            get => EditorPrefs.GetBool(OnKey, false);
            set
            {
                if (value == On) return;
                EditorPrefs.SetBool(OnKey, value);
                Menu.SetChecked(MenuPath, value);
                if (!value && Now != State.Training) Stop("auto-train switched off");
                if (Now == State.Failed) Set(State.Off, "");
                Debug.Log($"[RH Aviator] auto-train on Play {(value ? "on" : "off")}.");
            }
        }

        /// <summary>The Python venv with mlagents 1.1.0 (TRAINING-CONCEPT M28).</summary>
        public static string Venv
        {
            get => EditorPrefs.GetString(VenvKey, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "venvs", "mlagents-1.1.0").Replace('\\', '/'));
            set => EditorPrefs.SetString(VenvKey, value);
        }

        public static State Now => (State)SessionState.GetInt(StateKey, 0);
        public static string Why => SessionState.GetString(WhyKey, "");
        public static TrainerCommand.Mode RunningMode => (TrainerCommand.Mode)SessionState.GetInt(ModeKey, 0);
        public static bool Resumed => SessionState.GetBool(ResumedKey, false);
        public static string LogPath => SessionState.GetString(LogKey, "");
        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
        static string Exe => Path.Combine(Venv, "Scripts", "mlagents-learn.exe");
        static double Since => EditorApplication.timeSinceStartup - SessionState.GetFloat(SinceKey, 0f);

        [MenuItem(MenuPath, priority = 160)]
        static void Toggle() => On = !On;

        [MenuItem(MenuPath, true)]
        static bool ToggleShown()
        {
            Menu.SetChecked(MenuPath, On);
            return true;
        }

        /// <summary>What the open scenes need: nothing (no training scene, or no learning jet), self-play, or one side.</summary>
        public static TrainerCommand.Mode Needed(out int blueLearning, out int redLearning)
        {
            blueLearning = redLearning = 0;
            if (Object.FindAnyObjectByType<FighterTraining>() == null) return TrainerCommand.Mode.None;
            foreach (TrainingSquadron squadron in Object.FindObjectsByType<TrainingSquadron>(FindObjectsInactive.Exclude))
                foreach (AircraftView jet in squadron.Jets)
                {
                    if (jet == null || squadron.BrainOf(jet, out _) != FighterBrain.Learning) continue;
                    if (squadron.Side == Side.Red) redLearning++; else blueLearning++;
                }
            return TrainerCommand.ModeFor(blueLearning, redLearning);
        }

        /// <summary>Clears a failure and lets the next look start a trainer again (the window's Retry).</summary>
        public static void Retry()
        {
            if (Now == State.Failed) Set(State.Off, "");
        }

        /// <summary>Stops the trainer (never mid-Play: the session saves when Play ends).</summary>
        public static void Stop(string reason)
        {
            int pid = SessionState.GetInt(PidKey, 0);
            if (pid != 0 && Alive())
            {
                // The trainer is cmd.exe → mlagents-learn.exe → python.exe: the whole tree goes.
                try
                {
                    using (var kill = Process.Start(new ProcessStartInfo("taskkill", $"/PID {pid} /T /F") { UseShellExecute = false, CreateNoWindow = true }))
                        kill?.WaitForExit(5000);
                }
                catch (Exception e) { Debug.LogError($"[RH Aviator] auto-train: could not stop the trainer (process {pid}): {e.Message}"); }
                Debug.Log($"[RH Aviator] auto-train: trainer stopped ({reason}).");
            }
            SessionState.EraseInt(PidKey);
            _process = null;
            if (Now != State.Failed) Set(State.Off, "");
        }

        /// <summary>Sets the current run aside (renamed, not deleted) so the next session starts a new brain.</summary>
        public static string FreshRun()
        {
            Stop("a fresh run");
            TrainerCommand.Mode mode = Needed(out _, out _);
            if (mode == TrainerCommand.Mode.None) mode = TrainerCommand.Mode.SelfPlay;
            string runDir = Path.Combine(ProjectRoot, TrainerCommand.ResultsDir, TrainerCommand.RunOf(mode));
            string aside = SetAside(runDir, "fresh");
            Set(State.Off, "");
            return aside;
        }

        static string SetAside(string runDir, string reason)
        {
            if (!Directory.Exists(runDir)) return null;
            string aside = $"{runDir}.pre-{reason}-backup-{DateTime.Now:yyyyMMdd-HHmm}";
            Directory.Move(runDir, aside);
            Debug.Log($"[RH Aviator] auto-train: {Path.GetFileName(runDir)} set aside as {Path.GetFileName(aside)} (nothing deleted); the next session starts a new brain.");
            return aside;
        }

        static void Set(State state, string why)
        {
            SessionState.SetInt(StateKey, (int)state);
            SessionState.SetString(WhyKey, why ?? "");
            SessionState.SetFloat(SinceKey, (float)EditorApplication.timeSinceStartup);
        }

        static void Fail(string why)
        {
            string tail = TrainerCommand.Tail(ReadLog(), 6);
            Set(State.Failed, string.IsNullOrEmpty(tail) ? why : $"{why}\n{tail}");
            SessionState.SetBool(EnterKey, false);
            Debug.LogError($"[RH Aviator] auto-train: {why}. Learning jets fly the latest brain until it is retried (Fighter Brains window). Log: {LogPath}\n{tail}");
        }

        static bool Alive()
        {
            int pid = SessionState.GetInt(PidKey, 0);
            if (pid == 0) return false;
            try
            {
                if (_process == null || _process.Id != pid) _process = Process.GetProcessById(pid);
                return !_process.HasExited && _process.ProcessName.Equals("cmd", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException) { return false; }          // no such process: it has gone
            catch (InvalidOperationException) { return false; }  // it went while we looked
        }

        static string ReadLog()
        {
            string path = LogPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
            // cmd.exe holds the file open for writing: share it.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < _nextLook) return;
            _nextLook = EditorApplication.timeSinceStartup + 0.5;
            bool alive = Alive();
            switch (Now)
            {
                case State.Starting:
                    if (ReadLog().Contains(TrainerCommand.ReadyMarker))
                    {
                        Set(State.Listening, "");
                        Debug.Log($"[RH Aviator] auto-train: the trainer is ready ({TrainerCommand.RunOf(RunningMode)}, {(Resumed ? "resumed" : "a new brain")}).");
                        if (SessionState.GetBool(EnterKey, false))
                        {
                            SessionState.SetBool(EnterKey, false);
                            EditorApplication.EnterPlaymode();
                        }
                    }
                    else if (!alive) Fail("the trainer stopped before it was ready");
                    else if (Since > StartTimeoutS) { Stop("not ready in 3 minutes"); Fail("the trainer was not ready in 3 minutes"); }
                    return;
                case State.Listening:
                    if (!alive) Fail("the trainer stopped while it waited for Play");
                    break;
                case State.Training:
                    // Play ended without the event reaching us (a domain reload as Play stops can swallow it).
                    if (!EditorApplication.isPlayingOrWillChangePlaymode) PlayEnded();
                    return;
                case State.Saving:
                    if (!alive)
                    {
                        CopyLatest();
                        Set(State.Off, "");   // the next look starts the next session's trainer, resuming
                    }
                    // Never killed here: that would lose the session. It stays for the log; Stop trainer ends it.
                    else if (Since > SaveTimeoutS) Fail("the trainer has not finished saving after 3 minutes; it is still running (see its log, or Stop trainer)");
                    return;
            }

            // Edit mode: a trainer ready when one is wanted, none when not.
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            TrainerCommand.Mode needed = On ? Needed(out _, out _) : TrainerCommand.Mode.None;
            State now = Now;
            if (needed == TrainerCommand.Mode.None)
            {
                if (now == State.Listening) Stop(On ? "no learning jet in the open scene" : "auto-train off");
                return;
            }
            if (now == State.Listening && RunningMode != needed)
            {
                Stop("the brains changed: " + (needed == TrainerCommand.Mode.SelfPlay ? "both sides learn now" : "one side learns now"));
                now = Now;
            }
            if (now == State.Off) Launch(needed);
        }

        /// <summary>Headless training players are running (tools/headless-training): they train the same rolling run.</summary>
        public static bool HeadlessRunning => Process.GetProcessesByName(TrainerCommand.HeadlessPlayer).Length > 0;

        static void Launch(TrainerCommand.Mode mode)
        {
            if (!File.Exists(Exe)) { Fail($"no trainer at {Exe}: set the venv in the Fighter Brains window (TRAINING-CONCEPT M28)"); return; }
            // Two trainers on one run would write over each other's checkpoints.
            if (HeadlessRunning) { Fail($"headless training is running ({TrainerCommand.HeadlessPlayer} players): it trains the same brain. Stop it first (tools/headless-training/train_headless.ps1 -Action stop), then Retry"); return; }
            string root = ProjectRoot;
            string results = Path.Combine(root, TrainerCommand.ResultsDir).Replace('\\', '/');
            string run = TrainerCommand.RunOf(mode);
            string runDir = Path.Combine(results, run);
            TrainerCommand.History history = TrainerCommand.HistoryOf(runDir);
            if (history == TrainerCommand.History.Unusable) { SetAside(runDir, "unusable"); history = TrainerCommand.History.None; }
            bool resume = history == TrainerCommand.History.Resumable;

            string logs = Path.Combine(root, TrainerCommand.LogsDir);
            Directory.CreateDirectory(logs);
            string log = Path.Combine(logs, $"{run}-{DateTime.Now:yyyyMMdd-HHmmss}.log").Replace('\\', '/');
            string args = TrainerCommand.Arguments(TrainerCommand.ConfigOf(mode), run, results, resume);
            var start = new ProcessStartInfo("cmd.exe", TrainerCommand.CmdLine(Exe, args, log))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = root,
            };
            // Unbuffered, so "Listening" reaches the log when it is said; UTF-8, so the banner does not stop it on a cp1252 console.
            start.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            try
            {
                _process = Process.Start(start);
            }
            catch (Exception e)
            {
                Fail($"could not start the trainer: {e.Message}");
                return;
            }
            SessionState.SetInt(PidKey, _process.Id);
            SessionState.SetInt(ModeKey, (int)mode);
            SessionState.SetString(LogKey, log);
            SessionState.SetBool(ResumedKey, resume);
            Set(State.Starting, "");
            Debug.Log($"[RH Aviator] auto-train: starting the trainer: {run}, {(resume ? "resuming its brain" : "a new brain")}, {TrainerCommand.ConfigOf(mode)}. Log: {log}");
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                if (!On) return;
                TrainerCommand.Mode needed = Needed(out int blue, out int red);
                if (needed == TrainerCommand.Mode.None) return;
                State now = Now;
                if (now == State.Listening && RunningMode == needed)
                {
                    RecordSession(needed);
                    Set(State.Training, "");
                    return;
                }
                if (now == State.Failed)
                {
                    Debug.LogWarning($"[RH Aviator] auto-train: the trainer failed ({Why.Split('\n')[0]}); Play goes on without it, the learning jets fly the latest brain. Retry in the Fighter Brains window.");
                    return;
                }
                // Not ready yet (starting, saving the last session, or set up for other brains): hold Play, press it when ready.
                EditorApplication.isPlaying = false;
                SessionState.SetBool(EnterKey, true);
                string message = "Auto-train: Play follows when the trainer is ready (about 20 s).";
                Debug.Log($"[RH Aviator] {message} Blue {blue}, red {red} learning jets.");
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.ShowNotification(new GUIContent(message), 4.0);
            }
            else if (change == PlayModeStateChange.EnteredEditMode && Now == State.Training) PlayEnded();
        }

        /// <summary>
        /// Play has ended: a trainer that was connected ends its run (no restarts, <see cref="TrainerCommand.Arguments"/>),
        /// saving the brain, and stops by itself; one that never was still waits.
        /// </summary>
        static void PlayEnded()
        {
            if (ReadLog().Contains(TrainerCommand.ConnectedMarker))
            {
                Set(State.Saving, "");
                Debug.Log("[RH Aviator] auto-train: Play ended; the trainer is saving this session's brain.");
            }
            else
            {
                Set(State.Listening, "");
                Debug.LogWarning("[RH Aviator] auto-train: Play ended without connecting to the trainer; it is still waiting. Nothing was trained.");
            }
        }

        /// <summary>What this session trains with, kept until its brain is copied (M19: reward, scenario, settings).</summary>
        static void RecordSession(TrainerCommand.Mode mode)
        {
            SessionState.SetString(SessionKey, JsonUtility.ToJson(SessionInfo(mode, Scenario())));
            SessionState.SetString(SessionStartKey, DateTime.UtcNow.Ticks.ToString());
        }

        /// <summary>A brain's record before its steps are known: the run, config, senses, scenario, reward and settings.</summary>
        static BrainInfo SessionInfo(TrainerCommand.Mode mode, string scenario)
        {
            var training = Object.FindAnyObjectByType<FighterTraining>();
            var so = training != null ? new SerializedObject(training) : null;
            var reward = so?.FindProperty("rewardWeights").objectReferenceValue as RewardWeightsSO;
            var pilot = so?.FindProperty("learnedPilot").objectReferenceValue as LearnedPilotSO;
            string config = TrainerCommand.ConfigOf(mode);
            string configPath = Path.Combine(ProjectRoot, config);
            return new BrainInfo
            {
                run = TrainerCommand.RunOf(mode),
                config = config,
                sensesVersion = LearnedSenses.Version,
                sensesSize = LearnedSenses.Size,
                scenario = scenario,
                rewardWeights = reward != null ? EditorJsonUtility.ToJson(reward) : "",
                learnedPilot = pilot != null ? EditorJsonUtility.ToJson(pilot) : "",
                configText = File.Exists(configPath) ? File.ReadAllText(configPath) : "",
            };
        }

        /// <summary>
        /// Deep Water → RH Aviator → Take the Newest Headless Brain: the newest brain the headless run has written (its last
        /// checkpoint while it trains, its final brain once stopped) becomes the latest brain, so the Editor flies it.
        /// </summary>
        [MenuItem("Deep Water/RH Aviator/Take the Newest Headless Brain", priority = 161)]
        public static void TakeHeadlessBrain()
        {
            string run = TrainerCommand.SelfPlayRun;
            string runDir = Path.Combine(ProjectRoot, TrainerCommand.ResultsDir, run);
            string source = TrainerCommand.NewestBrain(runDir, out int steps);
            if (source == null) { Debug.LogWarning($"[RH Aviator] no brain in {runDir} yet: a headless run writes one at each checkpoint and when it stops."); return; }
            string scenario = Object.FindAnyObjectByType<FighterTraining>() != null
                ? "headless player of Dogfight Training as built; the open scene's squadrons: " + Scenario()
                : "headless player of Dogfight Training as built (squadrons not recorded: the training scene is not open)";
            BrainInfo info = SessionInfo(TrainerCommand.Mode.SelfPlay, scenario);
            Install(source, info, steps, File.GetLastWriteTimeUtc(source));
        }

        /// <summary>"blue: 8 Learning; red: 4 Line, 4 Saved Ace-2M" from the squadrons as they are.</summary>
        static string Scenario()
        {
            var squadrons = Object.FindObjectsByType<TrainingSquadron>(FindObjectsInactive.Exclude).OrderBy(s => s.Side);
            return string.Join("; ", squadrons.Select(s => s.Side.ToString().ToLowerInvariant() + ": " + string.Join(", ",
                s.Jets.Where(j => j != null).Select(j =>
                {
                    FighterBrain b = s.BrainOf(j, out var saved);
                    return b == FighterBrain.Saved && saved != null ? $"Saved {saved.name}" : b.ToString();
                }).GroupBy(n => n).Select(g => $"{g.Count()} {g.Key}"))));
        }

        /// <summary>The session's brain to <see cref="BrainLibrary.LatestPath"/>, with its record beside it.</summary>
        static void CopyLatest()
        {
            var info = JsonUtility.FromJson<BrainInfo>(SessionState.GetString(SessionKey, "{}")) ?? new BrainInfo();
            string run = string.IsNullOrEmpty(info.run) ? TrainerCommand.RunOf(RunningMode) : info.run;
            string runDir = Path.Combine(ProjectRoot, TrainerCommand.ResultsDir, run);
            string source = Path.Combine(runDir, "Fighter.onnx");
            long.TryParse(SessionState.GetString(SessionStartKey, "0"), out long started);
            if (!File.Exists(source) || File.GetLastWriteTimeUtc(source).Ticks < started)
            {
                Debug.LogWarning($"[RH Aviator] auto-train: the session left no new brain in {run} (too short to train a step?); the latest brain is unchanged.");
                return;
            }
            string status = Path.Combine(runDir, "run_logs", "training_status.json");
            info.run = run;
            Install(source, info, TrainerCommand.FinalSteps(File.Exists(status) ? File.ReadAllText(status) : ""), DateTime.UtcNow, "auto-train: session saved");
        }

        /// <summary>A brain to <see cref="BrainLibrary.LatestPath"/>, with its record beside it.</summary>
        static void Install(string source, BrainInfo info, int steps, DateTime trainedUtc, string what = "headless brain taken")
        {
            string target = Path.Combine(ProjectRoot, BrainLibrary.LatestPath);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, true);
            info.steps = steps;
            info.trainedUtc = trainedUtc.ToString("u");
            string run = info.run;
            string record = Path.ChangeExtension(BrainLibrary.LatestPath, ".json");
            File.WriteAllText(Path.Combine(ProjectRoot, record), JsonUtility.ToJson(info, true));
            AssetDatabase.ImportAsset(BrainLibrary.LatestPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(record, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[RH Aviator] {what}. The brain at {info.steps:N0} steps ({run}) is now {BrainLibrary.LatestPath}; learning jets fly it when no trainer runs.");
        }

        /// <summary>The latest brain's record, or null before the first session.</summary>
        public static BrainInfo LatestInfo()
        {
            string path = Path.Combine(ProjectRoot, Path.ChangeExtension(BrainLibrary.LatestPath, ".json"));
            return File.Exists(path) ? JsonUtility.FromJson<BrainInfo>(File.ReadAllText(path)) : null;
        }

        /// <summary>
        /// "Keep this brain" (M19, M34): the latest brain and its record copied to <see cref="BrainLibrary.KeptFolder"/> under
        /// a name, never over another kept brain. Returns the kept brain's asset path, or null with the reason.
        /// </summary>
        public static string KeepLatest(string name, out string why)
        {
            name = TrainerCommand.SafeName(name);
            string latest = Path.Combine(ProjectRoot, BrainLibrary.LatestPath);
            if (!File.Exists(latest)) { why = "there is no latest brain yet: train a session first"; return null; }
            if (name.Length == 0) { why = "the kept brain needs a name"; return null; }
            string asset = $"{BrainLibrary.KeptFolder}/{name}.onnx";
            string file = Path.Combine(ProjectRoot, asset);
            if (File.Exists(file)) { why = $"{asset} is already kept: pick another name"; return null; }
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.Copy(latest, file);
            BrainInfo info = LatestInfo() ?? new BrainInfo { sensesVersion = LearnedSenses.Version, sensesSize = LearnedSenses.Size };
            info.keptUtc = DateTime.UtcNow.ToString("u");
            File.WriteAllText(Path.ChangeExtension(file, ".json"), JsonUtility.ToJson(info, true));
            AssetDatabase.ImportAsset(asset);
            AssetDatabase.ImportAsset(Path.ChangeExtension(asset, ".json"));
            Debug.Log($"[RH Aviator] kept the latest brain ({info.steps:N0} steps, {info.run}) as {asset}, with its record; commit it to keep it (M19).");
            why = null;
            return asset;
        }
    }
}
