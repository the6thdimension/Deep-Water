using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;

namespace Micosmo.SensorToolkit.Experimental {
    public class ProjectiveSphere {

        static Vector3 projPole = Vector3.back;
        static Vector3 planeNormal = Vector3.back;
        
        public static Point Project(Vector3 point) {
            var spherePoint = point.normalized;
            var projDir = spherePoint - projPole;
            var dot = Vector3.Dot(projDir, -planeNormal);
            if (dot == 0f) {
                return Point.Infinity;
            }
            var onPlane = projPole + (2 * projDir) / dot;
            return new Point(onPlane);
        }

        public static Vector3 Unproject(Point point) {
            if (point.IsInfinity) {
                return projPole;
            }
            var x = point.Coords.x;
            var y = point.Coords.y;
            return new Vector3(
                (4*x)/(4+x*x+y*y),
                (4*y)/(4+x*x+y*y),
                -1+(8)/(4+x*x+y*y));
        }

        public struct Point {
            public Vector2 Coords;
            public bool IsInfinity => float.IsInfinity(Coords.x) || float.IsInfinity(Coords.y);
            public Point(Vector2 coords) {
                Coords = coords;
            }
            public static Point Infinity => new Point { Coords = Vector2.positiveInfinity };
            public Vector3 Unproject => Unproject(this);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator Point(Vector3 v) => new Point(v);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator Vector3(Point v) => new Vector3(v.Coords.x, v.Coords.y, 1f);
        }
    }

}