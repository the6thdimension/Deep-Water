using Unity.Collections;
using Unity.Mathematics;
using DeepWater.Sea.Core;

namespace DeepWater.Reckoning.Spike
{
    // SPIKE — throwaway code to settle two questions for the Reckoning core (2026-10-23):
    //   (1) can reckoned (engine-free) code use Unity Physics, and
    //   (2) does Unity Physics moving the ships (variant A, one mass for all directions) or our own
    //       integrator with direction-correct added mass + Unity Physics for contacts only
    //       (variant B) meet the pass criteria? See SPIKE-REPORT.md.

    /// <summary>One ship's reckoned state: centre of mass in world, orientation, velocities (world).</summary>
    public struct ShipState
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 LinearVelocity;
        public float3 AngularVelocity;
    }

    /// <summary>
    /// A box hull. Body axes: x = beam (sway / pitch axis), y = up (heave / yaw axis),
    /// z = length (surge / roll axis). Centre of mass at the box centre.
    /// Added-mass fractions are AUTHORED, order-of-magnitude values for ship-like sections
    /// (heave and pitch large, surge tiny, roll small); a real hull would get them from strip theory.
    /// </summary>
    public struct SpikeHull
    {
        public float LengthM, BeamM, HeightM, DraftM;
        public float MassKg;
        /// <summary>Rigid-body principal inertia about body x (pitch), y (yaw), z (roll), kg·m².</summary>
        public float3 Inertia;
        /// <summary>Added mass as a fraction of mass along body x (sway), y (heave), z (surge).</summary>
        public float3 AddedMassFraction;
        /// <summary>Added inertia as a fraction of inertia about body x (pitch), y (yaw), z (roll).</summary>
        public float3 AddedInertiaFraction;
        /// <summary>Probe damping (Sea State's model) for vertical relative motion.</summary>
        public BuoyancyDamping Damping;
        /// <summary>Horizontal damping as a fraction of vertical (Sea State applies full damping both ways,
        /// which stops a hull dead; AUTHORED for the spike so hulls can move).</summary>
        public float HorizontalDampingFraction;
        public int ProbeStart, ProbeCount;

        public float MeanAddedMass => (AddedMassFraction.x + AddedMassFraction.y + AddedMassFraction.z) / 3f;
        public float MeanAddedInertia => (AddedInertiaFraction.x + AddedInertiaFraction.y + AddedInertiaFraction.z) / 3f;

        /// <summary>Body-origin height that floats the box at its design draft in calm water (COM at the centre).</summary>
        public float RestHeight => 0.5f * HeightM - DraftM;

        /// <summary>
        /// Box hull with seawater displacement at its design draft and uniform-box inertia.
        /// Probes: nx × nz vertical columns over the waterplane, each spanning the full height
        /// (the layout Sea State's BuoyancyHullSO.FillBoxGrid uses).
        /// </summary>
        public static SpikeHull Box(float length, float beam, float height, float draft, int nx, int nz,
            float waterDensity, float gravity, NativeList<BuoyancyProbe> probes)
        {
            float mass = waterDensity * length * beam * draft;
            var hull = new SpikeHull
            {
                LengthM = length, BeamM = beam, HeightM = height, DraftM = draft,
                MassKg = mass,
                Inertia = mass / 12f * new float3(height * height + length * length,
                                                  beam * beam + length * length,
                                                  beam * beam + height * height),
                AddedMassFraction = new float3(0.5f, 0.8f, 0.05f),
                AddedInertiaFraction = new float3(0.8f, 0.5f, 0.15f),
                Damping = new BuoyancyDamping { LinearMs = 2f * 0.2f * math.sqrt(gravity * draft), QuadraticCd = 0.5f },
                HorizontalDampingFraction = 0.02f,
                ProbeStart = probes.Length,
                ProbeCount = nx * nz,
            };
            float cell = length * beam / (nx * nz);
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                probes.Add(new BuoyancyProbe
                {
                    LocalPosition = new float3(((ix + 0.5f) / nx - 0.5f) * beam, 0f, ((iz + 0.5f) / nz - 0.5f) * length),
                    AreaM2 = cell,
                    HeightM = height,
                });
            }
            return hull;
        }

        /// <summary>
        /// Metacentric height of the box: GM = KB + BM − KG with KB = d/2, BM = B²/(12 d), KG = H/2.
        /// </summary>
        public float MetacentricHeight => 0.5f * DraftM + BeamM * BeamM / (12f * DraftM) - 0.5f * HeightM;

        /// <summary>
        /// Natural roll period from the IMO weather criterion (2008 IS Code, Part A 2.3):
        /// T = 2 C B / √GM, C = 0.373 + 0.023 (B/d) − 0.043 (L/100). An empirical fit to real ships —
        /// the yardstick for "does this hull roll at a ship-like rate", not an exact answer.
        /// </summary>
        public float ImoRollPeriodS
        {
            get
            {
                float c = 0.373f + 0.023f * (BeamM / DraftM) - 0.043f * (LengthM / 100f);
                return 2f * c * BeamM / math.sqrt(MetacentricHeight);
            }
        }
    }

    /// <summary>
    /// The world's fingerprint: FNV-1a over the exact bit patterns of every ship's state. Two runs
    /// with the same Orders must produce the same Fix on every tick.
    /// </summary>
    public static class Fix
    {
        const ulong Offset = 14695981039346656037UL, Prime = 1099511628211UL;

        public static ulong Of(NativeArray<ShipState> ships, ulong running = Offset)
        {
            ulong h = running;
            for (int i = 0; i < ships.Length; i++)
            {
                ShipState s = ships[i];
                h = Mix(h, s.Position); h = Mix(h, s.Rotation.value);
                h = Mix(h, s.LinearVelocity); h = Mix(h, s.AngularVelocity);
            }
            return h;
        }

        static ulong Mix(ulong h, float3 v) => Mix(Mix(Mix(h, math.asuint(v.x)), math.asuint(v.y)), math.asuint(v.z));
        static ulong Mix(ulong h, float4 v) => Mix(Mix(Mix(Mix(h, math.asuint(v.x)), math.asuint(v.y)), math.asuint(v.z)), math.asuint(v.w));
        static ulong Mix(ulong h, uint w)
        {
            for (int b = 0; b < 4; b++) { h ^= (w >> (8 * b)) & 0xFF; h *= Prime; }
            return h;
        }
    }
}
