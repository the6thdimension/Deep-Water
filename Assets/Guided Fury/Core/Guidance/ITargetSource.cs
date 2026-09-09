using GuidedFury.Core.State;

namespace GuidedFury.Core.Guidance
{
    /// <summary>
    /// Source of target observations for a missile's guidance loop. Polled once per
    /// FixedUpdate by the entity.
    ///
    /// **Contract — Phase 3:** target sources can be stateful (finite-difference velocity
    /// estimation, seeker lock state). The entity calls <see cref="Update"/> once per fixed
    /// step *before* asking guidance for a command, so any internal state (dwell timers,
    /// previous-position cache) is current when <see cref="Sample"/> is read.
    ///
    /// **Stateless implementations** can just no-op <see cref="Update"/>.
    ///
    /// **Phase 3 implementations:**
    /// - <see cref="TransformTargetSource"/> — omniscient truth source backed by a Unity
    ///   Transform; computes velocity via finite difference in <see cref="Update"/>.
    /// - <see cref="Seekers.SeekerTargetSource"/> — wraps an <see cref="Seekers.ISeeker"/>;
    ///   advances seeker lock state in <see cref="Update"/>, returns the seeker's
    ///   observation in <see cref="Sample"/>.
    /// </summary>
    public interface ITargetSource
    {
        /// <summary>
        /// Advance any internal state by `dt` seconds. Called by the entity once per
        /// FixedUpdate before guidance is evaluated.
        /// </summary>
        void Update(in MissileState missileState, float dt);

        /// <summary>
        /// Return the current target observation. Implementations should return
        /// <see cref="TargetTrack.None"/> when no track is available.
        /// </summary>
        TargetTrack Sample();
    }
}
