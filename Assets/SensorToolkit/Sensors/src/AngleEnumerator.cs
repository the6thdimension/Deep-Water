using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Micosmo.SensorToolkit {

    public struct AngleEnumerator {
        public enum AngleMethodType { Center, Origin, BoundingBox }
        public enum SortByType { HorizontalAngle, CentralAngle }

        public AngleMethodType AngleMethod;
        public SortByType SortBy;

        public List<AngleResult> results { get; private set; }

        public static AngleEnumerator Create() => new AngleEnumerator {
            results = new List<AngleResult>()
        };

        public void Clear() {
            results.Clear();
        }

        public void Calculate(ReferenceFrame frame, FOVRange fov, List<Signal> signals) {
            Clear();
            fov.Distance *= fov.Distance;
            foreach (var signal in signals) {
                var angles =
                    AngleMethod == AngleMethodType.Origin ? frame.AngleTo(signal.Object.transform.position) :
                    AngleMethod == AngleMethodType.Center ? frame.AngleTo(signal.Bounds.center) :
                    AngleMethod == AngleMethodType.BoundingBox ? frame.AngleTo(signal.Bounds) :
                    default;
                var quadrance = signal.Bounds.SqrDistance(frame.Position);
                if (!fov.Contains(angles, quadrance)) {
                    continue;
                }
                var result = new AngleResult {
                    Object = signal.Object,
                    Angles = angles,
                    CentralAngle = angles.GetCentralAngle(),
                    Distance = quadrance
                };
                results.Add(result);
            }
            if (SortBy == SortByType.HorizontalAngle) {
                results.Sort(AngleResult.CompareHorizAngle);
            } else if (SortBy == SortByType.CentralAngle) {
                results.Sort(AngleResult.CompareCentralAngle);
            }
        }

        public void DrawGizmos() {
            SensorGizmos.PushColor(Color.black);
            int i = 0;
            foreach (var result in results) {
                SensorGizmos.Label(result.Object.transform.position, $"Index: {i}\n({result.Angles.HorizAngle.ToString("N1")},{result.Angles.VertAngle.ToString("N1")})");
                i++;
            }
            SensorGizmos.PopColor();
        }

        public struct AngleResult {
            public GameObject Object;
            public ViewAngles Angles;
            public float CentralAngle;
            public float Distance;
            public static int CompareCentralAngle(AngleResult r1, AngleResult r2) {
                var angleDiff = r1.CentralAngle - r2.CentralAngle;
                if (angleDiff != 0f) {
                    return angleDiff > 0f ? 1 : -1;
                }
                var distanceDiff = r1.Distance - r2.Distance;
                if (distanceDiff != 0f) {
                    return distanceDiff > 0f ? 1 : -1;
                }
                return 0;
            }
            public static int CompareHorizAngle(AngleResult r1, AngleResult r2) {
                //var a1 = r1.Coords.HorizAngle >= 0 ? r1.Coords.HorizAngle : 360f + r1.Coords.HorizAngle;
                //var a2 = r2.Coords.HorizAngle >= 0 ? r2.Coords.HorizAngle : 360f + r2.Coords.HorizAngle;
                var a1 = r1.Angles.HorizAngle;
                var a2 = r2.Angles.HorizAngle;
                var angleDiff = a1 - a2;
                if (angleDiff != 0f) {
                    return angleDiff > 0f ? 1 : -1;
                }
                var distanceDiff = r1.Distance - r2.Distance;
                if (distanceDiff != 0f) {
                    return distanceDiff > 0f ? 1 : -1;
                }
                return 0;
            }
        }
    }

}

