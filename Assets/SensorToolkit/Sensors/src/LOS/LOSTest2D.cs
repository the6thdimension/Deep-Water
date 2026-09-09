using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Micosmo.SensorToolkit {

    public class LOSTest2D : BaseLOSTest {

        List<Edge2D> edges = new List<Edge2D>();
        List<Edge2D> projectedEdges = new List<Edge2D>();
        ComponentCache losColliderOwnerCache;
        SobolSequence3D sobol = new SobolSequence3D();

        public override void DrawGizmos() {
            base.DrawGizmos();
            SensorGizmos.PushColor(Color.blue);
            foreach (var edge in edges) {
                edge.DrawGizmos(config.Frame.Position.z);
            }
            /*foreach (var edge in projectedEdges) {
                edge.DrawGizmos(Config.Origin.z);
            }*/
            SensorGizmos.PopColor();
        }

        protected override void Clear() {
            edges.Clear();
            projectedEdges.Clear();
        }

        protected override LOSRayResult TestPoint(Vector3 testPoint) {
            var saveQHT = Physics2D.queriesHitTriggers;
            Physics2D.queriesHitTriggers = !config.IgnoreTriggerColliders;
            var result = DoTest(testPoint);
            Physics2D.queriesHitTriggers = saveQHT;
            return result;
        }

        LOSRayResult DoTest(Vector3 testPoint) {
            var delta = (Vector2)testPoint - (Vector2)config.Frame.Position;

            var ray = new Ray(config.Frame.Position, delta.normalized);
            var result = new LOSRayResult() { OriginPoint = ray.origin, TargetPoint = testPoint, VisibilityMultiplier = 1f };
            var hitInfo = Physics2D.Raycast(ray.origin, ray.direction, delta.magnitude, config.BlocksLineOfSight);
            if (hitInfo.collider != null) {
                // Ray hit something, check that it was the target.
                var isTarget = (hitInfo.rigidbody != null && hitInfo.rigidbody.gameObject == config.InputSignal.Object) 
                    || hitInfo.collider.gameObject == config.InputSignal.Object;

                isTarget = isTarget || config.OwnedCollider2Ds.Contains(hitInfo.collider);
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
                        Collider2D = hitInfo.collider
                    };
                }
            }
            return result;
        }

        protected override bool IsInsideSignal() {
            var origin = Config.Frame.Position;
            var bounds = Config.InputSignal.Bounds;
            origin.Set(origin.x, origin.y, bounds.center.z);
            return bounds.Contains(origin);
        }

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
                var random3 = new Vector3(Mathf.Lerp(-1, 1, nextSobol.x), Mathf.Lerp(-1, 1, nextSobol.y), Mathf.Lerp(-1, 1, nextSobol.z));
                random3 *= .9f;
                var randomPoint = bounds.center + Vector3.Scale(bounds.extents, random3);
                storeIn.Add(randomPoint);
            }
        }

        void QualityGenerateTestPoints(List<Vector3> storeIn) {
            edges.Clear();
            projectedEdges.Clear();

            var bounds = config.InputSignal.Bounds;
            bounds.center = (Vector2)bounds.center;
            LOSUtils.MapBoundsToEdges(config.Frame.Position, bounds, edges);

            if (config.LimitViewAngle) {
                var fov = FOVRange2D.Of(config.MaxHorizAngle * 2f);
                FOVCuttingPlanes2D.From(Config.Frame, fov).Clip(edges);
            }
            if (edges.Count == 0) {
                return;
            }

            foreach (var edge in edges) {
                projectedEdges.Add(edge.ProjectCircle(config.Frame.Position));
            }

            for (int i = 0; i < config.NumberOfRays; i++) {
                int nAttempts = 0;
                Start:

                var nextSobol = sobol.Next();
                var randomPoint = LOSUtils.GetRandomPointOnEdges(projectedEdges, nextSobol);

                float boundsDist;
                var ray = new Ray((Vector2)config.Frame.Position, ((Vector2)(randomPoint - config.Frame.Position)).normalized);
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

                var intBoundsInPoint = ray.origin + ray.direction * boundsDist + new Vector3(0f, 0f, config.Frame.Position.z);
                var intBoundsOutPoint = LOSUtils.RaycastBoundsOutPoint(intBoundsInPoint, (intBoundsInPoint - Config.Frame.Position).normalized, bounds);

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
                var bounds = config.InputSignal.Bounds;
                bounds.center.Set(bounds.center.x, bounds.center.y, config.Frame.Position.z);
                float distance = Mathf.Sqrt((bounds.SqrDistance(config.Frame.Position)));
                visibilityScale *= config.VisibilityByDistance.Evaluate(distance / config.MaxDistance);
            }
            if (config.LimitViewAngle) {
                var horizAngle = Mathf.Abs(AngleUtils.PlanarAngleToBounds(config.Frame, Config.InputSignal.Bounds));
                visibilityScale *= config.VisibilityByHorizAngle.Evaluate(horizAngle / config.MaxHorizAngle);
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
                var horizAngle = Mathf.Abs(AngleUtils.PlanarAngleToPoint(config.Frame, target));
                visibilityScale *= config.VisibilityByHorizAngle.Evaluate(horizAngle / config.MaxHorizAngle);
            }
            return visibilityScale;
        }
    }

}
