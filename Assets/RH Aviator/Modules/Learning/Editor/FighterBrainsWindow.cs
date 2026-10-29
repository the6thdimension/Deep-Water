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
    /// "Keep this brain". Brains change in edit mode; in play it shows them.
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
            if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("Brains change in edit mode; this shows them.", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                foreach (TrainingSquadron squadron in squadrons) Squadron(squadron);
        }

        static void Squadron(TrainingSquadron squadron)
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{squadron.name} · {squadron.Side.ToString().ToUpperInvariant()}", EditorStyles.boldLabel, GUILayout.Width(180f));
                var brain = (FighterBrain)EditorGUILayout.EnumPopup(squadron.Brain);
                ModelAsset saved = squadron.SavedBrain;
                if (brain == FighterBrain.Saved) saved = (ModelAsset)EditorGUILayout.ObjectField(saved, typeof(ModelAsset), false);
                if (brain != squadron.Brain || saved != squadron.SavedBrain)
                {
                    Undo.RecordObject(squadron, "Squadron brain");
                    squadron.SetBrain(brain, saved);
                    Changed(squadron);
                }
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
                    if (picked != choice || (picked == 5 && saved != own?.SavedBrain))
                    {
                        if (own == null) own = Undo.AddComponent<JetBrain>(jet.gameObject);
                        Undo.RecordObject(own, "Jet brain");
                        own.Set(picked != 0, JetBrains[picked], picked == 5 ? saved : null);
                        EditorUtility.SetDirty(own);
                        squadron.Apply();
                        Changed(squadron);
                    }
                }
                if (hasOwn && own.Brain == FighterBrain.Saved) SavedNote(own.SavedBrain);
            }
            EditorGUI.indentLevel--;
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
