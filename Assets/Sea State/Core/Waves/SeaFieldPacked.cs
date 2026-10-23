using Unity.Collections;
using Unity.Mathematics;

namespace DeepWater.Sea.Core
{
    /// <summary>
    /// Four wave components laid out structure-of-arrays, with every per-component product
    /// the evaluation needs folded in ahead of time. One <see cref="float4"/> operation then
    /// advances four components at once (SIMD), and the inner loop does no redundant
    /// multiplies. Unused lanes hold zero coefficients and contribute nothing.
    ///
    /// Same water as <see cref="SeaFieldMath"/> (the reference, and the GPU twin's layout);
    /// results agree to float rounding — <c>SeaFieldTests.PackedKernel_MatchesReference</c>.
    /// </summary>
    public struct SeaComponentGroup
    {
        /// <summary>k·dx, k·dz: θ = Kx·x₀ + Kz·z₀ + Phase.</summary>
        public float4 Kx, Kz;
        /// <summary>Wrapped phase (φ − ω t).</summary>
        public float4 Phase;
        /// <summary>a — height amplitude.</summary>
        public float4 Amp;
        /// <summary>Q·a·dx, Q·a·dz — horizontal displacement amplitudes.</summary>
        public float4 HorizX, HorizZ;
        /// <summary>a·ω — vertical particle-velocity amplitude.</summary>
        public float4 VelY;
        /// <summary>Q·a·ω·dx, Q·a·ω·dz — horizontal particle-velocity amplitudes.</summary>
        public float4 VelX, VelZ;
        /// <summary>Q·a·k·dx², Q·a·k·dz², Q·a·k·dx·dz — horizontal-map derivatives (Jacobian).</summary>
        public float4 Dxx, Dzz, Dxz;
        /// <summary>a·k·dx, a·k·dz — slope amplitudes.</summary>
        public float4 SlopeX, SlopeZ;

        public static int GroupCount(int components) => (components + 3) >> 2;

        /// <summary>Write one prepared component into lane <paramref name="lane"/> (0–3).</summary>
        public void SetLane(int lane, in SeaFrameComponent c)
        {
            float dx = c.A.x, dz = c.A.y, k = c.A.z, a = c.A.w;
            float qa = c.B.x, phase = c.B.y, w = c.B.z;
            Kx[lane] = k * dx; Kz[lane] = k * dz; Phase[lane] = phase; Amp[lane] = a;
            HorizX[lane] = qa * dx; HorizZ[lane] = qa * dz;
            VelY[lane] = a * w; VelX[lane] = qa * w * dx; VelZ[lane] = qa * w * dz;
            float qak = qa * k;
            Dxx[lane] = qak * dx * dx; Dzz[lane] = qak * dz * dz; Dxz[lane] = qak * dx * dz;
            SlopeX[lane] = a * k * dx; SlopeZ[lane] = a * k * dz;
        }
    }

    /// <summary>Vectorised evaluation over <see cref="SeaComponentGroup"/>s.</summary>
    public static class SeaFieldPacked
    {
        /// <summary>Horizontal displacement D(x₀).</summary>
        public static float2 HorizontalDisplacement(in NativeArray<SeaComponentGroup> g, int groups, float2 x0)
        {
            float4 dx = 0f, dz = 0f;
            for (int i = 0; i < groups; i++)
            {
                SeaComponentGroup c = g[i];
                float4 s = math.sin(c.Kx * x0.x + c.Kz * x0.y + c.Phase);
                dx -= c.HorizX * s;
                dz -= c.HorizZ * s;
            }
            return new float2(math.csum(dx), math.csum(dz));
        }

        /// <summary>Fixed-iteration Lagrangian inverse (see <see cref="SeaFieldMath.InverseHorizontal"/>).</summary>
        public static float2 InverseHorizontal(in NativeArray<SeaComponentGroup> g, int groups, float2 x, int iterations)
        {
            float2 x0 = x;
            for (int it = 0; it < iterations; it++)
                x0 = x - HorizontalDisplacement(g, groups, x0);
            return x0;
        }

        /// <summary>Full evaluation at rest position x₀ (see <see cref="SeaFieldMath.Evaluate"/>).</summary>
        public static void Evaluate(in NativeArray<SeaComponentGroup> g, int groups, float2 x0,
            out float height, out float3 velocity, out float3 normal, out float jacobian)
        {
            float4 h = 0f, vy = 0f, vx = 0f, vz = 0f;
            float4 dxx = 0f, dzz = 0f, dxz = 0f, dydx = 0f, dydz = 0f;
            for (int i = 0; i < groups; i++)
            {
                SeaComponentGroup c = g[i];
                math.sincos(c.Kx * x0.x + c.Kz * x0.y + c.Phase, out float4 s, out float4 co);
                h += c.Amp * co;
                vy += c.VelY * s;
                vx += c.VelX * co;
                vz += c.VelZ * co;
                dxx += c.Dxx * co;
                dzz += c.Dzz * co;
                dxz += c.Dxz * co;
                dydx -= c.SlopeX * s;
                dydz -= c.SlopeZ * s;
            }

            float sxx = math.csum(dxx), szz = math.csum(dzz), sxz = math.csum(dxz);
            float3 tangentX = new float3(1f - sxx, math.csum(dydx), -sxz);
            float3 tangentZ = new float3(-sxz, math.csum(dydz), 1f - szz);

            height = math.csum(h);
            velocity = new float3(math.csum(vx), math.csum(vy), math.csum(vz));
            normal = math.normalizesafe(math.cross(tangentZ, tangentX), new float3(0f, 1f, 0f));
            jacobian = (1f - sxx) * (1f - szz) - sxz * sxz;
        }

        /// <summary>Jacobian only (foam calibration), over groups [start, start + groups).</summary>
        public static float Jacobian(in NativeArray<SeaComponentGroup> g, int start, int groups, float2 x0)
        {
            float4 dxx = 0f, dzz = 0f, dxz = 0f;
            for (int i = start; i < start + groups; i++)
            {
                SeaComponentGroup c = g[i];
                float4 co = math.cos(c.Kx * x0.x + c.Kz * x0.y + c.Phase);
                dxx += c.Dxx * co;
                dzz += c.Dzz * co;
                dxz += c.Dxz * co;
            }
            float sxx = math.csum(dxx), szz = math.csum(dzz), sxz = math.csum(dxz);
            return (1f - sxx) * (1f - szz) - sxz * sxz;
        }

        /// <summary>Pack prepared components into groups; returns the group count.</summary>
        public static int Pack(in NativeArray<SeaFrameComponent> frame, int count, NativeArray<SeaComponentGroup> groups)
        {
            int n = SeaComponentGroup.GroupCount(count);
            for (int gi = 0; gi < n; gi++)
            {
                var group = default(SeaComponentGroup);
                for (int lane = 0; lane < 4; lane++)
                {
                    int i = gi * 4 + lane;
                    if (i < count) group.SetLane(lane, frame[i]);
                }
                groups[gi] = group;
            }
            return n;
        }
    }
}
