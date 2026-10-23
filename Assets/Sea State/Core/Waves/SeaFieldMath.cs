using Unity.Collections;
using Unity.Mathematics;

namespace DeepWater.Sea.Core
{
    /// <summary>
    /// The sea surface as a Lagrangian sum of components — the single definition of "the
    /// water" shared by physics (Burst jobs) and, line for line, by the GPU decal in
    /// <c>Modules/Hdrp/SeaWaves.hlsl</c>. Keep the two in lockstep.
    ///
    /// A water particle whose rest position is x₀ sits at
    ///   P(x₀, t) = ( x₀ + Σ −Q aᵢ dᵢ sin θᵢ ,  Σ aᵢ cos θᵢ ),   θᵢ = kᵢ (dᵢ · x₀) + (φᵢ − ωᵢ t).
    /// Rendering displaces each vertex by P directly. A height query at a fixed world (x, z)
    /// must first invert the horizontal map: find x₀ with x₀ + D(x₀) = x.
    /// </summary>
    public static class SeaFieldMath
    {
        /// <summary>Horizontal displacement D(x₀) = Σ −Q a d sin θ.</summary>
        public static float2 HorizontalDisplacement(in NativeArray<SeaFrameComponent> c, int count, float2 x0)
        {
            float2 d = float2.zero;
            for (int i = 0; i < count; i++)
            {
                float4 a = c[i].A;
                float4 b = c[i].B;
                float theta = a.z * (a.x * x0.x + a.y * x0.y) + b.y;
                d -= b.x * math.sin(theta) * a.xy;
            }
            return d;
        }

        /// <summary>
        /// Rest position x₀ whose particle is at world (x, z) = <paramref name="x"/>, by fixed-point
        /// iteration x₀ ← x − D(x₀). The iteration count is fixed (never "until converged") so
        /// cost and result never depend on the data — a determinism and budgeting choice.
        /// Converges while the surface is not folded (Jacobian &gt; 0).
        /// </summary>
        public static float2 InverseHorizontal(in NativeArray<SeaFrameComponent> c, int count, float2 x, int iterations)
        {
            float2 x0 = x;
            for (int it = 0; it < iterations; it++)
                x0 = x - HorizontalDisplacement(c, count, x0);
            return x0;
        }

        /// <summary>
        /// Full evaluation at rest position x₀: vertical and horizontal displacement, particle
        /// velocity, surface normal and the horizontal Jacobian (foam / breaking indicator).
        /// </summary>
        public static void Evaluate(in NativeArray<SeaFrameComponent> c, int count, float2 x0,
            out float height, out float2 horizontal, out float3 velocity, out float3 normal, out float jacobian)
        {
            float h = 0f;
            float2 dxz = float2.zero;
            float3 v = float3.zero;
            // Partial derivatives of P with respect to x₀ (dPx/dz₀ == dPz/dx₀ by symmetry).
            float dxdx = 0f, dzdz = 0f, dxdz = 0f, dydx = 0f, dydz = 0f;

            for (int i = 0; i < count; i++)
            {
                float4 a = c[i].A;
                float4 b = c[i].B;
                float2 dir = a.xy;
                float k = a.z;
                float amp = a.w;
                float qa = b.x;
                float omega = b.z;

                float theta = k * (dir.x * x0.x + dir.y * x0.y) + b.y;
                math.sincos(theta, out float s, out float co);

                h += amp * co;
                dxz -= qa * s * dir;

                // ∂/∂t of η and D (∂θ/∂t = −ω).
                v.y += amp * omega * s;
                float hv = qa * omega * co;
                v.x += hv * dir.x;
                v.z += hv * dir.y;

                float qak = qa * k * co;
                dxdx += qak * dir.x * dir.x;
                dzdz += qak * dir.y * dir.y;
                dxdz += qak * dir.x * dir.y;

                float ak = amp * k * s;
                dydx -= ak * dir.x;
                dydz -= ak * dir.y;
            }

            float3 tangentX = new float3(1f - dxdx, dydx, -dxdz);
            float3 tangentZ = new float3(-dxdz, dydz, 1f - dzdz);

            height = h;
            horizontal = dxz;
            velocity = v;
            normal = math.normalizesafe(math.cross(tangentZ, tangentX), new float3(0f, 1f, 0f));
            jacobian = (1f - dxdx) * (1f - dzdz) - dxdz * dxdz;
        }

        /// <summary>
        /// Prepare components for time <paramref name="timeS"/>: scale amplitudes by
        /// <paramref name="amplitudeScale"/> and wrap the phase (φ − ω t) to [0, 2π) in double
        /// precision, so long runs do not lose phase accuracy to float rounding.
        /// </summary>
        public static SeaFrameComponent Prepare(in WaveComponent w, double timeS, float amplitudeScale)
        {
            const double TwoPi = 2.0 * math.PI_DBL;
            double phase = w.Phase - (double)w.Omega * timeS;
            phase -= TwoPi * math.floor(phase / TwoPi);

            float amp = w.Amplitude * amplitudeScale;
            return new SeaFrameComponent
            {
                A = new float4(w.Direction.x, w.Direction.y, w.Wavenumber, amp),
                B = new float4(w.Choppiness * amp, (float)phase, w.Omega, 0f),
            };
        }
    }
}
