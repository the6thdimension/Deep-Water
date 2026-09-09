using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Micosmo.SensorToolkit {

    public struct CuttingPlane {
        public Vector3 Point;
        public Vector3 Normal;

        public void Cut(List<Triangle> triangleList) {
            for (int i = triangleList.Count - 1; i >= 0; i--) {
                var tri = triangleList[i];
                Triangle slice1, slice2;
                var nSlices = tri.Slice(Point, Normal, out slice1, out slice2);
                if (nSlices == 0) {
                    triangleList.RemoveAt(i);
                } else {
                    triangleList[i] = slice1;
                    if (nSlices > 1) {
                        triangleList.Add(slice2);
                    }
                }
            }
        }

        public void Cut(List<Edge2D> edgeList) {
            for (int i = edgeList.Count - 1; i >= 0; i--) {
                var edge = edgeList[i];
                Edge2D slicedEdge;
                var nSlices = edge.Slice(Point, ((Vector2)Normal).normalized, out slicedEdge);
                if (nSlices == 0) {
                    edgeList.RemoveAt(i);
                } else {
                    edgeList[i] = slicedEdge;
                }
            }
        }
    }

    public struct FOVCuttingPlanes {
        CuttingPlane rightPlane, leftPlane, topPlane, bottomPlane;

        public static FOVCuttingPlanes From(ReferenceFrame frame, FOVRange fov) {
            var horizRightRot = Quaternion.AngleAxis(fov.HorizAngle / 2f, frame.Up);
            var horizLeftRot = Quaternion.Inverse(horizRightRot);
            var vertUpRot = Quaternion.AngleAxis(fov.VertAngle / 2f, frame.Right);
            var vertDownRot = Quaternion.Inverse(vertUpRot);
            return new FOVCuttingPlanes {
                rightPlane = new CuttingPlane { Point = frame.Position, Normal = horizRightRot * (-frame.Right) },
                leftPlane = new CuttingPlane { Point = frame.Position, Normal = horizLeftRot * frame.Right },
                topPlane = new CuttingPlane { Point = frame.Position, Normal = vertUpRot * frame.Up },
                bottomPlane = new CuttingPlane { Point = frame.Position, Normal = vertDownRot * (-frame.Up) }
            };
        }
        public void Clip(List<Triangle> triangles) {
            rightPlane.Cut(triangles);
            leftPlane.Cut(triangles);
            topPlane.Cut(triangles);
            bottomPlane.Cut(triangles);
        }
    }

    public struct FOVCuttingPlanes2D {
        CuttingPlane rightPlane, leftPlane;

        public static FOVCuttingPlanes2D From(ReferenceFrame frame, FOVRange2D fov) {
            var rightRot = Quaternion.AngleAxis(fov.Angle / 2f, Vector3.back);
            var leftRot = Quaternion.Inverse(rightRot);
            return new FOVCuttingPlanes2D {
                rightPlane = new CuttingPlane { Point = frame.Position, Normal = rightRot * (-frame.Right) },
                leftPlane = new CuttingPlane { Point = frame.Position, Normal = leftRot * (frame.Right) }
            };
        }
        public void Clip(List<Edge2D> edges) {
            rightPlane.Cut(edges);
            leftPlane.Cut(edges);
        }
    }

}