using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.InferenceEngine;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.Aviator.Core;
using Object = UnityEngine.Object;

namespace DeepWater.Aviator.Learning.Editor
{
    /// <summary>
    /// Deep Water → RH Aviator → Fighter Brains (A17, A18; M31–M34): every training jet in the open scene with the brain
    /// that flies it, set per squadron or per jet, and the auto-trainer: its switch, what it is doing, the latest brain and
    /// "Keep this brain". Brains change in edit mode, or in play at once (A23): a squadron's or one jet's, from the list or
    /// a kept brain's file (Load…), for that session, or kept in the scene when Play stops.
    /// </summary>
    public sealed class FighterBrainsWindow : EditorWindow
    {
        static readonly string[] JetChoices = { "Squadron's", "Learning", "Rookie", "Line", "Ace", "Saved", "Scripted" };
        static readonly FighterBrain[] JetBrains = { FighterBrain.Learning, FighterBrain.Learning, FighterBrain.Rookie, FighterBrain.Line, FighterBrain.Ace, FighterBrain.Saved, FighterBrain.Scripted };

        // Loading a brain to check its fit costs a few milliseconds: once per file version, not every repaint.
        static readonly Dictionary<string, string> FitCache = new Dictionary<string, string>();
        Vector2 _scroll;
        double _headlessLook;
        bool _headless;

        [MenuItem("Deep Water/RH Aviator/Fighter Brains", priority = 159)]
        public static void Open() => GetWindow<FighterBrainsWindow>("Fighter Brains").minSize = new Vector2(420f, 300f);

        void OnInspectorUpdate() => Repaint();   // the trainer's state changes on its own

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            TrainerSection();
            EditorGUILayout.Space(10f);
            BrainsSection();
            EditorGUILayout.EndScrollView();
        }

