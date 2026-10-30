using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using DeepWater.AirWing.Editor;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.Modules.World;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Editor;
using DeepWater.Aviator.Modules;
using DeepWater.Aviator.ScriptableObjects;
using DeepWater.Sea.Core;

namespace DeepWater.Aviator.Learning.Editor
{
    /// <summary>
    /// Deep Water → RH Aviator → Build Dogfight Training Scene (A13–A15; TRAINING-CONCEPT M21–M28, SIX 2026-10-29).
    /// Writes <c>Assets/_SCENES/Dogfight Training.unity</c> in place: Dogfight's world (the same sea, land, runway and
    /// landmark, from <see cref="DogfightBuilder.BuildWorld"/>), Air Wing's runner with no player seat, two squadrons of
    /// eight F/A-18Es (8 v 8; SIX 2026-10-29) on spawn points 20 km apart facing each other (blue south, red north), the arena that brings them
    /// back, the fight, the training director and a spectator camera. It also writes the three training assets with
    /// their provenance (the arena, the learned pilot and its reward), so numbers and sources cannot drift apart.
    /// <para>"Add Training Squadrons to the Open Scene" puts the same squadrons, arena, fight and director into any scene
    /// with an Air Wing runner (Dogfight, say), unsaved.</para>
    /// </summary>
    public static class FighterTrainingBuilder
    {
        public const string ScenePath = "Assets/_SCENES/Dogfight Training.unity";
        public const string TrainingDir = AviatorTools.Root + "/ScriptableObjects/Training";
        public const string ArenaPath = TrainingDir + "/TrainingArena.asset";
        public const string LearnedPilotPath = TrainingDir + "/LearnedPilot.asset";
        public const string RewardPath = TrainingDir + "/RewardWeights.asset";
        public const string PriceListPath = TrainingDir + "/PriceList.asset";
        public const string BehaviorName = "Fighter";
        /// <summary>Jets a side: 8 v 8 since 2026-10-29 (SIX; it was 4 v 4, M21).</summary>
        public const int SquadronSize = 8;

        // AUTHORED layout (M25, SIX 2026-10-29), metres; +Z is north, the sea datum y = 0. Dogfight's coast is at z = −4,500.
        static readonly Vector3 BlueSpawn = new Vector3(0f, 1829f, 2000f);    // 6,000 ft, heading 000
        static readonly Vector3 RedSpawn = new Vector3(0f, 1829f, 22000f);    // 6,000 ft, heading 180: 20 km apart
        const float SpacingM = 300f, StartKt = 400f;
        static readonly Color BlueColour = new Color(0.35f, 0.65f, 1f, 0.95f), RedColour = new Color(1f, 0.4f, 0.35f, 0.95f);

        [MenuItem("Deep Water/RH Aviator/Build Dogfight Training Scene")]
        public static void Build()
        {
            if (AirWingBuilders.AnyDirtyScene()) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AirWingBuilders.PrefabPath) == null) AirWingBuilders.BuildPrefab();
            AviatorTools.ApplyPilotProfiles();
            ApplyTrainingAssets();

