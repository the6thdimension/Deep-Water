using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Micosmo.SensorToolkit.Experimental;

namespace Micosmo.SensorToolkit.Example.Developer {
    public class TestProjectiveSphere : MonoBehaviour {

        public List<Polygon> Polygons = new List<Polygon>();

        void OnDrawGizmos() {
            SensorGizmos.PushColor(Color.blue);
            SensorGizmos.SphereGizmo(transform.position, 1f);
            SensorGizmos.PopColor();

            foreach (var polygon in Polygons) {
                polygon.DrawGizmos(this);
            }
        }

        void ProjectPointGizmo(Vector3 point, float pointSize = 0.02f) {
            var spherePoint = transform.InverseTransformPoint(point).normalized;
            var projPoint = ProjectiveSphere.Project(spherePoint);
            SensorGizmos.PushColor(Color.red);
            SensorGizmos.SphereGizmo(transform.TransformPoint(spherePoint), pointSize);
            SensorGizmos.PopColor();
            if (!projPoint.IsInfinity) {
                var worldProjPoint = transform.TransformPoint(projPoint);
                SensorGizmos.PushColor(Color.green);
                SensorGizmos.SphereGizmo(worldProjPoint, pointSize);
                SensorGizmos.PopColor();
            }
        }

        void ProjectPolylineGizmo(List<Vector3> polyline, bool showProjLines, float pointSize = 0.02f) {
            var projPolyline = new List<ProjectiveSphere.Point>();
            foreach (var pt in polyline) {
                var localPoint = transform.InverseTransformPoint(pt);
                projPolyline.Add(ProjectiveSphere.Project(localPoint));
            }
            SensorGizmos.PushColor(Color.red);
            foreach (var pt in projPolyline) {
                var spherePoint = transform.TransformPoint(pt.Unproject);
                SensorGizmos.SphereGizmo(spherePoint, pointSize);
            }
            var isClockWise = Vector3.Cross((Vector3)projPolyline[1] - (Vector3)projPolyline[0], (Vector3)projPolyline[2] - (Vector3)projPolyline[1]).z > 0;
            SensorGizmos.PushColor(isClockWise ? Color.green : Color.yellow);
            SensorGizmos.Polyline(2f, projPolyline.Select(pt => transform.TransformPoint(pt)).ToArray());
            SensorGizmos.PopColor();
            SensorGizmos.PopColor();
            if (showProjLines) {
                SensorGizmos.PushColor(Color.white);
                for (int i = 0; i < polyline.Count; i++) {
                    var spherePoint = transform.TransformPoint(projPolyline[i].Unproject);
                    var pt = transform.TransformPoint(projPolyline[i]);
                    SensorGizmos.ThickLineNoZTest(spherePoint, pt, 2f);
                }
                SensorGizmos.PopColor();
            }
        }

        [System.Serializable]
        public struct Polygon {
            public Transform Object;
            public bool FaceSphere;
            public bool IsBox;
            public bool ShowProjectionLines;
            public float CircleRadius;
            public int CircleResolution;
            public Vector2 BoxSize;

            public List<Vector3> GetPolyline(TestProjectiveSphere sphere) {
                var frame = FaceSphere
                    ? ReferenceFrame.From(Object.position, Quaternion.LookRotation((sphere.transform.position - Object.position).normalized, Object.up))
                    : ReferenceFrame.From(Object);
                var points = new List<Vector3>();
                
                if (IsBox) {
                    var up = Vector3.Cross(frame.Right, frame.Forward).normalized;
                    var pt1 = frame.Position + frame.Right * BoxSize.x - up * BoxSize.y;
                    var pt2 = frame.Position - frame.Right * BoxSize.x - up * BoxSize.y;
                    var pt3 = frame.Position - frame.Right * BoxSize.x + up * BoxSize.y;
                    var pt4 = frame.Position + frame.Right * BoxSize.x + up * BoxSize.y;
                    AddStraightPolyline(pt1, pt2, points);
                    AddStraightPolyline(pt2, pt3, points);
                    AddStraightPolyline(pt3, pt4, points);
                    AddStraightPolyline(pt4, pt1, points);
                } else {
                    for (int i = 0; i < CircleResolution; i++) {
                        var angle = 360f * ((float)i / CircleResolution);
                        var dir = Quaternion.AngleAxis(angle, frame.Forward) * frame.Right;
                        points.Add(frame.Position + dir * CircleRadius);
                    }
                }
                
                points.Add(points[0]);
                return points;
            }
            
            void AddStraightPolyline(Vector3 start, Vector3 end, List<Vector3> polyline) {
                var step = (end - start) / CircleResolution;
                for (int i = 0; i < CircleResolution; i++) {
                    polyline.Add(start + step * i);
                }
            }
            
            public void DrawGizmos(TestProjectiveSphere sphere) {
                var polyline = GetPolyline(sphere);
                SensorGizmos.Polyline(2f, polyline.ToArray());
                sphere.ProjectPolylineGizmo(polyline, ShowProjectionLines);
            }
        }
    }
}