        void TrainerSection()
        {
            EditorGUILayout.LabelField("Auto-train", EditorStyles.boldLabel);
            bool on = EditorGUILayout.ToggleLeft("Train whenever Play is on (Dogfight Training)", AutoTrainer.On);
            if (on != AutoTrainer.On) AutoTrainer.On = on;

            TrainerCommand.Mode needed = AutoTrainer.Needed(out int blue, out int red);
            string what = needed == TrainerCommand.Mode.None ? "no learning jet: nothing to train"
                        : needed == TrainerCommand.Mode.SelfPlay ? $"self-play: blue {blue} and red {red} learning jets"
                        : $"one side learns (blue {blue}, red {red}) against the rest, no self-play";
            EditorGUILayout.LabelField("Brains", what);

            AutoTrainer.State state = AutoTrainer.Now;
            string run = TrainerCommand.RunOf(AutoTrainer.RunningMode);
            switch (state)
            {
                case AutoTrainer.State.Off:
                    EditorGUILayout.HelpBox(AutoTrainer.On ? "No trainer running yet; it starts with Dogfight Training open and a learning jet." : "Off: Play flies the latest brain, no training.", MessageType.None);
                    break;
                case AutoTrainer.State.Starting:
                    EditorGUILayout.HelpBox($"Starting the trainer ({run}, {(AutoTrainer.Resumed ? "resuming" : "a new brain")})… Play waits for it.", MessageType.Info);
                    break;
                case AutoTrainer.State.Listening:
                    EditorGUILayout.HelpBox($"Ready: press Play to train ({run}, {(AutoTrainer.Resumed ? "resuming" : "a new brain")}). In play, T switches the clock between ×20 and ×1.", MessageType.Info);
                    break;
                case AutoTrainer.State.Training:
                    EditorGUILayout.HelpBox($"Training ({run}). Stop Play to save the brain.", MessageType.Info);
                    break;
                case AutoTrainer.State.Saving:
                    EditorGUILayout.HelpBox("Saving this session's brain…", MessageType.Info);
                    break;
                case AutoTrainer.State.Failed:
                    EditorGUILayout.HelpBox("The trainer failed: " + AutoTrainer.Why, MessageType.Error);
                    if (GUILayout.Button("Retry")) AutoTrainer.Retry();
                    break;
            }

            // Looking for the players costs a process listing: every two seconds, not every repaint.
            if (EditorApplication.timeSinceStartup > _headlessLook)
            {
                _headlessLook = EditorApplication.timeSinceStartup + 2.0;
                _headless = AutoTrainer.HeadlessRunning;
            }
            if (_headless)
                EditorGUILayout.HelpBox("Headless training is running (tools/headless-training/train_headless.ps1): it trains the same brain, " +
                                        "so auto-train waits. Take its newest checkpoint to fly it here.", MessageType.Info);
            if (GUILayout.Button("Take the newest headless brain")) AutoTrainer.TakeHeadlessBrain();

            BrainInfo latest = AutoTrainer.LatestInfo();
            ModelAsset latestAsset = BrainLibrary.Latest();
            if (latestAsset == null) EditorGUILayout.LabelField("Latest brain", "none yet: learning jets fly the scripted stand-in");
            else
            {
                string fit = Fit(latestAsset);
                string steps = latest != null && latest.steps >= 0 ? $"{latest.steps:N0} steps" : "steps unknown";
                EditorGUILayout.LabelField("Latest brain", $"{steps} · {latest?.run} · {latest?.trainedUtc}" + (fit == null ? "" : " · DOES NOT FIT"));
                if (fit != null) EditorGUILayout.HelpBox($"The latest brain does not fit: {fit}. Start a fresh run.", MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(latestAsset == null))
                    if (GUILayout.Button("Keep this brain…")) Keep(latest);
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(AutoTrainer.LogPath) || !File.Exists(AutoTrainer.LogPath)))
                    if (GUILayout.Button("Open log")) EditorUtility.OpenWithDefaultApp(AutoTrainer.LogPath);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("Start a fresh run") &&
                        EditorUtility.DisplayDialog("Start a fresh run?",
                            "The current run's results are renamed aside (…pre-fresh-backup-…), not deleted, and the next session trains a new brain from scratch. " +
                            "The latest brain stays until that session ends.", "Fresh run", "Cancel"))
                        AutoTrainer.FreshRun();
                    if (GUILayout.Button("Stop trainer")) AutoTrainer.Stop("stopped from the Fighter Brains window");
                }
            }
            string venv = EditorGUILayout.DelayedTextField(new GUIContent("Python venv", "mlagents 1.1.0 on Python 3.10 (TRAINING-CONCEPT M28)"), AutoTrainer.Venv);
            if (venv != AutoTrainer.Venv) AutoTrainer.Venv = venv;
        }

        static void Keep(BrainInfo latest)
        {
            string folder = Path.Combine(AutoTrainer.ProjectRoot, BrainLibrary.KeptFolder);
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            string suggested = latest != null && latest.steps >= 0 ? $"Fighter-{latest.steps / 1000}k" : "Fighter";
            string path = EditorUtility.SaveFilePanelInProject("Keep this brain", suggested, "onnx",
                "A name for the kept brain. It is saved with its record (steps, reward, settings) in Policies/Kept; commit it to keep it.", BrainLibrary.KeptFolder);
            if (string.IsNullOrEmpty(path)) return;
            if (AutoTrainer.KeepLatest(Path.GetFileNameWithoutExtension(path), out string why) == null)
                EditorUtility.DisplayDialog("Not kept", why, "OK");
        }

        void BrainsSection()
        {
            EditorGUILayout.LabelField("Brains", EditorStyles.boldLabel);
            TrainingSquadron[] squadrons = Object.FindObjectsByType<TrainingSquadron>(FindObjectsInactive.Exclude).OrderBy(s => s.Side).ThenBy(s => s.name).ToArray();
            if (squadrons.Length == 0)
            {
                EditorGUILayout.HelpBox("No training squadrons in the open scene. Open Assets/_SCENES/Dogfight Training.unity.", MessageType.None);
                return;
            }
            bool playing = EditorApplication.isPlaying;
            if (playing)
            {
                EditorGUILayout.HelpBox("In play a change flies at once and lasts this session. A jet flying a kept brain flies it and does not learn. " +
                                        "Tick below to keep this session's choices in the scene when Play stops.", MessageType.Info);
                PlayChoices.Keep = EditorGUILayout.ToggleLeft($"Keep these choices after Play ({PlayChoices.Count} changed)", PlayChoices.Keep);
                if (_refused != null) EditorGUILayout.HelpBox(_refused, MessageType.Warning);
            }
            using (new EditorGUI.DisabledScope(!playing && EditorApplication.isPlayingOrWillChangePlaymode))
                foreach (TrainingSquadron squadron in squadrons) Squadron(squadron, playing);
        }

        // Why the last change in play was refused, shown until the next one.
        static string _refused;

        static void Squadron(TrainingSquadron squadron, bool playing)
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{squadron.name} · {squadron.Side.ToString().ToUpperInvariant()}", EditorStyles.boldLabel, GUILayout.Width(180f));
                var brain = (FighterBrain)EditorGUILayout.EnumPopup(squadron.Brain);
                ModelAsset saved = squadron.SavedBrain;
                if (brain == FighterBrain.Saved) saved = (ModelAsset)EditorGUILayout.ObjectField(saved, typeof(ModelAsset), false);
                if (GUILayout.Button(new GUIContent("Load…", "Fly a kept brain (an .onnx in Policies/Kept) on every jet of the squadron without its own"), GUILayout.Width(60f))
                    && PickBrain() is ModelAsset loaded)
                {
                    brain = FighterBrain.Saved;
                    saved = loaded;
                }
                if (brain != squadron.Brain || saved != squadron.SavedBrain) SetSquadron(squadron, brain, saved, playing);
            }
            if (squadron.Brain == FighterBrain.Saved) SavedNote(squadron.SavedBrain);

            EditorGUI.indentLevel++;
            foreach (AircraftView jet in squadron.Jets)
            {
                if (jet == null) continue;
                JetBrain own = jet.GetComponent<JetBrain>();
                bool hasOwn = own != null && own.OwnBrain;
                int choice = hasOwn ? Array.IndexOf(JetBrains, own.Brain, 1) : 0;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{jet.name}  #{jet.AircraftId}", GUILayout.Width(180f));
                    int picked = EditorGUILayout.Popup(choice, JetChoices);
                    ModelAsset saved = hasOwn ? own.SavedBrain : null;
                    if (picked == 5) saved = (ModelAsset)EditorGUILayout.ObjectField(saved, typeof(ModelAsset), false);
                    else EditorGUILayout.LabelField(picked == 0 ? $"→ {squadron.BrainOf(jet, out _)}" : "own brain", EditorStyles.miniLabel);
                    if (GUILayout.Button(new GUIContent("Load…", $"Fly a kept brain (an .onnx in Policies/Kept) on {jet.name} alone"), GUILayout.Width(60f))
                        && PickBrain() is ModelAsset loaded)
                    {
                        picked = 5;
                        saved = loaded;
                    }
                    if (picked != choice || (picked == 5 && saved != own?.SavedBrain)) SetJet(squadron, jet, picked, saved, playing);
                }
                if (hasOwn && own.Brain == FighterBrain.Saved) SavedNote(own.SavedBrain);
            }
            EditorGUI.indentLevel--;
        }

        /// <summary>A kept brain from a file picker in Policies/Kept, or null (cancelled, or not a brain inside the project).</summary>
        static ModelAsset PickBrain()
        {
            string folder = Path.Combine(AutoTrainer.ProjectRoot, BrainLibrary.KeptFolder);
            string file = EditorUtility.OpenFilePanel("Load a kept brain", Directory.Exists(folder) ? folder : AutoTrainer.ProjectRoot, "onnx");
            if (string.IsNullOrEmpty(file)) return null;
            string full = Path.GetFullPath(file).Replace('\\', '/'), root = Path.GetFullPath(AutoTrainer.ProjectRoot).Replace('\\', '/').TrimEnd('/') + "/";
            var brain = full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? AssetDatabase.LoadAssetAtPath<ModelAsset>(full.Substring(root.Length)) : null;
            if (brain == null)
                EditorUtility.DisplayDialog("Not a brain here", $"{file} is not a brain inside the project. Copy it into {BrainLibrary.KeptFolder} first.", "OK");
            return brain;
        }

        static void SetSquadron(TrainingSquadron squadron, FighterBrain brain, ModelAsset saved, bool playing)
        {
            if (playing)
            {
                _refused = squadron.SetBrainLive(brain, saved);
                if (_refused == null) PlayChoices.Squadron(squadron, brain, saved);
                else _refused = $"{squadron.name} not changed: {_refused}.";
                return;
            }
            Undo.RecordObject(squadron, "Squadron brain");
            squadron.SetBrain(brain, saved);
            Changed(squadron);
        }

        static void SetJet(TrainingSquadron squadron, AircraftView jet, int picked, ModelAsset saved, bool playing)
        {
            bool own = picked != 0;
            FighterBrain brain = JetBrains[picked];
            if (picked != 5) saved = null;
            if (playing)
            {
                _refused = squadron.SetJetBrainLive(jet, own, brain, saved);
                if (_refused == null) PlayChoices.Jet(squadron, jet, own, brain, saved);
                else _refused = $"{jet.name} not changed: {_refused}.";
                return;
            }
            JetBrain mine = jet.GetComponent<JetBrain>();
            if (mine == null) mine = Undo.AddComponent<JetBrain>(jet.gameObject);
            Undo.RecordObject(mine, "Jet brain");
            mine.Set(own, brain, saved);
            EditorUtility.SetDirty(mine);
            squadron.Apply();
            Changed(squadron);
        }

        /// <summary>
        /// The brain choices made in play (A23), put into the scene when Play stops if "Keep these choices" is ticked, as if
        /// made in edit mode (undoable, the scene marked changed, not saved). Squadrons are found by name, jets by aircraft id.
        /// </summary>
        static class PlayChoices
        {
            struct Choice { public string Squadron; public int JetId; public bool Own; public FighterBrain Brain; public string SavedPath; }

            static readonly List<Choice> Made = new List<Choice>();
            public static bool Keep;
            public static int Count => Made.Count;

            [InitializeOnLoadMethod]
            static void Hook() => EditorApplication.playModeStateChanged += Changed;

            public static void Squadron(TrainingSquadron s, FighterBrain brain, ModelAsset saved) =>
                Made.Add(new Choice { Squadron = s.name, JetId = 0, Brain = brain, SavedPath = AssetDatabase.GetAssetPath(saved) });

            public static void Jet(TrainingSquadron s, AircraftView jet, bool own, FighterBrain brain, ModelAsset saved) =>
                Made.Add(new Choice { Squadron = s.name, JetId = jet.AircraftId, Own = own, Brain = brain, SavedPath = AssetDatabase.GetAssetPath(saved) });

            static void Changed(PlayModeStateChange change)
            {
                if (change == PlayModeStateChange.EnteredPlayMode) { Made.Clear(); Keep = false; _refused = null; return; }
                if (change != PlayModeStateChange.EnteredEditMode) return;
                if (Keep && Made.Count > 0) Apply();
                Made.Clear();
                Keep = false;
                _refused = null;
            }

            static void Apply()
            {
                TrainingSquadron[] squadrons = Object.FindObjectsByType<TrainingSquadron>(FindObjectsInactive.Exclude);
                int kept = 0;
                foreach (Choice c in Made)   // in the order made: a later choice wins
                {
                    TrainingSquadron s = squadrons.FirstOrDefault(q => q.name == c.Squadron);
                    if (s == null) { Debug.LogWarning($"[RH Aviator] kept brain choice: no squadron '{c.Squadron}' in the open scene; skipped."); continue; }
                    var saved = string.IsNullOrEmpty(c.SavedPath) ? null : AssetDatabase.LoadAssetAtPath<ModelAsset>(c.SavedPath);
                    if (c.JetId == 0) SetSquadron(s, c.Brain, saved, false);
                    else
                    {
                        AircraftView jet = s.Jets.FirstOrDefault(j => j != null && j.AircraftId == c.JetId);
                        if (jet == null) { Debug.LogWarning($"[RH Aviator] kept brain choice: no jet #{c.JetId} in {c.Squadron}; skipped."); continue; }
                        SetJet(s, jet, c.Own ? Array.IndexOf(JetBrains, c.Brain, 1) : 0, saved, false);
                    }
                    kept++;
                }
                Debug.Log($"[RH Aviator] kept {kept} of {Made.Count} brain choices made in play; the scene is changed, not saved.");
            }
        }

        static void SavedNote(ModelAsset saved)
        {
            if (saved == null) EditorGUILayout.HelpBox("No brain chosen: it flies the scripted stand-in. Pick a kept brain (Policies/Kept).", MessageType.Warning);
            else if (Fit(saved) is string why) EditorGUILayout.HelpBox($"{saved.name} does not fit: {why}. It flies the scripted stand-in.", MessageType.Warning);
        }

        static void Changed(TrainingSquadron squadron)
        {
            EditorUtility.SetDirty(squadron);
            EditorSceneManager.MarkSceneDirty(squadron.gameObject.scene);
        }

        /// <summary>Null when the brain fits the senses, else why not; cached per file and its write time.</summary>
        static string Fit(ModelAsset brain)
        {
            string path = AssetDatabase.GetAssetPath(brain);
            string key = path + "@" + (File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0);
            if (!FitCache.TryGetValue(key, out string why))
            {
                BrainLibrary.Fits(brain, out why);
                FitCache[key] = why;
            }
            return why;
        }
    }
}
