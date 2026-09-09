namespace GuidedFury.Core.Aero
{
    /// <summary>
    /// Identifies an aero model implementation. Profile picks one of these; the missile
    /// instantiates the matching <see cref="IAeroModel"/> at launch.
    /// </summary>
    public enum AeroModelKind : byte
    {
        /// <summary>L3-equivalent scalar aero. Uses profile.DragCoefficient and profile.LiftSlopePerRad directly; no Mach dependence. Cheap fallback when authoring tables isn't worth it.</summary>
        Simple    = 0,

        /// <summary>Tabulated aero — Mach-aware curves for Cd, Cl_α, Cm_α, Cm_δ. Authored in the MissileProfileSO via AnimationCurves.</summary>
        Tabulated = 1,
    }
}
