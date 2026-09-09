// SceneViewFollowerWindow.cs
// JMAC's Tools > Scene View Follower
// Makes the Scene view camera follow a target object with customizable controls.
// Drop this file anywhere in an Editor folder.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;

namespace JMAC.SVF.Editor
{
    public class SceneViewFollowerWindow : EditorWindow
    {
        private const string MENU = "JMAC's Tools/Scene View Follower";
        private const string PREF_KEY = "JMAC_SceneViewFollower_Settings";

        [Serializable]
        private class Settings
        {
            public bool enabled = false;
            public bool followInPlayMode = true;
            public Transform target;

            public FollowMode followMode = FollowMode.OffsetFromTargetForward;
            public Vector3 positionOffset = new Vector3(0, 2f, 0);
            public float distance = 6f; // used by OffsetFromTargetForward
            public bool lookAtTarget = true;
            public Vector3 lookAtOffset = Vector3.zero;
            public bool matchTargetRotation = false;

            public bool lockToPivot = false; // use SceneView pivot rather than camera
            public float smoothTime = 0.15f; // seconds
            public bool useSmoothing = true;

            public bool onlyWhenSceneViewFocused = false;
            public bool respectUserNavigation = true; // If user is orbiting, temporarily pause follow
            public float resumeAfterSeconds = 1.0f;

            public bool showGizmos = true;
            public KeyCode toggleHotkey = KeyCode.F12;
        }

        private enum FollowMode
        {
            OffsetFromTargetForward, // position = target.position - target.forward * distance + offset
            FixedWorldOffset,        // position = target.position + world offset (ignores target rotation)
            MatchTargetPosition      // position = target.position (plus offset)
        }

        private Settings _s = new Settings();

        // smoothing state
        private Vector3 _vel; // for SmoothDamp
        private Quaternion _qVel = Quaternion.identity; // custom slerp helper
        private double _lastTime;
        private double _lastUserNavTime;
        private Vector3 _lastScenePivot;
        private Quaternion _lastSceneRot;

        [MenuItem(MENU)]
        public static void ShowWindow()
        {
            var win = GetWindow<SceneViewFollowerWindow>();
            win.titleContent = new GUIContent("Scene Follower", EditorGUIUtility.IconContent("SceneViewFx").image);
            win.minSize = new Vector2(310, 340);
            win.Focus();
        }

        private void OnEnable()
        {
            Load();
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui += DuringSceneGUI;
            _lastTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= DuringSceneGUI;
            Save();
        }

        private void DuringSceneGUI(SceneView sv)
        {
            // hotkey toggle
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == _s.toggleHotkey)
            {
                _s.enabled = !_s.enabled;
                Repaint();
                e.Use();
            }

            // detect user navigation to temporarily pause follow
            if (_s.respectUserNavigation)
            {
                if (e.isMouse && (e.button == 1 || e.button == 2) && (e.type == EventType.MouseDrag || e.type == EventType.ScrollWheel))
                {
                    _lastUserNavTime = EditorApplication.timeSinceStartup;
                }
                if (UnityEditor.Tools.current == Tool.View)
                {
                    // compare pivot/rotation changes to detect navigation via gizmos or shortcuts
                    if (_lastScenePivot != sv.pivot || _lastSceneRot != sv.rotation)
                    {
                        _lastUserNavTime = EditorApplication.timeSinceStartup;
                    }
                }
                _lastScenePivot = sv.pivot;
                _lastSceneRot = sv.rotation;
            }

