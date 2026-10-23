using UnityEngine;

namespace RH.Shared.Contracts
{
    /// <summary>
    /// Universal target-track contract — the lingua franca between sensors (radar, EO/IR,
    /// IRST) and consumers (missile seekers, AI aircraft, fire-control panels).
    ///
    /// **Design intent (P-shared-isolation):** none of the producer / consumer modules
    /// reference each other. Both reference RH.Shared. A radar publishes ITargetTrack;
    /// a seeker consumes ITargetTrack. Either module can be swapped or rewritten without
    /// touching the other.
    ///
    /// **State, not events.** A track is a *current best estimate*. Sensors update tracks
    /// each step; consumers poll for the latest. Avoids ordering / replay / loss bugs
    /// inherent to event-only contracts. Stale tracks self-report via `LastUpdated`.
    /// </summary>
    public interface ITargetTrack
    {
        /// <summary>Stable identifier across updates. Same physical target = same id.</summary>
        int TrackId { get; }

        /// <summary>World-frame estimated position. Includes sensor noise/error.</summary>
        Vector3 Position { get; }

        /// <summary>World-frame estimated velocity. May be zero for new tracks.</summary>
        Vector3 Velocity { get; }

        /// <summary>True iff the track is currently locked (high-quality, declared).</summary>
        bool IsLocked { get; }

        /// <summary>Track quality 0..1. 0 = just acquired, 1 = high-confidence sustained lock.</summary>
        float Quality { get; }

        /// <summary>Time.timeAsDouble of the most recent update. Consumers ignore stale tracks.</summary>
        double LastUpdated { get; }

        /// <summary>Optional classification (e.g. "Aircraft", "Missile", "Ship"). Empty = unknown.</summary>
        string Classification { get; }
    }
}
