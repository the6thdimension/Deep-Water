using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Aero
{
    /// <summary>
    /// Aerodynamic coefficient producer. Pure function from (missile state, atmosphere,
    /// profile) → <see cref="AeroSample"/>. The L4 integrator consumes the sample to compute
    /// forces and moments.
    ///
    /// **Contract:**
    /// - Pure function. No hidden state. Same inputs → same outputs.
    /// - Deterministic. No `UnityEngine.Random`, no global side-channel reads. Per P7, any
    ///   randomness (gust noise, etc.) comes via injected RNG — none in Phase 1.
    /// - Implementations are typically stateless and shareable as singletons. Per-missile
    ///   data (curves, coefficients) belongs on the implementation instance (e.g.
    ///   <see cref="TabulatedAeroModel"/> caches the AnimationCurves it was built with).
    /// </summary>
    public interface IAeroModel
    {
        AeroModelKind Kind { get; }

        /// <summary>
        /// Sample aerodynamic coefficients for the current step.
        /// </summary>
        /// <param name="state">Missile state (used for Mach calc via velocity + atmosphere).</param>
        /// <param name="atmo">Atmosphere sample at the missile's current altitude.</param>
        /// <param name="profile">Profile (in case the model wants scalar fallbacks).</param>
        AeroSample Sample(in MissileState state, in AtmosphereSample atmo, in MissileProfileData profile);
    }
}
