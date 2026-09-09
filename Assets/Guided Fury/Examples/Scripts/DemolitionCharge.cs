using UnityEngine;
using GuidedFury.Core.Missile;
using GuidedFury.ScriptableObjects.Profiles;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Emplaced ordnance — a stationary demolition charge. On <see cref="Detonate"/> it
    /// spawns the profile's explosion VFX/SFX and applies the profile's physical blast via
    /// the same <see cref="DetonationEffects"/> mechanism missiles use, then deactivates.
    ///
    /// No MissileEntity, no integrator — a charge has no flight model, so wrapping
    /// MissileBehaviour would drag a whole sim along for a stationary boom. All tuning
    /// (FX prefabs, blast radius/impulse) comes from the assigned MissileProfileSO's
    /// detonation section; the flight fields of that profile are simply ignored.
    /// </summary>
    public sealed class DemolitionCharge : MonoBehaviour
    {
        [Tooltip("Profile supplying the detonation values (explosion prefabs, lifetime, SFX, blast radius/impulse). Flight fields are ignored.")]
        [SerializeField] private MissileProfileSO profile;

        /// <summary>True until detonated (and a profile is assigned).</summary>
        public bool IsArmed => !consumed && profile != null && gameObject.activeInHierarchy;

        private bool consumed;

        public void Detonate()
        {
            if (!IsArmed) return;
            consumed = true;

            Vector3 pos = transform.position;
            DetonationEffects.SpawnExplosionVisuals(profile, pos);
            // Ignore only the charge itself, NOT transform.root: the pad's blast props share
            // the pad root, and excluding the root would exempt everything on the pad.
            DetonationEffects.ApplyBlastImpulse(profile, pos, transform, name);
            gameObject.SetActive(false);
        }
    }
}
