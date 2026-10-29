using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.Modules.World;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Learning.Editor;
using DeepWater.Aviator.Modules;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Learning.Tests
{
    /// <summary>
    /// Brain choice and the auto-trainer (A17, A18; TRAINING-CONCEPT M31–M34): what each brain puts on a jet, a jet's own
    /// brain over its squadron's, whether a brain fits the senses, and the trainer's command line, run history and steps.
    /// </summary>
    public sealed class FighterBrainTests
    {
        [Test]
        public void TheBrainsKeepTheOldSquadronPilotNumbersSoSavedScenesReadTheSame()
        {
            Assert.AreEqual(0, (int)FighterBrain.Learning);
            Assert.AreEqual(1, (int)FighterBrain.Rookie);
            Assert.AreEqual(2, (int)FighterBrain.Line);
            Assert.AreEqual(3, (int)FighterBrain.Ace);
        }

        [Test]
        public void EachBrainSaysWhoFliesAndHowMLAgentsPicksThePolicy()
        {
            BrainPlan learning = BrainPlan.For(FighterBrain.Learning);
            Assert.IsTrue(learning.Agent);
            Assert.AreEqual(BehaviorType.Default, learning.Behaviour, "trainer if connected, else a model, else the stand-in");
            BrainPlan saved = BrainPlan.For(FighterBrain.Saved);
            Assert.IsTrue(saved.Agent);
            Assert.AreEqual(BehaviorType.InferenceOnly, saved.Behaviour, "a saved brain flies, never trains");
            BrainPlan scripted = BrainPlan.For(FighterBrain.Scripted);
            Assert.IsTrue(scripted.Agent);
            Assert.AreEqual(BehaviorType.HeuristicOnly, scripted.Behaviour);
            foreach (FighterBrain rule in new[] { FighterBrain.Rookie, FighterBrain.Line, FighterBrain.Ace })
            {
                Assert.IsFalse(BrainPlan.For(rule).Agent, $"{rule}: the rule pilot flies");
                Assert.AreEqual(rule, BrainPlan.For(rule).RuleSkill);
            }
        }

        [Test]
        public void AJetsOwnBrainOverridesItsSquadronsAndTheSquadronSetsTheComponents()
        {
            var made = new System.Collections.Generic.List<Object>();
            try
            {
                PilotProfileSO rookie = Profile("Rookie", made), line = Profile("Line", made), ace = Profile("Ace", made);
                AircraftView one = Jet("Red 1", made), two = Jet("Red 2", made);
                var point = new GameObject("Red Spawn");
                made.Add(point);
                point.AddComponent<SpawnPoint>().Configure("Red", new[] { one, two }, 300f, Color.red);
                var squadron = point.AddComponent<TrainingSquadron>();
                squadron.Configure(Side.Red, FighterBrain.Learning, rookie, line, ace);

                foreach (AircraftView jet in new[] { one, two })
                {
                    Assert.IsTrue(jet.GetComponent<FighterAgent>().enabled);
                    Assert.IsFalse(jet.GetComponent<AviatorPilot>().enabled);
                    Assert.AreEqual(BehaviorType.Default, jet.GetComponent<BehaviorParameters>().BehaviorType);
                    Assert.AreEqual(1, jet.GetComponent<BehaviorParameters>().TeamId, "red is team 1 for self-play");
                    Assert.IsNull(jet.GetComponent<BehaviorParameters>().Model, "the latest brain is set only in play, so the scene never names a git-ignored file");
                }

                two.gameObject.AddComponent<JetBrain>().Set(true, FighterBrain.Scripted, null);
                squadron.SetBrain(FighterBrain.Line, null);
                Assert.AreEqual(FighterBrain.Line, squadron.BrainOf(one, out _));
                Assert.AreEqual(FighterBrain.Scripted, squadron.BrainOf(two, out _));
                Assert.IsFalse(one.GetComponent<FighterAgent>().enabled);
                Assert.IsTrue(one.GetComponent<AviatorPilot>().enabled);
                Assert.AreSame(line, new SerializedObject(one.GetComponent<AviatorPilot>()).FindProperty("profile").objectReferenceValue);
                Assert.IsTrue(two.GetComponent<FighterAgent>().enabled);
                Assert.IsFalse(two.GetComponent<AviatorPilot>().enabled);
                Assert.AreEqual(BehaviorType.HeuristicOnly, two.GetComponent<BehaviorParameters>().BehaviorType);

                // A saved brain with nothing chosen would throw when the agent starts: it flies the stand-in instead.
                two.GetComponent<JetBrain>().Set(true, FighterBrain.Saved, null);
                squadron.Apply();
                Assert.AreEqual(BehaviorType.HeuristicOnly, two.GetComponent<BehaviorParameters>().BehaviorType);

                two.GetComponent<JetBrain>().Set(false, FighterBrain.Saved, null);
                Assert.AreEqual(FighterBrain.Line, squadron.BrainOf(two, out _), "own brain off: the squadron's again");
                Assert.AreSame(squadron, TrainingSquadron.Of(two));
            }
            finally
            {
                foreach (Object o in made) if (o != null) Object.DestroyImmediate(o);
            }
        }

        [Test]
        public void ABrainFitsOnlyWhenItWasTrainedOnTheSensesAsTheyAreNow()
        {
            Assert.IsTrue(BrainLibrary.Fits(ModelWith(BrainLibrary.ObservationInput, LearnedSenses.Size), out string why), why);
            Assert.IsFalse(BrainLibrary.Fits(ModelWith(BrainLibrary.ObservationInput, 68), out why), "version 1's 68 senses");
            StringAssert.Contains("68", why);
            StringAssert.Contains(LearnedSenses.Size.ToString(), why);
            Assert.IsFalse(BrainLibrary.Fits(ModelWith("input", LearnedSenses.Size), out why));
            Assert.IsFalse(BrainLibrary.Fits((ModelAsset)null, out _));
        }

        [Test]
        public void SelfPlayNeedsLearningJetsOnBothSides()
        {
            Assert.AreEqual(TrainerCommand.Mode.SelfPlay, TrainerCommand.ModeFor(8, 8));
            Assert.AreEqual(TrainerCommand.Mode.SelfPlay, TrainerCommand.ModeFor(1, 1));
            Assert.AreEqual(TrainerCommand.Mode.VsRest, TrainerCommand.ModeFor(8, 0));
            Assert.AreEqual(TrainerCommand.Mode.VsRest, TrainerCommand.ModeFor(0, 3));
            Assert.AreEqual(TrainerCommand.Mode.None, TrainerCommand.ModeFor(0, 0));
            Assert.AreEqual(TrainerCommand.SelfPlayConfig, TrainerCommand.ConfigOf(TrainerCommand.Mode.SelfPlay));
            Assert.AreEqual(TrainerCommand.VsRestConfig, TrainerCommand.ConfigOf(TrainerCommand.Mode.VsRest));
            Assert.AreNotEqual(TrainerCommand.RunOf(TrainerCommand.Mode.SelfPlay), TrainerCommand.RunOf(TrainerCommand.Mode.VsRest), "one rolling run per config");
        }

        [Test]
        public void TheCommandLineResumesTheRollingRunAndSendsBothStreamsToTheLog()
        {
            string args = TrainerCommand.Arguments("Training/config/fighter_selfplay.yaml", "auto-selfplay", "F:/P/Training/results", resume: true);
            Assert.AreEqual("\"Training/config/fighter_selfplay.yaml\" --run-id=auto-selfplay --results-dir=\"F:/P/Training/results\" --timeout-wait=604800 --max-lifetime-restarts=0 --resume", args);
            StringAssert.DoesNotContain("--resume", TrainerCommand.Arguments("c.yaml", "r", "d", resume: false));
            string cmd = TrainerCommand.CmdLine("C:/venv/Scripts/mlagents-learn.exe", args, "F:/P/log.txt");
            StringAssert.StartsWith("/d /s /c \"\"C:/venv/Scripts/mlagents-learn.exe\" ", cmd);
            StringAssert.EndsWith(" > \"F:/P/log.txt\" 2>&1\"", cmd);
        }

        [Test]
        public void ARunResumesOnlyFromACheckpoint()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dw-run-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                Assert.AreEqual(TrainerCommand.History.None, TrainerCommand.HistoryOf(dir));
                Directory.CreateDirectory(Path.Combine(dir, "Fighter"));
                Assert.AreEqual(TrainerCommand.History.Unusable, TrainerCommand.HistoryOf(dir), "a run that never saved cannot resume");
                File.WriteAllText(Path.Combine(dir, "Fighter", "checkpoint.pt"), "");
                Assert.AreEqual(TrainerCommand.History.Resumable, TrainerCommand.HistoryOf(dir));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);   // the test's own temp folder
            }
        }

        [Test]
        public void TheNewestBrainIsTheLastWrittenFinalOrCheckpoint()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dw-brains-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                Assert.IsNull(TrainerCommand.NewestBrain(dir, out int none));
                Assert.AreEqual(-1, none);
                Directory.CreateDirectory(Path.Combine(dir, "Fighter"));
                Directory.CreateDirectory(Path.Combine(dir, "run_logs"));
                string final = Path.Combine(dir, "Fighter.onnx");
                File.WriteAllText(final, "");
                File.WriteAllText(Path.Combine(dir, "run_logs", "training_status.json"), "{\"Fighter\": {\"final_checkpoint\": {\"steps\": 101593}}}");
                File.SetLastWriteTimeUtc(final, new System.DateTime(2026, 10, 29, 23, 0, 0));
                string older = Path.Combine(dir, "Fighter", "Fighter-97428.onnx");
                File.WriteAllText(older, "");
                File.SetLastWriteTimeUtc(older, new System.DateTime(2026, 10, 29, 22, 0, 0));
                Assert.AreEqual(final, TrainerCommand.NewestBrain(dir, out int steps), "a stopped run: its final brain");
                Assert.AreEqual(101593, steps);
                string newer = Path.Combine(dir, "Fighter", "Fighter-600000.onnx");
                File.WriteAllText(newer, "");
                File.SetLastWriteTimeUtc(newer, new System.DateTime(2026, 10, 30, 1, 0, 0));
                Assert.AreEqual(newer, TrainerCommand.NewestBrain(dir, out steps), "a run still training: its last checkpoint");
                Assert.AreEqual(600000, steps);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);   // the test's own temp folder
            }
        }

        [Test]
        public void TheStepsComeFromTheFinalCheckpoint()
        {
            // The shape of the smoke run's run_logs/training_status.json (2026-10-29).
            const string status = "{\"Fighter\": {\"checkpoints\": [{\"steps\": 29940, \"file_path\": \"a.onnx\"}, {\"steps\": 30068}], " +
                                  "\"elo\": 1198.0, \"final_checkpoint\": {\"steps\": 30068, \"file_path\": \"Fighter.onnx\"}}}";
            Assert.AreEqual(30068, TrainerCommand.FinalSteps(status));
            Assert.AreEqual(-1, TrainerCommand.FinalSteps("{\"Fighter\": {\"checkpoints\": [{\"steps\": 5}]}}"));
            Assert.AreEqual(-1, TrainerCommand.FinalSteps(""));
        }

        [Test]
        public void TheLogTailAndKeptNamesAreTidy()
        {
            Assert.AreEqual("c\nd", TrainerCommand.Tail("a\r\nb\r\nc\r\nd\r\n\r\n", 2));
            Assert.AreEqual("Ace-v-Line-2M", TrainerCommand.SafeName(" Ace v Line 2M "));
            Assert.AreEqual("a-b", TrainerCommand.SafeName("a/b"));
            Assert.AreEqual("", TrainerCommand.SafeName("  "));
        }

        static Model ModelWith(string input, int size)
        {
            var model = new Model();
            model.inputs.Add(new Model.Input { name = input, index = 0, dataType = DataType.Float, shape = new DynamicTensorShape(-1, size) });
            return model;
        }

        static PilotProfileSO Profile(string name, System.Collections.Generic.List<Object> made)
        {
            var p = ScriptableObject.CreateInstance<PilotProfileSO>();
            p.name = name;
            made.Add(p);
            return p;
        }

        static AircraftView Jet(string name, System.Collections.Generic.List<Object> made)
        {
            var go = new GameObject(name);
            made.Add(go);
            var view = go.AddComponent<AircraftView>();
            go.AddComponent<AviatorPilot>();
            go.AddComponent<FighterAgent>();   // brings its Behavior Parameters
            return view;
        }
    }
}
