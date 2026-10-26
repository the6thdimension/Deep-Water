using System;
using System.Collections.Generic;
using DeepWater.AirWing.Core.State;
using UnityEngine;

namespace DeepWater.AirWing.Core.Simulation
{
    /// <summary>One aircraft's id and state, as the Fix reads it.</summary>
    public readonly struct AircraftRecord
    {
        public readonly int Id;
        public readonly AirframeState State;

        public AircraftRecord(int id, AirframeState state)
        {
            Id = id;
            State = state;
        }
    }

    /// <summary>One tick's fingerprint, and whether any aircraft touched a surface through PhysX on it (W7).</summary>
    public readonly struct FixRecord
    {
        public readonly long Tick;
        public readonly ulong Hash;
        /// <summary>Contact ticks repeat on the same machine only; a divergence there is not a core bug by itself.</summary>
        public readonly bool Contact;

        public FixRecord(long tick, ulong hash, bool contact)
        {
            Tick = tick;
            Hash = hash;
            Contact = contact;
        }
    }

    /// <summary>
    /// The flight state's fingerprint (Reckoning's "Fix", W7): FNV-1a over exact bit patterns. Two runs with the
    /// same seed and the same input log must produce the same Fix on every tick; the first tick that differs
    /// names where they split. The mixing is copied from General Quarters' CombatFix, not referenced (W31).
    /// </summary>
    public static class FlightFix
    {
        public const ulong Offset = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;

        public static ulong Mix(ulong h, uint w)
        {
            for (int b = 0; b < 4; b++)
            {
                h ^= (w >> (8 * b)) & 0xFF;
                h *= Prime;
            }
            return h;
        }

        public static ulong Mix(ulong h, int v) => Mix(h, unchecked((uint)v));
        public static ulong Mix(ulong h, long v) => Mix(Mix(h, unchecked((uint)v)), unchecked((uint)(v >> 32)));
        public static ulong Mix(ulong h, bool v) => Mix(h, v ? 1u : 0u);
        public static ulong Mix(ulong h, float v) => Mix(h, unchecked((uint)BitConverter.SingleToInt32Bits(v)));
        public static ulong Mix(ulong h, Vector3 v) => Mix(Mix(Mix(h, v.x), v.y), v.z);
        public static ulong Mix(ulong h, Quaternion q) => Mix(Mix(Mix(Mix(h, q.x), q.y), q.z), q.w);

        public static ulong Mix(ulong h, in AirframeState s)
        {
            h = Mix(h, s.Position);
            h = Mix(h, s.Velocity);
            h = Mix(h, s.Attitude);
            h = Mix(h, s.BodyRates);
            h = Mix(h, s.LoadFactor);
            h = Mix(h, s.AngleOfAttack);
            h = Mix(h, s.MassKg);
            h = Mix(h, s.FuelKg);
            h = Mix(h, s.EngineSpool);
            h = Mix(h, s.AfterburnerSpool);
            h = Mix(h, (int)s.Status);
            h = Mix(h, s.InContact);
            h = Mix(h, s.Controls.StabilatorRad);
            h = Mix(h, s.Controls.AileronRad);
            h = Mix(h, s.Controls.RudderRad);
            h = Mix(h, s.Controls.PitchIntegralRadS);
            return Mix(h, s.Controls.LoadFactorCommand);
        }

        /// <summary>
        /// The Fix of one tick. <paramref name="aircraft"/> must be in ascending id order, so the same fleet always
        /// hashes the same way; anything else throws.
        /// </summary>
        public static FixRecord Of(long tick, IReadOnlyList<AircraftRecord> aircraft)
        {
            ulong h = Mix(Offset, tick);
            bool contact = false;
            int lastId = int.MinValue;
            for (int i = 0; i < aircraft.Count; i++)
            {
                AircraftRecord a = aircraft[i];
                if (i > 0 && a.Id <= lastId)
                    throw new ArgumentException($"Fix input must be in ascending id order: {a.Id} after {lastId}.", nameof(aircraft));
                lastId = a.Id;
                h = Mix(Mix(h, a.Id), a.State);
                contact |= a.State.InContact;
            }
            return new FixRecord(tick, h, contact);
        }
    }

    /// <summary>The run's Fix sequence, one record per tick, and where two runs first part company.</summary>
    public sealed class FixLog
    {
        readonly List<FixRecord> _all = new List<FixRecord>();

        public IReadOnlyList<FixRecord> All => _all;

        public void Append(FixRecord record)
        {
            if (_all.Count > 0 && record.Tick <= _all[_all.Count - 1].Tick)
                throw new InvalidOperationException($"Fix log is append-only: tick {record.Tick} after tick {_all[_all.Count - 1].Tick}.");
            _all.Add(record);
        }

        /// <summary>
        /// Index of the first record that differs from <paramref name="other"/> (tick or hash), or of the first record
        /// only one log has; −1 when the two are identical.
        /// </summary>
        public int FirstDivergence(FixLog other)
        {
            int n = Math.Min(_all.Count, other._all.Count);
            for (int i = 0; i < n; i++)
                if (_all[i].Tick != other._all[i].Tick || _all[i].Hash != other._all[i].Hash)
                    return i;
            return _all.Count == other._all.Count ? -1 : n;
        }
    }
}
