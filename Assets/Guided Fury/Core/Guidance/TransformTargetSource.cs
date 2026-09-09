using UnityEngine;
using GuidedFury.Core.State;

namespace GuidedFury.Core.Guidance
{
    /// <summary>
    /// Omniscient target source that reads world-truth position from a Unity Transform.
    /// Velocity is estimated by finite difference between Update calls. The first Update
    /// produces zero velocity (no previous sample yet) — guidance laws that depend on
    /// target velocity must tolerate this for one tick.
    ///
    /// **Phase 3 simplification:** no FOV check, no lock-on, no noise, no occlusion. This
    /// is the "perfect knowledge" target source — appropriate as the *input* to a seeker
    /// (truth being filtered) or as a direct source for missiles without seekers.
    /// </summary>
    public sealed class TransformTargetSource : ITargetSource
    {
        private readonly Transform target;
        private Vector3 previousPosition;
        private Vector3 currentVelocity;
        private bool hasPrevious;
        private bool hasSample;

        public TransformTargetSource(Transform target)
        {
            this.target = target;
        }

        public void Update(in MissileState missileState, float dt)
        {
            // missileState is unused here — TransformTargetSource is omniscient and doesn't
            // care where the missile is. It's part of the ITargetSource contract because
            // stateful sources (seekers) need it.

            if (target == null)
            {
                hasSample = false;
                return;
            }

            Vector3 position = target.position;

            if (hasPrevious && dt > 1e-5f)
                currentVelocity = (position - previousPosition) / dt;
            else
                currentVelocity = Vector3.zero;

            previousPosition = position;
            hasPrevious = true;
            hasSample = true;
        }

        public TargetTrack Sample()
        {
            if (!hasSample || target == null)
                return TargetTrack.None;

            return TargetTrack.Omniscient(previousPosition, currentVelocity);
        }
    }
}
