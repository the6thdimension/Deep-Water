using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.Modules.Cockpit;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Core.Tree;
using DeepWater.Aviator.Modules;

namespace DeepWater.Aviator.Editor
{
    /// <summary>
    /// The Fight Tree window (RH Aviator A28, slice 4a): a rule pilot's two behavior trees, live in play. It shows the
    /// jet selected in the Hierarchy, else the one the spectator camera is on, and lights the branches that ran on the
    /// pilot's last watch: green, it did its job; red, it did not (a question answered no, or a branch that stopped);
    /// grey, not reached. Under the trees, the pilot's last rule firings. It reads only; it changes nothing in the fight.
    /// </summary>
    public sealed class FightTreeWindow : EditorWindow
    {
        const int Firings = 12;

        Vector2 _scroll;
        GUIStyle _ran, _failed, _skipped;

        [MenuItem("Deep Water/RH Aviator/Fight Tree")]
        static void Open() => GetWindow<FightTreeWindow>("Fight Tree");

        // Repaints about ten times a second in play: the trees change at 4 Hz.
        void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

        void OnGUI()
        {
            Styles();
            if (!Application.isPlaying) { EditorGUILayout.HelpBox("Enter Play: the trees are drawn from a flying rule pilot.", MessageType.Info); return; }

            AviatorPilot pilot = Followed(out string why);
            if (pilot == null || pilot.Seat == null) { EditorGUILayout.HelpBox(why, MessageType.Info); return; }
            AviatorSeat seat = pilot.Seat;
            int id = pilot.GetComponent<AircraftView>().AircraftId;

            EditorGUILayout.LabelField($"Aircraft {id} · {pilot.name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{seat.Mode} · target {(seat.TargetId == 0 ? "none" : seat.TargetId.ToString())} · {seat.Pursuit}");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            Section("Fight", seat.FightTree, seat.Mode == PilotMode.Fight || seat.Mode == PilotMode.Join || seat.Mode == PilotMode.Fingertip);
            Section("Sensors", seat.Habits?.Tree, seat.Habits != null);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last rules fired", EditorStyles.boldLabel);
            IReadOnlyList<RuleFiring> log = pilot.Log.Firings;
            for (int i = log.Count - 1, shown = 0; i >= 0 && shown < Firings; i--)
            {
                if (log[i].AircraftId != id) continue;
                EditorGUILayout.LabelField($"{log[i].Tick / 50f,7:F1} s  {log[i].Rule}", log[i].Reason, EditorStyles.wordWrappedMiniLabel);
                shown++;
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>The jet to show: a rule pilot's jet selected in the Hierarchy, else the spectator camera's; null with the reason why not.</summary>
        static AviatorPilot Followed(out string why)
        {
            why = null;
            GameObject go = Selection.activeGameObject;
            AviatorPilot picked = go != null ? go.GetComponentInParent<AviatorPilot>() : null;
            if (picked != null && picked.Seat != null) return picked;
            SpectatorCamera cam = FindFirstObjectByType<SpectatorCamera>();
            if (cam == null || cam.Target == null) { why = "Select a jet flown by a rule pilot (one with an AviatorPilot), or point the spectator camera at one."; return null; }
            var p = cam.Target.GetComponent<AviatorPilot>();
            if (p == null || p.Seat == null)
                why = $"The camera is on aircraft {cam.Target.AircraftId}, which no rule pilot flies (the player, or a learned pilot): select a rule pilot's jet.";
            return p;
        }

        void Section(string title, Node root, bool live)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (root == null) { EditorGUILayout.LabelField("  none: it has not been ordered to fight"); return; }
            if (!live) EditorGUILayout.LabelField("  not running in this mode; showing its last watch", EditorStyles.miniLabel);
            Draw(root, root.LastTick, 0);
        }

        void Draw(Node n, long watch, int depth)
        {
            bool ran = n.LastTick == watch && watch != long.MinValue;
            GUIStyle style = !ran ? _skipped : n.Status == NodeStatus.Failure ? _failed : _ran;
            string mark = !ran ? "·" : n.Status == NodeStatus.Success ? "✓" : n.Status == NodeStatus.Failure ? "✗" : "…";
            EditorGUILayout.LabelField(new string(' ', depth * 4) + mark + " " + n.Name + Kind(n), style);
            for (int i = 0; i < n.Children.Count; i++) Draw(n.Children[i], watch, depth + 1);
        }

        static string Kind(Node n) => n is Selector ? "  (first that applies)" : n is Sequence ? "  (in turn)" : "";

        void Styles()
        {
            if (_ran != null) return;
            _ran = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.35f, 0.8f, 0.4f) } };
            _failed = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.9f, 0.4f, 0.35f) } };
            _skipped = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };
        }
    }
}
