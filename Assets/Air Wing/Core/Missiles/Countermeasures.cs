using System;
using DeepWater.AirWing.Core.Simulation;

namespace DeepWater.AirWing.Core.Missiles
{
    /// <summary>
    /// A jet's chaff and flares (SENSORS-CONCEPT J4; Air Wing W69), baked from <c>AircraftProfileSO</c>, every number
    /// AUTHORED for a game: how many of each it carries (refilled on respawn), how often a held button drops one, and the
    /// chance each drop breaks a missile coming at it. Chaff works only on radar missiles, flares only on heat-seekers.
    /// </summary>
    public struct CountermeasureProfile
    {
        public bool IsSet;
        /// <summary>Chaff and flares carried at the start and after each respawn.</summary>
        public int Chaff, Flares;
        /// <summary>The chance one drop breaks each missile of its kind coming at this jet, 0 … 1.</summary>
        public float ChaffBreakChance, FlareBreakChance;
        /// <summary>The least time between two drops of the same kind, seconds: a held button drops one per interval.</summary>
        public float IntervalS;

        public void Validate()
        {
            if (Chaff < 0 || Flares < 0) throw new ArgumentException("countermeasures: chaff and flares carried must be zero or more");
            if (!(ChaffBreakChance >= 0f && ChaffBreakChance <= 1f && FlareBreakChance >= 0f && FlareBreakChance <= 1f))
                throw new ArgumentException("countermeasures: break chances 0 … 1");
            if (!(IntervalS > 0f)) throw new ArgumentException("countermeasures: the drop interval must be positive");
        }

        /// <summary>
        /// Whether a drop this tick works on a missile coming at the jet: chaff on a radar missile, flares on a heat-seeker,
        /// each at its chance. One draw from <paramref name="draws"/> only when the kind matches, so a drop that cannot work
        /// never shifts the sequence.
        /// </summary>
        public bool Breaks(bool radarMissile, bool chaff, bool flare, DeterministicRng draws)
        {
            if (radarMissile && chaff) return draws.NextFloat01() < ChaffBreakChance;
            if (!radarMissile && flare) return draws.NextFloat01() < FlareBreakChance;
            return false;
        }
    }
}
