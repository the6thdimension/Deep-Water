using UnityEngine;

namespace RH.Shared.Contracts
{
    /// <summary>
    /// Threat-side contract: anything in the world that a sensor can detect or an AI
    /// agent can prioritize. Implemented by aircraft, missiles, ships, ground vehicles
    /// — anything with a position + radar/IR signature.
    ///
    /// **Why this exists as a separate contract from MonoBehaviour:** sensors and AI
    /// shouldn't have to know the *concrete type* of a target. They iterate
    /// `IThreat` instances (collected by a scene registry or a query helper) and reason
    /// about signature, position, velocity, faction. Lets a missile track an aircraft,
    /// a ship, or another missile through the same code path.
    ///
    /// **Faction model:** integer faction id (0 = neutral, 1+ = combatants). Producer
    /// systems decide their own faction scheme — `IsHostileTo` is the single decision point.
    /// </summary>
    public interface IThreat
    {
        /// <summary>Where the threat is, world frame.</summary>
        Vector3 Position { get; }

        /// <summary>Velocity, world frame. Zero for static threats.</summary>
        Vector3 Velocity { get; }

        /// <summary>Radar cross section, m². Affects detection range. ~5 = fighter, ~50k = destroyer.</summary>
        float RadarCrossSectionM2 { get; }

        /// <summary>Infrared signature, dimensionless 0..∞. ~1 = idle fighter, ~10 = afterburner.</summary>
        float InfraredSignature { get; }

        /// <summary>True if this threat still exists / is alive / hasn't been destroyed.</summary>
        bool IsAlive { get; }

        /// <summary>Coarse classification ("Aircraft", "Missile", "Ship", "Ground"). For UI + AI priority.</summary>
        string Classification { get; }

        /// <summary>Faction id. Sensors / AI use IsHostileTo() to decide if the threat is engageable.</summary>
        int FactionId { get; }

        /// <summary>True if `other` is a faction this threat is hostile to.</summary>
        bool IsHostileTo(int otherFactionId);
    }
}
