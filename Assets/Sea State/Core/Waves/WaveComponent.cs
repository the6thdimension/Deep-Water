using Unity.Mathematics;

namespace DeepWater.Sea.Core
{
    /// <summary>
    /// One baked wave of the sea: a single Lagrangian (Gerstner / "choppy") component drawn
    /// from the directional spectrum. Immutable for the lifetime of a sea state.
    ///
    /// Phase convention: θ = k (d · x₀) − ω t + φ, so the wave travels along +d.
    /// Height  η = a cos θ.  Horizontal displacement D = −Q a d sin θ.
    /// </summary>
    public struct WaveComponent
    {
        /// <summary>Unit propagation direction in the horizontal plane (x = world X, y = world Z).</summary>
        public float2 Direction;
        /// <summary>Wavenumber k, rad/m. Deep water: ω² = g k.</summary>
        public float Wavenumber;
        /// <summary>Angular frequency ω, rad/s.</summary>
        public float Omega;
        /// <summary>Amplitude a, m. a = √(2 · S(ω,θ) · Δω) for its spectral bin.</summary>
        public float Amplitude;
        /// <summary>Random phase φ, rad, from the seeded RNG.</summary>
        public float Phase;
        /// <summary>Choppiness Q (0 = linear Airy, 1 = first-order Lagrangian / choppy-wave model).</summary>
        public float Choppiness;
    }

    /// <summary>
    /// A component prepared for one instant: amplitude already scaled by the sea-state
    /// cross-fade weight, and the time-dependent phase (φ − ω t) wrapped to [0, 2π) in double
    /// precision. This exact layout (2 × float4, 32 bytes) is shared by the Burst query job and
    /// the GPU wave decal, so what physics evaluates and what HDRP draws are the same numbers.
    /// </summary>
    public struct SeaFrameComponent
    {
        /// <summary>x, y = direction; z = wavenumber k; w = scaled amplitude a.</summary>
        public float4 A;
        /// <summary>
        /// x = Q·a (horizontal amplitude); y = wrapped phase (φ − ω t); z = ω;
        /// w = render gain (GPU only: pre-compensates HDRP's deformation blur; 1 = none, ignored by physics).
        /// </summary>
        public float4 B;
    }

    /// <summary>Result of one sea query at a world position.</summary>
    public struct SeaSample
    {
        /// <summary>World-space Y of the water surface directly above/below the query point.</summary>
        public float SurfaceHeight;
        /// <summary>Unit surface normal.</summary>
        public float3 Normal;
        /// <summary>Velocity of the surface water particle, m/s (orbital motion).</summary>
        public float3 Velocity;
        /// <summary>Lagrangian Jacobian of the surface (1 = undistorted, ≤ 0 = folded / breaking).</summary>
        public float Jacobian;
    }
}
