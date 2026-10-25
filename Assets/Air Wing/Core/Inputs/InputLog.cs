using System;
using System.Collections.Generic;

namespace DeepWater.AirWing.Core.Inputs
{
    /// <summary>One aircraft's Pilot Inputs for one tick, and the seat that issued them.</summary>
    public readonly struct InputEntry
    {
        public readonly long Tick;
        public readonly int AircraftId;
        public readonly SeatKind Seat;
        public readonly PilotInputs Inputs;

        public InputEntry(long tick, int aircraftId, SeatKind seat, PilotInputs inputs)
        {
            Tick = tick;
            AircraftId = aircraftId;
            Seat = seat;
            Inputs = inputs;
        }

        public override string ToString() => FormattableString.Invariant($"tick {Tick} aircraft {AircraftId} {Seat}: {Inputs}");
    }

    /// <summary>
    /// The append-only record of every tick's Pilot Inputs (W28). Feeding it back reproduces the run, so it is
    /// what replay, saves and multiplayer will stand on. Entries arrive in tick order with at most one per
    /// aircraft per tick; anything else is a bug in the caller and throws.
    /// </summary>
    public sealed class InputLog
    {
        readonly List<InputEntry> _all = new List<InputEntry>();
        readonly List<int> _aircraftThisTick = new List<int>();
        long _lastTick = long.MinValue;

        /// <summary>Every entry, in the order it was logged (tick order).</summary>
        public IReadOnlyList<InputEntry> All => _all;

        public int Count => _all.Count;

        /// <summary>Log <paramref name="inputs"/> (clamped; see <see cref="PilotInputs.Clamped"/>) and return the stored entry.</summary>
        public InputEntry Append(long tick, int aircraftId, SeatKind seat, PilotInputs inputs)
        {
            if (tick < _lastTick)
                throw new InvalidOperationException($"Input log is append-only: tick {tick} arrived after tick {_lastTick}.");
            if (tick != _lastTick)
            {
                _lastTick = tick;
                _aircraftThisTick.Clear();
            }
            if (_aircraftThisTick.Contains(aircraftId))
                throw new InvalidOperationException($"Aircraft {aircraftId} already has inputs for tick {tick}.");
            _aircraftThisTick.Add(aircraftId);

            var entry = new InputEntry(tick, aircraftId, seat, inputs.Clamped());
            _all.Add(entry);
            return entry;
        }

        /// <summary>The entry for <paramref name="aircraftId"/> at <paramref name="tick"/>, if one was logged.</summary>
        public bool TryGet(long tick, int aircraftId, out InputEntry entry)
        {
            // Entries are in tick order: binary-search the first entry of the tick, then scan that tick.
            int lo = 0, hi = _all.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_all[mid].Tick < tick) lo = mid + 1;
                else hi = mid;
            }
            for (int i = lo; i < _all.Count && _all[i].Tick == tick; i++)
            {
                if (_all[i].AircraftId != aircraftId) continue;
                entry = _all[i];
                return true;
            }
            entry = default;
            return false;
        }
    }
}
