using DeepWater.Sea.Core;
using Unity.Mathematics;

namespace DeepWater.Sea.Modules.Hdrp
{
    /// <summary>
    /// One residual-foam step exactly as the GPU took it — inputs, region and the wave frame —
    /// so <see cref="ResidualFoamStepJob"/> can replay it on the CPU and the two fields can be
    /// compared. Recorded only while a verification tool asks for it.
    /// </summary>
    public sealed class ResidualFoamStepRecord
    {
        public ResidualFoamParams Params;
        public float DtS;
        /// <summary>Grid centre after the step's <see cref="ResidualFoamGrid.Advance"/>, world XZ.</summary>
        public float2 Centre;
        /// <summary>Index shift the step applied to the previous field.</summary>
        public int2 Shift;
        public SeaFrameComponent[] Components;
        /// <summary>Foam deposited after the step (see <see cref="ResidualFoamMath.ApplyDeposits"/>); empty when none.</summary>
        public FoamDeposit[] Deposits;
    }
}
