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
        private readonly bool midcourseDatalink;

        public SeekerTargetSource(ISeeker seeker, ITargetSource truthSource, bool midcourseDatalink = false)
        {
            this.seeker = seeker ?? throw new System.ArgumentNullException(nameof(seeker));
            this.truthSource = truthSource; // null is allowed → seeker will simply never lock
            // Midcourse datalink: while the seeker has NO lock, guidance flies on the truth
            // track (modeling launcher-fed command guidance / TVM midcourse updates); once
            // the seeker acquires, its own observation takes over (terminal homing). This is
            // what lets a vertically-launched round tip over toward a target 90 degrees off
            // boresight instead of climbing ballistically forever. Opt-in per profile.
            this.midcourseDatalink = midcourseDatalink;
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
            TargetTrack observation = seeker.GetObservation();
            if (!observation.HasTrack && midcourseDatalink && truthSource != null)
                return truthSource.Sample(); // datalink midcourse until the seeker acquires
            return observation;
        }
    }
}
