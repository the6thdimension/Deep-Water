using System;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.State;

namespace DeepWater.AirWing.Core.Seats
{
    /// <summary>
    /// Keeps issuing the last inputs it was given (W18). It stands in for an empty seat until RH Aviator's AI seat
    /// exists (Phase 3), and it is what a seat falls back to when the player hands an aircraft over.
    /// </summary>
    public sealed class HoldSeat : ISeat
    {
        PilotInputs _inputs;

        public HoldSeat(PilotInputs inputs) => _inputs = inputs.Clamped();

        public SeatKind Kind => SeatKind.Hold;

        public void Hold(PilotInputs inputs) => _inputs = inputs.Clamped();

        public PilotInputs Issue(long tick, int aircraftId, in AirframeState state) => _inputs;
    }

    /// <summary>
    /// Plays an input log back (W28): at each tick it issues exactly what the log holds for that aircraft. A tick
    /// the log does not cover is an error, not a guess.
    /// </summary>
    public sealed class ReplaySeat : ISeat
    {
        readonly InputLog _log;

        public ReplaySeat(InputLog log) => _log = log ?? throw new ArgumentNullException(nameof(log));

        public SeatKind Kind => SeatKind.Replay;

        public PilotInputs Issue(long tick, int aircraftId, in AirframeState state)
        {
            if (!_log.TryGet(tick, aircraftId, out InputEntry entry))
                throw new InvalidOperationException($"The replayed log has no inputs for aircraft {aircraftId} at tick {tick}.");
            return entry.Inputs;
        }
    }
}
