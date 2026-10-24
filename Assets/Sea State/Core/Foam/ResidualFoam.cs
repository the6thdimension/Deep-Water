using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeepWater.Sea.Core
{
    /// <summary>
    /// Inputs of one residual-foam step. Built by the renderer from the sea conditions and its
    /// look (all tuning lives there); recorded per step so the CPU twin can replay the GPU.
    /// </summary>
    public struct ResidualFoamParams
    {
        /// <summary>Unit vector the wind blows toward, world XZ.</summary>
        public float2 Downwind;
        /// <summary>Surface drift speed along <see cref="Downwind"/>, m/s (moves the grid, see <see cref="ResidualFoamGrid"/>).</summary>
        public float DriftMs;
        /// <summary>Peak cross-wind speed toward the windrow lines, m/s (0 = no windrows).</summary>
        public float ConvergenceMs;
        /// <summary>Distance between windrow lines, m.</summary>
        public float SpacingM;
        /// <summary>Lateral meander of the lines, m.</summary>
        public float MeanderM;
        /// <summary>Along-wind wavelength of the meander, m. Every along-wind period of the pattern divides 2× this.</summary>
        public float MeanderWavelengthM;
        /// <summary>Spacing varies by ±this fraction across the wind, so lines bunch and spread (0 = a regular grid).</summary>
        public float SpacingVariation;
        /// <summary>Convergence varies by up to this fraction along each line, so streaks come in segments (0 = uniform).</summary>
        public float StrengthVariation;
        /// <summary>How far the windrow pattern has drifted downwind, m (wrapped to 2× the meander wavelength).</summary>
        public float AlongOffsetM;
        /// <summary>Foam e-folding lifetime, s.</summary>
        public float LifetimeS;
        /// <summary>Foam level produced per second at full breaking.</summary>
        public float ProductionPerS;
        /// <summary>Upper bound on the foam level.</summary>
        public float Cap;
        /// <summary>Jacobian below which the surface breaks (whitecap threshold).</summary>
        public float JacobianThreshold;
        /// <summary>Sharpening of the breaking indicator: b = saturate((J_thr − J) · gain).</summary>
        public float BreakingGain;
    }

    /// <summary>
    /// Foam put into the water from outside the sea model (a splash, a shell or missile hitting the water, a ship's wake),
    /// added once, in the next step, after the integration: up to <see cref="Level"/> at <see cref="Centre"/>, falling off as
    /// a Gaussian with σ = <see cref="RadiusM"/> / 2 and cut off beyond 1.5 × the radius. It then drifts, gathers and decays
    /// with the rest of the residual foam, and the field's cap still holds. The foam it adds is
    /// Level × 2πσ² (texel-summed), less whatever the cap clips.
    /// </summary>
    public struct FoamDeposit
    {
        /// <summary>World XZ, m.</summary>
        public float2 Centre;
        public float RadiusM;
        /// <summary>Foam level added at the centre (the field's units; the look's cap bounds the result).</summary>
        public float Level;
    }

    /// <summary>
    /// Where the residual foam grid sits. The grid drifts with the surface water: drift is carried
    /// as a sub-texel <see cref="Fraction"/> plus whole-texel moves of the grid, never by
    /// resampling the field, so drift neither smears nor loses foam. Following the camera only
    /// ever re-centres the grid by whole texels, which is an exact copy.
    /// </summary>
    public struct ResidualFoamGrid
    {
        public int Resolution;
        public float SizeM;
        /// <summary>Grid centre in whole texels.</summary>
        public int2 Index;
        /// <summary>Sub-texel drift the grid carries, texels, in [0, 1).</summary>
        public float2 Fraction;

        public float TexelM => SizeM / Resolution;

        /// <summary>World XZ of the grid centre.</summary>
        public float2 CentreWorld => (float2)((new double2(Index) + new double2(Fraction)) * TexelM);

        public static ResidualFoamGrid At(float2 world, int resolution, float sizeM)
        {
            var g = new ResidualFoamGrid { Resolution = resolution, SizeM = sizeM };
            g.Index = (int2)math.round(world / g.TexelM);
            return g;
        }

        /// <summary>
        /// The water moved by <paramref name="driftM"/>: the grid moves with it (its texels keep
        /// their foam), then re-centres on <paramref name="followWorld"/> in whole texels.
        /// Returns the index shift: texel i of the new grid holds what texel i + shift held.
        /// </summary>
        public int2 Advance(float2 driftM, float2 followWorld)
        {
            float2 f = Fraction + driftM / TexelM;
            int2 carry = (int2)math.floor(f);
            Fraction = f - carry;
            Index += carry;
            int2 target = (int2)math.round(followWorld / TexelM - Fraction);
            int2 shift = target - Index;
            Index = target;
            return shift;
        }
    }

    /// <summary>
    /// Residual foam: what breaking crests leave behind once the whitecap itself has gone,
    /// carried downwind by the surface drift and gathered into windrows by Langmuir
    /// convergence — the "foam blown in streaks along the direction of the wind" of
    /// Beaufort force 7 and above.
    ///
    /// The field is a square N×N grid of texel centres in world XZ (row-major, X fastest) that
    /// drifts with the water (<see cref="ResidualFoamGrid"/>). One step, in the grid's frame:
    ///   F ← shift(F) − dt/h · Σ_faces (upwind flux of the windrow flow) ;  F ← F · e^(−dt/T) + P · b · dt
    /// The flux form conserves foam exactly and never goes negative (CFL ≪ 1), so foam piles up
    /// on the convergence lines as it does at sea, limited only by the texel. (A semi-Lagrangian
    /// step with a compression factor was tried first and created foam on unresolved lines:
    /// +34 % in 30 s.) Production b comes from the same wave field physics uses.
    ///
    /// This is the CPU reference of <c>Modules/Hdrp/Shaders/SeaFoam.compute</c>. Keep the two
    /// in lockstep, line for line.
    /// </summary>
    public static class ResidualFoamMath
    {
        const float TwoPi = 2f * math.PI;

        /// <summary>
        /// Cross-wind coordinate whose multiples of the spacing are the convergence lines. A warp
        /// makes the local spacing vary by ±<see cref="ResidualFoamParams.SpacingVariation"/> (drifting
        /// slowly along the wind) and a meander bends each line, so the lines bunch, spread and wander
        /// instead of forming a grid. ∂s/∂(cross-wind) ≥ 1 − variation − 3.55 · meander / spacing must
        /// stay positive, so every point still flows to its nearest line.
        /// </summary>
        public static float WindrowCoordinate(in ResidualFoamParams p, float2 x)
        {
            float2 d = p.Downwind;
            float along = math.dot(x, d) - p.AlongOffsetM;
            float across = d.x * x.y - d.y * x.x;
            float warpScale = 2.7f * p.SpacingM;
            float warp = p.SpacingVariation * warpScale / TwoPi
                         * math.sin(TwoPi * across / warpScale + 0.9f * math.sin(TwoPi * along / (2f * p.MeanderWavelengthM)));
            return across + warp + p.MeanderM * math.sin(TwoPi * along / p.MeanderWavelengthM
                                                         + 1.3f * math.sin(TwoPi * across / (2.3f * p.SpacingM)));
        }

        /// <summary>
        /// Windrow flow relative to the drifting water: across the wind, onto s = k·spacing, its
        /// strength gated along each line so streaks form in segments.
        /// </summary>
        public static float2 ConvergenceVelocity(in ResidualFoamParams p, float2 x)
        {
            float2 d = p.Downwind;
            float2 n = new float2(-d.y, d.x);
            float along = math.dot(x, d) - p.AlongOffsetM;
            float across = d.x * x.y - d.y * x.x;
            float gate = 1f - p.StrengthVariation * 0.5f
                         * (1f + math.sin(TwoPi * along / (p.MeanderWavelengthM * (2f / 3f)) + 2.1f * math.sin(TwoPi * across / (1.9f * p.SpacingM))));
            float s = WindrowCoordinate(p, x);
            return -p.ConvergenceMs * gate * math.sin(TwoPi * s / p.SpacingM) * n;
        }

        /// <summary>Horizontal Jacobian of the Lagrangian map at rest position x₀ (same sum as SeaFieldMath.Evaluate).</summary>
        public static float Jacobian(in NativeArray<SeaFrameComponent> c, int count, float2 x0)
        {
            float dxdx = 0f, dzdz = 0f, dxdz = 0f;
            for (int i = 0; i < count; i++)
            {
                float4 a = c[i].A;
                float4 b = c[i].B;
                float qak = b.x * a.z * math.cos(a.z * (a.x * x0.x + a.y * x0.y) + b.y);
                dxdx += qak * a.x * a.x;
                dzdz += qak * a.y * a.y;
                dxdz += qak * a.x * a.y;
            }
            return (1f - dxdx) * (1f - dzdz) - dxdz * dxdz;
        }

        /// <summary>Breaking indicator 0–1: how far the surface is compressed past the whitecap threshold.</summary>
        public static float Breaking(in ResidualFoamParams p, float jacobian)
            => math.saturate((p.JacobianThreshold - jacobian) * p.BreakingGain);

        /// <summary>World XZ of texel (i, j) of a grid of <paramref name="n"/> texels over <paramref name="sizeM"/> centred at <paramref name="centre"/>.</summary>
        public static float2 TexelWorld(int i, int j, int n, float2 centre, float sizeM)
            => centre + ((new float2(i, j) + 0.5f) / n - 0.5f) * sizeM;

        /// <summary>The foam a deposit adds at world XZ <paramref name="x"/>: a Gaussian of σ = radius / 2, zero beyond 1.5 × the radius.</summary>
        public static float DepositLevel(in FoamDeposit d, float2 x)
        {
            float r2 = math.lengthsq(x - d.Centre);
            float radius = math.max(d.RadiusM, 1e-3f);
            if (r2 > 2.25f * radius * radius) return 0f;
            float sigma = 0.5f * radius;
            return d.Level * math.exp(-0.5f * r2 / (sigma * sigma));
        }

        /// <summary>
        /// Add <paramref name="count"/> deposits to a field of <paramref name="n"/>² texels over <paramref name="sizeM"/> centred at
        /// <paramref name="centre"/>, then clamp each touched texel to <paramref name="cap"/>. The CPU twin of the
        /// DWSeaFoamDeposit kernel, run after <see cref="ResidualFoamStepJob"/> with the step's centre and cap.
        /// </summary>
        public static void ApplyDeposits(NativeArray<float> field, int n, float2 centre, float sizeM, float cap, FoamDeposit[] deposits, int count)
        {
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float2 x = TexelWorld(i, j, n, centre, sizeM);
                float add = 0f;
                for (int k = 0; k < count; k++) add += DepositLevel(deposits[k], x);
                if (add > 0f) field[j * n + i] = math.min(field[j * n + i] + add, cap);
            }
        }
    }

    /// <summary>
    /// One residual-foam step on the CPU — the reference the GPU integrator is checked
    /// against, and what the unit tests drive. One index per texel.
    /// </summary>
    [BurstCompile]
    public struct ResidualFoamStepJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Previous;
        [WriteOnly] public NativeArray<float> Next;
        /// <summary>Wave components at the step's time (the frame the GPU drew). Count 0 = no production.</summary>
        [ReadOnly] public NativeArray<SeaFrameComponent> Components;
        public int ComponentCount;
        public int Resolution;
        public float SizeM;
        /// <summary>World XZ of the grid centre after <see cref="ResidualFoamGrid.Advance"/>.</summary>
        public float2 Centre;
        /// <summary>Index shift returned by <see cref="ResidualFoamGrid.Advance"/>.</summary>
        public int2 Shift;
        public float DtS;
        public ResidualFoamParams Params;

        float Load(int i, int j)
        {
            int n = Resolution;
            i += Shift.x;
            j += Shift.y;
            return i >= 0 && j >= 0 && i < n && j < n ? Previous[j * n + i] : 0f;
        }

        public void Execute(int index)
        {
            int n = Resolution;
            int i = index % n, j = index / n;
            float h = SizeM / n;
            float2 x = ResidualFoamMath.TexelWorld(i, j, n, Centre, SizeM);

            float fC = Load(i, j), fE = Load(i + 1, j), fW = Load(i - 1, j), fN = Load(i, j + 1), fS = Load(i, j - 1);
            float uE = ResidualFoamMath.ConvergenceVelocity(Params, x + new float2(0.5f * h, 0f)).x;
            float uW = ResidualFoamMath.ConvergenceVelocity(Params, x - new float2(0.5f * h, 0f)).x;
            float uN = ResidualFoamMath.ConvergenceVelocity(Params, x + new float2(0f, 0.5f * h)).y;
            float uS = ResidualFoamMath.ConvergenceVelocity(Params, x - new float2(0f, 0.5f * h)).y;
            // Upwind fluxes through the four faces (positive = toward +X / +Z).
            float flE = uE > 0f ? uE * fC : uE * fE;
            float flW = uW > 0f ? uW * fW : uW * fC;
            float flN = uN > 0f ? uN * fC : uN * fN;
            float flS = uS > 0f ? uS * fS : uS * fC;

            float f = fC - DtS / h * (flE - flW + flN - flS);
            f *= math.exp(-DtS / Params.LifetimeS);
            if (ComponentCount > 0)
                f += Params.ProductionPerS * ResidualFoamMath.Breaking(Params, ResidualFoamMath.Jacobian(Components, ComponentCount, x)) * DtS;
            Next[index] = math.min(f, Params.Cap);
        }
    }
}
