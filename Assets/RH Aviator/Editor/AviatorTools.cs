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
    /// <see cref="AddAiJet"/> puts an AI F/A-18E beside the player's jet in the open scene, without saving it: the
    /// Dogfight scene Air Wing builds keeps one jet until slice 3b (W51).
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
            so.provenance =
                "All AUTHORED (RH Aviator W45; no public source describes a generic line pilot), tuned headless against slice 3a's " +
                "pass criteria (ROADMAP, fixed before tuning). Hands: guidanceGain (2, the classic value of the L1 path-following law, Park, Deyst and How, AIAA 2004-4900, PUBLIC method), maxBankDeg, rollRatePerBankDeg, climbDegPerM, " +
                "maxClimbDeg, maxDiveDeg, gPerPathDeg, maxG (below the F/A-18E's 7.5 G limit: a line pilot does not fly at the limit), " +
                "throttlePerKt, throttleTrim, throttleIntegralPerKtS, speedBrakeOverKt. Ground guard: floorAglM (100 m, about 330 ft), guardLookAheadS, " +
                "guardClimbDeg, guardMaxBankDeg. Route: leadInM, carrotM, carrotTurnRadii, throughM, minRunM, captureDeg, captureFraction, cruiseKt, bandMarginKt. " +
                "After the course: holdAltitudeM (3,000 ft), holdKt.";
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            so.Bake();   // throws if anything is out of shape
            Debug.Log($"[RH Aviator] Applied the line pilot to {LinePilotPath}.");
        }

        [MenuItem("Deep Water/RH Aviator/Add AI Jet to the Open Scene")]
        public static void AddAiJet()
        {
            AircraftView player = null;
            int maxId = 0;
            foreach (var v in Object.FindObjectsByType<AircraftView>(FindObjectsSortMode.None))
            {
                if (v.AircraftId > maxId) maxId = v.AircraftId;
                if (v.Seat == AircraftView.StartSeat.Player) player = v;
            }
            if (player == null) { Debug.LogError("[RH Aviator] no player jet in the open scene; nothing added."); return; }
            var pilot = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(LinePilotPath);
            var route = AssetDatabase.LoadAssetAtPath<GateCourseSO>(DogfightRoutePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JetPrefabPath);
            if (pilot == null || route == null || prefab == null)
            {
                Debug.LogError($"[RH Aviator] missing {(pilot == null ? LinePilotPath + " (Apply Line Pilot Profile) " : "")}{(route == null ? DogfightRoutePath + " " : "")}{(prefab == null ? JetPrefabPath : "")}; nothing added.");
                return;
            }

            var jet = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.gameObject.scene);
            jet.name = "AI F/A-18E";
            Transform p = player.transform;
            jet.transform.SetPositionAndRotation(p.position + p.rotation * new Vector3(80f, 0f, -80f), p.rotation);
            var view = new SerializedObject(jet.GetComponent<AircraftView>());
            view.FindProperty("aircraftId").intValue = maxId + 1;
            view.FindProperty("seat").enumValueIndex = (int)AircraftView.StartSeat.Hold;
            view.FindProperty("startSpeedKt").floatValue = new SerializedObject(player).FindProperty("startSpeedKt").floatValue;
            view.ApplyModifiedProperties();
            var ai = new SerializedObject(jet.AddComponent<AviatorPilot>());
            ai.FindProperty("profile").objectReferenceValue = pilot;
            ai.FindProperty("route").objectReferenceValue = route;
            ai.ApplyModifiedProperties();
            Undo.RegisterCreatedObjectUndo(jet, "Add AI jet");
            EditorSceneManager.MarkSceneDirty(jet.scene);
            Debug.Log($"[RH Aviator] Added aircraft {maxId + 1}, an AI F/A-18E flying {route.name}, 80 m right of and behind the player. The scene is not saved.");
        }
    }
}
