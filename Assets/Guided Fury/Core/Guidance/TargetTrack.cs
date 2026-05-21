using UnityEngine;

namespace GuidedFury.Core.Guidance
{
    /// <summary>
    /// A guidance-time observation of a target. Produced by an <see cref="ITargetSource"/>,
    /// consumed by an <see cref="IGuidanceLaw"/>.
    ///
    /// **Phase 2 surface (intentionally omniscient):**
    /// - HasTrack: false means "no target available, do not steer." Guidance laws must respect
    ///   this and return a default (zero) command.
    /// - Position / Velocity: world-frame, meters and m/s. In Phase 2 these come straight from
    ///   a Unity Transform with no noise or lag.
    ///
    /// **Phase 3+ additions (planned, not present yet):**
    /// - AcquisitionTime / LastUpdateTime: for stale-track logic when the seeker loses lock.
    /// - SignalStrength / Confidence: for guidance laws that need to adapt to track quality.
    /// - Acceleration: needed by augmented ProNav (APN), which exploits known target accel.
    /// - SeekerFrameAngle: gimbal angle for FOV/boresight checks.
    /// Adding fields is non-breaking; removing is. We add as later phases need them.
    /// </summary>
    public struct TargetTrack
    {
        public bool    HasTrack;
        public Vector3 Position;
        public Vector3 Velocity;

        /// <summary>A "no target" track. Guidance laws will return zero command on this.</summary>
        public static TargetTrack None => default;

        /// <summary>Construct a fresh omniscient track from position + velocity.</summary>
        public static TargetTrack Omniscient(Vector3 position, Vector3 velocity)
        {
            return new TargetTrack
            {
                HasTrack = true,
                Position = position,
                Velocity = velocity,
            };
        }
    }
}
