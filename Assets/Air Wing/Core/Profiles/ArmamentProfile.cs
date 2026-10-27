using System;
using DeepWater.Gunnery.Guns;
using UnityEngine;

namespace DeepWater.AirWing.Core.Profiles
{
    /// <summary>
    /// The aircraft's gun as the airframe carries it (Gunnery K1: the gun's rounds and their flight are Gunnery's; where
    /// it sits and where it points are Air Wing's). Unset for an unarmed type.
    /// </summary>
    public struct GunMount
    {
        public GunProfile Gun;
        /// <summary>The muzzle, body frame about the centre of mass (Unity axes), m.</summary>
        public Vector3 MuzzleM;
        /// <summary>The bore line, body frame (unit). Usually the nose, a little up.</summary>
        public Vector3 BoreBody;

        public bool IsSet => Gun.IsSet;

        public void Validate()
        {
            Gun.Validate();
            if (BoreBody.sqrMagnitude < 1e-6f) throw new ArgumentException($"gun {Gun.Name}: no bore line");
        }
    }

    /// <summary>How much the aircraft can take (Gunnery K5, abstract): its hit points and the size rounds must pass within.</summary>
    public struct CombatProfile
    {
        /// <summary>Hit points; each round that hits takes one (AUTHORED).</summary>
        public float HitPoints;
        /// <summary>The sphere rounds are tested against, m (AUTHORED, about the presented size).</summary>
        public float HitRadiusM;

        public bool IsSet => HitPoints > 0f && HitRadiusM > 0f;

        public void Validate()
        {
            if (!IsSet) throw new ArgumentException("combat: hit points and a hit radius must be positive");
        }
    }
}
