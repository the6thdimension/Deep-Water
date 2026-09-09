using UnityEngine;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Aero
{
    /// <summary>
    /// Tabulated aero model — Mach-aware coefficient curves. Authored in
    /// <see cref="GuidedFury.ScriptableObjects.Profiles.MissileProfileSO"/> via
    /// <see cref="AnimationCurve"/> assets.
    ///
    /// **The four authored curves:**
    /// - `cdVsMach`: Cd(M) — drag coefficient. Transonic spike usually centered around M≈1.05.
    /// - `clAlphaVsMach`: Cl_α(M) per radian — lift slope. Often drops past M≈1.
    /// - `cmAlphaVsMach`: Cm_α(M) per radian — pitch stiffness. **Negative for a stable airframe.**
    /// - `cmDeltaVsMach`: Cm_δ(M) per radian — control surface effectiveness. Drops at high Mach for fin-stabilized missiles.
    ///
    /// **Why AnimationCurve:** designer-friendly, serializable in the SO, evaluated cheaply
    /// (~tens of nanoseconds per call). Burst-friendliness is moot because L4 is a hero-tier
    /// integrator that runs on at most a handful of missiles concurrently — not a 1000-entity
    /// hot path.
    ///
    /// **Per-missile instance:** unlike SimpleAeroModel's singleton, this model carries
    /// authored curves and is created per missile at launch.
    /// </summary>
    public sealed class TabulatedAeroModel : IAeroModel
    {
        private readonly AnimationCurve cdVsMach;
        private readonly AnimationCurve clAlphaVsMach;
        private readonly AnimationCurve cmAlphaVsMach;
        private readonly AnimationCurve cmDeltaVsMach;

        public TabulatedAeroModel(
            AnimationCurve cdVsMach,
            AnimationCurve clAlphaVsMach,
            AnimationCurve cmAlphaVsMach,
            AnimationCurve cmDeltaVsMach)
        {
            this.cdVsMach      = cdVsMach;
            this.clAlphaVsMach = clAlphaVsMach;
            this.cmAlphaVsMach = cmAlphaVsMach;
            this.cmDeltaVsMach = cmDeltaVsMach;
        }

        public AeroModelKind Kind => AeroModelKind.Tabulated;

        public AeroSample Sample(in MissileState state, in AtmosphereSample atmo, in MissileProfileData profile)
        {
            // Compute Mach from velocity vs speed of sound. Clamp to non-negative (could be
            // zero at standstill — Cd at M=0 is the static value).
            float speed = state.Velocity.magnitude;
            float a = Mathf.Max(atmo.SpeedOfSound, 1f); // floor so we don't divide by zero
            float mach = speed / a;

            return new AeroSample
            {
                Cd            = SafeEval(cdVsMach,      mach, profile.DragCoefficient),
                ClAlphaPerRad = SafeEval(clAlphaVsMach, mach, profile.LiftSlopePerRad),
                CmAlphaPerRad = SafeEval(cmAlphaVsMach, mach, -2f),
                CmDeltaPerRad = SafeEval(cmDeltaVsMach, mach, -2f),
            };
        }

        /// <summary>
        /// Evaluate the curve at `t`. If the curve is null or has no keys (designer hasn't
        /// authored it yet), fall back to a sensible default — better than NaN.
        /// </summary>
        private static float SafeEval(AnimationCurve curve, float t, float fallback)
        {
            if (curve == null || curve.length == 0) return fallback;
            return curve.Evaluate(t);
        }
    }
}
