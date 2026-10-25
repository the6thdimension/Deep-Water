using DeepWater.AirWing.Core.State;

namespace DeepWater.AirWing.Core.Inputs
{
    /// <summary>Who issued an aircraft's Pilot Inputs. Recorded with every entry in the input log.</summary>
    public enum SeatKind : byte
    {
        /// <summary>The player's devices: stick, pad, keyboard, or the mouse instructor working the stick.</summary>
        Player = 0,
        /// <summary>An RH Aviator AI pilot (Phase 3).</summary>
        Ai = 1,
        /// <summary>Keeps the last inputs; stands in for an empty seat until the AI seat exists.</summary>
        Hold = 2,
        /// <summary>A scenario or test script.</summary>
        Scenario = 3,
        /// <summary>Plays an input log back (W28).</summary>
        Replay = 4,
    }

    /// <summary>
    /// A seat is an issuer, not a code path (C19, W18): whoever sits in it, the aircraft receives the same
    /// <see cref="PilotInputs"/> through the same door. Seats can change hands between ticks; the input log
    /// keeps the same shape either way.
    /// </summary>
    public interface ISeat
    {
        SeatKind Kind { get; }

        /// <summary>
        /// This tick's Pilot Inputs for <paramref name="aircraftId"/>, decided from its state at the start of the
        /// tick. Called once per aircraft per tick, inside the fixed tick, before the core steps.
        /// </summary>
        PilotInputs Issue(long tick, int aircraftId, in AirframeState state);
    }
}