            // gizmos
            if (_s.showGizmos && _s.target)
            {
                Handles.color = new Color(0.2f, 0.8f, 1f, 0.6f);
                Handles.SphereHandleCap(0, _s.target.position + _s.lookAtOffset, Quaternion.identity, HandleUtility.GetHandleSize(_s.target.position) * 0.2f, EventType.Repaint);
            }
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope("HelpBox"))
            {
                EditorGUILayout.LabelField("Scene View Follower", EditorStyles.boldLabel);
                _s.enabled = EditorGUILayout.ToggleLeft(new GUIContent("Enable follow", "Toggle following in the Scene view"), _s.enabled);
                _s.followInPlayMode = EditorGUILayout.ToggleLeft(new GUIContent("Follow in Play Mode"), _s.followInPlayMode);
                _s.onlyWhenSceneViewFocused = EditorGUILayout.ToggleLeft(new GUIContent("Only when Scene view focused"), _s.onlyWhenSceneViewFocused);

                EditorGUILayout.Space(4);
                _s.target = (Transform)EditorGUILayout.ObjectField(new GUIContent("Target"), _s.target, typeof(Transform), true);

                _s.followMode = (FollowMode)EditorGUILayout.EnumPopup("Follow Mode", _s.followMode);
                _s.positionOffset = EditorGUILayout.Vector3Field(new GUIContent("Position Offset"), _s.positionOffset);
                if (_s.followMode == FollowMode.OffsetFromTargetForward)
                {
                    _s.distance = EditorGUILayout.FloatField(new GUIContent("Distance"), _s.distance);
                }

                EditorGUILayout.Space(6);
                _s.lookAtTarget = EditorGUILayout.ToggleLeft(new GUIContent("Look At Target"), _s.lookAtTarget);
                if (_s.lookAtTarget)
                    _s.lookAtOffset = EditorGUILayout.Vector3Field(new GUIContent("LookAt Offset"), _s.lookAtOffset);

                _s.matchTargetRotation = EditorGUILayout.ToggleLeft(new GUIContent("Match Target Rotation"), _s.matchTargetRotation);

                EditorGUILayout.Space(6);
                _s.useSmoothing = EditorGUILayout.ToggleLeft(new GUIContent("Use Smoothing"), _s.useSmoothing);
                using (new EditorGUI.DisabledScope(!_s.useSmoothing))
                {
                    _s.smoothTime = EditorGUILayout.Slider(new GUIContent("Smooth Time (s)"), Mathf.Max(0.01f, _s.smoothTime), 0.01f, 1.0f);
                }

                EditorGUILayout.Space(6);
                _s.respectUserNavigation = EditorGUILayout.ToggleLeft(new GUIContent("Pause while navigating"), _s.respectUserNavigation);
                if (_s.respectUserNavigation)
                {
                    _s.resumeAfterSeconds = EditorGUILayout.Slider(new GUIContent("Resume After (s)"), _s.resumeAfterSeconds, 0.1f, 3f);
                }

                EditorGUILayout.Space(6);
                _s.showGizmos = EditorGUILayout.ToggleLeft(new GUIContent("Show LookAt Gizmo"), _s.showGizmos);

                EditorGUILayout.Space(6);
                _s.toggleHotkey = (KeyCode)EditorGUILayout.EnumPopup(new GUIContent("Toggle Hotkey"), _s.toggleHotkey);

                EditorGUILayout.Space(6);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Snap Now"))
                    {
                        Follow(now:true);
                    }
                    if (GUILayout.Button("Align Scene to Target"))
                    {
                        AlignSceneToTarget();
                    }
                    if (GUILayout.Button("Focus Target (F)"))
                    {
                        FocusTarget();
                    }
                }

