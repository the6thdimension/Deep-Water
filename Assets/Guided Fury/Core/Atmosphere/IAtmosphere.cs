namespace GuidedFury.Core.Atmosphere
{
    /// <summary>
    /// Atmosphere model interface. Phase 1 has one implementation (StandardAtmosphere = ISA);
    /// the interface exists so that environment-specific atmospheres (Mars demo, custom
    /// scenarios, weather-perturbed) can slot in without touching integrators.
    ///
    /// **Contract:**
    /// - Sample MUST be a pure function of altitudeMetersMsl. No hidden state, no RNG.
    /// - Determinism is non-negotiable here: integrators are deterministic; the atmosphere
    ///   they sample must be too. Stochastic atmosphere effects (turbulence) go in a separate
    ///   IDisturbance interface later, with seeded RNG (P7).
    /// - Implementations must remain valid for altitudes from -500 m (Dead Sea-ish) to at
    ///   least 80,000 m (ISA upper bound). Below or above, returning the boundary sample is
    ///   acceptable; throwing is not.
    /// </summary>
    public interface IAtmosphere
    {
        /// <summary>
        /// Sample the atmosphere at the given altitude above mean sea level (meters).
        /// </summary>
        AtmosphereSample Sample(float altitudeMetersMsl);
    }
}
