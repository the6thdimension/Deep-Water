using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Guidance
{
    /// <summary>
    /// A guidance law: a pure function from (own state + target observation + profile) to a
    /// command for the physics integrator.
    ///
    /// **Contract:**
    /// - Pure function. Implementations MUST NOT hold per-missile state. If a law needs
    ///   history (e.g. a smoothed LOS rate), it derives it from analytical kinematics in the
    ///   context — not from internal fields. This keeps laws thread-safe, shareable as
    ///   singletons, and Burst-portable.
    /// - Deterministic. No `UnityEngine.Random`, no global side-channel reads. If randomness
    ///   is needed (seeker noise at L5), it comes via injected RNG (P7).
    /// - If `context.Target.HasTrack` is false, the law MUST return a default
    ///   <see cref="MissileCommand"/> (zero accel, zero throttle). Guidance over no track is
    ///   silently doing nothing — that's the contract.
    /// </summary>
    public interface IGuidanceLaw
    {
        /// <summary>
        /// Identifier for the law kind, useful for diagnostics and authoring. Each
        /// implementation returns a stable enum value.
        /// </summary>
        GuidanceLawKind Kind { get; }

        /// <summary>
        /// Compute the command for this fixed step. See contract on the interface.
        /// </summary>
        MissileCommand ComputeCommand(in GuidanceContext context, in MissileProfileData profile);
    }

    /// <summary>
    /// Enumeration of guidance laws available in the system. Authors pick one of these in the
    /// missile profile; the entity instantiates the matching law at launch.
    /// </summary>
    public enum GuidanceLawKind : byte
    {
        /// <summary>No guidance. Dumb bombs, unguided rockets, sounding-rocket Phase 1 test cases.</summary>
        None                     = 0,

        /// <summary>Aim-at-target. Cheap, simple, useful as a sanity baseline. Tail-chase only — no lead.</summary>
        Pursuit                  = 1,

        /// <summary>Proportional Navigation. The workhorse intercept law. Honors target motion via LOS rate.</summary>
        ProportionalNavigation   = 2,

        // Augmented ProNav, lead pursuit, command guidance, terrain-following, etc. will arrive
        // as later phases need them. Adding values is non-breaking; removing is not.
    }
}
