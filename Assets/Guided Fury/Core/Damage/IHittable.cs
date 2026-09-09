using UnityEngine;

namespace GuidedFury.Core.Damage
{
    /// <summary>
    /// Minimal "respond to a missile impact" interface. Implementers receive notification
    /// when a missile detonates against / within proximity-fuze radius of them.
    ///
    /// **Phase 2.5 status:** small and pragmatic. The eventual Phase 4+ damage system will
    /// expand this with warhead profile, blast geometry, fragmentation pattern, kill
    /// criteria, etc., and probably grow into a richer `IDamageable` hierarchy. For now this
    /// is the contract the missile uses to tell anything it hits "you've been hit."
    /// </summary>
    public interface IHittable
    {
        /// <summary>
        /// Called by the missile on impact. Implementer decides what to do (apply impulse,
        /// play effect, destroy itself, etc.).
        /// </summary>
        /// <param name="impactPosition">World-space point of impact.</param>
        /// <param name="missileVelocity">Missile world velocity at impact (m/s).</param>
        /// <param name="missileMassKg">Missile mass at impact (kg).</param>
        void OnMissileHit(Vector3 impactPosition, Vector3 missileVelocity, float missileMassKg);
    }
}
