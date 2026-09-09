using System.Collections.Generic;
using UnityEngine;
using GuidedFury.ScriptableObjects.Profiles;

namespace GuidedFury.Core.Missile
{
    /// <summary>
    /// Shared detonation logic: explosion VFX/SFX spawning and the physical blast impulse.
    /// Extracted from <see cref="MissileBehaviour"/> so non-missile ordnance (emplaced
    /// demolition charges, future bombs) can detonate with identical, profile-driven
    /// behavior instead of duplicating the FX + physics code.
    ///
    /// All tuning lives on the <see cref="MissileProfileSO"/> (explosion prefabs, lifetime,
    /// SFX, blast radius/impulse) — this class is pure mechanism.
    /// </summary>
    public static class DetonationEffects
    {
        // Shared, allocation-free scratch buffers for the blast overlap query. Main-thread
        // only; fully re-filled/cleared on every call.
        private static readonly Collider[] BlastOverlapBuffer = new Collider[256];
        private static readonly HashSet<Rigidbody> BlastSeenBodies = new HashSet<Rigidbody>();

        /// <summary>
        /// Spawn the profile's explosion prefab (aerial vs ground picked by altitude
        /// threshold, falling back to whichever one is wired) and play the explosion SFX.
        /// Safe to call with either prefab or the SFX unset — missing pieces are skipped.
        /// </summary>
        public static void SpawnExplosionVisuals(MissileProfileSO profile, Vector3 worldPos)
        {
            if (profile == null) return;

            bool aerial = worldPos.y > profile.aerialAltitudeThresholdM;
            GameObject prefab = aerial ? profile.explosionPrefabAerial : profile.explosionPrefabGround;
            if (prefab == null)
                prefab = aerial ? profile.explosionPrefabGround : profile.explosionPrefabAerial;

            if (prefab != null)
            {
                var fx = Object.Instantiate(prefab, worldPos, Quaternion.identity);
                if (profile.explosionLifetimeS > 0f)
                    Object.Destroy(fx, profile.explosionLifetimeS);
            }

            if (profile.explosionSfx != null)
                AudioSource.PlayClipAtPoint(profile.explosionSfx, worldPos, profile.explosionSfxVolume);
        }

        /// <summary>
        /// Physical blast: outward impulse on every non-kinematic Rigidbody inside the
        /// profile's blast radius (Unity AddExplosionForce, linear falloff, impulse mode).
        /// Disabled when blastRadiusM is 0. <paramref name="ignoreRoot"/> excludes the
        /// detonating object's own hierarchy; <paramref name="contextName"/> labels the
        /// buffer-overflow warning.
        /// </summary>
        public static void ApplyBlastImpulse(MissileProfileSO profile, Vector3 worldPos,
                                             Transform ignoreRoot, string contextName)
        {
            if (profile == null || profile.blastRadiusM <= 0f || profile.blastImpulseNs <= 0f)
                return;

            int count = Physics.OverlapSphereNonAlloc(worldPos, profile.blastRadiusM, BlastOverlapBuffer);
            if (count >= BlastOverlapBuffer.Length)
                Debug.LogWarning($"[GuidedFury] {contextName}: blast overlap filled its {BlastOverlapBuffer.Length}-collider buffer; " +
                                 "some rigidbodies may not receive the impulse. Consider a smaller blastRadiusM.");

            BlastSeenBodies.Clear();
            for (int i = 0; i < count; i++)
            {
                Rigidbody rb = BlastOverlapBuffer[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                if (ignoreRoot != null && rb.transform.root == ignoreRoot) continue;
                if (!BlastSeenBodies.Add(rb)) continue;   // one impulse per body, not per collider

                rb.AddExplosionForce(profile.blastImpulseNs, worldPos, profile.blastRadiusM,
                                     profile.blastUpwardsModifier, ForceMode.Impulse);
            }
            BlastSeenBodies.Clear();
        }
    }
}
