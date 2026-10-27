using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.ScriptableObjects;
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
                "0.5 G per degree sits 1.6 times inside where the hold rings, at 0.6 it degrades and at 0.8 it diverges).";
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            so.Bake();   // throws if anything is out of shape
            Debug.Log($"[RH Aviator] Applied the line pilot to {LinePilotPath}.");
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
            own.ApplyModifiedProperties();
            Debug.Log($"[RH Aviator] The player's jet (aircraft {player.AircraftId}) has an AI pilot too, holding course while the player is elsewhere.");
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