            // Load assets only after the scene switch (AP12).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AirWingBuilders.RigAssetsPresent()) return;
            DogfightBuilder.BuildWorld(out SeaStateDirector sea);
            AirWingRunner runner = AirWingBuilders.BuildRunner(sea);
            AddTraining(runner, firstId: 1);
            // The camera starts behind blue 1, looking north toward the merge.
            AirWingBuilders.BuildSpectatorCamera(runner, BlueSpawn + new Vector3(0f, 8f, -40f), Quaternion.identity, follow: 1);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RH Aviator] Built {ScenePath}: Dogfight's world; blue 1–{SquadronSize} at (0, 1,829, 2,000) heading 000 and red {SquadronSize + 1}–{2 * SquadronSize} at (0, 1,829, 22,000) heading 180, " +
                      "line abreast 300 m, 400 kt; learned pilots on both sides; the arena (back 3 s after going down, 5 s protected, collisions inside 15 m, a 40 km box); " +
                      "the fight, the training director and the spectator camera. No player seat.");
        }

        [MenuItem("Deep Water/RH Aviator/Add Training Squadrons to the Open Scene")]
        public static void AddToOpenScene()
        {
            var runner = Object.FindAnyObjectByType<AirWingRunner>();
            if (runner == null) { Debug.LogError("[RH Aviator] no Air Wing runner in the open scene; nothing added."); return; }
            AviatorTools.ApplyPilotProfiles();
            ApplyTrainingAssets();
            int maxId = Object.FindObjectsByType<AircraftView>(FindObjectsSortMode.None).Select(v => v.AircraftId).DefaultIfEmpty(0).Max();
            AddTraining(runner, maxId + 1);
            EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
            Debug.Log($"[RH Aviator] Added the training squadrons to {runner.gameObject.scene.name}: blue {maxId + 1}–{maxId + SquadronSize}, red {maxId + SquadronSize + 1}–{maxId + 2 * SquadronSize}. The scene is not saved.");
        }

        /// <summary>Both squadrons, the arena, the fight and the director, in the open scene beside <paramref name="runner"/>.</summary>
        static void AddTraining(AirWingRunner runner, int firstId)
        {
            Squadron("Blue", Side.Blue, BlueSpawn, 0f, firstId, BlueColour);
            Squadron("Red", Side.Red, RedSpawn, 180f, firstId + SquadronSize, RedColour);

            var arena = Object.FindAnyObjectByType<AirWingArena>();
            if (arena == null) arena = new GameObject("Arena").AddComponent<AirWingArena>();
            AirWingBuilders.Set(arena, "runner", runner);
            AirWingBuilders.Set(arena, "arena", AssetDatabase.LoadAssetAtPath<ArenaSO>(ArenaPath));

            var fight = Object.FindAnyObjectByType<AviatorFight>();
            if (fight == null) fight = new GameObject("RH Aviator Fight").AddComponent<AviatorFight>();

            var training = Object.FindAnyObjectByType<FighterTraining>();
            if (training == null) training = new GameObject("Fighter Training").AddComponent<FighterTraining>();
            AirWingBuilders.Set(training, "runner", runner);
            AirWingBuilders.Set(training, "fight", fight);
            AirWingBuilders.Set(training, "arena", arena);
            AirWingBuilders.Set(training, "learnedPilot", AssetDatabase.LoadAssetAtPath<LearnedPilotSO>(LearnedPilotPath));
            AirWingBuilders.Set(training, "rewardWeights", AssetDatabase.LoadAssetAtPath<RewardWeightsSO>(RewardPath));
            AirWingBuilders.Set(training, "priceList", AssetDatabase.LoadAssetAtPath<PriceListSO>(PriceListPath));
        }

        /// <summary>
        /// One squadron: a spawn point and four jets in its slots, each carrying a learned pilot (agent and behaviour)
        /// and a rule pilot, the squadron's setting choosing which flies (A15). Ids rise from <paramref name="firstId"/>.
        /// </summary>
        static void Squadron(string label, Side side, Vector3 at, float headingDeg, int firstId, Color colour)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AirWingBuilders.PrefabPath);
            var point = new GameObject($"{label} Spawn");
            point.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, headingDeg, 0f));
            var jets = new AircraftView[SquadronSize];
            for (int i = 0; i < SquadronSize; i++)
            {
                var jet = (GameObject)PrefabUtility.InstantiatePrefab(prefab, point.scene);
                jet.name = $"{label} {i + 1}";
                var view = new SerializedObject(jet.GetComponent<AircraftView>());
                view.FindProperty("aircraftId").intValue = firstId + i;
                view.FindProperty("seat").enumValueIndex = (int)AircraftView.StartSeat.Hold;
                view.FindProperty("startSpeedKt").floatValue = StartKt;
                view.ApplyModifiedPropertiesWithoutUndo();
                jets[i] = jet.GetComponent<AircraftView>();

                jet.AddComponent<AviatorPilot>();
                var agent = jet.AddComponent<FighterAgent>();   // brings its Behavior Parameters
                var behaviour = jet.GetComponent<BehaviorParameters>();
                behaviour.BehaviorName = BehaviorName;
                FighterAgent.Shape(behaviour);   // the senses and actions as they are now (the agent shapes itself again when it wakes)
                behaviour.BehaviorType = BehaviorType.Default;
                agent.SetSide(side);
            }
            point.AddComponent<SpawnPoint>().Configure(label, jets, SpacingM, colour);
            point.AddComponent<TrainingSquadron>().Configure(side, FighterBrain.Learning,
                AssetDatabase.LoadAssetAtPath<PilotProfileSO>(AviatorTools.RookiePilotPath),
                AssetDatabase.LoadAssetAtPath<PilotProfileSO>(AviatorTools.LinePilotPath),
                AssetDatabase.LoadAssetAtPath<PilotProfileSO>(AviatorTools.AcePilotPath));
        }

        /// <summary>The arena, the learned pilot and its reward, written with their provenance (SIX's picks of 2026-10-29).</summary>
        [MenuItem("Deep Water/RH Aviator/Apply Training Assets")]
        public static void ApplyTrainingAssets()
        {
            Directory.CreateDirectory(TrainingDir);
            const string Picks = "AUTHORED 2026-10-29 from SIX's picks in the training interview (TRAINING-CONCEPT M21–M28; all eight recommended options).";

            ArenaSO arena = Ensure<ArenaSO>(ArenaPath);
            arena.respawnDelayS = 3f; arena.protectionS = 5f; arena.jitterHeightM = 300f; arena.jitterHeadingDeg = 15f; arena.seed = 2026;
            arena.collisionM = 15f; arena.centreM = new Vector3(0f, 0f, 12000f); arena.halfWidthM = new Vector2(20000f, 20000f);
            arena.provenance = Picks + " Respawn (M26, Air Wing W59): respawnDelayS 3 (a fall from 6,000 ft takes about 30 s, wasted in training), " +
                               "protectionS 5 (can't be hit and can't fire: against spawn camping, which a learner finds). Jitter (M25): jitterHeightM 300, jitterHeadingDeg 15, seed 2026, " +
                               "so the brain never learns one start. Collisions (W60): collisionM 15, about the F/A-18E's 13.6 m span plus a margin. The box (W61): centred on the merge " +
                               "midway between the spawn points 20 km apart, (0, 12,000), half-widths 20 km: 40 × 40 km.";
            EditorUtility.SetDirty(arena);

            LearnedPilotSO pilot = Ensure<LearnedPilotSO>(LearnedPilotPath);
            pilot.eyesAndHands = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(AviatorTools.AcePilotPath);
            pilot.decisionPeriodTicks = 5; pilot.maxLifeS = 180f;
            pilot.turnRangeDeg = 180f; pilot.climbRangeM = 1500f; pilot.minKt = 150f; pilot.maxKt = 600f; pilot.minG = 2f;
            pilot.provenance = Picks + " Eyes and hands (M23, M17): the ace's profile (150° field of view, 0.35 s late, 10 km, 12 s memory; 7 G, 75° bank), " +
                               "so a learned pilot wins by judgement, not a better airframe or better eyes. decisionPeriodTicks 5: ten decisions a second, the rule pilots' " +
                               "trigger rate (A12). maxLifeS 180: a life longer than three minutes is cut (interrupted, not lost). Actions (M1, M23): turnRangeDeg 180 " +
                               "(any heading), climbRangeM 1500, minKt 150 and maxKt 600 (the jet's fighting range, below its 7.5 G corner and above its stall), minG 2.";
            EditorUtility.SetDirty(pilot);

            RewardWeightsSO reward = Ensure<RewardWeightsSO>(RewardPath);
            reward.kill = 1f; reward.shotDown = -1f; reward.crash = -1f; reward.collision = -1f; reward.friendlyKill = -1f; reward.leftArena = -1f;
            reward.hitLanded = 0.05f; reward.hitTaken = -0.05f; reward.friendlyHit = -0.05f;
            reward.pointedConePerS = 0.02f; reward.coneDeg = 30f; reward.coneRangeM = 1000f; reward.squadronShare = 0.25f;
            reward.provenance = Picks + " M24 (M5: outcomes, lightly shaped): kill +1; shotDown, crash, collision, friendlyKill and leftArena −1 each. " +
                                "hitLanded +0.05 and hitTaken −0.05 per round (ten hit points: one kill's hits are worth +0.5), friendlyHit −0.05. " +
                                "pointedConePerS +0.02 inside coneDeg 30 of an enemy's tail inside coneRangeM 1000 AND pointing within 30 of it (A11's measure; " +
                                "the fight's own cone score stays position-only, A9). squadronShare 0.25 of every squadmate's kill or loss.";
            EditorUtility.SetDirty(reward);

            PriceListSO prices = ApplyPriceList();
            AssetDatabase.SaveAssets();
            arena.Bake(); pilot.Bake(); reward.Bake(); prices.Bake();   // throws if anything is out of shape
            Debug.Log($"[RH Aviator] Applied the training assets: {ArenaPath}, {LearnedPilotPath}, {RewardPath}, {PriceListPath}.");
        }

        /// <summary>The price list (A23) with its provenance, written on its own so it can be refreshed without the other training assets.</summary>
        public static PriceListSO ApplyPriceList()
        {
            PriceListSO prices = Ensure<PriceListSO>(PriceListPath);
            prices.jet = 1000f; prices.friendlyKillPenalty = 500f;
            prices.round = 0.5f; prices.heatMissile = 50f; prices.radarMissile = 120f; prices.chaff = 3f; prices.flare = 3f; prices.fuelPerKg = 0.02f;
            prices.provenance = "A23 (SIX 2026-10-30: \"a price tag on every bullet and missile, friendly kill, enemy kill, death, fuel\"; shown on the scoreboard first, " +
                                "the reward later on SIX's go). Game credits, every number AUTHORED, none a real-world price (BLUEPRINT standing constraint 8). " +
                                "jet 1000: what an enemy kill earns and what losing your own costs, however it went down. friendlyKillPenalty 500 on top of the jet the side lost. " +
                                "Scaled so one kill pays for a whole sortie: round 0.5 (the full 412 rounds 206), heatMissile 50, radarMissile 120 (the long shot dearer), " +
                                "chaff 3 and flare 3 (all 30 of each 90), fuelPerKg 0.02 (a full tank of about 6,500 kg is 130). Radar time and IFF questions are free.";
            EditorUtility.SetDirty(prices);
            return prices;
        }

        static T Ensure<T>(string path) where T : ScriptableObject
        {
            var so = AssetDatabase.LoadAssetAtPath<T>(path);
            if (so != null) return so;
            so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }
    }
}
