using GuidedFury.Core.Guidance;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Seekers
{
    /// <summary>
    /// An <see cref="ITargetSource"/> that filters a truth source through a seeker.
    ///
    /// **Why this exists:** guidance laws ask their target source for an observation. With
    /// a raw <see cref="TransformTargetSource"/>, guidance sees truth directly. With this
    /// wrapper, guidance only sees the truth *when the seeker has lock* — exactly the seam
    /// real missiles have.
    ///
    /// **Per-step flow** (driven by the entity's Update call on this source):
    /// 1. Update the underlying truth source so it has fresh data.
    /// 2. Pass the truth observation to the seeker so it can update its lock state.
    /// 3. Subsequent Sample() returns the seeker's current observation (None if no lock).
    /// </summary>
    public sealed class SeekerTargetSource : ITargetSource
    {
        private readonly ISeeker seeker;
        private readonly ITargetSource truthSource;

        public SeekerTargetSource(ISeeker seeker, ITargetSource truthSource)
        {
            this.seeker = seeker ?? throw new System.ArgumentNullException(nameof(seeker));
            this.truthSource = truthSource; // null is allowed → seeker will simply never lock
        }

        /// <summary>True iff the underlying seeker currently has lock.</summary>
        public bool HasLock => seeker.HasLock;

        public void Update(in MissileState missileState, float dt)
        {
            // 1) Refresh truth.
            truthSource?.Update(in missileState, dt);

            // 2) Pass truth into the seeker so it can update its lock.
            TargetTrack truth = truthSource != null ? truthSource.Sample() : TargetTrack.None;
            seeker.Update(in missileState, in truth, dt);
        }

        public TargetTrack Sample()
        {
            return seeker.GetObservation();
        }
    }
}
