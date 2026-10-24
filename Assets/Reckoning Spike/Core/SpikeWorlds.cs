using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace DeepWater.Reckoning.Spike
{
    /// <summary>A way of moving the ships one tick, given this tick's sea loads.</summary>
    public interface ISpikeWorld : IDisposable
    {
        string Name { get; }
        NativeArray<ShipState> Ships { get; }
        void Step(SpikeSeaLoads loads, float dt, float gravity);
        /// <summary>Ticks on which some pair of hulls was within 5 cm or overlapping.</summary>
        int ContactTicks { get; }
        /// <summary>Deepest overlap seen, m.</summary>
        float MaxPenetrationM { get; }
    }

    public static class SpikeContacts
    {
        public const float ContactBandM = 0.05f;

        /// <summary>
        /// Closest approach of hull j to hull i from Unity Physics' collider distance query (negative
        /// = overlap). The normal is oriented from i toward j (checked against the centres, so a sign
        /// convention misread cannot pull hulls together).
        /// </summary>
        public static bool Closest(in BlobAssetReference<Collider> ci, in RigidTransform ti,
            in BlobAssetReference<Collider> cj, in RigidTransform tj, float maxDistance,
            out float distance, out float3 pointWorld, out float3 normalWorld)
        {
            var input = new ColliderDistanceInput(cj, maxDistance, math.mul(math.inverse(ti), tj));
            if (!ci.Value.CalculateDistance(input, out DistanceHit hit))
            {
                distance = maxDistance; pointWorld = float3.zero; normalWorld = float3.zero;
                return false;
            }
            distance = hit.Distance;
            pointWorld = math.transform(ti, hit.Position);
            normalWorld = math.rotate(ti.rot, hit.SurfaceNormal);
            if (math.dot(normalWorld, tj.pos - ti.pos) < 0f) normalWorld = -normalWorld;
            return true;
        }

        public static BlobAssetReference<Collider> Hull(in SpikeHull h) => BoxCollider.Create(
            new BoxGeometry { Center = float3.zero, Orientation = quaternion.identity,
                              Size = new float3(h.BeamM, h.HeightM, h.LengthM), BevelRadius = 0.05f },
            CollisionFilter.Default);
    }

    // ───────────────────────────── Variant A ─────────────────────────────

    /// <summary>
    /// Variant A: Unity Physics moves the ships (immediate mode, single thread, Burst). Sea loads are
    /// applied as impulses before each step. Unity Physics has one mass per body, so added mass is
    /// the mean of the hull's three fractions (and the same for inertia); gravity is scaled back
    /// (GravityFactor) so weight stays m g and the hull still floats at its design draft.
    /// </summary>
    public sealed class SpikeWorldA : ISpikeWorld
    {
        public string Name => "A · Unity Physics moves ships (averaged added mass)";
        public NativeArray<ShipState> Ships => _ships;
        public int ContactTicks => _contactTicks;
        public float MaxPenetrationM => _maxPenetration;

        PhysicsWorld _world;
        SimulationContext _context;
        NativeReference<int> _staticChanged;
        NativeArray<BlobAssetReference<Collider>> _colliders;
        NativeArray<ShipState> _ships;
        NativeArray<float> _stats;
        int _contactTicks;
        float _maxPenetration;

        public SpikeWorldA(NativeArray<SpikeHull> hulls, NativeArray<ShipState> initial, float dt, float gravity)
        {
            int n = hulls.Length;
            _ships = new NativeArray<ShipState>(initial, Allocator.Persistent);
            _colliders = new NativeArray<BlobAssetReference<Collider>>(n, Allocator.Persistent);
            _stats = new NativeArray<float>(2, Allocator.Persistent);
            _world = new PhysicsWorld(0, n, 0);
            NativeArray<RigidBody> bodies = _world.Bodies;
            NativeArray<MotionData> datas = _world.MotionDatas;
            NativeArray<MotionVelocity> velocities = _world.MotionVelocities;
            for (int i = 0; i < n; i++)
            {
                SpikeHull h = hulls[i];
                ShipState s = initial[i];
                _colliders[i] = SpikeContacts.Hull(h);
                var t = new RigidTransform(s.Rotation, s.Position);
                bodies[i] = new RigidBody { Collider = _colliders[i], WorldFromBody = t, Entity = Entity.Null, Scale = 1f };
                datas[i] = new MotionData { WorldFromMotion = t, BodyFromMotion = RigidTransform.identity };
                float massScale = 1f + h.MeanAddedMass, inertiaScale = 1f + h.MeanAddedInertia;
                velocities[i] = new MotionVelocity
                {
                    LinearVelocity = s.LinearVelocity,
                    AngularVelocity = math.rotate(math.inverse(s.Rotation), s.AngularVelocity),
                    InverseInertia = 1f / (h.Inertia * inertiaScale),
                    InverseMass = 1f / (h.MassKg * massScale),
                    AngularExpansionFactor = _colliders[i].Value.MassProperties.AngularExpansionFactor,
                    GravityFactor = 1f / massScale,
                };
            }
            float3 g = new float3(0f, -gravity, 0f);
            _world.CollisionWorld.BuildBroadphase(ref _world, dt, g);
            _staticChanged = new NativeReference<int>(0, Allocator.Persistent);
            _context = new SimulationContext();
            _context.Reset(Input(dt, g));
        }

        SimulationStepInput Input(float dt, float3 g) => new SimulationStepInput
        {
            World = _world, TimeStep = dt, Gravity = g, NumSubsteps = 1, NumSolverIterations = 4,
            SynchronizeCollisionWorld = true,
            SolverStabilizationHeuristicSettings = Solver.StabilizationHeuristicSettings.Default,
            HaveStaticBodiesChanged = _staticChanged.AsReadOnly(),
        };

        public void Step(SpikeSeaLoads loads, float dt, float gravity)
        {
            new StepJob
            {
                Input = Input(dt, new float3(0f, -gravity, 0f)), Context = _context, Loads = loads.ShipLoads,
                Colliders = _colliders, Ships = _ships, Stats = _stats, Dt = dt,
            }.Run();
            if (_stats[1] < SpikeContacts.ContactBandM) _contactTicks++;
            _maxPenetration = math.max(_maxPenetration, -_stats[1]);
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
        struct StepJob : IJob
        {
            public SimulationStepInput Input;
            public SimulationContext Context;
            [ReadOnly] public NativeArray<float3x2> Loads;
            [ReadOnly] public NativeArray<BlobAssetReference<Collider>> Colliders;
            public NativeArray<ShipState> Ships;
            /// <summary>[1] = smallest pair distance after the step (negative = overlap).</summary>
            public NativeArray<float> Stats;
            public float Dt;

            public void Execute()
            {
                NativeArray<MotionVelocity> velocities = Input.World.MotionVelocities;
                NativeArray<MotionData> datas = Input.World.MotionDatas;
                for (int i = 0; i < velocities.Length; i++)
                {
                    MotionVelocity mv = velocities[i];
                    quaternion rot = datas[i].WorldFromMotion.rot;
                    mv.LinearVelocity += Loads[i].c0 * (mv.InverseMass * Dt);
                    mv.AngularVelocity += math.rotate(math.inverse(rot), Loads[i].c1) * mv.InverseInertia * Dt;
                    velocities[i] = mv;
                }

                Simulation.StepImmediate(Input, ref Context);

                for (int i = 0; i < velocities.Length; i++)
                {
                    RigidTransform t = datas[i].WorldFromMotion;
                    Ships[i] = new ShipState
                    {
                        Position = t.pos, Rotation = t.rot,
                        LinearVelocity = velocities[i].LinearVelocity,
                        AngularVelocity = math.rotate(t.rot, velocities[i].AngularVelocity),
                    };
                }

                float closest = float.MaxValue;
                for (int i = 0; i < Ships.Length; i++)
                for (int j = i + 1; j < Ships.Length; j++)
                {
                    var ti = new RigidTransform(Ships[i].Rotation, Ships[i].Position);
                    var tj = new RigidTransform(Ships[j].Rotation, Ships[j].Position);
                    if (SpikeContacts.Closest(Colliders[i], ti, Colliders[j], tj, SpikeContacts.ContactBandM, out float d, out _, out _))
                        closest = math.min(closest, d);
                }
                Stats[1] = closest;
            }
        }

        public void Dispose()
        {
            _context.Dispose();
            _world.Dispose();
            if (_staticChanged.IsCreated) _staticChanged.Dispose();
            if (_colliders.IsCreated)
            {
                for (int i = 0; i < _colliders.Length; i++) _colliders[i].Dispose();
                _colliders.Dispose();
            }
            if (_ships.IsCreated) _ships.Dispose();
            if (_stats.IsCreated) _stats.Dispose();
        }
    }

    // ───────────────────────────── Variant B ─────────────────────────────

    /// <summary>
    /// Variant B: our own six-axis integrator (semi-implicit Euler) with direction-correct added mass
    /// and inertia in body axes (coupling and Munk moments neglected); Unity Physics only finds
    /// contacts (collider distance query), resolved as a soft spring–damper push.
    /// </summary>
    public sealed class SpikeWorldB : ISpikeWorld
    {
        public string Name => "B · own integrator + Unity Physics contacts";
        public NativeArray<ShipState> Ships => _ships;
        public int ContactTicks => _contactTicks;
        public float MaxPenetrationM => _maxPenetration;

        /// <summary>Contact spring period, s (soft: hulls are not rigid bodies at metre scale).</summary>
        public const float ContactPeriodS = 0.25f;
        public const float ContactDampingRatio = 0.6f;

        NativeArray<SpikeHull> _hulls;
        NativeArray<BlobAssetReference<Collider>> _colliders;
        NativeArray<ShipState> _ships;
        NativeArray<float> _stats;
        int _contactTicks;
        float _maxPenetration;

        public SpikeWorldB(NativeArray<SpikeHull> hulls, NativeArray<ShipState> initial)
        {
            _hulls = hulls;
            _ships = new NativeArray<ShipState>(initial, Allocator.Persistent);
            _colliders = new NativeArray<BlobAssetReference<Collider>>(hulls.Length, Allocator.Persistent);
            for (int i = 0; i < hulls.Length; i++) _colliders[i] = SpikeContacts.Hull(hulls[i]);
            _stats = new NativeArray<float>(2, Allocator.Persistent);
        }

        public void Step(SpikeSeaLoads loads, float dt, float gravity)
        {
            new StepJob
            {
                Ships = _ships, Hulls = _hulls, Colliders = _colliders, SeaLoads = loads.ShipLoads,
                Stats = _stats, Dt = dt, Gravity = gravity,
            }.Run();
            if (_stats[1] < SpikeContacts.ContactBandM) _contactTicks++;
            _maxPenetration = math.max(_maxPenetration, -_stats[1]);
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
        struct StepJob : IJob
        {
            public NativeArray<ShipState> Ships;
            [ReadOnly] public NativeArray<SpikeHull> Hulls;
            [ReadOnly] public NativeArray<BlobAssetReference<Collider>> Colliders;
            [ReadOnly] public NativeArray<float3x2> SeaLoads;
            public NativeArray<float> Stats;
            public float Dt, Gravity;

            public void Execute()
            {
                int n = Ships.Length;
                var contact = new NativeArray<float3x2>(n, Allocator.Temp);
                float closest = float.MaxValue;
                for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    ShipState si = Ships[i], sj = Ships[j];
                    var ti = new RigidTransform(si.Rotation, si.Position);
                    var tj = new RigidTransform(sj.Rotation, sj.Position);
                    if (!SpikeContacts.Closest(Colliders[i], ti, Colliders[j], tj, SpikeContacts.ContactBandM,
                            out float d, out float3 p, out float3 nrm)) continue;
                    closest = math.min(closest, d);
                    if (d >= 0f) continue;

                    float mi = Hulls[i].MassKg, mj = Hulls[j].MassKg;
                    float mRed = mi * mj / (mi + mj);
                    float omega = 2f * math.PI / ContactPeriodS;
                    float k = mRed * omega * omega;
                    float c = 2f * ContactDampingRatio * math.sqrt(k * mRed);
                    float3 vi = si.LinearVelocity + math.cross(si.AngularVelocity, p - si.Position);
                    float3 vj = sj.LinearVelocity + math.cross(sj.AngularVelocity, p - sj.Position);
                    float closing = -math.dot(vj - vi, nrm);
                    float3 f = math.max(0f, k * -d + c * closing) * nrm;
                    contact[j] += new float3x2(f, math.cross(p - sj.Position, f));
                    contact[i] += new float3x2(-f, math.cross(p - si.Position, -f));
                }
                Stats[1] = closest;

                for (int s = 0; s < n; s++)
                {
                    SpikeHull h = Hulls[s];
                    ShipState st = Ships[s];
                    quaternion q = st.Rotation, qi = math.inverse(q);
                    float3 force = SeaLoads[s].c0 + contact[s].c0 + new float3(0f, -h.MassKg * Gravity, 0f);
                    float3 torque = SeaLoads[s].c1 + contact[s].c1;

                    float3 massEff = h.MassKg * (1f + h.AddedMassFraction);
                    float3 inertiaEff = h.Inertia * (1f + h.AddedInertiaFraction);

                    float3 vBody = math.rotate(qi, st.LinearVelocity) + math.rotate(qi, force) / massEff * Dt;
                    st.LinearVelocity = math.rotate(q, vBody);
                    st.Position += st.LinearVelocity * Dt;

                    float3 wBody = math.rotate(qi, st.AngularVelocity);
                    float3 wDot = (math.rotate(qi, torque) - math.cross(wBody, inertiaEff * wBody)) / inertiaEff;
                    wBody += wDot * Dt;
                    st.AngularVelocity = math.rotate(q, wBody);
                    quaternion spin = new quaternion(new float4(st.AngularVelocity * (0.5f * Dt), 0f));
                    st.Rotation = math.normalize(new quaternion(q.value + math.mul(spin, q).value));
                    Ships[s] = st;
                }
            }
        }

        public void Dispose()
        {
            if (_colliders.IsCreated)
            {
                for (int i = 0; i < _colliders.Length; i++) _colliders[i].Dispose();
                _colliders.Dispose();
            }
            if (_ships.IsCreated) _ships.Dispose();
            if (_stats.IsCreated) _stats.Dispose();
        }
    }
}
