using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using DeepWater.Sea.Core;

namespace DeepWater.Reckoning.Spike
{
    /// <summary>
    /// Sea loads per probe — Sea State's probe model with one correction. Sea State applies each
    /// column's force at the column centre (hull mid-height); for a vertical force that only matters
    /// once the hull heels, and then it drops the "centre of buoyancy below centre of gravity" term:
    /// the hull's stability height becomes BM alone (5.3 m for the 154 m box instead of 2.4 m) and it
    /// rolls ~1.5× too fast. Here the force acts at the centroid of the submerged part of the column.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
    public struct SpikeProbeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SeaComponentGroup> Groups;
        public int GroupCount;
        public float SeaLevel;
        public int InverseIterations;
        public float WaterDensity;
        public float Gravity;
        /// <summary>Reproduce Sea State's current behaviour (force at the column centre) for comparison.</summary>
        public bool ApplyAtColumnCentre;

        [ReadOnly] public NativeArray<BuoyancyProbe> Probes;
        [ReadOnly] public NativeArray<int> ProbeShip;
        [ReadOnly] public NativeArray<ShipState> Ships;
        [ReadOnly] public NativeArray<SpikeHull> Hulls;

        /// <summary>c0 = force (world), c1 = torque about the centre of mass (world).</summary>
        [WriteOnly] public NativeArray<float3x2> Output;

        public void Execute(int i)
        {
            int ship = ProbeShip[i];
            ShipState s = Ships[ship];
            SpikeHull hull = Hulls[ship];
            BuoyancyProbe probe = Probes[i];

            float3 up = math.mul(s.Rotation, new float3(0f, 1f, 0f));
            float3 centre = s.Position + math.mul(s.Rotation, probe.LocalPosition);
            float2 x0 = SeaFieldPacked.InverseHorizontal(Groups, GroupCount, centre.xz, InverseIterations);
            SeaFieldPacked.Evaluate(Groups, GroupCount, x0, out float height, out float3 waterVelocity, out _, out _);
            float surfaceY = SeaLevel + height;

            float submerged = BuoyancyModel.SubmergedHeight(surfaceY, centre.y, probe.HeightM);
            if (submerged <= 0f) { Output[i] = default; return; }

            // The correction: act at the submerged centroid along the column, not at its centre.
            float3 apply = ApplyAtColumnCentre ? centre : centre + up * (0.5f * submerged - 0.5f * probe.HeightM);
            float3 arm = apply - s.Position;
            float3 pointVelocity = s.LinearVelocity + math.cross(s.AngularVelocity, arm);
            float3 vRel = pointVelocity - waterVelocity;

            float fraction = submerged / probe.HeightM;
            float k = WaterDensity * probe.AreaM2 * fraction;
            float3 vVertical = new float3(0f, vRel.y, 0f);
            float3 vHorizontal = new float3(vRel.x, 0f, vRel.z);
            float3 damping =
                -k * (hull.Damping.LinearMs + 0.5f * hull.Damping.QuadraticCd * math.abs(vRel.y)) * vVertical
                - k * hull.HorizontalDampingFraction *
                  (hull.Damping.LinearMs + 0.5f * hull.Damping.QuadraticCd * math.length(vHorizontal)) * vHorizontal;

            float3 force = new float3(0f, WaterDensity * Gravity * probe.AreaM2 * submerged, 0f) + damping;
            Output[i] = new float3x2(force, math.cross(arm, force));
        }
    }

    /// <summary>Sums each ship's probes in probe order — a fixed order, so the sum is deterministic.</summary>
    [BurstCompile(FloatMode = FloatMode.Deterministic, CompileSynchronously = true)]
    public struct SpikeProbeSumJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<SpikeHull> Hulls;
        [ReadOnly] public NativeArray<float3x2> Probes;
        [WriteOnly] public NativeArray<float3x2> Output;

        public void Execute(int ship)
        {
            SpikeHull hull = Hulls[ship];
            float3 f = float3.zero, t = float3.zero;
            for (int i = hull.ProbeStart; i < hull.ProbeStart + hull.ProbeCount; i++)
            {
                f += Probes[i].c0;
                t += Probes[i].c1;
            }
            Output[ship] = new float3x2(f, t);
        }
    }

    /// <summary>One tick's sea loads for every ship: probe job + per-ship sum.</summary>
    public sealed class SpikeSeaLoads : System.IDisposable
    {
        public NativeArray<BuoyancyProbe> Probes;
        public NativeArray<int> ProbeShip;
        public NativeArray<SpikeHull> Hulls;
        public NativeArray<float3x2> ShipLoads;
        NativeArray<float3x2> _probeLoads;

        public SpikeSeaLoads(NativeArray<SpikeHull> hulls, NativeArray<BuoyancyProbe> probes)
        {
            Hulls = hulls;
            Probes = probes;
            ProbeShip = new NativeArray<int>(probes.Length, Allocator.Persistent);
            for (int s = 0; s < hulls.Length; s++)
                for (int i = 0; i < hulls[s].ProbeCount; i++) ProbeShip[hulls[s].ProbeStart + i] = s;
            _probeLoads = new NativeArray<float3x2>(probes.Length, Allocator.Persistent);
            ShipLoads = new NativeArray<float3x2>(hulls.Length, Allocator.Persistent);
        }

        public void Compute(SeaSimulation sea, NativeArray<ShipState> ships, float waterDensity, float gravity,
            bool applyAtColumnCentre = false)
        {
            JobHandle probes = new SpikeProbeJob
            {
                Groups = sea.PhysicsGroups, GroupCount = sea.PhysicsGroupCount, SeaLevel = sea.SeaLevel,
                InverseIterations = sea.InverseIterations, WaterDensity = waterDensity, Gravity = gravity,
                ApplyAtColumnCentre = applyAtColumnCentre,
                Probes = Probes, ProbeShip = ProbeShip, Ships = ships, Hulls = Hulls, Output = _probeLoads,
            }.Schedule(Probes.Length, 32);
            new SpikeProbeSumJob { Hulls = Hulls, Probes = _probeLoads, Output = ShipLoads }
                .Schedule(Hulls.Length, 1, probes).Complete();
        }

        public void Dispose()
        {
            if (ProbeShip.IsCreated) ProbeShip.Dispose();
            if (_probeLoads.IsCreated) _probeLoads.Dispose();
            if (ShipLoads.IsCreated) ShipLoads.Dispose();
        }
    }
}