                if (GUI.changed)
                    Save();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Hotkey: " + _s.toggleHotkey + " to toggle follow on/off.\nTip: Enable 'Pause while navigating' to keep control when orbiting.", MessageType.Info);
        }

        private void OnEditorUpdate()
        {
            if (!_s.enabled) return;
            if (Application.isPlaying && !_s.followInPlayMode) return;

            SceneView sv = SceneView.lastActiveSceneView ?? SceneView.currentDrawingSceneView;
            if (sv == null) return;
            if (_s.onlyWhenSceneViewFocused && (EditorWindow.focusedWindow != sv)) return;
            if (_s.target == null) return;

            // pause while user navigates
            if (_s.respectUserNavigation)
            {
                double dtNav = EditorApplication.timeSinceStartup - _lastUserNavTime;
                if (dtNav < _s.resumeAfterSeconds)
                    return;
            }

            Follow(now:false, sv);
        }

        private void AlignSceneToTarget()
        {
            var sv = SceneView.lastActiveSceneView ?? SceneView.currentDrawingSceneView;
            if (sv == null || _s.target == null) return;

            Vector3 pos; Quaternion rot; Vector3 pivot;
            ComputeDesired(out pos, out rot, out pivot);

            // Snap instantly
            sv.LookAt(pivot, rot, sv.size, true, false);
            sv.Repaint();
        }

        private void FocusTarget()
        {
            var sv = SceneView.lastActiveSceneView ?? SceneView.currentDrawingSceneView;
            if (sv == null || _s.target == null) return;
            sv.Frame(_s.target.GetComponent<Renderer>() ? _s.target.GetComponent<Renderer>().bounds : new Bounds(_s.target.position, Vector3.one * 2f), false);
        }

        private void Follow(bool now = false, SceneView sv = null)
        {
            sv = sv ?? SceneView.lastActiveSceneView ?? SceneView.currentDrawingSceneView;
            if (sv == null || _s.target == null) return;

            Vector3 desiredPos; Quaternion desiredRot; Vector3 desiredPivot;
            ComputeDesired(out desiredPos, out desiredRot, out desiredPivot);

            // current state
            Vector3 curPivot = sv.pivot;
            Quaternion curRot = sv.rotation;

            double t = EditorApplication.timeSinceStartup;
            float dt = Mathf.Clamp((float)(t - _lastTime), 0f, 0.1f);
            _lastTime = t;

            if (now || !_s.useSmoothing)
            {
                sv.pivot = desiredPivot;
                sv.rotation = desiredRot;
            }
            else
            {
                sv.pivot = Vector3.SmoothDamp(curPivot, desiredPivot, ref _vel, _s.smoothTime, Mathf.Infinity, dt);
                sv.rotation = SmoothDamp(curRot, desiredRot, ref _qVel, _s.smoothTime, dt);
            }

            sv.Repaint();
        }

        private void ComputeDesired(out Vector3 desiredCamPos, out Quaternion desiredRot, out Vector3 desiredPivot)
        {
            Transform t = _s.target;
            Vector3 pivot = t.position;

            // rotation
            desiredRot = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.rotation : Quaternion.identity;

            if (_s.matchTargetRotation)
                desiredRot = t.rotation;
            else if (_s.lookAtTarget)
                desiredRot = Quaternion.LookRotation((t.position + _s.lookAtOffset) - GetCandidateCameraPos(t), Vector3.up);

            // position modes
            Vector3 camPos = GetCandidateCameraPos(t);

            desiredCamPos = camPos;
            desiredPivot = _s.lockToPivot ? (t.position + _s.lookAtOffset) : camPos; // if locked to pivot, keep pivot at target; otherwise pivot roughly at camera pos

            // If we want LookAt but pivot should be the lookAt target for better orbiting
            if (_s.lookAtTarget)
            {
                desiredPivot = t.position + _s.lookAtOffset;
            }
        }

        private Vector3 GetCandidateCameraPos(Transform t)
        {
            switch (_s.followMode)
            {
                case FollowMode.OffsetFromTargetForward:
                    return t.position - t.forward * _s.distance + _s.positionOffset;
                case FollowMode.FixedWorldOffset:
                    return t.position + _s.positionOffset;
                case FollowMode.MatchTargetPosition:
                default:
                    return t.position + _s.positionOffset;
            }
        }

        // Quaternion smooth damp adapted from unity wiki-style snippets
        private static Quaternion SmoothDamp(Quaternion rot, Quaternion target, ref Quaternion deriv, float time, float deltaTime)
        {
            if (deltaTime < Mathf.Epsilon) return target;
            // Ensure shortest path
            float dot = Quaternion.Dot(rot, target);
            float multi = dot > 0f ? 1f : -1f;
            target.x *= multi; target.y *= multi; target.z *= multi; target.w *= multi;

            Vector4 result = new Vector4(
                Mathf.SmoothDamp(rot.x, target.x, ref deriv.x, time, Mathf.Infinity, deltaTime),
                Mathf.SmoothDamp(rot.y, target.y, ref deriv.y, time, Mathf.Infinity, deltaTime),
                Mathf.SmoothDamp(rot.z, target.z, ref deriv.z, time, Mathf.Infinity, deltaTime),
                Mathf.SmoothDamp(rot.w, target.w, ref deriv.w, time, Mathf.Infinity, deltaTime)
            ).normalized;

            // update deriv
            float dtInv = 1f / deltaTime;
            deriv.x = (result.x - rot.x) * dtInv;
            deriv.y = (result.y - rot.y) * dtInv;
            deriv.z = (result.z - rot.z) * dtInv;
            deriv.w = (result.w - rot.w) * dtInv;

            Quaternion q = new Quaternion(result.x, result.y, result.z, result.w);
            return q;
        }

        private void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(_s);
                EditorPrefs.SetString(PREF_KEY, json);
            }
            catch { }
        }

        private void Load()
        {
            try
            {
                if (!EditorPrefs.HasKey(PREF_KEY)) return;
                string json = EditorPrefs.GetString(PREF_KEY);
                var loaded = JsonUtility.FromJson<Settings>(json);
                if (loaded != null) _s = loaded;
            }
            catch { }
        }
    }
}
#endif
