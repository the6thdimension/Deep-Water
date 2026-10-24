using System;
using UnityEngine;

namespace DeepWater.GeneralQuarters.ScriptableObjects
{
    /// <summary>An effect: a particle-system prefab, how long an instance lives, and its size.</summary>
    [Serializable]
    public struct EffectSlot
    {
        public GameObject prefab;
        [Tooltip("Seconds before a one-shot instance goes back to the pool (its longest particle life). Looping effects ignore it.")]
        public float lifetimeS;
        [Tooltip("Uniform scale of the instance.")]
        public float scale;
    }

    /// <summary>One gun mount's rig in the ship model: which parts train and elevate, how fast, and how long its barrel is.</summary>
    [Serializable]
    public struct MountRig
    {
        [Tooltip("The gun mount's name in the loadout (Mk 45, CIWS fwd, …).")]
        public string mountName;
        public string trainTransform;
        [Tooltip("Empty for a mount that only trains.")]
        public string elevateTransform;
        public float trainRateDegS;
        public float elevateRateDegS;
        [Tooltip("Pivot to muzzle along the barrel, m (from the Blender model).")]
        public float barrelM;
        [Tooltip("Seconds the mount stays on its target after the last burst before it returns to rest.")]
        public float holdS;
    }

    /// <summary>Sounds the presentation can synthesise when no recording is assigned.</summary>
    public enum SynthSound : byte
    {
        None = 0,
        Mk45Report = 1,
        CiwsBurst = 2,
        Mk38Burst = 3,
        Airburst = 4,
        Splash = 5,
        ShipHit = 6,
        Alarm = 7,
        MissileLaunch = 8,
        Detonation = 9,
    }

    /// <summary>A sound: a recording if one is assigned, otherwise the synthesised stand-in.</summary>
    [Serializable]
    public struct SoundSlot
    {
        public AudioClip clip;
        public SynthSound synth;
        [Range(0f, 1f)] public float volume;
        [Tooltip("Beyond this distance the sound is not heard, m.")]
        public float maxDistanceM;
        [Tooltip("Inside this distance it is at full volume, m.")]
        public float minDistanceM;
        public float pitch;
    }

    /// <summary>
    /// How General Quarters looks and sounds in a fight (slice 6, G18 and G30–G33): the effect for each kind of event,
    /// foam for splashes and the wake, the gun mounts' rigs, the VLS hatches, and the sounds. Presentation only: none of
    /// it is read by the combat core, so nothing here changes a run. AUTHORED throughout.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPresentation", menuName = "Deep Water/General Quarters/Combat Presentation", order = 25)]
    public class CombatPresentationSO : ScriptableObject
    {
        [Header("Effects")]
        public EffectSlot vlsLaunch;
        public EffectSlot muzzleMk45, muzzleCiws, muzzleMk38;
        [Tooltip("A gun round arriving in the air (flak burst).")]
        public EffectSlot airburst;
        [Tooltip("A missile warhead detonating, or a target killed in the air.")]
        public EffectSlot warhead;
        public EffectSlot splash;
        public EffectSlot shipHit;
        [Tooltip("Looping: burns where the ship was hit until the next run.")]
        public EffectSlot fire;
        [Tooltip("Looping: smoke from a system that went offline.")]
        public EffectSlot systemSmoke;
        [Tooltip("Looping: spray at the bow, its rate scaled by speed.")]
        public EffectSlot bowSpray;
        [Tooltip("Looping, follows the missile: rocket exhaust and smoke trail.")]
        public EffectSlot essmTrail;
        [Tooltip("Looping, follows the threat: turbojet glow and a thin trail.")]
        public EffectSlot threatTrail;
        [Tooltip("A kill or a round arriving lower than this throws a splash, m.")]
        public float splashBelowAltitudeM = 20f;
        [Tooltip("Speed of the tracer streaks, m/s.")]
        public float tracerSpeedMs = 1100f;

        [Header("Foam (Sea State)")]
        public float splashFoamRadiusM = 12f;
        public float splashFoamLevel = 0.5f;
        [Tooltip("Seconds between wake deposits.")]
        public float wakeIntervalS = 0.25f;
        public float wakeFoamRadiusM = 9f;
        [Tooltip("Foam level the wake deposits at 30 kn; it scales with speed.")]
        public float wakeFoamLevelAt30Kn = 0.35f;
        [Tooltip("No wake below this speed, kn.")]
        public float wakeMinSpeedKn = 3f;
        [Tooltip("Ship-frame points (m) where the wake is laid: the stern and both quarters, and the bow wave either side.")]
        public Vector3[] wakePoints = new Vector3[0];
        [Tooltip("Bow spray emission at 30 kn, particles per second; it scales with speed squared.")]
        public float bowSprayRateAt30Kn = 40f;
        public Vector3 bowSprayPoint;

        [Header("Gun mounts")]
        public MountRig[] mounts = new MountRig[0];

        [Header("VLS hatches")]
        [Tooltip("Name prefix of each VLS group's cells in the ship model, in loadout group order.")]
        public string[] vlsCellPrefixes = new string[0];
        public Vector3 hatchSize = new Vector3(0.62f, 0.06f, 0.62f);
        public Material hatchMaterial;
        public float hatchOpenS = 0.3f, hatchHoldS = 2.5f, hatchCloseS = 0.6f;
        [Tooltip("Degrees the hatch swings open.")]
        public float hatchOpenDeg = 110f;

        [Header("Audio")]
        public SoundSlot vlsLaunchSound;
        public SoundSlot mk45Sound, ciwsSound, mk38Sound;
        public SoundSlot airburstSound, detonationSound, splashSound, shipHitSound;
        [Tooltip("Played when general quarters is sounded (a raid starts).")]
        public SoundSlot alarmSound;
        [Tooltip("Sound arrives late by distance / this, m/s.")]
        public float speedOfSoundMs = 343f;
        [Tooltip("Sounds that can play at once.")]
        public int voices = 24;

        [TextArea(3, 12)] public string provenance;
    }
}
