using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Modules;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Editor
{
    /// <summary>
    /// Deep Water → RH Aviator menu. <see cref="ApplyLinePilot"/> writes the "line" pilot's numbers and their
    /// provenance into its asset, so the numbers and their sources cannot drift apart (as Air Wing's HarvL1Data).
    /// <see cref="AddAiJet"/> puts an AI F/A-18E beside the player's jet in the open scene, without saving it, and
    /// <see cref="AddAiWingman"/> one that joins the player's wing: the Dogfight scene Air Wing builds keeps one jet (W51).
    /// </summary>
    public static class AviatorTools
    {
        public const string Root = "Assets/RH Aviator";
        public const string LinePilotPath = Root + "/ScriptableObjects/Pilot_Line.asset";
        public const string RookiePilotPath = Root + "/ScriptableObjects/Pilot_Rookie.asset";
        public const string AcePilotPath = Root + "/ScriptableObjects/Pilot_Ace.asset";
        public const string DogfightRoutePath = "Assets/Air Wing/Examples/Dogfight/Dogfight_Gates.asset";
        const string JetPrefabPath = "Assets/Air Wing/Prefabs/FA18E_AirWing.prefab";

        [MenuItem("Deep Water/RH Aviator/Apply Line Pilot Profile")]
        public static void ApplyLinePilot()
        {
            var so = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(LinePilotPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<PilotProfileSO>();
                Directory.CreateDirectory(Path.GetDirectoryName(LinePilotPath));
                AssetDatabase.CreateAsset(so, LinePilotPath);
            }
            Undo.RecordObject(so, "Apply line pilot");
            so.displayName = "line";
            // The hands (W37). AUTHORED: tuned headless against slice 3a's pass criteria (fixed before tuning).
            so.guidanceGain = 2f; so.maxBankDeg = 70f; so.rollRatePerBankDeg = 2f;
            so.climbDegPerM = 0.06f; so.maxClimbDeg = 20f; so.maxDiveDeg = 20f; so.gPerPathDeg = 0.25f;
            so.maxG = 6f;
            so.throttlePerKt = 0.02f; so.throttleTrim = 0.75f; so.throttleIntegralPerKtS = 0.004f; so.useAfterburner = true; so.speedBrakeOverKt = 25f;
            // The ground guard (Air Wing AP25).
            so.floorAglM = 100f; so.guardLookAheadS = 6f; so.guardClimbDeg = 8f; so.guardMaxBankDeg = 30f;
            // Route flying.
            so.leadInM = 3000f; so.carrotM = 400f; so.carrotTurnRadii = 1f; so.throughM = 1000f; so.minRunM = 1500f; so.captureDeg = 30f; so.captureFraction = 0.7f;
            so.cruiseKt = 350f; so.bandMarginKt = 60f;
            so.holdAltitudeM = 914f; so.holdKt = 350f;
            // Fingertip (W40): about three wingspans out (the F/A-18E spans 13.6 m) and a little behind, level with the leader.
            so.slotRightM = 40f; so.slotBackM = 10f; so.slotUpM = 0f; so.slotLookAheadM = 800f;
            so.slotGainPerS = 0.25f; so.joinDecelMps2 = 1.5f; so.joinOvertakeKt = 150f; so.slotOvertakeKt = 30f; so.joinMinKt = 250f;
            so.inSlotM = 10f; so.inSlotClosureKt = 5f; so.lostSlotM = 150f; so.slotSpeedLead = 2f; so.slotSpeedBrakeOverKt = 8f; so.slotClimbDegPerM = 0.03f; so.slotGPerPathDeg = 0.5f; so.slotRotationShare = 0.75f;
            // Going home and the break (W41, W47). The shape is the U.S. Navy's overhead break as publicly described
            // (initial at 800 ft and about 350 kt, a level break to the left, downwind at 600 ft, a descending 180 to a
            // short final); every number here is AUTHORED to fit this jet and Dogfight's runway.
            so.initialM = 5556f; so.initialCaptureM = 150f; so.initialKt = 350f; so.breakAlongM = 600f; so.breakHeightM = 244f; so.breakKt = 240f;
            so.downwindOffsetM = 1500f; so.downwindHeightM = 183f; so.finalTurnAlongM = 1500f;
            so.approachKt = 145f; so.gearKt = 250f; so.patternBankDeg = 40f; so.patternGPerPathDeg = 0.1f; so.patternMaxDiveDeg = 6f; so.patternFloorAglM = 30f;
            so.glidePathDeg = 3f; so.touchdownM = 300f; so.flareHeightM = 9f; so.flarePathDeg = 0.8f; so.finalCarrotM = 300f;
            so.rolloutPedalPerM = 0.05f; so.rolloutPedalPerDeg = 0.1f; so.bingoFlightTimeS = 0f;
            // The fight (Pilot L1, W38, W45). AUTHORED: a game's skill numbers, not any real pilot's or air force's.
            so.fieldOfViewDeg = 120f; so.reactionDelayS = 0.7f; so.memoryS = 8f; so.visualRangeM = 8000f;
            so.spotRatePerS = 1f; so.spotHalfRangeM = 4000f; so.spotNoseSize = 0.5f; so.spotTailSize = 0.6f;
            so.cornerKt = 330f; so.leadS = 1f; so.lagS = 1f; so.lagRangeM = 800f; so.overshootClosureKt = 60f; so.lagSlowKt = 40f;
            so.threatRangeM = 1500f; so.threatConeDeg = 60f; so.fightFloorM = 300f; so.clearRangeM = 8000f; so.slowKt = 220f; so.turnMatch = 0.7f; so.rangeKtPerM = 0.05f; so.attackRangeM = 600f; so.chaseOverKt = 150f;
            so.avoidM = 100f; so.avoidS = 3f;
            so.joinAvoidM = 25f;
            so.gunRangeM = 900f; so.triggerMils = 12f;
            so.missileRangeM = 3500f;
            so.provenance =
                "All AUTHORED (RH Aviator W45; no public source describes a generic line pilot), tuned headless against slice 3a's " +
                "pass criteria (ROADMAP, fixed before tuning). Hands: guidanceGain (2, the classic value of the L1 path-following law, Park, Deyst and How, AIAA 2004-4900, PUBLIC method), maxBankDeg, rollRatePerBankDeg, climbDegPerM, " +
                "maxClimbDeg, maxDiveDeg, gPerPathDeg, maxG (below the F/A-18E's 7.5 G limit: a line pilot does not fly at the limit), " +
                "throttlePerKt, throttleTrim, throttleIntegralPerKtS, speedBrakeOverKt. Ground guard: floorAglM (100 m, about 330 ft), guardLookAheadS, " +
                "guardClimbDeg, guardMaxBankDeg. Route: leadInM, carrotM, carrotTurnRadii, throughM, minRunM, captureDeg, captureFraction, cruiseKt, bandMarginKt. " +
                "After the course: holdAltitudeM (3,000 ft), holdKt. " +
                "Fingertip (W40): slotRightM (40 m, about three of the F/A-18E's 13.6 m wingspans, centre to centre), slotBackM, slotUpM, slotLookAheadM, " +
                "slotGainPerS, joinDecelMps2 (below what an F/A-18E at idle loses near 400 kt, about 2.3 m/s² in slice 3b's trace, so the plan is flyable), " +
                "joinOvertakeKt, slotOvertakeKt, joinMinKt, inSlotM, inSlotClosureKt, lostSlotM, slotSpeedLead, slotSpeedBrakeOverKt, slotClimbDegPerM, slotGPerPathDeg, slotRotationShare (the last three picked by FormationTests.Sweep, a grid on the fixed hold scenario: " +
                "0.5 G per degree sits 1.6 times inside where the hold rings, at 0.6 it degrades and at 0.8 it diverges). " +
                "Going home (W41, W47; the shape as the U.S. Navy publicly describes its overhead break, the numbers fitted): initialM, initialCaptureM, initialKt, breakAlongM, breakHeightM (800 ft), breakKt, " +
                "downwindOffsetM, downwindHeightM (600 ft), finalTurnAlongM, approachKt (145 kt, as Air Wing's landing test), gearKt, patternBankDeg, patternGPerPathDeg, patternMaxDiveDeg, patternFloorAglM, glidePathDeg, touchdownM, flareHeightM, flarePathDeg, finalCarrotM, " +
                "rolloutPedalPerM, rolloutPedalPerDeg, bingoFlightTimeS (a STAND-IN for bingo fuel until fuel burn exists; 0 = never). " +
                "The fight (Pilot L1, W38, W45; a game's skill numbers, AUTHORED, not any real pilot's or air force's): fieldOfViewDeg, reactionDelayS, memoryS, visualRangeM, " +
                "spotting by size (SENSORS-CONCEPT J8, SIX 2026-10-29): spotRatePerS 1 (close up, seen in about a second), spotHalfRangeM 4000 (a jet seen from the beam is spotted at half that rate 4 km out), " +
                "spotNoseSize 0.5 and spotTailSize 0.6 (nose-on a jet looks half its beam size, from behind six tenths: a game's guess, not measured); " +
                "cornerKt, leadS, lagS, lagRangeM, overshootClosureKt, lagSlowKt, threatRangeM, threatConeDeg, fightFloorM, clearRangeM, slowKt (energy sense; 0 = none), turnMatch, rangeKtPerM, attackRangeM, chaseOverKt (range control; 0 = none), avoidM, avoidS (collision avoidance); joinAvoidM (25 m, inside the 40 m slot spacing: a join with no fight avoids too, after a survey of four-ship joins found crossed joins meeting at 0 m). Trigger discipline (Gunnery K6): gunRangeM 900, triggerMils 12 (a burst that would pass within 12 mils of where the target will be; AUTHORED). Missile discipline (Air Wing W65, A16; SIX 2026-10-29: rookies early, aces close): missileRangeM 3500 (launch with the seeker locked on its target inside 3.5 km, one at a time; AUTHORED).";
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            so.Bake();   // throws if anything is out of shape
            Debug.Log($"[RH Aviator] Applied the line pilot to {LinePilotPath}.");
        }

        /// <summary>
        /// The three pilots (W45): line, then rookie and ace written from it, one brain with different numbers. The
        /// rookie sees less and later, pulls less, flies pure pursuit only and fights too fast; the ace sees more and
        /// sooner, pulls more, and uses lead and lag. Everything else is the line pilot's.
        /// </summary>
        [MenuItem("Deep Water/RH Aviator/Apply Pilot Profiles (Rookie, Line, Ace)")]
        public static void ApplyPilotProfiles()
        {
            ApplyLinePilot();
            var line = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(LinePilotPath);
            Write(RookiePilotPath, line, "rookie", so =>
            {
                so.maxG = 4.5f; so.maxBankDeg = 60f;
                so.fieldOfViewDeg = 90f; so.reactionDelayS = 1.2f; so.memoryS = 4f; so.visualRangeM = 6000f;
                so.spotRatePerS = 0.6f; so.spotHalfRangeM = 3000f;
                so.cornerKt = 380f; so.leadS = 0f; so.lagS = 0f; so.threatRangeM = 1000f; so.slowKt = 0f; so.turnMatch = 0f; so.rangeKtPerM = 0f;
                so.gunRangeM = 1400f; so.triggerMils = 30f;
                so.missileRangeM = 5000f;
            }, "Rookie, from the line pilot (AUTHORED): maxG 4.5, maxBankDeg 60, fieldOfViewDeg 90, reactionDelayS 1.2, memoryS 4, visualRangeM 6000, spotRatePerS 0.6 and spotHalfRangeM 3000 (spots later), cornerKt 380 (fights too fast), leadS 0 and lagS 0 (pure pursuit only), threatRangeM 1000, slowKt 0 (no energy sense), turnMatch 0, rangeKtPerM 0 (no range control), gunRangeM 1400 and triggerMils 30 (sprays: long and wide), missileRangeM 5000 (launches as soon as it locks).");
            Write(AcePilotPath, line, "ace", so =>
            {
                so.maxG = 7f; so.maxBankDeg = 75f;
                so.fieldOfViewDeg = 150f; so.reactionDelayS = 0.35f; so.memoryS = 12f; so.visualRangeM = 10000f;
                so.spotRatePerS = 1.5f; so.spotHalfRangeM = 5000f;
                so.cornerKt = 320f; so.leadS = 1.2f; so.lagS = 1.2f; so.threatRangeM = 2000f; so.slowKt = 240f; so.turnMatch = 1f;
                so.gunRangeM = 800f; so.triggerMils = 6f;
                so.missileRangeM = 2500f;
            }, "Ace, from the line pilot (AUTHORED): maxG 7 (inside the F/A-18E's 7.5), maxBankDeg 75, fieldOfViewDeg 150, reactionDelayS 0.35, memoryS 12, visualRangeM 10000, spotRatePerS 1.5 and spotHalfRangeM 5000 (spots sooner), cornerKt 320, leadS 1.2, lagS 1.2, threatRangeM 2000, slowKt 240, turnMatch 1, gunRangeM 800 and triggerMils 6 (waits for the shot: close and tight), missileRangeM 2500 (launches close, where the missile has the energy).");
        }

        static void Write(string path, PilotProfileSO from, string name, System.Action<PilotProfileSO> change, string note)
        {
            var so = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<PilotProfileSO>();
                AssetDatabase.CreateAsset(so, path);
            }
            Undo.RecordObject(so, "Apply " + name + " pilot");
            EditorUtility.CopySerialized(from, so);
            so.name = Path.GetFileNameWithoutExtension(path);
            so.displayName = name;
            change(so);
            so.provenance = from.provenance + " " + note;
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            so.Bake();
            Debug.Log($"[RH Aviator] Applied the {name} pilot to {path}.");
        }

        [MenuItem("Deep Water/RH Aviator/Add AI Jet to the Open Scene")]
        public static void AddAiJet() => AddJet(AviatorPilot.Task.Route, new Vector3(80f, 0f, -80f));

        /// <summary>
        /// Slice 3b's smoke (W40, W42): an AI wingman 600 m behind and 200 m right of the player, ordered to join its
        /// wing; and an AI pilot in the player's own jet, holding course, so the jet has someone flying it when the
        /// player cycles into the wingman (Tab, D-pad right). Unsaved, as <see cref="AddAiJet"/>.
        /// </summary>
        [MenuItem("Deep Water/RH Aviator/Add AI Wingman to the Open Scene")]
        public static void AddAiWingman()
        {
            AircraftView player = AddJet(AviatorPilot.Task.Wingman, new Vector3(200f, 0f, -600f));
            if (player == null || player.GetComponent<AviatorPilot>() != null) return;
            var own = new SerializedObject(Undo.AddComponent<AviatorPilot>(player.gameObject));
            own.FindProperty("profile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(LinePilotPath);
            own.FindProperty("task").enumValueIndex = (int)AviatorPilot.Task.HoldCourse;
            var runway = GameObject.Find("Runway 09-27");
            if (runway != null) own.FindProperty("homeRunway").objectReferenceValue = runway.GetComponent<DeepWater.AirWing.Modules.World.FlatGround>();
            own.ApplyModifiedProperties();
            Debug.Log($"[RH Aviator] The player's jet (aircraft {player.AircraftId}) has an AI pilot too, holding course while the player is elsewhere.");
        }

        /// <summary>
        /// SIX's 4 v 4 (2026-10-27; W43 at eight, W52, W53): three blue wingmen (line) in echelon behind the player,
        /// four red (rookie, line, line, ace) 20 km ahead and coming the other way at 6,000 ft, an AI pilot in the
        /// player's own jet (it fights when the player cycles away), the fight with its score panel, and the orders.
        /// Unsaved. Red hunts whichever jet the player flies. No weapons: rear-cone seconds only.
        /// </summary>
        [MenuItem("Deep Water/RH Aviator/Add 4 v 4 Dogfight to the Open Scene")]
        public static void AddFourVersusFour()
        {
            ApplyPilotProfiles();
            AircraftView player = null;
            int maxId = 0;
            foreach (var v in Object.FindObjectsByType<AircraftView>(FindObjectsSortMode.None))
            {
                if (v.AircraftId > maxId) maxId = v.AircraftId;
                if (v.Seat == AircraftView.StartSeat.Player) player = v;
            }
            if (player == null) { Debug.LogError("[RH Aviator] no player jet in the open scene; nothing added."); return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JetPrefabPath);
            var runway = GameObject.Find("Runway 09-27");
            Transform p = player.transform;
            float startKt = new SerializedObject(player).FindProperty("startSpeedKt").floatValue;
            void Jet(string name, Vector3 offset, float yaw, string pilotPath, AviatorPilot.Task task, Side side, int id, bool own = false)
            {
                GameObject jet = own ? player.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.gameObject.scene);
                if (!own)
                {
                    jet.name = name;
                    jet.transform.SetPositionAndRotation(p.position + p.rotation * offset, p.rotation * Quaternion.Euler(0f, yaw, 0f));
                    var view = new SerializedObject(jet.GetComponent<AircraftView>());
                    view.FindProperty("aircraftId").intValue = id;
                    view.FindProperty("seat").enumValueIndex = (int)AircraftView.StartSeat.Hold;
                    view.FindProperty("startSpeedKt").floatValue = startKt;
                    view.ApplyModifiedProperties();
                    Undo.RegisterCreatedObjectUndo(jet, "Add 4 v 4");
                }
                // Not ?? : in the Editor a missing component comes back as a fake null that ?? does not see.
                AviatorPilot pilot = jet.GetComponent<AviatorPilot>();
                if (pilot == null) pilot = Undo.AddComponent<AviatorPilot>(jet);
                var ai = new SerializedObject(pilot);
                ai.FindProperty("profile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(pilotPath);
                ai.FindProperty("task").enumValueIndex = (int)task;
                ai.FindProperty("side").enumValueIndex = (int)side;
                ai.FindProperty("leaderId").intValue = player.AircraftId;
                ai.FindProperty("slotPosition").intValue = side == Side.Blue && !own ? id - maxId : 0;   // 1 left, 2 right, 3 outside right
                if (runway != null) ai.FindProperty("homeRunway").objectReferenceValue = runway.GetComponent<DeepWater.AirWing.Modules.World.FlatGround>();
                ai.ApplyModifiedProperties();
            }
            Jet(null, Vector3.zero, 0f, LinePilotPath, AviatorPilot.Task.Fight, Side.Blue, player.AircraftId, own: true);
            for (int i = 1; i <= 3; i++) Jet($"Blue {i + 1}", new Vector3(60f * i, 0f, -60f * i), 0f, LinePilotPath, AviatorPilot.Task.Fight, Side.Blue, maxId + i);
            string[] reds = { RookiePilotPath, LinePilotPath, LinePilotPath, AcePilotPath };
            for (int i = 0; i < 4; i++) Jet($"Red {i + 1}", new Vector3(-300f + 200f * i, 300f, 20000f), 180f, reds[i], AviatorPilot.Task.Fight, Side.Red, maxId + 4 + i);
            if (Object.FindAnyObjectByType<AviatorFight>() == null) Undo.AddComponent<AviatorFight>(new GameObject("RH Aviator Fight"));
            if (Object.FindAnyObjectByType<AviatorOrders>() == null) Undo.AddComponent<AviatorOrders>(new GameObject("RH Aviator Orders"));
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log($"[RH Aviator] Added the 4 v 4: blue {player.AircraftId}, {maxId + 1}–{maxId + 3}; red {maxId + 4}–{maxId + 7} (rookie, line, line, ace), 20 km ahead. The scene is not saved.");
        }

        /// <summary>Adds an AI jet at <paramref name="offset"/> from the player's (its frame); returns the player's view, or null.</summary>
        static AircraftView AddJet(AviatorPilot.Task task, Vector3 offset)
        {
            AircraftView player = null;
            int maxId = 0;
            foreach (var v in Object.FindObjectsByType<AircraftView>(FindObjectsSortMode.None))
            {
                if (v.AircraftId > maxId) maxId = v.AircraftId;
                if (v.Seat == AircraftView.StartSeat.Player) player = v;
            }
            if (player == null) { Debug.LogError("[RH Aviator] no player jet in the open scene; nothing added."); return null; }
            var pilot = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(LinePilotPath);
            var route = AssetDatabase.LoadAssetAtPath<GateCourseSO>(DogfightRoutePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JetPrefabPath);
            if (pilot == null || route == null || prefab == null)
            {
                Debug.LogError($"[RH Aviator] missing {(pilot == null ? LinePilotPath + " (Apply Line Pilot Profile) " : "")}{(route == null ? DogfightRoutePath + " " : "")}{(prefab == null ? JetPrefabPath : "")}; nothing added.");
                return null;
            }

            var jet = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.gameObject.scene);
            jet.name = "AI F/A-18E";
            Transform p = player.transform;
            jet.transform.SetPositionAndRotation(p.position + p.rotation * offset, p.rotation);
            var view = new SerializedObject(jet.GetComponent<AircraftView>());
            view.FindProperty("aircraftId").intValue = maxId + 1;
            view.FindProperty("seat").enumValueIndex = (int)AircraftView.StartSeat.Hold;
            view.FindProperty("startSpeedKt").floatValue = new SerializedObject(player).FindProperty("startSpeedKt").floatValue;
            view.ApplyModifiedProperties();
            var ai = new SerializedObject(jet.AddComponent<AviatorPilot>());
            ai.FindProperty("profile").objectReferenceValue = pilot;
            ai.FindProperty("route").objectReferenceValue = route;
            ai.FindProperty("task").enumValueIndex = (int)task;
            var runway = GameObject.Find("Runway 09-27");
            if (runway != null) ai.FindProperty("homeRunway").objectReferenceValue = runway.GetComponent<DeepWater.AirWing.Modules.World.FlatGround>();
            if (Object.FindAnyObjectByType<AviatorOrders>() == null) Undo.AddComponent<AviatorOrders>(new GameObject("RH Aviator Orders"));
            ai.FindProperty("leaderId").intValue = player.AircraftId;
            ai.ApplyModifiedProperties();
            Undo.RegisterCreatedObjectUndo(jet, "Add AI jet");
            EditorSceneManager.MarkSceneDirty(jet.scene);
            string what = task == AviatorPilot.Task.Wingman ? $"joining aircraft {player.AircraftId}'s wing" : $"flying {route.name}";
            Debug.Log($"[RH Aviator] Added aircraft {maxId + 1}, an AI F/A-18E {what}, {offset.x:F0} m right of and {-offset.z:F0} m behind the player. The scene is not saved.");
            return player;
        }
    }
}
