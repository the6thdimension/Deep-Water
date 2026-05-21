namespace GuidedFury.Core.Seekers
{
    /// <summary>
    /// Identifies a seeker implementation. Authors pick one in the missile profile; the
    /// missile builds the matching seeker at launch.
    ///
    /// **Phase 3 surface:** generic cone-based geometry only. Phase 4+ adds signal-aware
    /// seekers (heat signature for IR, RCS for radar, illumination for SARH).
    /// </summary>
    public enum SeekerKind : byte
    {
        /// <summary>No seeker. Guidance uses raw truth-target data via TransformTargetSource.</summary>
        None       = 0,

        /// <summary>Geometric cone seeker: target must be inside FOV and within range to acquire.</summary>
        ConeSeeker = 1,
    }
}
