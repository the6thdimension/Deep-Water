using UnityEngine;

namespace RH.Shared.Contracts
{
    /// <summary>
    /// One hit on one target (Gunnery K3): who hit whom, when, where, with what. A weapon module produces it (Gunnery
    /// for rounds, Guided Fury for warheads); the target's own module applies it to its hit points (Air Wing for jets,
    /// General Quarters for ships). The weapon never touches the target's state, so a ship and a jet can be hit by the
    /// same round without either module knowing the other.
    /// </summary>
    public readonly struct HitReport
    {
        /// <summary>The tick of the hit, on the producer's clock.</summary>
        public readonly long Tick;
        public readonly int ShooterId, TargetId;
        /// <summary>What hit: a gun profile's or a missile profile's name, for logs.</summary>
        public readonly string Weapon;
        /// <summary>Rounds that hit (a warhead counts as one).</summary>
        public readonly int Rounds;
        /// <summary>Where, world frame.</summary>
        public readonly Vector3 Point;
        /// <summary>The impact speed relative to the target, m/s.</summary>
        public readonly float ImpactSpeedMps;

        public HitReport(long tick, int shooterId, int targetId, string weapon, int rounds, Vector3 point, float impactSpeedMps)
        {
            Tick = tick;
            ShooterId = shooterId;
            TargetId = targetId;
            Weapon = weapon;
            Rounds = rounds;
            Point = point;
            ImpactSpeedMps = impactSpeedMps;
        }

        public override string ToString() => System.FormattableString.Invariant(
            $"tick {Tick} · {ShooterId} hit {TargetId} with {Rounds} × {Weapon} at {ImpactSpeedMps:F0} m/s");
    }
}
