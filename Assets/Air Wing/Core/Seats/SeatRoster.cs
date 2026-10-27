using System;
using System.Collections.Generic;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.State;

namespace DeepWater.AirWing.Core.Seats
{
    /// <summary>
    /// Who flies what (W18, W42). There is one player seat; it sits in one aircraft at a time, and every other aircraft
    /// is flown by its own seat: the AI pilot RH Aviator gave it, or, without one, a <see cref="HoldSeat"/> that keeps
    /// the inputs it was left with. <see cref="Cycle"/> moves the player to the next aircraft in id order; the one left
    /// goes back to its own seat at once. Changes take effect on the next tick, and the input log keeps its shape: one
    /// entry per aircraft per tick, with the seat that issued it.
    /// <para>Air Wing never learns what an AI is (W5): RH Aviator hands over an <see cref="ISeat"/> through
    /// <see cref="SetOwnSeat"/>, nothing more.</para>
    /// </summary>
    public sealed class SeatRoster
    {
        readonly FlightCore _core;
        readonly PlayerSeat _player;
        readonly Dictionary<int, ISeat> _own = new Dictionary<int, ISeat>();

        /// <summary>The aircraft the player flies, or 0 when the player sits in none.</summary>
        public int PlayerAircraftId { get; private set; }

        /// <summary>Raised after the player moves: the aircraft left (0 for none) and the one taken.</summary>
        public event Action<int, int> Moved;

        /// <param name="playerAircraftId">The aircraft the core already seated the player in at the start (0: none).</param>
        public SeatRoster(FlightCore core, PlayerSeat player, int playerAircraftId = 0)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _player = player;
            if (playerAircraftId != 0 && (player == null || _core.SeatOf(playerAircraftId) != player))
                throw new ArgumentException($"aircraft {playerAircraftId} is not seated with the player seat", nameof(playerAircraftId));
            PlayerAircraftId = playerAircraftId;
        }

        public PlayerSeat Player => _player;

        /// <summary>
        /// The seat that flies <paramref name="id"/> whenever the player is not in it. If the player is not in it now,
        /// it takes the aircraft at once.
        /// </summary>
        public void SetOwnSeat(int id, ISeat seat)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            if (seat is PlayerSeat) throw new ArgumentException("the player seat is not an aircraft's own seat", nameof(seat));
            _core.Find(id);   // throws for an unknown aircraft
            _own[id] = seat;
            if (id != PlayerAircraftId) _core.SetSeat(id, seat);
        }

        /// <summary>The seat that flies <paramref name="id"/> without the player, or null when none was given.</summary>
        public ISeat OwnSeatOf(int id) => _own.TryGetValue(id, out ISeat s) ? s : null;

        /// <summary>Put the player in aircraft <paramref name="id"/> (0: in none). The aircraft left goes to its own seat.</summary>
        public void TakeSeat(int id)
        {
            if (_player == null) throw new InvalidOperationException("there is no player seat to move");
            int left = PlayerAircraftId;
            if (id == left) return;
            if (left != 0)
            {
                Aircraft a = _core.Find(left);
                ISeat own = OwnSeatOf(left);
                // Without an AI pilot, the jet holds what the player last gave it (W18).
                _core.SetSeat(left, own ?? new HoldSeat(a.LastInputs));
            }
            if (id != 0)
            {
                Aircraft taken = _core.Find(id);
                _player.TakeOver(taken.State, taken.LastInputs);
                _core.SetSeat(id, _player);
            }
            PlayerAircraftId = id;
            Moved?.Invoke(left, id);
        }

        /// <summary>
        /// Move the player to the next aircraft that has not crashed, in id order, wrapping round (W42). Returns the
        /// aircraft now flown; the same one when there is no other.
        /// </summary>
        public int Cycle()
        {
            if (_player == null) return PlayerAircraftId;
            IReadOnlyList<Aircraft> all = _core.Aircraft;
            int start = 0;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Id == PlayerAircraftId) { start = i + 1; break; }
            for (int k = 0; k < all.Count; k++)
            {
                Aircraft a = all[(start + k) % all.Count];
                if (a.Id == PlayerAircraftId || a.State.Status != FlightStatus.Flying) continue;
                TakeSeat(a.Id);
                break;
            }
            return PlayerAircraftId;
        }

        /// <summary>One line for the HUD's pick list: who flies <paramref name="id"/> (W42).</summary>
        public string Describe(int id)
        {
            if (id == PlayerAircraftId)
                return _player.Owner == DeviceSource.None ? "YOU" : $"YOU · {_player.Owner.ToString().ToUpperInvariant()}";
            ISeat seat = _core.SeatOf(id);
            string kind = seat.Kind.ToString().ToUpperInvariant();
            return seat is ISeatStatus status ? $"{kind} · {status.Status}" : kind;
        }
    }
}
