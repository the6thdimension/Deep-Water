namespace GuidedFury.Core.Aero
{
    /// <summary>
    /// Per-step aerodynamic coefficient sample. Produced by <see cref="IAeroModel.Sample"/>
    /// once per FixedUpdate; consumed by the L4 integrator to compute forces and moments.
    ///
    /// **Conventions:** all coefficients dimensionless. Forces and moments are computed as
    /// `coefficient × q × A × (arm)`, where:
    /// - q = dynamic pressure = ½·ρ·v²
    /// - A = reference area (profile.ReferenceAreaM2)
    /// - arm = reference length for moments (profile.LengthM, or a per-coefficient override)
    ///
    /// **Units / meaning:**
    /// - Cd: drag along velocity vector. Always ≥ 0. Range ~0.1–0.5 typical for slender missile bodies.
    /// - ClAlphaPerRad: lift slope per radian of angle of attack. Range ~5–15 typical (slender body theory ~2·π).
    /// - CmAlphaPerRad: pitching moment slope per radian of AoA. **Negative = stable** (pitches body back toward velocity); positive = unstable.
    /// - CmDeltaPerRad: pitching moment per radian of control surface deflection. Sign convention: positive δ produces positive Cm (nose-up).
    /// </summary>
    public struct AeroSample
    {
        public float Cd;
        public float ClAlphaPerRad;
        public float CmAlphaPerRad;
        public float CmDeltaPerRad;
    }
}
