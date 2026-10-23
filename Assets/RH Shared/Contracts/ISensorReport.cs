using System.Collections.Generic;

namespace RH.Shared.Contracts
{
    /// <summary>
    /// Producer-side contract for any sensor that emits target tracks. Implemented by
    /// radar entities, EO/IR systems, IRSTs, sonar (eventually). Consumers (missile
    /// seekers, AI tactical brain, fire-control UI) only see the interface — never the
    /// concrete sensor type.
    ///
    /// **Snapshot pattern.** `GetCurrentTracks` returns the producer's current view; the
    /// consumer is free to copy / filter / cache. The implementation is responsible for
    /// not handing out internal mutable state — return a read-only collection or a copy.
    /// </summary>
    public interface ISensorReport
    {
        /// <summary>Human-readable sensor name (e.g. "SPG-62", "APG-79"). For HUDs / logs.</summary>
        string SensorName { get; }

        /// <summary>True if the sensor is currently powered + radiating / scanning.</summary>
        bool IsActive { get; }

        /// <summary>All tracks the sensor currently holds. Read-only snapshot.</summary>
        IReadOnlyList<ITargetTrack> GetCurrentTracks();

        /// <summary>Look up a single track by id. Returns null if not held.</summary>
        ITargetTrack GetTrack(int trackId);
    }
}
