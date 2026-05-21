using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Micosmo.SensorToolkit {

    public class LOSTest3D : BaseLOSTest {

        List<Triangle> triangles = new List<Triangle>();
        List<Triangle> projectedTriangles = new List<Triangle>();
        ComponentCache losColliderOwnerCache;
        SobolSequence3D sobol = new SobolSequence3D();

        QueryTriggerInteraction queryTriggerInteraction => Config.IgnoreTriggerColliders 
            ? QueryTriggerInteraction.Ignore 
            : QueryTriggerInteraction.Collide;

        public override void DrawGizmos() {
            base.DrawGizmos();
            SensorGizmos.PushColor(Color.blue);
            foreach (var triangle in triangles) {
                triangle.DrawGizmos();
            }
            /*foreach (var triangle in projectedTriangles) {
                triangle.DrawGizmos();
            }*/
            SensorGizmos.PopColor();
        }

        protected override void Clear() {
            triangles.Clear();
            projectedTriangles.Clear();
        }

        protected override LOSRayResult TestPoint(Vector3 testPoint) {
            var delta = testPoint - config.Frame.Position;

            var ray = new Ray(config.Frame.Position, delta.normalized);
            RaycastHit hitInfo;
            var result = new LOSRayResult() { OriginPoint = ray.origin, TargetPoint = testPoint, VisibilityMultiplier = 1f };
            if (Physics.Raycast(ray, out hitInfo, delta.magnitude, config.BlocksLineOfSight, queryTriggerInteraction)) {
                // Ray hit something, check that it was the target.
                var isTarget = (hitInfo.rigidbody != null && hitInfo.rigidbody.gameObject == config.InputSignal.Object) 
                    || hitInfo.collider.gameObject == config.InputSignal.Object;

                isTarget = isTarget || (config.OwnedColliders?.Contains(hitInfo.collider) ?? false);
                var losColliderOwner = losColliderOwnerCache.GetComponent<LOSColliderOwner>(config.InputSignal.Object);
                if (losColliderOwner != null) {
                    isTarget = isTarget || losColliderOwner.IsColliderOwner(hitInfo.collider);
                }

                if (!isTarget) {
                    result.RayHit = new RayHit() {
                        IsObstructing = true,
                        Point = hitInfo.point,
                        Normal = hitInfo.normal,
                        Distance = hitInfo.distance,
                        DistanceFraction = hitInfo.distance / delta.magnitude,
                        Collider = hitInfo.collider
                    };
                }
            }
            return result;
        }

        protected override bool IsInsideSignal() => Config.InputSignal.Bounds.Contains(Config.Frame.Position);

        protected override void GenerateTestPoints(List<Vector3> storeIn) {
            if (Config.PointSamplingMethod == PointSamplingMethod.Fast) {
                FastGenerateTestPoints(storeIn);
            } else if (Config.PointSamplingMethod == PointSamplingMethod.Quality) {
                QualityGenerateTestPoints(storeIn);
            }
        }

        void FastGenerateTestPoints(List<Vector3> storeIn) {
            var bounds = Config.InputSignal.Bounds;
            for (int i = 0; i < Config.NumberOfRays; i++) {
                var nextSobol = sobol.Next();
                var random3 = new Vector3(Mathf.Lerp(-1,1, nextSobol.x), Mathf.Lerp(-1, 1, nextSobol.y), Mathf.Lerp(-1, 1, nextSobol.z));
                random3 *= .9f;
                var randomPoint = bounds.center + Vector3.Scale(bounds.extents, random3);
                storeIn.Add(randomPoint);
            }
        }

        void QualityGenerateTestPoints(List<Vector3> storeIn) {
            triangles.Clear();
            projectedTriangles.Clear();

            var bounds = config.InputSignal.Bounds;
            LOSUtils.MapBoundsToTriangles(config.Frame.Position, bounds, triangles);

            if (config.LimitViewAngle) {
                var fov = FOVRange.Of(config.MaxHorizAngle * 2f, config.MaxVertAngle * 2f);
                FOVCuttingPlanes.From(config.Frame, fov).Clip(triangles);
            }
            if (triangles.Count == 0) {
                return;
            }

            foreach (var triangle in triangles) {
                projectedTriangles.Add(triangle.ProjectSphere(config.Frame.Position));
            }

            for (int i = 0; i < config.NumberOfRays; i++) {
                int nAttempts = 0;
                Start:
                
                var nextSobol = sobol.Next();
                var randomPoint = LOSUtils.GetRandomPointInTriangles(projectedTriangles, nextSobol);

                float boundsDist;
                var ray = new Ray(config.Frame.Position, (randomPoint - config.Frame.Position).normalized);
                bounds.IntersectRay(ray, out boundsDist);

                if (boundsDist == 0f) {
                    if (nAttempts < 2) {
                        // Very rarely the random point will be outside the bounds, try again.
                        nAttempts++;
                        goto Start;
                    }
                    // Tried three times and still no good. Ignore this point. Doubt this will ever happen. But don't want to
                    // search forever in case there's a configuration that would cause infinite loops.
                    continue;
                }

                var intBoundsInPoint = ray.origin + ray.direction * boundsDist;
                var intBoundsOutPoint = LOSUtils.RaycastBoundsOutPoint(intBoundsInPoint, (intBoundsInPoint - config.Frame.Position).normalized, bounds);

                var midpoint = (intBoundsOutPoint + intBoundsInPoint) / 2f;
                var penetration = midpoint - intBoundsInPoint;

                if (config.LimitDistance) {
                    penetration = Vector3.ClampMagnitude(penetration, config.MaxDistance / 100f);
                }

                storeIn.Add(intBoundsInPoint + penetration);
            }
        }

        protected override float GetVisibilityScale() {
            var visibilityScale = 1f;
            if (config.LimitDistance) {
                float distance = Mathf.Sqrt((config.InputSignal.Bounds.SqrDistance(config.Frame.Position)));
                visibilityScale *= config.VisibilityByDistance.Evaluate(distance / config.MaxDistance);
            }
            if (config.LimitViewAngle) {
                var coords = AngleUtils.ViewAnglesToBounds(config.Frame, config.InputSignal.Bounds).Abs;
                visibilityScale *= config.VisibilityByHorizAngle.Evaluate(coords.HorizAngle / config.MaxHorizAngle)
                    * config.VisibilityByVertAngle.Evaluate(coords.VertAngle / config.MaxVertAngle);
            }
            return visibilityScale;
        }

        protected override float GetRayVisibilityScale(Vector3 target) {
            var visibilityScale = 1f;
            if (config.LimitDistance) {
                float distance = (config.Frame.Position - target).magnitude;
                visibilityScale *= config.VisibilityByDistance.Evaluate(distance / config.MaxDistance);
            }
            if (config.LimitViewAngle) {
                var coords = AngleUtils.ViewAnglesToPoint(config.Frame, target).Abs;
                visibilityScale *= config.VisibilityByHorizAngle.Evaluate(coords.HorizAngle / config.MaxHorizAngle)
                    * config.VisibilityByVertAngle.Evaluate(coords.VertAngle / config.MaxVertAngle);
            }
            return visibilityScale;
        }
    }

}