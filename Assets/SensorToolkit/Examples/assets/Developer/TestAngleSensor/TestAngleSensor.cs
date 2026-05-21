using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Micosmo.SensorToolkit.Example.Developer {
    public class TestAngleSensor : MonoBehaviour {
        public static Rect GUIRect = new Rect(10, 10, 150, 100);

        [Header("Prefabs")]
        public ScreenRay ScreenRayPrefab;
        public TargetGraphics TargetGraphicsPrefab;
        [Header("References")]
        public Sensor InputSensor;
        [Header("Attributes")]
        public FOVRange FOV;
        public AngleEnumerator.AngleMethodType AngleMethod;
        public AngleEnumerator.SortByType SortBy;
        public float VerticalAnglePenalty = 0.5f;
        public float DistancePenalty = 0.5f;
        [Header("Runtime")]
        public GameObject Target;

        ScreenRay screenRay;
        TargetGraphics targetGraphics;
        TargetGraphics nextTargetGraphics;
        AngleEnumerator angleEnumerator = AngleEnumerator.Create();

        void Awake() {
            screenRay = Instantiate(ScreenRayPrefab.gameObject).GetComponent<ScreenRay>();
            targetGraphics = Instantiate(TargetGraphicsPrefab.gameObject).GetComponent<TargetGraphics>();
            targetGraphics.SetStyle(0);
            nextTargetGraphics = Instantiate(TargetGraphicsPrefab.gameObject).GetComponent<TargetGraphics>();
            nextTargetGraphics.SetStyle(1);
        }

        void Update() {
            if (Target) { 
                transform.LookAt(Target.transform); 
            }
            targetGraphics.Target = Target;
            if (Target) {
                nextTargetGraphics.Target = GetSuccessor(Target.transform.position, VerticalAnglePenalty, DistancePenalty, Target);
            }
        }

        void ResetState() {
            Target = null;
            targetGraphics.Target = null;
            nextTargetGraphics.Target = null;
            screenRay.Clear();
            transform.rotation = Quaternion.identity;
        }

        void TargetNearest() {
            var results = GetDetectionsByCentralAngle();
            Target = results.Count > 0 ? results[0].Object : null;
        }

        void TargetNext() {
            if (Target) {
                Target = GetSuccessor(Target.transform.position, VerticalAnglePenalty, DistancePenalty, Target);
            }
        }
        public GameObject GetSuccessor(Vector3 refPoint, float vertAnglePenalty, float distPenalty, GameObject ignore = null) {
            if (screenRay.IsZero) {
                return null;
            }
            var frame = ReferenceFrame.From(transform, screenRay.Direction);
            var dirCoords = AngleUtils.ViewAnglesToPoint(frame, refPoint);
            var results = GetDetectionsByPlanarAngle();
            var bestGrade = float.MaxValue;
            GameObject bestObject = null;

            foreach (var result in results) {
                if (ReferenceEquals(result.Object, ignore)) {
                    continue;
                }
                var deltaHoriz = result.Angles.HorizAngle - dirCoords.HorizAngle;
                if (deltaHoriz < 0) {
                    deltaHoriz += 360f;
                }
                var grade = deltaHoriz + (vertAnglePenalty * Mathf.Abs(result.Angles.VertAngle)) + (distPenalty * result.Distance);
                if (grade < bestGrade) {
                    bestGrade = grade;
                    bestObject = result.Object;
                }
            }

            return bestObject;
        }

        public List<AngleEnumerator.AngleResult> GetDetectionsByCentralAngle() {
            var frame = ReferenceFrame.From(transform, Vector2.right);
            angleEnumerator.AngleMethod = AngleEnumerator.AngleMethodType.BoundingBox;
            angleEnumerator.SortBy = AngleEnumerator.SortByType.CentralAngle;
            angleEnumerator.Calculate(frame, FOV, InputSensor.GetSignals());
            return angleEnumerator.results;
        }
        public List<AngleEnumerator.AngleResult> GetDetectionsByPlanarAngle() {
            var frame = ReferenceFrame.From(transform, screenRay.Direction);
            angleEnumerator.AngleMethod = AngleEnumerator.AngleMethodType.Center;
            angleEnumerator.SortBy = AngleEnumerator.SortByType.HorizontalAngle;
            angleEnumerator.Calculate(frame, FOV, InputSensor.GetSignals());
            return angleEnumerator.results;
        }

        void OnDrawGizmosSelected() {
            if (!Application.isPlaying) {
                return;
            }
            var frame = ReferenceFrame.From(transform, screenRay.IsZero ? Vector2.right : screenRay.Direction);
            frame.DrawGizmos(1);
            FOV.DrawGizmos(frame);
            angleEnumerator.DrawGizmos();
        }

        void OnGUI() {
#if UNITY_EDITOR
            GUI.skin.label.fontSize = 20;
            GUILayout.BeginArea(GUIRect, EditorStyles.helpBox);
            if (GUILayout.Button("Reset")) {
                ResetState();
            }
            if (GUILayout.Button("Target Nearest")) {
                TargetNearest();
            }
            if (GUILayout.Button("Target Next")) {
                TargetNext();
            }
            GUILayout.EndArea();
            GUI.skin = null;
#endif
        }
    }
}