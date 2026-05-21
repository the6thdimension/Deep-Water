using GuidedFury.Core.Guidance;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Seekers
{
    /// <summary>
    /// A missile seeker. Observes the world (via truth sources) and decides what the
    /// guidance loop is allowed to see.
    ///
    /// **Contract:**
    /// - The seeker is stepped once per FixedUpdate by the entity, before guidance runs.
    /// - Returns either a valid <see cref="TargetTrack"/> (when locked) or
    ///   <see cref="TargetTrack.None"/> (when not).
    /// - Implementations may be stateful (track lock-state, gimbal angle, etc.) so each
    ///   missile gets its own instance — they are NOT shared singletons like guidance laws.
    /// - Implementations MUST be deterministic given the same inputs (truth target +
    ///   missile state + dt). Stochastic effects (glint noise, jitter) come later via
    ///   injected RNG (P7) at L5.
    ///
    /// **Why a separate Update step (vs. just sampling in ITargetSource):** seekers have
    /// real time-evolving state — acquisition dwell, lock-loss timer, gimbal slew. We need
    /// a clean tick point so that state advances by exactly dt seconds per call. The target
    /// source then just reports the seeker's current observation.
    /// </summary>
    public interface ISeeker
    {
        SeekerKind Kind { get; }

        /// <summary>True iff the seeker currently has lock on a target.</summary>
        bool HasLock { get; }

        /// <summary>
        /// Advance seeker state by `dt` seconds. The seeker reads the truth observation
        /// (where the target actually is) and updates its internal lock state.
        /// </summary>
        /// <param name="missileState">Own state (position, orientation, velocity).</param>
        /// <param name="truthTarget">Ground-truth target observation (omniscient).
        ///   Pass <see cref="TargetTrack.None"/> if there's no truth target available.</param>
        /// <param name="dt">Fixed step duration in seconds.</param>
        void Update(in MissileState missileState, in TargetTrack truthTarget, float dt);

        /// <summary>
        /// Return what the seeker reports to guidance. If <see cref="HasLock"/> is false,
        /// this MUST return <see cref="TargetTrack.None"/>.
        /// </summary>
        TargetTrack GetObservation();
    }
}
