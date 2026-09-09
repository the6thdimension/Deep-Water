using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Micosmo.SensorToolkit {
    [System.Serializable]
    public struct ReferenceFrame {
        public Vector3 Position;
        public Vector3 Forward, Right, Up;
        public ReferenceFrame(Vector3 position, Vector3 forward, Vector3 right, Vector3 up) {
            Position = position; Forward = forward; Right = right; Up = up;
        }
        public static ReferenceFrame Identity => new ReferenceFrame { Forward = Vector3.forward, Right = Vector3.right, Up = Vector3.up };
        public static ReferenceFrame Planar(Vector3 position, Vector3 forward, Vector3 right) =>
            new ReferenceFrame(position, forward, right, default);
        public static ReferenceFrame From(Transform transform) =>
            new ReferenceFrame(transform.position, transform.forward, transform.right, transform.up);
        public static ReferenceFrame From(Vector3 position, Quaternion rotation) =>
            new ReferenceFrame(position, rotation * Vector3.forward, rotation * Vector3.right, rotation * Vector3.up);
        public static ReferenceFrame From(Transform transform, Vector2 horizDir) {
            var forward = transform.forward;
            if (horizDir == Vector2.zero) {
                horizDir = Vector2.right;
            }
            horizDir = horizDir.normalized;
            var right = horizDir.x * transform.right + horizDir.y * transform.up;
            var up = Vector3.Cross(forward, right);
            return new ReferenceFrame(transform.position, forward, right, up);
        }
        public ViewAngles AngleTo(Bounds bounds) => AngleUtils.ViewAnglesToBounds(this, bounds);
        public SphericalCoords SphericalCoordsTo(Bounds bounds) => AngleUtils.SphericalCoordsToBounds(this, bounds);
        public ViewAngles AngleTo(Vector3 target) => AngleUtils.ViewAnglesToPoint(this, target);
        public SphericalCoords SphericalCoordsTo(Vector3 target) => AngleUtils.SphericalCoordsToPoint(this, target);
        public ReferenceFrame Push(Vector3 nextPosition, Vector3 nextForward) {
            // Minimizes twist rotation to push the frame forward and align with a new forward direction
            var n0 = Up;
            var t0 = Forward;
            var t1 = nextForward.normalized;
            var v1 = nextPosition - Position;
            var c1 = v1.sqrMagnitude;
            var n0_l = n0 - (2 / c1) * Vector3.Dot(v1, n0) * v1;
            var t0_l = t0 - (2 / c1) * Vector3.Dot(v1, t0) * v1;
            var v2 = t1 - t0_l;
            var c2 = v2.sqrMagnitude;
            var n1 = n0_l - (2 / c2) * Vector3.Dot(v2, n0_l) * v2;
            return From(nextPosition, Quaternion.LookRotation(t1, n1));
        }
        public Vector3 LocalToWorld(Vector3 localPosition) => Position + (Right * localPosition.x) + (Up * localPosition.y) + (Forward * localPosition.z);
        public Vector3 WorldToLocal(Vector3 worldPosition) {
            var delta = worldPosition - Position;
            return new Vector3(Vector3.Dot(delta, Right), Vector3.Dot(delta, Up), Vector3.Dot(delta, Forward));
        }
        public void DrawGizmos(float size) {
            var length = 10f * size;
            var thickness = 2f * size;
            var that = this;
            SensorGizmos.WithColor(Color.blue, () => SensorGizmos.ThickLineNoZTest(that.Position, that.Position + that.Forward * length, thickness));
            SensorGizmos.WithColor(Color.red, () => SensorGizmos.ThickLineNoZTest(that.Position, that.Position + that.Right * length, thickness));
            SensorGizmos.WithColor(Color.green, () => SensorGizmos.ThickLineNoZTest(that.Position, that.Position + that.Up * length, thickness));
        }
    }

    [System.Serializable]
    public struct ViewAngles {
        public float HorizAngle;
        public float VertAngle;
        public ViewAngles Abs => new ViewAngles(Mathf.Abs(HorizAngle), Mathf.Abs(VertAngle));
        public ViewAngles(float horizAngle, float vertAngle) {
            HorizAngle = horizAngle; VertAngle = vertAngle;
        }
        public float GetCentralAngle() {
            var horiz = HorizAngle * Mathf.Deg2Rad;
            var vert = VertAngle * Mathf.Deg2Rad;
            return Mathf.Rad2Deg * Mathf.Acos(Mathf.Cos(horiz) * Mathf.Cos(vert));
        }
        public Vector3 ToCartesian(float distance) {
            var horiz = HorizAngle * Mathf.Deg2Rad;
            var vert = VertAngle * Mathf.Deg2Rad;
            var cosVert = Mathf.Cos(vert);
            var sinVert = Mathf.Sin(vert);
            return new Vector3(cosVert * Mathf.Sin(horiz), sinVert, cosVert * Mathf.Cos(horiz)) * distance;
        }
    }

    [System.Serializable]
    public struct SphericalCoords {
        public ViewAngles Angles;
        public float Radius;
        public SphericalCoords(ViewAngles angles, float radius) {
            Angles = angles; Radius = radius;
        }
        public Vector3 ToCartesian() => Angles.ToCartesian(Radius);
    }

    public class AngleUtils {

        public static float PlanarAngleToPoint(ReferenceFrame frame, Vector3 target) {
            var delta = (target - frame.Position);
            var proj = Vector3.Dot(delta, frame.Right);
            var dist = Vector3.Dot(delta, frame.Forward);
            var tri = new CircleInscribedTriangle(new Vector2(proj, dist));
            return tri.GetAngle();
        }

        public static float PlanarAngleToBounds(ReferenceFrame frame, Bounds bounds) {
            var center = bounds.center;
            var extents = bounds.extents;
            CircleInscribedTriangle cwTri = default;
            CircleInscribedTriangle acwTri = default;

            for (int i = 0; i < 8; i++) {
                var xSign = (i & 1) == 0 ? 1 : -1;
                var ySign = (i & 2) == 0 ? 1 : -1;
                var zSign = (i & 4) == 0 ? 1 : -1;
                var point = center + new Vector3(extents.x * xSign, extents.y * ySign, extents.z * zSign);

                var delta = (point - frame.Position);
                var proj = Vector3.Dot(delta, frame.Right);
                var dist = Vector3.Dot(delta, frame.Forward);

                var tri = new CircleInscribedTriangle(new Vector2(proj, dist));

                cwTri = CircleInscribedTriangle.NearestClockwise(cwTri, tri);
                acwTri = CircleInscribedTriangle.NearestAntiClockwise(acwTri, tri);
            }

            var a1 = cwTri.GetAngle();
            var a2 = acwTri.GetAngle();

            if (a1 >= 0 && a2 <= 0 && (a1 - a2 < 180f)) {
                return 0f;
            }

            var nearest = (Mathf.Abs(a1) < Mathf.Abs(a2)) ? a1 : a2;
            return nearest;
        }

        public static ViewAngles ViewAnglesToPoint(ReferenceFrame frame, Vector3 target) {
            var horizAngle = PlanarAngleToPoint(frame, target);
            var toTarget = target - frame.Position;
            var projToTarget = toTarget - (Vector3.Dot(toTarget, frame.Up) * frame.Up);
            var vertAngle = PlanarAngleToPoint(ReferenceFrame.Planar(frame.Position, projToTarget.normalized, frame.Up), target);
            return new ViewAngles(horizAngle, vertAngle);
        }

        public static SphericalCoords SphericalCoordsToPoint(ReferenceFrame frame, Vector3 target) {
            var angles = ViewAnglesToPoint(frame, target);
            var radius = Vector3.Distance(frame.Position, target);
            return new SphericalCoords(angles, radius);
        }

        public static ViewAngles ViewAnglesToBounds(ReferenceFrame frame, Bounds bounds) {
            var horizAngle = PlanarAngleToBounds(frame, bounds);
            var toTarget = (bounds.center - frame.Position);
            var projToTarget = toTarget - (Vector3.Dot(toTarget, frame.Up) * frame.Up);
            var vertAngle = PlanarAngleToBounds(ReferenceFrame.Planar(frame.Position, projToTarget.normalized, frame.Up), bounds);
            return new ViewAngles(horizAngle, vertAngle);
        }

        public static SphericalCoords SphericalCoordsToBounds(ReferenceFrame frame, Bounds bounds) {
            var angles = ViewAnglesToBounds(frame, bounds);
            var radius = Mathf.Sqrt(bounds.SqrDistance(frame.Position));
            return new SphericalCoords(angles, radius);
        }

        struct CircleInscribedTriangle {
            Vector2 coords;
            bool isValid => coords != Vector2.zero;
            public CircleInscribedTriangle(Vector2 coords) {
                this.coords = coords.normalized;
            }
            public float GetAngle() => -Mathf.Atan2(-coords.x, coords.y) * Mathf.Rad2Deg;
            public static CircleInscribedTriangle NearestClockwise(CircleInscribedTriangle tri1, CircleInscribedTriangle tri2) {
                if (!tri1.isValid) {
                    return tri2;
                } else if (!tri2.isValid) {
                    return tri1;
                }
                return tri1.IsNearestClockwise(tri2) ? tri1 : tri2;
            }
            public static CircleInscribedTriangle NearestAntiClockwise(CircleInscribedTriangle tri1, CircleInscribedTriangle tri2) {
                if (!tri1.isValid) {
                    return tri2;
                } else if (!tri2.isValid) {
                    return tri1;
                }
                return tri1.IsNearestClockwise(tri2) ? tri2 : tri1;
            }
            int GetClockwiseQuadrant() => coords.x > 0 ? (coords.y > 0 ? 0 : 1) : (coords.y > 0 ? 3 : 2);
            int GetAntiClockwiseQuadrant() => 3 - GetClockwiseQuadrant();
            bool IsNearestClockwise(CircleInscribedTriangle other) {
                var myQuad = GetClockwiseQuadrant();
                var otherQuad = other.GetClockwiseQuadrant();
                if (myQuad < otherQuad) {
                    return true;
                } else if (myQuad > otherQuad) {
                    return false;
                }
                switch (myQuad) {
                    case 0:
                        return coords.x < other.coords.x;
                    case 1:
                        return coords.y > other.coords.y;
                    case 2:
                        return coords.x > other.coords.x;
                    case 3:
                        return coords.y < other.coords.y;
                    default:
                        return false;
                }
            }
        }
    }

}